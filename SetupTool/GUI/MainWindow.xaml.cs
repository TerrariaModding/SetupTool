using SetupTool.Tasks;
using SetupTool.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SetupTool.GUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, ITaskInterface
	{
		private static CancellationTokenSource cancelSource;
		private bool closeOnCancel;

		public MainWindow()
		{
			InitializeComponent();
		}

		public CancellationToken CancellationToken => cancelSource.Token;

		/*public IAsyncResult BeginInvoke(Delegate action)
		{
			return Dispatcher.BeginInvoke(action);
		}*/

		public object Invoke(Delegate action)
		{
			return Dispatcher.Invoke(action);
		}

		public void SetMaxProgress(int max)
		{
			if (!CheckAccess())
			{
				Dispatcher.Invoke(() => SetMaxProgress(max));
				return;
			}
			pbMain.Maximum = max;
		}

		public void SetProgress(int progress)
		{
			if (!CheckAccess())
			{
				Dispatcher.Invoke(() => SetProgress(progress));
				return;
			}
			pbMain.Value = progress;
		}

		public void SetStatus(string status)
		{
			if (!CheckAccess())
			{
				Dispatcher.Invoke(() => SetStatus(status));
				return;
			}
			lblStatus.Text = status;
		}

		private void btnSetup_Click(object sender, RoutedEventArgs e)
		{
			RunTask(new SetupTask(this,
				new DecompileTask(this, Defines.ProjectConfig.DecompiledSrcDir),
				new ProcessProjectTask(this, Defines.ProjectConfig.Projects)
			));
		}

		private void btnDiff_Click(object sender, RoutedEventArgs e)
		{
			if (lstProjects.SelectedItem == null)
				return;
            string name = lstProjects.SelectedItem.ToString()?.Split('-')[0];

			var projects = Defines.ProjectConfig.Projects;
			if (!projects.ContainsKey(name))
				return;

			var project = projects[name];
			
			string baseDir = Defines.ProjectConfig.DecompiledSrcDir;
			if (!string.IsNullOrEmpty(project.Parent))
			{
				if (!projects.ContainsKey(project.Parent))
					throw new Exception($"Missing parent '{project.Parent}' for project '{project.Name}'");
				baseDir = System.IO.Path.Combine(Defines.ProjectConfig.SrcDir, projects[project.Parent].SrcDir);
			}

			RunTask(new DiffTask(this,
				baseDir,
				System.IO.Path.Combine(Defines.ProjectConfig.SrcDir, project.SrcDir),
				System.IO.Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir),
				new JsonProperty<DateTime>(Defines.Settings, project.Name + "DiffCutoff", new DateTime(2015, 01, 01)))
			);
		}

		private void btnPatch_Click(object sender, RoutedEventArgs e)
		{
			if (lstProjects.SelectedItem == null)
				return;
			string name = lstProjects.SelectedItem.ToString()?.Split('-')[0];

			var projects = Defines.ProjectConfig.Projects;
			if (!projects.ContainsKey(name))
				return;

			var project = projects[name];

			string baseDir = Defines.ProjectConfig.DecompiledSrcDir;
			if (!string.IsNullOrEmpty(project.Parent))
			{
				if (!projects.ContainsKey(project.Parent))
					throw new Exception($"Missing parent '{project.Parent}' for project '{project.Name}'");
				baseDir = System.IO.Path.Combine(Defines.ProjectConfig.SrcDir, projects[project.Parent].SrcDir);
			}

			RunTask(new PatchTask(this,
				baseDir,
				System.IO.Path.Combine(Defines.ProjectConfig.SrcDir, project.SrcDir),
				System.IO.Path.Combine(Defines.ProjectConfig.PatchesDir, project.PatchesDir),
				new JsonProperty<DateTime>(Defines.Settings, project.Name + "DiffCutoff", new DateTime(2015, 01, 01)),
                project.Name)
			);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			var projects = Defines.ProjectConfig.Projects;

			foreach (var project in projects)
            {
                string name = project.Value.Name + "-Reliant on: ";

                if (project.Value.ReliantOn.Count > 0)
                    name += string.Join(", ", project.Value.ReliantOn);
                else
                    name += "Nothing";

				lstProjects.Items.Add(name);
            }
		}

		private void RunTask(BaseTask task)
		{
			cancelSource = new CancellationTokenSource();
			btnSetup.IsEnabled = false;
			btnDiff.IsEnabled = false;
			btnPatch.IsEnabled = false;
			btnCancel.IsEnabled = true;

			new Thread(() => RunTaskThread(task)).Start();
		}

		private void RunTaskThread(BaseTask task)
		{
			var errorLogFile = System.IO.Path.Combine(Defines.ProjectConfig.LogsDir, "error.log");
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
					Invoke(new Action(() =>
					{
						lblStatus.Text = "Cancelled";
						if (e.Message != new OperationCanceledException().Message)
							lblStatus.Text += ": " + e.Message;
					}));

					return;
				}

                if (task.Failed() || task.Warnings())
                    task.Finished();

				Invoke(new Action(() =>
				{
					lblStatus.Text = task.Failed() ? "Failed" : "Done";
				}));
			}
			catch (Exception e)
			{
				var status = "";
				Invoke(new Action(() =>
				{
					status = lblStatus.Text;
					lblStatus.Text = "Error: " + e.Message.Trim();
				}));

				BaseTask.CreateDirectory(Defines.ProjectConfig.LogsDir);
				File.WriteAllText(errorLogFile, status + "\r\n" + e);
			}
			finally
			{
				Invoke(new Action(() =>
				{
					btnSetup.IsEnabled = true;
					btnDiff.IsEnabled = true;
					btnPatch.IsEnabled = true;
					btnCancel.IsEnabled = false;
					pbMain.Value = 0;
					if (closeOnCancel)
						Close();
				}));
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (btnCancel.IsEnabled)
			{
				cancelSource.Cancel();
				e.Cancel = true;
				closeOnCancel = true;
			}
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			cancelSource.Cancel();
		}
    }
}
