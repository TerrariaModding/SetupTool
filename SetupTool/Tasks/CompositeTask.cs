using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Tasks
{
	public class CompositeTask : BaseTask
	{
		private BaseTask[] tasks;
		private BaseTask failed;

		public CompositeTask(ITaskInterface taskInterface, params BaseTask[] tasks) : base(taskInterface)
		{
			this.tasks = tasks;
		}

		public override bool Configure()
		{
			return tasks.All(task => task.Configure());
		}

		public override bool Failed()
		{
			return failed != null;
		}

		public override void Finished()
		{
			if (failed != null)
				failed.Finished();
			else
				foreach (var task in tasks)
					task.Finished();
		}

		public override void Run()
		{
			foreach (var task in tasks)
			{
				task.Run();
				if (task.Failed())
				{
					failed = task;
					return;
				}
			}
		}
	}
}
