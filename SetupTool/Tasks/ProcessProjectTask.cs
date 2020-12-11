﻿using SetupTool.Util;
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

			foreach (var project in _projects.Values)
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

				new PatchTask(new Program.ConsoleTaskCallback(),
					baseDir,
					Path.Combine(Defines.ProjectConfig.SrcDir, project.SrcDir),
					Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir),
					new JsonProperty<DateTime>(Defines.Settings, project.Name + "DiffCutoff", new DateTime(2015, 01, 01)))
				.Run();
			}
		}
	}
}
