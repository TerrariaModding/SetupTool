using Newtonsoft.Json;
using SetupTool.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public class ProjectConfig
    {
        public static Project VanillaProject;

		[JsonIgnore]
		private string _path;

		public string ConfigPath => _path;

		public string LogsDir;
		public string ClientVersion;
		public string ServerVersion;
		public string PatchesDir;
		public string SrcDir;
		public string DecompSrcDir;
		public Dictionary<string, Project> Projects;

		public string DecompiledSrcDir => Path.Combine(SrcDir, DecompSrcDir);

		public ProjectConfig(string path)
		{
			_path = path;
		}

		public static ProjectConfig Load(string path)
		{
			try
			{
				var conf = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(path));
				conf._path = path;
				conf.OnLoad();

				// Fetch and save an instance of the vanilla project for use in copying patch files based on directory locations
                if (conf.Projects.ContainsKey("Terraria"))
                    VanillaProject = conf.Projects["Terraria"];

                return conf;
			}
			catch (IOException ex)
			{
				throw new ProjectIOException($"Failed to read file '{path}'", ex);
			}
			catch (JsonException ex)
			{
				throw new ProjectIOException($"Failed to parse JSON '{path}'", ex);
			}
		}

		private void OnLoad()
		{
			List<string> errors = new List<string>();

			if (string.IsNullOrWhiteSpace(LogsDir))
				errors.Add("Missing 'LogsDir'");
			if (string.IsNullOrWhiteSpace(ClientVersion))
				errors.Add("Missing 'ClientVersion'");
			if (string.IsNullOrWhiteSpace(ServerVersion))
				errors.Add("Missing 'ServerVersion'");
			if (string.IsNullOrWhiteSpace(PatchesDir))
				errors.Add("Missing 'PatchesDir'");
			if (string.IsNullOrWhiteSpace(SrcDir))
				errors.Add("Missing 'SrcDir'");
			if (string.IsNullOrWhiteSpace(DecompSrcDir))
				errors.Add("Missing 'DecompSrcDir");
			
			if (Projects == null)
				errors.Add("Missing 'Projects'");
			else
			{
				foreach (var project in Projects)
				{
					project.Value.Name = project.Key;

					if (string.IsNullOrWhiteSpace(project.Value.PatchesDir))
						errors.Add($"Missing 'Projects/{project.Key}/PatchesDir");
					if (string.IsNullOrWhiteSpace(project.Value.SrcDir))
						errors.Add($"Missing 'Projects/{project.Key}/SrcDir");
				}
			}

			if (errors.Count > 0)
				throw new ProjectIOException($"Failed to load file '{_path}' due to the following errors:\n{errors.JoinStrings()}");

		}

		public void Save()
		{
			if (string.IsNullOrWhiteSpace(_path))
				throw new ProjectIOException("Failed to save - no path specified");

			try
			{
				File.WriteAllText(_path, JsonConvert.SerializeObject(this));
			}
			catch (IOException ex)
			{
				throw new ProjectIOException($"Failed to save file '{_path}'", ex);
			}
			catch (JsonException ex)
			{
				throw new ProjectIOException($"Failed to serialize JSON '{_path}'", ex);
			}
		}

		public class Project
		{
			[JsonIgnore]
			public string Name;

			public string Parent;
			public string PatchesDir;
			public string SrcDir;
            public bool CopyVanillaPatches;
        }
	}
}
