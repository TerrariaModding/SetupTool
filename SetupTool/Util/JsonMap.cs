using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupTool.Util
{
	public class JsonMap
	{
		public delegate void OnErrorCallback(string error, Exception ex);
		public event OnErrorCallback ErrorCallback;

		public Dictionary<string, object> _properties = new Dictionary<string, object>();

		public readonly string SettingsPath;

		public JsonMap(string path, OnErrorCallback errorCallback = null)
		{
			SettingsPath = path;

			if (errorCallback != null)
				ErrorCallback += errorCallback;

			Load();
		}

		public object this[string propertyName]
		{
			get => _properties[propertyName];
			set => _properties[propertyName] = value;
		}

		public bool Contains(string propertyName) => _properties.ContainsKey(propertyName);

		public void Save()
		{
			try
			{
				File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(_properties, Newtonsoft.Json.Formatting.Indented));
			}
			catch (Exception ex)
			{
				OnError($"Failed to save json file: '{SettingsPath}'", ex);
			}
		}

		public void Load()
		{
			try
			{
				if (!File.Exists(SettingsPath))
					return;

				_properties = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(SettingsPath));

				// Convert JArrays to Arrays
				List<KeyValuePair<string, object>> ToChange = new List<KeyValuePair<string, object>>();

				foreach (var pair in _properties)
					ToChange.Add(new KeyValuePair<string, object>(pair.Key, JsonUtil.GetProperValue(pair.Value)));

				foreach (var pair in ToChange)
					_properties[pair.Key] = pair.Value;

			} catch (Exception ex)
			{
				OnError($"Failed to parse json file: '{SettingsPath}'", ex);
			}
		}

		public void OnError(string error, Exception ex = null)
		{
			ErrorCallback?.Invoke(error, ex);
		}
	}
}
