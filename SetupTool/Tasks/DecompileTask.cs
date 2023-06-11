using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using SetupTool.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace SetupTool.Tasks
{
	public class DecompileTask : BaseTask
	{
		private class ExtendedProjectDecompiler : WholeProjectDecompiler
		{
			public ExtendedProjectDecompiler(DecompilerSettings settings, IAssemblyResolver assemblyResolver)
				: base(settings, assemblyResolver, assemblyReferenceClassifier: null, debugInfoProvider: null) { }

			public new bool IncludeTypeWhenDecompilingProject(PEFile module, TypeDefinitionHandle type)
				=> base.IncludeTypeWhenDecompilingProject(module, type);
		}

		private readonly string _srcDir;
		private readonly bool _serverOnly;
		private readonly bool _formatOutput;

		private ExtendedProjectDecompiler projectDecompiler;

		private readonly DecompilerSettings _decompilerSettings;

		public DecompileTask(ITaskInterface task, string srcDir, bool serverOnly = false, bool formatOutput = true) : base(task)
		{
			_srcDir = srcDir;
			_serverOnly = serverOnly;
			_formatOutput = formatOutput;

			var formatting = FormattingOptionsFactory.CreateKRStyle();

			// Arrays should have a new line for every entry, since it's easier to insert values in patches that way.
			formatting.ArrayInitializerWrapping = Wrapping.WrapAlways;
			formatting.ArrayInitializerBraceStyle = BraceStyle.EndOfLine;

			// Force wrapping for chained calls for the same reason.
			// Hm, doesn't work.
			//formatting.ChainedMethodCallWrapping = Wrapping.WrapAlways;

			_decompilerSettings = new(LanguageVersion.Latest)
			{
				RemoveDeadCode = true,
				CSharpFormattingOptions = formatting,

				// Switch expressions are not patching-friendly,
				// and do not even support expression bodies at this time:
				// https://github.com/dotnet/csharplang/issues/3037
				SwitchExpressions = false,
			};
		}

		public override bool Configure()
		{
			if (File.Exists(Defines.TerrariaPath) && File.Exists(Defines.TerrariaServerPath))
				return true;

			return (bool)TaskInterface.Invoke(new Func<bool>(SelectTerrariaDialog));
		}

		public static bool SelectTerrariaDialog()
		{
			while (true)
			{
				var dialog = new OpenFileDialog
				{
					InitialDirectory = Path.GetFullPath(Directory.Exists(Defines.SteamDir) ? Defines.SteamDir : "."),
					Filter = "Terraria|Terraria.exe",
					Title = "Select Terraria.exe"
				};

				if (dialog.ShowDialog() != DialogResult.OK)
					return false;

				string err = null;
				if (Path.GetFileName(dialog.FileName) != "Terraria.exe")
					err = "File must be named Terraria.exe";
				else if (!File.Exists(Path.Combine(Path.GetDirectoryName(dialog.FileName), "TerrariaServer.exe")))
					err = "TerrariaServer.exe does not exist in the same directory";

				if (err != null)
				{
					if (MessageBox.Show(err, "Invalid Selection", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Cancel)
						return false;
				}
				else
				{
					Defines.SteamDir.Value = Path.GetDirectoryName(dialog.FileName);
					Utilities.UpdateSteamDirTargetsFile();
					return true;
				}
			}
		}

		public override void Run()
		{
			TaskInterface.SetStatus("Deleting Old Src");
			if (Directory.Exists(_srcDir))
				Directory.Delete(_srcDir, true);

			var clientModule = _serverOnly ? null : ReadModule(Defines.TerrariaPath, new Version(Defines.ProjectConfig.ClientVersion));
			var serverModule = ReadModule(Defines.TerrariaServerPath, new Version(Defines.ProjectConfig.ServerVersion));
			var mainModule = _serverOnly ? serverModule : clientModule;

			var embeddedAssemblyResolver = new EmbeddedAssemblyResolver(mainModule, mainModule.DetectTargetFrameworkId());

			projectDecompiler = new ExtendedProjectDecompiler(_decompilerSettings, embeddedAssemblyResolver);


			var items = new List<WorkItem>();
			var files = new HashSet<string>();
			var resources = new HashSet<string>();
			var exclude = new List<string>();

			// Decompile embedded library sources directly into Terraria project. Treated the same as Terraria source
			var decompiledLibraries = new[] { "ReLogic" };
			foreach (var lib in decompiledLibraries)
			{
				var libRes = mainModule.Resources.Single(r => r.Name.EndsWith(lib + ".dll"));
				AddEmbeddedLibrary(libRes, projectDecompiler.AssemblyResolver, items);
				exclude.Add(GetOutputPath(libRes.Name, mainModule));
			}

			if (!_serverOnly)
				AddModule(clientModule, projectDecompiler.AssemblyResolver, items, files, resources, exclude);

			AddModule(serverModule, projectDecompiler.AssemblyResolver, items, files, resources, exclude, _serverOnly ? null : "SERVER");

			items.Add(WriteTerrariaProjectFile(mainModule, files, resources, decompiledLibraries));
			items.Add(WriteCommonConfigurationFile());

			ExecuteParallel(items);
		}

		private void AddEmbeddedLibrary(Resource res, IAssemblyResolver resolver, List<WorkItem> items)
		{
			using var s = res.TryOpenStream();
			s.Position = 0;
			var module = new PEFile(res.Name, s, PEStreamOptions.PrefetchEntireImage);

			var files = new HashSet<string>();
			var resources = new HashSet<string>();
			AddModule(module, resolver, items, files, resources);
			items.Add(WriteProjectFile(module, "Library", files, resources, w => {
				// references
				w.WriteStartElement("ItemGroup");
				foreach (var r in module.AssemblyReferences.OrderBy(r => r.Name))
				{
					if (r.Name == "mscorlib") continue;

					w.WriteStartElement("Reference");
					w.WriteAttributeString("Include", r.Name);
					w.WriteEndElement();
				}
				w.WriteEndElement(); // </ItemGroup>

				// TODO: resolve references to embedded terraria libraries with their HintPath
			}));
		}

		protected PEFile ReadModule(string path, Version version)
		{
			if (version == null)
				throw new Exception("Version unspecified");

			var versionedPath = path.Insert(path.LastIndexOf('.'), $"_v{version}");
			if (File.Exists(versionedPath))
				path = versionedPath;

			TaskInterface.SetStatus("Loading " + Path.GetFileName(path));
			using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				var module = new PEFile(path, fileStream, PEStreamOptions.PrefetchEntireImage);
				var assemblyName = new AssemblyName(module.FullName);
				if (assemblyName.Version != version)
					throw new Exception($"{assemblyName.Name} version {assemblyName.Version}. Expected {version}");

				return module;
			}
		}

		// memoized
		private static ConditionalWeakTable<PEFile, string> assemblyTitleCache = new ConditionalWeakTable<PEFile, string>();
		private static string GetAssemblyTitle(PEFile module)
		{
			if (!assemblyTitleCache.TryGetValue(module, out var title))
				assemblyTitleCache.Add(module, title = GetCustomAttributes(module)[nameof(AssemblyTitleAttribute)]);

			return title;
		}

		private static bool IsCultureFile(string path)
		{
			if (!path.Contains('-'))
				return false;

			try
			{
				CultureInfo.GetCultureInfo(Path.GetFileNameWithoutExtension(path));
				return true;
			}
			catch (CultureNotFoundException) { }
			return false;
		}


		private static string GetOutputPath(string path, PEFile module)
		{
			if (path.EndsWith(".dll"))
			{
				var asmRef = module.AssemblyReferences.SingleOrDefault(r => path.EndsWith(r.Name + ".dll"));
				if (asmRef != null)
					path = Path.Combine(path.Substring(0, path.Length - asmRef.Name.Length - 5), asmRef.Name + ".dll");
			}

			var rootNamespace = GetAssemblyTitle(module);
			if (path.StartsWith(rootNamespace))
				path = path.Substring(rootNamespace.Length + 1);

			path = path.Replace("Libraries.", "Libraries/"); // lets leave the folder structure in here alone
			path = path.Replace('\\', '/');

			// . to /
			int stopFolderzingAt = path.IndexOf('/');
			if (stopFolderzingAt < 0)
				stopFolderzingAt = path.LastIndexOf('.');
			path = new StringBuilder(path).Replace(".", "/", 0, stopFolderzingAt).ToString();

			// default lang files should be called Main
			if (IsCultureFile(path))
				path = path.Insert(path.LastIndexOf('.'), "/Main");

			return path;
		}

		private IEnumerable<IGrouping<string, TypeDefinitionHandle>> GetCodeFiles(PEFile module)
		{
			var metadata = module.Metadata;
			return module.Metadata.GetTopLevelTypeDefinitions().Where(td => projectDecompiler.IncludeTypeWhenDecompilingProject(module, td))
				.GroupBy(h =>
				{
					var type = metadata.GetTypeDefinition(h);
					var path = WholeProjectDecompiler.CleanUpFileName(metadata.GetString(type.Name)) + ".cs";
					if (!string.IsNullOrEmpty(metadata.GetString(type.Namespace)))
						path = Path.Combine(WholeProjectDecompiler.CleanUpFileName(metadata.GetString(type.Namespace)), path);
					return GetOutputPath(path, module);
				}, StringComparer.OrdinalIgnoreCase);
		}

		private static IEnumerable<(string path, Resource r)> GetResourceFiles(PEFile module)
		{
			return module.Resources.Where(r => r.ResourceType == ResourceType.Embedded).Select(res => (GetOutputPath(res.Name, module), res));
		}

		private DecompilerTypeSystem AddModule(PEFile module, IAssemblyResolver resolver, List<WorkItem> items, ISet<string> sourceSet, ISet<string> resourceSet, ICollection<string> exclude = null, string conditional = null)
		{
			var projectDir = GetAssemblyTitle(module);
			var sources = GetCodeFiles(module).ToList();
			var resources = GetResourceFiles(module).ToList();
			if (exclude != null)
			{
				sources.RemoveAll(src => exclude.Contains(src.Key));
				resources.RemoveAll(res => exclude.Contains(res.path));
			}

			var ts = new DecompilerTypeSystem(module, resolver, _decompilerSettings);
			items.AddRange(sources
				.Where(src => sourceSet.Add(src.Key))
				.Select(src => DecompileSourceFile(ts, src, projectDir, conditional)));

			if (conditional != null && resources.Any(res => !resourceSet.Contains(res.path)))
				throw new Exception($"Conditional ({conditional}) resources not supported");

			items.AddRange(resources
				.Where(res => resourceSet.Add(res.path))
				.Select(res => ExtractResource(res.path, res.r, projectDir)));

			return ts;
		}

		private WorkItem ExtractResource(string name, Resource res, string projectDir)
		{
			return new WorkItem("Extracting: " + name, () =>
			{
				var path = Path.Combine(_srcDir, projectDir, name);
				CreateParentDirectory(path);

				var s = res.TryOpenStream();
				s.Position = 0;
				using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
					s.CopyTo(fs);
			});
		}

		private CSharpDecompiler CreateDecompiler(DecompilerTypeSystem ts)
		{
			var decompiler = new CSharpDecompiler(ts, projectDecompiler.Settings)
			{
				CancellationToken = TaskInterface.CancellationToken
			};
			decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());
			decompiler.AstTransforms.Add(new RemoveCLSCompliantAttribute());
			return decompiler;
		}

		private WorkItem DecompileSourceFile(DecompilerTypeSystem ts, IGrouping<string, TypeDefinitionHandle> src, string projectName, string conditional = null)
		{
			return new WorkItem("Decompiling: " + src.Key, updateStatus =>
			{
				var path = Path.Combine(_srcDir, projectName, src.Key);
				CreateParentDirectory(path);

				using (var w = new StringWriter())
				{
					if (conditional != null)
						w.WriteLine("#if " + conditional);

					CreateDecompiler(ts)
						.DecompileTypes(src.ToArray())
						.AcceptVisitor(new CSharpOutputVisitor(w, projectDecompiler.Settings.CSharpFormattingOptions));

					if (conditional != null)
						w.WriteLine("#endif");

					string source = w.ToString();
					if (_formatOutput)
					{
						updateStatus("Formatting: " + src.Key);
						source = FormatTask.Format(source, TaskInterface.CancellationToken, true);
					}

					File.WriteAllText(path, source);
				}
			});
		}

		private WorkItem WriteTerrariaProjectFile(PEFile module, IEnumerable<string> sources, IEnumerable<string> resources, ICollection<string> decompiledLibraries)
		{
			return WriteProjectFile(module, "WinExe", sources, resources, w => {
				//configurations
				w.WriteStartElement("PropertyGroup");
				w.WriteAttributeString("Condition", "$(Configuration.Contains('Server'))");
				w.WriteElementString("OutputType", "Exe");
				w.WriteElementString("OutputName", "$(OutputName)Server");
				w.WriteEndElement(); // </PropertyGroup>

				// references
				w.WriteStartElement("ItemGroup");

				var references = module.AssemblyReferences.Where(r => r.Name != "mscorlib").OrderBy(r => r.Name).ToArray();
				var projectReferences = decompiledLibraries != null
					? references.Where(r => decompiledLibraries.Contains(r.Name)).ToArray()
					: Array.Empty<ICSharpCode.Decompiler.Metadata.AssemblyReference>();
				var normalReferences = references.Except(projectReferences).ToArray();

				foreach (var r in projectReferences)
				{
					w.WriteStartElement("ProjectReference");
					w.WriteAttributeString("Include", $"../{r.Name}/{r.Name}.csproj");
					w.WriteEndElement();
				}

				foreach (var r in projectReferences)
				{
					w.WriteStartElement("EmbeddedResource");
					w.WriteAttributeString("Include", $"../{r.Name}/bin/$(Configuration)/$(TargetFramework)/{r.Name}.dll");
					w.WriteElementString("LogicalName", $"Terraria.Libraries.{r.Name}.{r.Name}.dll");
					w.WriteEndElement();
				}

				foreach (var r in normalReferences)
				{
					w.WriteStartElement("Reference");
					w.WriteAttributeString("Include", r.Name);
					w.WriteEndElement();
				}

				w.WriteEndElement(); // </ItemGroup>

			});
		}

		private WorkItem WriteProjectFile(PEFile module, string outputType, IEnumerable<string> sources, IEnumerable<string> resources, Action<XmlTextWriter> writeSpecificConfig)
		{
			var name = GetAssemblyTitle(module);
			var filename = name + ".csproj";
			return new WorkItem("Writing: " + filename, () =>
			{
				var path = Path.Combine(_srcDir, name, filename);
				CreateParentDirectory(path);

				using (var sw = new StreamWriter(path))
				using (var w = CreateXmlWriter(sw))
				{
					w.Formatting = System.Xml.Formatting.Indented;
					w.WriteStartElement("Project");
					w.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

					w.WriteStartElement("Import");
					w.WriteAttributeString("Project", "../Configuration.targets");
					w.WriteEndElement(); // </Import>

					w.WriteStartElement("PropertyGroup");
					w.WriteElementString("OutputType", outputType);
					w.WriteElementString("Version", new AssemblyName(module.FullName).Version.ToString());

					var attribs = GetCustomAttributes(module);
					w.WriteElementString("Company", attribs[nameof(AssemblyCompanyAttribute)]);
					w.WriteElementString("Copyright", attribs[nameof(AssemblyCopyrightAttribute)]);

					w.WriteElementString("RootNamespace", module.Name);
					w.WriteEndElement(); // </PropertyGroup>

					writeSpecificConfig(w);

					// resources
					w.WriteStartElement("ItemGroup");
					foreach (var r in ApplyWildcards(resources, sources.ToArray()).OrderBy(r => r))
					{
						w.WriteStartElement("EmbeddedResource");
						w.WriteAttributeString("Include", r);
						w.WriteEndElement();
					}
					w.WriteEndElement(); // </ItemGroup>
					w.WriteEndElement(); // </Project>

					sw.Write(Environment.NewLine);
				}
			});
		}

		private WorkItem WriteCommonConfigurationFile()
		{
			var filename = "Configuration.targets";
			return new WorkItem("Writing: " + filename, () => {
				var path = Path.Combine(_srcDir, filename);
				CreateParentDirectory(path);

				using (var sw = new StreamWriter(path))
				using (var w = CreateXmlWriter(sw))
				{
					w.Formatting = System.Xml.Formatting.Indented;
					w.WriteStartElement("Project");

					w.WriteStartElement("PropertyGroup");
					w.WriteElementString("TargetFramework", "net40");
					w.WriteElementString("Configurations", "Debug;Release;ServerDebug;ServerRelease");
					w.WriteElementString("AssemblySearchPaths", "$(AssemblySearchPaths);{GAC}");
					w.WriteElementString("PlatformTarget", "x86");
					w.WriteElementString("AllowUnsafeBlocks", "true");
					w.WriteElementString("Optimize", "true");
					w.WriteEndElement(); // </PropertyGroup>

					//configurations
					w.WriteStartElement("PropertyGroup");
					w.WriteAttributeString("Condition", "$(Configuration.Contains('Server'))");
					w.WriteElementString("DefineConstants", "$(DefineConstants);SERVER");
					w.WriteEndElement(); // </PropertyGroup>

					w.WriteStartElement("PropertyGroup");
					w.WriteAttributeString("Condition", "!$(Configuration.Contains('Server'))");
					w.WriteElementString("DefineConstants", "$(DefineConstants);CLIENT");
					w.WriteEndElement(); // </PropertyGroup>

					w.WriteStartElement("PropertyGroup");
					w.WriteAttributeString("Condition", "$(Configuration.Contains('Debug'))");
					w.WriteElementString("Optimize", "false");
					w.WriteElementString("DefineConstants", "$(DefineConstants);DEBUG");
					w.WriteEndElement(); // </PropertyGroup>

					w.WriteEndElement(); // </Project>

					sw.Write(Environment.NewLine);
				}
			});
		}

		private static XmlTextWriter CreateXmlWriter(StreamWriter streamWriter)
		{
			return new XmlTextWriter(streamWriter)
			{
				Formatting = System.Xml.Formatting.Indented,
				IndentChar = '\t',
				Indentation = 1,
			};
		}

		private IEnumerable<string> ApplyWildcards(IEnumerable<string> include, IReadOnlyList<string> exclude)
		{
			var wildpaths = new HashSet<string>();
			foreach (var path in include)
			{
				if (wildpaths.Any(path.StartsWith))
					continue;

				string wpath = path;
				string cards = "";
				while (wpath.Contains('/'))
				{
					var parent = wpath.Substring(0, wpath.LastIndexOf('/'));
					if (exclude.Any(e => e.StartsWith(parent)))
						break; //can't use parent as a wildcard

					wpath = parent;
					if (cards.Length < 2)
						cards += "*";
				}

				if (wpath != path)
				{
					wildpaths.Add(wpath);
					yield return $"{wpath}/{cards}";
				}
				else
				{
					yield return path;
				}
			}
		}

		private static string[] knownAttributes = { nameof(AssemblyCompanyAttribute), nameof(AssemblyCopyrightAttribute), nameof(AssemblyTitleAttribute) };
		private static IDictionary<string, string> GetCustomAttributes(PEFile module)
		{
			var dict = new Dictionary<string, string>();

			var reader = module.Reader.GetMetadataReader();
			var attribs = reader.GetAssemblyDefinition().GetCustomAttributes().Select(reader.GetCustomAttribute);
			foreach (var attrib in attribs)
			{
				var ctor = reader.GetMemberReference((MemberReferenceHandle)attrib.Constructor);
				var attrTypeName = reader.GetString(reader.GetTypeReference((TypeReferenceHandle)ctor.Parent).Name);
				if (!knownAttributes.Contains(attrTypeName))
					continue;

				var value = attrib.DecodeValue(new IDGAFAttributeTypeProvider());
				dict[attrTypeName] = value.FixedArguments.Single().Value as string;
			}

			return dict;
		}

		private class IDGAFAttributeTypeProvider : ICustomAttributeTypeProvider<object>
		{
			public object GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
			public object GetSystemType() => throw new NotImplementedException();
			public object GetSZArrayType(object elementType) => throw new NotImplementedException();
			public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => throw new NotImplementedException();
			public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => throw new NotImplementedException();
			public object GetTypeFromSerializedName(string name) => throw new NotImplementedException();
			public PrimitiveTypeCode GetUnderlyingEnumType(object type) => throw new NotImplementedException();
			public bool IsSystemType(object type) => throw new NotImplementedException();
		}
	}
}
