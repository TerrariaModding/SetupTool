using SetupTool.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public static class TaskRunner
	{
		private static CancellationTokenSource cancelSource;

		public static void RunTask(BaseTask task)
		{
			cancelSource = new CancellationTokenSource();
			//foreach (var b in taskButtons.Keys) b.Enabled = false;
			//buttonCancel.Enabled = true;

			new Thread(() => RunTaskThread(task)).Start();
		}

		private static void RunTaskThread(BaseTask task)
		{
			var status = "";

			var errorLogFile = Path.Combine(Defines.LogsDir, "error.log");
			try
			{
				BaseTask.DeleteFile(errorLogFile);

				if (!task.Configure())
					return;

				if (!task.StartupWarning())
					return;

				try
				{
					task.Run();

					if (cancelSource.IsCancellationRequested)
						throw new OperationCanceledException();
				}
				catch (OperationCanceledException e)
				{
					/*Invoke(new Action(() =>
					{
						labelStatus.Text = "Cancelled";
						if (e.Message != new OperationCanceledException().Message)
							labelStatus.Text += ": " + e.Message;
					}));*/

					Console.WriteLine();
					Console.WriteLine(status = "Cancelled" + (e.Message != new OperationCanceledException().Message ? ": " + e.Message : ""));

					return;
				}

				if (task.Failed() || task.Warnings())
					task.Finished();

				/*Invoke(new Action(() =>
				{
					labelStatus.Text = task.Failed() ? "Failed" : "Done";
				}));*/

				Console.WriteLine();
				Console.WriteLine(status = task.Failed() ? "Failed" : "Done");
			}
			catch (Exception e)
			{
				/*var status = "";
				Invoke(new Action(() =>
				{
					status = labelStatus.Text;
					labelStatus.Text = "Error: " + e.Message.Trim();
				}));*/

				Console.WriteLine();
				Console.WriteLine("Error: " + e.Message.Trim());

				BaseTask.CreateDirectory(Defines.LogsDir);
				File.WriteAllText(errorLogFile, status + "\r\n" + e);
			}
			finally
			{
				/*Invoke(new Action(() =>
				{
					foreach (var b in taskButtons.Keys) b.Enabled = true;
					buttonCancel.Enabled = false;
					progressBar.Value = 0;
					if (closeOnCancel) Close();
				}));*/
			}
		}
	}
}
