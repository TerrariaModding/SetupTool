using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SetupTool.Tasks
{
	public interface ITaskInterface
	{
		void SetMaxProgress(int max);

		void SetStatus(string status);

		void SetProgress(int progress);

		CancellationToken CancellationToken { get; }

		object Invoke(Delegate action);

		//IAsyncResult BeginInvoke(Delegate action);
	}
}
