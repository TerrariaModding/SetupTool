using SetupTool.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Tasks
{
	public class ProcessProjectTask : BaseTask
	{
		private Dictionary<string, ProjectConfig.Project> _projects;

		public ProcessProjectTask(ITaskInterface taskInterface, Dictionary<string, ProjectConfig.Project> projects) : base(taskInterface)
		{
			_projects = projects;
		}

		public override void Run()
		{
			if (_projects == null)
				return;

            // Set up all projects that don't require vanilla to be set up before-hand (vanilla is also set up here, ofc)
			foreach (var project in _projects.Values.Where(x => !x.CopyVanillaPatches))
                RunProject(project);

            // Set up all projects that require vanilla *after* vanilla has been set up
            foreach (var project in _projects.Values.Where(x => x.CopyVanillaPatches))
                RunProject(project);
		}

        private void RunProject(ProjectConfig.Project project)
        {
            Console.WriteLine(" - " + project.Name);

            Console.WriteLine("     - Patching...");

            string baseDir = Defines.ProjectConfig.DecompiledSrcDir;
            if (!string.IsNullOrEmpty(project.Parent))
            {
                if (!_projects.ContainsKey(project.Parent))
                    throw new Exception($"Missing parent '{project.Parent}' for project '{project.Name}'");
                baseDir = Path.Combine(Defines.ProjectConfig.SrcDir, _projects[project.Parent].SrcDir);
            }

            if (project.CopyVanillaPatches)
            {
                Console.WriteLine($"{project.Name} has {nameof(project.CopyVanillaPatches)} set to True");
                Console.WriteLine("Copying vanilla patches...");

                if (ProjectConfig.VanillaProject == null)
                    throw new Exception("Unable to copy vanilla patch files as there was no vanilla project instance found");
                string vanillaPatchesDir = Path.Combine(Defines.ProjectConfig.PatchesDir, ProjectConfig.VanillaProject.PatchesDir);
                string projectPatchesDir = Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir);
                Directory.CreateDirectory(projectPatchesDir);

                foreach (FileInfo file in new DirectoryInfo(vanillaPatchesDir).GetFiles())
                    file.CopyTo(Path.Combine(projectPatchesDir, file.Name), true);


                Console.WriteLine("Copied vanilla patches!");
            }

            new PatchTask(TaskInterface,
                    baseDir,
                    Path.Combine(Defines.ProjectConfig.SrcDir, project.SrcDir),
                    Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir),
                    new JsonProperty<DateTime>(Defines.Settings, project.Name + "DiffCutoff", new DateTime(2015, 01, 01)))
                .Run();
		}
	}
}
