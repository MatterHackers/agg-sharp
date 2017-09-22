using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace MatterHackers.Agg.Platform
{
	public enum OSType { Unknown, Windows, Mac, X11, Other, Android };

	public static class AggContext
	{
		/// <summary>
		/// Construct the specified type from a fully qualified typename
		/// </summary>
		/// <typeparam name="T">The type to construct</typeparam>
		/// <param name="typeString">The fully qualified typename: i.e. "MyNamespace.MyType, MyAssembly"</param>
		/// <returns></returns>
		public static T CreateInstanceFrom<T>(string typeString) where T : class
		{
			var type = Type.GetType(typeString);
			return (type == null) ? null : Activator.CreateInstance(type) as T;
		}

		private static IImageIOProvider _imageIO = null;
		public static IImageIOProvider ImageIO
		{
			get
			{
				if (_imageIO == null)
				{
					// ImageIO Provider
					ImageIO = CreateInstanceFrom<IImageIOProvider>(Config.ProviderTypes.ImageIOProvider);
				}

				return _imageIO;
			}
			set
			{
				_imageIO = value;
			}
		}

		private static IFileDialogProvider _fileDialogs = null;
		public static IFileDialogProvider FileDialogs
		{
			get
			{
				if (_fileDialogs == null)
				{
					// FileDialog Provider
					FileDialogs = CreateInstanceFrom<IFileDialogProvider>(Config.ProviderTypes.DialogProvider);
				}

				return _fileDialogs;
			}
			set
			{
				_fileDialogs = value;
			}
		}

		private static IStaticData _staticData = null;
		public static IStaticData StaticData
		{
			get
			{
				if (_staticData == null)
				{
					// StaticData Provider
					StaticData = CreateInstanceFrom<IStaticData>(Config.ProviderTypes.StaticDataProvider);
				}

				return _staticData;
			}
			set
			{
				_staticData = value;
			}
		}

		private static IOsInformationProvider _osInformation = null;
		public static IOsInformationProvider OsInformation
		{
			get
			{
				if (_osInformation == null)
				{
					// OsInformation Provider
					OsInformation = CreateInstanceFrom<IOsInformationProvider>(Config.ProviderTypes.OsInformationProvider);
				}

				return _osInformation;
			}
			set
			{
				_osInformation = value;
			}
		}

		public static OSType OperatingSystem => OsInformation.OperatingSystem;
		public static Point2D DesktopSize => OsInformation.DesktopSize;

		private static PlatformConfig _config = null;
		public static PlatformConfig Config
		{
			get
			{
				if(_config	== null)
				{
					if (File.Exists("config.json"))
					{
						// Use the file system based config or fall back to empty
						_config = JsonConvert.DeserializeObject<PlatformConfig>(File.ReadAllText("config.json"));
					}
					else
					{
						// On desktop/msbuild the config.json embedded resource does not have a namespace qualifier. On Android/Mono it does and the caller should pass the right/full resource name
						LoadConfigFromCallingAssembly("config.json");
					}
				}

				return _config;
			}
			set
			{
				_config = value;
			}
		}

		public static void LoadConfigFromCallingAssembly(string embeddedResourceName)
		{
			// Look for and use an embedded config
			var resourceStream = Assembly.GetCallingAssembly()?.GetManifestResourceStream(embeddedResourceName);
			if (resourceStream != null)
			{
				using (var reader = new StreamReader(resourceStream))
				{
					AggContext.Config = JsonConvert.DeserializeObject<PlatformConfig>(reader.ReadToEnd());
				}
			}
		}

		public class PlatformConfig
		{
			public ProviderSettings ProviderTypes { get; set; }
            public OpenScadSettings OpenScad { get; set; }
		}

		public class ProviderSettings
		{
			public string OsInformationProvider { get; set; }
			public string DialogProvider { get; set; }
			public string ImageIOProvider { get; set; }
			public string StaticDataProvider { get; set; }
			public string SystemWindowProvider { get; set; }
		}

        public class OpenScadSettings
        {
            public string ExecutablePath { get; set; }
        }

		public class SliceEngineSettings
		{
			public bool RunInProcess { get; set; } = false;
		}
	}
}
