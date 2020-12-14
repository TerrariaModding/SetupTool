/*using SetupTool.Tasks;
using SetupTool.Util;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SetupTool
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			TaskRunner.RunTask(new CompositeTask(new ConsoleTaskCallback(),
				new DecompileTask(new ConsoleTaskCallback(), Defines.ProjectConfig.DecompiledSrcDir),
				new ProcessProjectTask(new ConsoleTaskCallback(), Defines.ProjectConfig.Projects)
			));

			Utilities.UpdateSteamDirTargetsFile();
		}

		public class ConsoleTaskCallback : ITaskInterface
		{
			public CancellationToken CancellationToken => new CancellationToken();

			public IAsyncResult BeginInvoke(Delegate action)
			{
				return Task.Run(() => Invoke(action));
			}

			public object Invoke(Delegate action)
			{
				return action.DynamicInvoke();
			}

			private int _maxProgress;
			private int _lastPercent;

			public void SetMaxProgress(int max)
			{
				_maxProgress = max;
			}

			public void SetProgress(int progress)
			{
				if (_maxProgress < progress)
					_maxProgress = progress;
				if (_maxProgress == 0)
					_maxProgress = 100;
				int percentDone = progress * 100 / _maxProgress;

				if (percentDone == _lastPercent)
					return;

				_lastPercent = percentDone;

				//Console.Write("\r              ");
				//Console.Write("\rProgress: " + percentDone.ToString("D2") + "%");

				//Console.WriteLine("Progress: " + percentDone.ToString("D2") + "%");
				Console.WriteLine("Progress: " + percentDone.ToString("D2") + "%    " + progress + "/" + _maxProgress);
			}

			public void SetStatus(string status)
			{
				//Console.WriteLine(status);
			}
		}
	}
}
*/