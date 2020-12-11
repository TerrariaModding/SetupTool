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
		private string[] _projects;

		public ProcessProjectTask(ITaskInterface taskInterface, params string[] projects) : base(taskInterface)
		{
			_projects = projects;
		}

		public override void Run()
		{
			if (_projects == null)
				return;
			foreach (string project in _projects)
			{
				Console.WriteLine(" - " + project);

				Console.WriteLine("     - Patching...");
				new PatchTask(new Program.ConsoleTaskCallback(), Defines.DecompiledSrcDir, Path.Combine(Defines.SrcDir, project), Path.Combine(Defines.PatchesDir, project), new JsonProperty<DateTime>(Defines.Settings, project + "DiffCutoff", new DateTime(2015, 01, 01))).Run();
			}
		}
	}
}
