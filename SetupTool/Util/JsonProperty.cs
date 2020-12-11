using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public class JsonProperty<T>
	{
		public readonly string Key;
		private readonly JsonMap _map;

		public JsonProperty(JsonMap map, string key, bool required = true)
		{
			_map = map;
			Key = key;

			if (!map.Contains(key))
			{
				string error = $"Missing required property '{key}' in '{Path.GetFileName(_map.SettingsPath)}'";
				Debug.WriteLine(error);

				if (required)
					map.OnError(error);
			}
		}

		public JsonProperty(JsonMap map, string key, T value)
		{
			_map = map;
			Key = key;

			if (!_map.Contains(key))
				Set(value);
		}

		public T Value
		{
			get => Get();
			set => Set(value);
		}

		public void Set(T value)
		{
			_map[Key] = value;
			_map.Save();
		}

		public T Get()
		{
			if (!_map.Contains(Key))
				return default(T);
			try
			{
				return (T)_map[Key];
			} catch
			{
				string error = $"Invalid Property '{Key}' in '{_map.SettingsPath}'";
				Debug.WriteLine(error);
				//_map.OnError(error);
				return default(T);
			}
		}

		public static implicit operator T(JsonProperty<T> that) => that == null ? default(T) : that.Value;
	}
}
