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

		public static long PhysicalMemory => OsInformation.PhysicalMemory;
		public static OSType OperatingSystem => OsInformation.OperatingSystem;
		public static Point2D DesktopSize => OsInformation.DesktopSize;

		private static PlatformConfig _config = null;
		public static PlatformConfig Config
		{
			get
			{
				if (_config == null)
				{
					_config = new PlatformConfig();
				}

				return _config;
			}
		}

		public class PlatformConfig
		{
			public ProviderSettings ProviderTypes { get; set; } = new ProviderSettings();
			public AggGraphicsMode GraphicsMode { get; set; } = new AggGraphicsMode();
		}

		public class ProviderSettings
		{
			public string OsInformationProvider { get; set; } = "MatterHackers.Agg.Platform.WinformsInformationProvider, agg_platform_win32";
			public string DialogProvider { get; set; } = "MatterHackers.Agg.Platform.WinformsFileDialogProvider, agg_platform_win32";
			public string ImageIOProvider { get; set; } = "MatterHackers.Agg.Image.ImageIOWindowsPlugin, agg_platform_win32";
			public string StaticDataProvider { get; set; } = "MatterHackers.Agg.FileSystemStaticData, agg_platform_win32";
			public string SystemWindowProvider { get; set; } = "MatterHackers.Agg.UI.BitmapWinformsWindowProvider, agg_platform_win32";
		}

		public class AggGraphicsMode
		{
			/// <summary>
			/// The ColorFormat of the color buffer - when cast from int, constructs a new ColorFormat with the specified aggregate bits per pixel
			/// </summary>
			public int Color { get; set; } = 32;

			/// <summary>
			/// The number of bits in the depth buffer - a System.Int32 that contains the bits per pixel for the depth buffer
			/// </summary>
			public int Depth { get; set; } = 24;

			/// <summary>
			/// The number of bits in the stencil buffer - a System.Int32 that contains the bits per pixel for the stencil buffer
			/// </summary>
			public int Stencil { get; set; } = 0;

			/// <summary>
			/// The number of samples for FSAA - a System.Int32 that contains the number of FSAA samples per pixel
			/// </summary>
			public int FSAASamples { get; set; } = 8;
		}
	}
}
