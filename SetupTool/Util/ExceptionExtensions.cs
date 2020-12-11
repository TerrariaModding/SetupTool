using System;
using System.Collections.Generic;

namespace SetupTool.Util
{
	public static class ExceptionExtensions
	{
		public static void PrintStackTrace(this Exception e, bool fullSubStackTraces = false, int depth = -1)
		{
			List<Exception> exceptions = new List<Exception>();

			e.EnumerateExceptions(exceptions, 0, depth);

			Console.WriteLine(e.GetMessageWithStackTrace());

			var lastException = e;
			foreach (var ex in exceptions)
			{
				if (!string.IsNullOrWhiteSpace(lastException.StackTrace))
					Console.WriteLine();
				lastException = ex;
				Console.WriteLine($"Caused by: {ex.GetMessageWithStackTrace(fullSubStackTraces)}");
			}
		}

		public static string GetMessageWithStackTrace(this Exception e, bool fullStackTrace = true)
		{
			// TODO: If fullStackTrace is false then only show first line of stack trace

			var msg = $"{e.GetType().FullName}: {e.Message}";
			if (fullStackTrace && !string.IsNullOrWhiteSpace(e.StackTrace))
				msg += $"\n{e.StackTrace}";

			return msg;
		}

		private static void EnumerateExceptions(this Exception e,
												List<Exception> exceptions,
												int currentDepth,
												int maxDepth)
		{
			if (currentDepth >= maxDepth && maxDepth != -1)
				return;

			if (e.InnerException != null)
			{
				exceptions.Add(e.InnerException);

				e.InnerException.EnumerateExceptions(exceptions, currentDepth + 1, maxDepth);
			}
		}
	}
}
