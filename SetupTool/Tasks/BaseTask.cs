using SetupTool.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Tasks
{
	public abstract class BaseTask
	{
		protected delegate void UpdateStatus(string status);
		protected delegate void Worker(UpdateStatus updateStatus);

		protected class WorkItem
		{
			public readonly string status;
			public readonly Worker worker;

			public WorkItem(string status, Worker worker)
			{
				this.status = status;
				this.worker = worker;
			}

			public WorkItem(string status, Action action) : this(status, _ => action()) { }
		}

		protected void ExecuteParallel(List<WorkItem> items, bool resetProgress = true, int maxDegree = 0)
		{
			try
			{
				if (resetProgress)
				{
					TaskInterface.SetMaxProgress(items.Count());
					Progress = 0;
				}

				var working = new List<Ref<string>>();
				void UpdateStatus() => TaskInterface.SetStatus(string.Join("\r\n", working.Select(r => r.Item)));

				Parallel.ForEach(Partitioner.Create(items, EnumerablePartitionerOptions.NoBuffering),
					new ParallelOptions { MaxDegreeOfParallelism = maxDegree > 0 ? maxDegree : Environment.ProcessorCount },
					item => {
						TaskInterface.CancellationToken.ThrowIfCancellationRequested();
						var status = new Ref<string>(item.status);
						lock (working)
						{
							working.Add(status);
							UpdateStatus();
						}

						void SetStatus(string s)
						{
							lock (working)
							{
								status.Item = s;
								UpdateStatus();
							}
						}

						item.worker(SetStatus);

						lock (working)
						{
							working.Remove(status);
							TaskInterface.SetProgress(++Progress);
							UpdateStatus();
						}
					});
			}
			catch (AggregateException ex)
			{
				var actual = ex.Flatten().InnerExceptions.Where(e => !(e is OperationCanceledException));
				if (!actual.Any())
					throw new OperationCanceledException();

				throw new AggregateException(actual);
			}
		}

		public static string PreparePath(string path)
			=> path.Replace('/', Path.DirectorySeparatorChar);

		public static string RelPath(string basePath, string path)
		{
			if (path.Last() == Path.DirectorySeparatorChar)
				path = path.Substring(0, path.Length - 1);

			if (basePath.Last() != Path.DirectorySeparatorChar)
				basePath += Path.DirectorySeparatorChar;

			if (path + Path.DirectorySeparatorChar == basePath) return "";

			if (!path.StartsWith(basePath))
			{
				path = Path.GetFullPath(path);
				basePath = Path.GetFullPath(basePath);
			}

			if (!path.StartsWith(basePath))
				throw new ArgumentException("Path \"" + path + "\" is not relative to \"" + basePath + "\"");

			return path.Substring(basePath.Length);
		}

		public static void CreateDirectory(string dir)
		{
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
		}

		public static void CreateParentDirectory(string path)
		{
			CreateDirectory(Path.GetDirectoryName(path));
		}

		public static void DeleteFile(string path)
		{
			if (File.Exists(path))
			{
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
			}
		}

		public static void Copy(string from, string to)
		{
			CreateParentDirectory(to);

			if (File.Exists(to))
			{
				File.SetAttributes(to, FileAttributes.Normal);
			}

			File.Copy(from, to, true);
		}

		public static IEnumerable<(string file, string relPath)> EnumerateFiles(string dir) =>
			Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
			.Select(path => (file: path, relPath: RelPath(dir, path)));

		public static void DeleteAllFiles(string dir)
		{
			foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
			{
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}
		}

		public static bool DeleteEmptyDirs(string dir)
		{
			if (!Directory.Exists(dir))
				return true;

			return DeleteEmptyDirsRecursion(dir);
		}

		private static bool DeleteEmptyDirsRecursion(string dir)
		{
			bool allEmpty = true;

			foreach (string subDir in Directory.EnumerateDirectories(dir))
				allEmpty &= DeleteEmptyDirsRecursion(subDir);

			if (!allEmpty || Directory.EnumerateFiles(dir).Any())
				return false;

			Directory.Delete(dir);

			return true;
		}

		protected readonly ITaskInterface TaskInterface;
		protected int Progress;

		protected BaseTask(ITaskInterface taskInterface)
		{
			TaskInterface = taskInterface;
		}

		public abstract void Run();

		public virtual bool Configure() => true;

		public virtual bool StartupWarning() => true;

		public virtual bool Failed() => false;

		public virtual bool Warnings() => false;

		public virtual void Finished() { }
	}
}
