using SetupTool.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public static class Utilities
	{
		private static readonly string targetsFilePath = Path.Combine(Defines.ProjectConfig.SrcDir, "TerrariaSteamPath.targets");
		public static void UpdateSteamDirTargetsFile()
		{
			BaseTask.CreateParentDirectory(targetsFilePath);

			string targetsText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""14.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <TerrariaSteamPath>{Defines.SteamDir}</TerrariaSteamPath>
  </PropertyGroup>
</Project>";


			if (File.Exists(targetsFilePath) && targetsText == File.ReadAllText(targetsFilePath))
				return;

			File.WriteAllText(targetsFilePath, targetsText);
		}
	}
}
