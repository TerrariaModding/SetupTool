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
        public static List<string> PatchedProjects;

        private Dictionary<string, ProjectConfig.Project> _projects;

		public ProcessProjectTask(ITaskInterface taskInterface, Dictionary<string, ProjectConfig.Project> projects) : base(taskInterface)
		{
			_projects = projects;
		}

		public override void Run()
		{
			if (_projects == null)
				return;

            PatchedProjects = new List<string>();
            Dictionary<string, ProjectConfig.Project> projectsToRun = _projects;

            while (projectsToRun.Keys.Count > 0)
                foreach (ProjectConfig.Project project in _projects.Values)
                {
                    bool missingRef = false;

                    foreach (string refProj in project.ReliantOn.Where(refProj => !PatchedProjects.Contains(refProj)))
                        missingRef = true;

                    if (missingRef)
                        continue;
                    
                    RunProject(project);
                    projectsToRun.Remove(project.Name);
                }

            PatchedProjects = new List<string>();
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

            if (project.ReliantOn.Count > 0)
            {
                Console.WriteLine($"Copying projects that {project.Name} relies on for patching!");

                foreach (string projectString in project.ReliantOn)
                {
                    Console.WriteLine("Copying reliant patches...");
                    ProjectConfig.Project refProject = projectString == "Terraria" 
                        ? Defines.VanillaProject 
                        : Defines.ProjectConfig.Projects[projectString];

                    string vanillaPatchesDir = Path.Combine(Defines.ProjectConfig.PatchesDir, refProject.PatchesDir);
                    string projectPatchesDir = Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir);
                    Directory.CreateDirectory(projectPatchesDir);

                    foreach (string dir in Directory.GetDirectories(vanillaPatchesDir, "*", SearchOption.AllDirectories))
                        Directory.CreateDirectory(dir.Replace(vanillaPatchesDir, projectPatchesDir));

                    foreach (string file in Directory.GetFiles(vanillaPatchesDir, "*", SearchOption.AllDirectories))
                        File.Copy(file, file.Replace(vanillaPatchesDir, projectPatchesDir), true);


                    Console.WriteLine("Copied reliant patches!");
                }
            }

            new PatchTask(TaskInterface,
                    baseDir,
                    Path.Combine(Defines.ProjectConfig.SrcDir, project.SrcDir),
                    Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir),
                    new JsonProperty<DateTime>(Defines.Settings, project.Name + "DiffCutoff", new DateTime(2015, 01, 01)),
                    project.Name)
                .Run();
		}
	}
}
