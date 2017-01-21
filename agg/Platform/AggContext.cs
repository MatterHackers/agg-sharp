using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace MatterHackers.Agg.Platform
{
	public enum OSType { Unknown, Windows, Mac, X11, Other, Android };

	// TODO: alternate names AggConfig, AggContext, AggPlatform
	public static class AggContext
	{
		// Takes a full typename string: i.e. "MyNamespace.MyType, MyAssembly"
		public static T CreateInstanceFrom<T>(string typeString) where T : class
		{
			var type = Type.GetType(typeString);
			return (type == null) ? null : Activator.CreateInstance(type) as T;
		}

		public static IImageIOProvider ImageIO { get; set; }
		public static IFileDialogProvider FileDialogs { get; set; }
		public static IStaticData StaticData { get; set; }
		public static OSType OperatingSystem { get; set; }
		public static Point2D DesktopSize { get; set; }
		public static PlatformConfig Config { get; set; }

		private static IOsInformationProvider osInformation = null;
		public static IOsInformationProvider OsInformation
		{
			get { return OsInformation; }
			set
			{
				if (osInformation != value)
				{
					osInformation = value;
					OperatingSystem = osInformation?.OperatingSystem ?? OSType.Unknown;
					DesktopSize = osInformation?.DesktopSize ?? new Point2D();
				}
			}
		}

		static AggContext()
		{
			Init();
		}

		public static void Init()
		{
			PlatformConfig config = null;

			if (File.Exists("config.json"))
			{
				// Use the file system based config or fall back to empty
				config = JsonConvert.DeserializeObject<PlatformConfig>(File.ReadAllText("config.json"));
			}
			else
			{
				// Look for and use an embedded config
				var resourceStream = Assembly.GetCallingAssembly()?.GetManifestResourceStream("config.json");
				if (resourceStream != null)
				{
					using (var reader = new StreamReader(resourceStream))
					{
						config = JsonConvert.DeserializeObject<PlatformConfig>(reader.ReadToEnd());
					}
				}
			}

			Init(config);
		}

		public static void Init(PlatformConfig config)
		{
			Config = config ?? new PlatformConfig();

			// OsInformation Provider
			OsInformation = CreateInstanceFrom<IOsInformationProvider>(Config.ProviderTypes.OsInformationProvider);

			// ImageIO Provider
			ImageIO = CreateInstanceFrom<IImageIOProvider>(Config.ProviderTypes.ImageIOProvider);

			// FileDialog Provider
			FileDialogs = CreateInstanceFrom<IFileDialogProvider>(Config.ProviderTypes.DialogProvider);

			// StaticData Provider
			StaticData = CreateInstanceFrom<IStaticData>(Config.ProviderTypes.StaticDataProvider);
		}

		public class PlatformConfig
		{
			public ProviderSettings ProviderTypes { get; set; }
		}

		public class ProviderSettings
		{
			public string OsInformationProvider { get; set; }
			public string DialogProvider { get; set; }
			public string ImageIOProvider { get; set; }
			public string StaticDataProvider { get; set; }
			public string SystemWindowProvider { get; set; }
		}

		public class SliceEngineSettings
		{
			public bool RunInProcess { get; set; } = false;
		}
	}
}
