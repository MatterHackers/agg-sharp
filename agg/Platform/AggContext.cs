/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.Font;
using System;
using System.Threading;

namespace MatterHackers.Agg.Platform
{
	public enum OSType
	{
		Unknown,
		Windows,
		Mac,
		X11,
		Other,
		Android
	}

	public static class AggContext
	{
		// Guards lazy creation of the providers below. Monitor is reentrant, so the
		// FileDialogs/OsInformation getters may safely resolve Config while holding it.
		private static readonly object initLock = new object();

		private static volatile IFileDialogProvider _fileDialogs = null;
		private static volatile IOsInformationProvider _osInformation = null;
		private static volatile PlatformConfig _config = null;

		/// <summary>
		/// Construct the specified type from a fully qualified typename
		/// </summary>
		/// <typeparam name="T">The type to construct</typeparam>
		/// <param name="typeString">The fully qualified typename: i.e. "MyNamespace.MyType, MyAssembly"</param>
		/// <returns>An instance of the given type</returns>
		public static T CreateInstanceFrom<T>(string typeString) where T : class
		{
			var type = Type.GetType(typeString);
			return (type == null) ? null : Activator.CreateInstance(type) as T;
		}


		public static IFileDialogProvider FileDialogs
		{
			get
			{
				if (_fileDialogs == null)
				{
					lock (initLock)
					{
						if (_fileDialogs == null)
						{
							// FileDialog Provider
							_fileDialogs = CreateInstanceFrom<IFileDialogProvider>(Config.ProviderTypes.DialogProvider);
						}
					}
				}

				return _fileDialogs;
			}

			set
			{
				_fileDialogs = value;
			}
		}

		public static IOsInformationProvider OsInformation
		{
			get
			{
				if (_osInformation == null)
				{
					lock (initLock)
					{
						if (_osInformation == null)
						{
							// OsInformation Provider
							_osInformation = CreateInstanceFrom<IOsInformationProvider>(Config.ProviderTypes.OsInformationProvider);
						}
					}
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

		public static PlatformConfig Config
		{
			get
			{
				if (_config == null)
				{
					lock (initLock)
					{
						if (_config == null)
						{
							_config = new PlatformConfig();
						}
					}
				}

				return _config;
			}
		}

		public class PlatformConfig
		{
			public ProviderSettings ProviderTypes { get; set; } = new ProviderSettings();

			public AggGraphicsMode GraphicsMode { get; set; } = new AggGraphicsMode();
		}

		/// <summary>
		/// Which platform assembly the lazily-created providers come from. Defaulted per OS rather than
		/// hard-coded to Windows so that a demo (or an application) needs no source change to run on macOS:
		/// the win32 assembly is built for <c>net10.0-windows</c> and would not even load there.
		/// </summary>
		public class ProviderSettings
		{
			private static readonly bool IsMac = System.OperatingSystem.IsMacOS();

			public string OsInformationProvider { get; set; } = IsMac
				? "MatterHackers.Agg.Platform.MacInformationProvider, agg_platform_mac"
				: "MatterHackers.Agg.Platform.WinformsInformationProvider, agg_platform_win32";

			public string DialogProvider { get; set; } = IsMac
				? "MatterHackers.Agg.Platform.MacFileDialogProvider, agg_platform_mac"
				: "MatterHackers.Agg.Platform.WinformsFileDialogProvider, agg_platform_win32";

			public string SystemWindowProvider { get; set; } = IsMac
				? "MatterHackers.Agg.UI.WebGpuMacWindowProvider, agg_platform_mac"
				: "MatterHackers.Agg.UI.WebGpuWinformsWindowProvider, agg_platform_win32";
		}

		public class AggGraphicsMode
		{
			/// <summary>
			/// Gets or sets the ColorFormat of the color buffer - when cast from int, constructs a new ColorFormat with the specified aggregate bits per pixel
			/// </summary>
			public int Color { get; set; } = 32;

			/// <summary>
			/// Gets or sets the number of bits in the depth buffer - a System.Int32 that contains the bits per pixel for the depth buffer
			/// </summary>
			public int Depth { get; set; } = 24;

			/// <summary>
			/// Gets or sets the number of bits in the stencil buffer - a System.Int32 that contains the bits per pixel for the stencil buffer
			/// </summary>
			public int Stencil { get; set; } = 0;

			/// <summary>
			/// Gets or sets the number of samples for FSAA - a System.Int32 that contains the number of FSAA samples per pixel
			/// </summary>
			public int FSAASamples { get; set; } = 8;
		}

		// Lazy backing fields so that touching AggContext does not parse the embedded
		// SVG fonts under the CLR type-initialization lock. A set replaces the whole
		// Lazy (reference assignment is atomic), so readers see either the old or the
		// new fully-resolved value.
		private static volatile Lazy<TypeFace> _defaultFont = new Lazy<TypeFace>(() => LiberationSansFont.Instance, LazyThreadSafetyMode.ExecutionAndPublication);
		private static volatile Lazy<TypeFace> _defaultFontBold = new Lazy<TypeFace>(() => LiberationSansBoldFont.Instance, LazyThreadSafetyMode.ExecutionAndPublication);
		private static volatile Lazy<TypeFace> _defaultFontItalic = new Lazy<TypeFace>(() => LiberationSansFont.Instance, LazyThreadSafetyMode.ExecutionAndPublication);
		private static volatile Lazy<TypeFace> _defaultFontBoldItalic = new Lazy<TypeFace>(() => LiberationSansBoldFont.Instance, LazyThreadSafetyMode.ExecutionAndPublication);

		private static Lazy<TypeFace> AlreadyResolved(TypeFace value)
		{
			return new Lazy<TypeFace>(() => value, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		public static TypeFace DefaultFont
		{
			get => _defaultFont.Value;
			set => _defaultFont = AlreadyResolved(value);
		}

		public static TypeFace DefaultFontBold
		{
			get => _defaultFontBold.Value;
			set => _defaultFontBold = AlreadyResolved(value);
		}

		public static TypeFace DefaultFontItalic
		{
			get => _defaultFontItalic.Value;
			set => _defaultFontItalic = AlreadyResolved(value);
		}

		public static TypeFace DefaultFontBoldItalic
		{
			get => _defaultFontBoldItalic.Value;
			set => _defaultFontBoldItalic = AlreadyResolved(value);
		}
	}
}
