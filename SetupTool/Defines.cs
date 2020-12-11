using SetupTool.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool
{
	public static class Defines
	{
		public static readonly string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		// Config Files

		public static JsonMap Settings => _settingsInstance;
		private static JsonMap _settingsInstance = new JsonMap(Path.Combine(AppDir, "settings.json"), OnJsonMapError);

		public static JsonMap ProjectConfig => _projectConfigInstance;
		private static JsonMap _projectConfigInstance = new JsonMap("config.json", OnJsonMapError);

		public static void OnJsonMapError(string error, Exception ex)
		{
			Console.WriteLine("Error: " + error);

			if (ex != null)
				Console.WriteLine(ex.Message + ": " + ex.StackTrace);

			Environment.Exit(1);
		}
		
		// Global Settings
		public static JsonProperty<string> SteamDir = new JsonProperty<string>(Settings, "SteamDir", @"C:\Program Files (x86)\Steam\steamapps\common\terraria");
		public static JsonProperty<string> TerrariaPath = new JsonProperty<string>(Settings, "TerrariaPath", Path.Combine(SteamDir, "Terraria.exe"));
		public static JsonProperty<string> TerrariaServerPath = new JsonProperty<string>(Settings, "TerrariaServerPath", Path.Combine(SteamDir, "TerrariaServer.exe"));

		// Project Config
		public static readonly JsonProperty<string> LogsDir = new JsonProperty<string>(ProjectConfig, "LogsDir");
		public static readonly JsonProperty<string> ClientVersion = new JsonProperty<string>(ProjectConfig, "ClientVersion");
		public static readonly JsonProperty<string> ServerVersion = new JsonProperty<string>(ProjectConfig, "ServerVersion");
		public static readonly JsonProperty<string> PatchesDir = new JsonProperty<string>(ProjectConfig, "PatchesDir");
		public static readonly JsonProperty<string> SrcDir = new JsonProperty<string>(ProjectConfig, "SrcDir");
		public static readonly JsonProperty<string[]> Projects = new JsonProperty<string[]>(ProjectConfig, "Projects");

		public static readonly string DecompiledSrcDir = Path.Combine(SrcDir, "decompiled");
	}
}
