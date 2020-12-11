using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public static class JsonUtil
	{
		public static object GetProperValue(object obj)
		{
			if (obj != null && obj.GetType() == typeof(JArray))
			{
				JArray arr = (JArray)obj;

				if (arr.Count == 0)
					return arr.ToObject<object[]>();

				switch (arr.First.Type)
				{
					case JTokenType.Object:
						return arr.ToObject<object[]>();
					case JTokenType.Integer:
						return arr.ToObject<int[]>();
					case JTokenType.Float:
						return arr.ToObject<float[]>();
					case JTokenType.String:
						return arr.ToObject<string[]>();
					case JTokenType.Boolean:
						return arr.ToObject<bool[]>();
					case JTokenType.Date:
						return arr.ToObject<DateTime[]>();
					case JTokenType.Guid:
						return arr.ToObject<Guid[]>();
					case JTokenType.Uri:
						return arr.ToObject<Uri[]>();
					case JTokenType.TimeSpan:
						return arr.ToObject<TimeSpan[]>();
					default:
						return obj;
				}
			}

			return obj;
		}
	}
}
