using SetupTool.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SetupTool.Tasks
{
	public class SetupTask : CompositeTask
	{
		public SetupTask(ITaskInterface taskInterface, params BaseTask[] tasks) : base(taskInterface, tasks) { }

		public override bool StartupWarning()
		{
			return MessageBox.Show(
					   "Any changes in /src will be lost.\r\n",
					   "Ready for Setup", MessageBoxButton.OKCancel, MessageBoxImage.Information)
				   == MessageBoxResult.OK;
		}
	}
}
