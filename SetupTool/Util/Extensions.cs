using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public static class Extensions
	{
		public static string JoinStrings(this List<string> list, string separator = "\n")
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < list.Count; i++)
			{
				sb.Append(list[i]);

				if (i < list.Count - 1)
					sb.Append(separator);
			}

			return sb.ToString();
		}
	}
}
