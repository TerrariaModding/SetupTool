using SetupTool.Exceptions;
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
        public static ProjectConfig.Project VanillaProject;

		public static readonly string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		// Config Files

		public static JsonMap Settings => _settingsInstance;
		private static JsonMap _settingsInstance = new JsonMap(Path.Combine(AppDir, "settings.json"), OnJsonMapError);

		public static ProjectConfig ProjectConfig;

		static Defines()
		{
			try
			{
				ProjectConfig = ProjectConfig.Load("config.json");
                VanillaProject = ProjectConfig.Projects["Terraria"];
            }
			catch (ProjectIOException ex)
			{
				ex.PrintStackTrace();
				Environment.Exit(1);
			}
		}

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
	}
}
