/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The disk-backed <see cref="IStaticData"/>: every path is resolved against <see cref="RootPath"/>
	/// and read straight from the filesystem.
	/// </summary>
	public class StaticData : IStaticData
	{
		private static Dictionary<string, ImageBuffer> cachedImages = new Dictionary<string, ImageBuffer>();
		private static Dictionary<(string, int, int, string), ImageBuffer> cachedIcons = new Dictionary<(string, int, int, string), ImageBuffer>();

		private StaticData()
		{
			string appPathAndFile = Assembly.GetExecutingAssembly().Location;
			string pathToAppFolder = Path.GetDirectoryName(appPathAndFile);

			if (string.IsNullOrEmpty(RootPath))
			{
				RootPath = Path.Combine(pathToAppFolder, "StaticData");
			}
		}

		// Guards singleton creation and RootPath so the constructor's default RootPath write
		// cannot race another thread's read or write of RootPath.
		private static readonly object instanceLocker = new object();

		private static IStaticData _instance = null;

		private static string rootPath;

		public static double DeviceScale => GuiWidget.DeviceScale;

		/// <summary>
		/// Gets or sets the process-wide asset provider. Defaults to the disk-backed implementation;
		/// a host with no filesystem (WASM, for one) assigns its own provider during startup, before
		/// anything reads an asset. Assigning after the default has been created is allowed but the
		/// icons already handed out will have come from disk.
		/// </summary>
		public static IStaticData Instance
		{
			get
			{
				lock (instanceLocker)
				{
					if (_instance == null)
					{
						_instance = new StaticData();
					}

					return _instance;
				}
			}

			set
			{
				// Refuse null loudly. Clearing the instance would silently fall back to lazily building
				// the disk-backed provider, so a host with no filesystem would not fail here - it would
				// fail much later with "Bad icon load", nowhere near the assignment that caused it.
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value), "StaticData.Instance cannot be set to null.");
				}

				lock (instanceLocker)
				{
					_instance = value;
				}
			}
		}

		public static string RootPath
		{
			get
			{
				lock (instanceLocker)
				{
					return rootPath;
				}
			}

			set
			{
				lock (instanceLocker)
				{
					rootPath = value;
				}
			}
		}

		public static void OverrideRootPath(string overridePath)
		{
			Console.WriteLine("   Overriding StaticData: " + Path.GetFullPath(overridePath));
			RootPath = overridePath;
		}

		public void PurgeCache()
		{
			cachedImages.Clear();
		}

		public bool DirectoryExists(string path)
		{
			return Directory.Exists(MapPath(path));
		}

		public bool FileExists(string path)
		{
			return File.Exists(MapPath(path));
		}

		public IEnumerable<string> GetDirectories(string path)
		{
			return Directory.GetDirectories(MapPath(path));
		}

		/// <summary>
		/// Gets the files of the given directory as paths relative to <see cref="RootPath"/>, so that
		/// they can be passed straight back to the other members (which all map through RootPath).
		/// </summary>
		/// <remarks>
		/// This used to chop the string at the first literal "StaticData" it found, which produced
		/// garbage whenever "StaticData" appeared earlier in the root (a user named StaticData, a
		/// build folder, a temp root) and whenever the root folder was not called "StaticData" at all.
		/// </remarks>
		public IEnumerable<string> GetFiles(string path)
		{
			// Map "" rather than using RootPath directly - RootPath may be relative, and the paths
			// coming back from Directory.GetFiles are always full.
			var fullRoot = MapPath("");
			return Directory.GetFiles(MapPath(path)).Select(p => Path.GetRelativePath(fullRoot, p));
		}

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="invertImage">describes if the icon should be inverted</param>
		/// <returns>An ImageBuffer initialized with data from the given file</returns>
		public ImageBuffer LoadIcon(string path)
		{
			var fullPath = Path.Combine("Icons", path);
			if (FileExists(fullPath))
			{
				if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
				{
					var defaultSize = (int)(16 * DeviceScale);
					return SvgParser.ParseAndRender(MapPath(fullPath), defaultSize, defaultSize);
				}

				var icon = LoadImage(fullPath);
				return (DeviceScale == 1) ? icon : icon.CreateScaledImage(DeviceScale);
			}

			return null;
		}

        /// <summary>
        /// Load the specified file from the StaticData/Icons path and scale it to the given size,
        /// adjusting for the device scale in GuiWidget
        /// </summary>
        /// <param name="path">The file path to load</param>
        /// <param name="width">Width to scale to</param>
        /// <param name="height">Height to scale to</param>
        /// <param name="invertImage">describes if the icon should be inverted</param>
        /// <param name="processing">Optional function to process the source image before scaling.</param>
        /// <returns>The image buffer at the right scale</returns>
        public ImageBuffer LoadIcon(string path,
			int width,
			int height,
			bool invertImage = false,
            Func<ImageBuffer, (ImageBuffer processed, string key)> processSource = null)
		{
			int deviceWidth = (int)(width * DeviceScale);
			int deviceHeight = (int)(height * DeviceScale);

			ImageBuffer cachedIcon;
			lock (locker)
			{
				var key = "";
				if (processSource != null)
                {
                    (_, key) = processSource(null);
                }
				if (!cachedIcons.TryGetValue((path, deviceWidth, deviceHeight, key), out cachedIcon))
				{
					// SVG icons are rendered directly at the device-scaled size for best quality
					if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
					{
						var svgFullPath = MapPath(Path.Combine("Icons", path));
						cachedIcon = File.Exists(svgFullPath)
							? SvgParser.ParseAndRender(svgFullPath, deviceWidth, deviceHeight)
							: null;
					}
					else
					{
						cachedIcon = LoadIcon(path);
					}

					if (cachedIcon == null)
					{
#if DEBUG
						throw new Exception("Bad icon load");
#else
						// try to load a image to show the bad load
						cachedIcon = LoadIcon("bad_load.png");
						if (cachedIcon == null)
						{
							// create an image so things don't crash
							cachedIcon = new ImageBuffer(32, 32);
							var graphics = cachedIcon.NewGraphics2D();
							graphics.FillRectangle(cachedIcon.GetBounds(), Color.White);
							graphics.DrawLine(Color.White, new Vector2(0, 0), new Vector2(cachedIcon.Width, cachedIcon.Height));
							graphics.DrawLine(Color.White, new Vector2(0, cachedIcon.Height), new Vector2(cachedIcon.Width, 0));
						}
#endif
					}

					if (processSource != null)
                    {
						cachedIcon = processSource(cachedIcon).processed;
                    }

					cachedIcon.SetRecieveBlender(new BlenderPreMultBGRA());

					// Scale if required. Both sides of this comparison are device pixels: the loader above
					// already applied DeviceScale, and the caller's width/height are design pixels. Comparing
					// against the design size instead let an icon through unscaled whenever its source size
					// times DeviceScale happened to equal the requested design size - half of what was asked for.
					if (cachedIcon.Width != deviceWidth || cachedIcon.Height != deviceHeight)
					{
						cachedIcon = cachedIcon.CreateScaledImage(deviceWidth, deviceHeight);
					}

					// Make the premultiplied stamp above true where it matters most: nothing that is fully
					// transparent may still carry color. Scaling is the last thing that can reintroduce it -
					// it averages color and alpha separately, so an edge pixel can land on alpha 0 with the
					// color of its neighbours - and a premultiplied blender adds that color outright.
					cachedIcon.ClearFullyTransparentPixels();

					// only cache relatively small images
					if (cachedIcon.Width < 200 && cachedIcon.Height < 200)
					{
						cachedIcons.Add((path, deviceWidth, deviceHeight, key), cachedIcon);
					}
				}
			}

			var cacheCopy = new ImageBuffer(cachedIcon);

			// Themed icons are black and need be inverted on dark themes, or when white icons are requested
			if (invertImage)
			{
				cacheCopy = cacheCopy.InvertLightness();
				cacheCopy.SetRecieveBlender(new BlenderPreMultBGRA());
				cacheCopy.ClearFullyTransparentPixels();
			}

			return cacheCopy;
		}

		public ImageSequence LoadSequence(string path)
		{
			ImageSequence sequence = null;

			if (DirectoryExists(path))
			{
				sequence = new ImageSequence();
				string propertiesPath = Path.Combine(path, "properties.json");
				if (FileExists(propertiesPath))
				{
					string jsonData = ReadAllText(propertiesPath);

					var properties = JsonConvert.DeserializeObject<ImageSequence.Properties>(jsonData);
					sequence.FramesPerSecond = properties.FramePerFrame;
					sequence.Looping = properties.Looping;
				}

				var pngFiles = GetFiles(path).Where(fileName => Path.GetExtension(fileName).ToUpper() == ".PNG").OrderBy(s => s);
				foreach (string pngPath in pngFiles)
				{
					ImageBuffer image = LoadImage(pngPath);
					sequence.AddImage(image);
				}
			}
			else if (this.FileExists(path)
				&& string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
			{
				sequence = new ImageSequence();

				using (var fileStream = this.OpenStream(path))
				{
					LoadImageSequenceData(fileStream, sequence);
				}
			}

			return sequence;
		}

		public void LoadImageData(Stream imageStream, ImageBuffer destImage)
		{
			destImage.CopyFrom(ImageIO.LoadImage(imageStream));
		}

		public void LoadImageSequenceData(Stream stream, ImageSequence sequence)
		{
			lock (locker)
			{
				ImageIO.LoadImageData(stream, sequence);
			}
		}

		private static object locker = new object();

		private void LoadImage(string path, ImageBuffer destImage)
		{
			lock (locker)
			{
				if (!cachedImages.TryGetValue(path, out ImageBuffer cachedImage))
				{
					cachedImage = ImageIO.LoadImage(MapPath(path));

					if (cachedImage.Width < 200 && cachedImage.Height < 200)
					{
						// only cache relatively small images
						cachedImages.Add(path, cachedImage);
					}
				}

				destImage.CopyFrom(cachedImage);
			}
		}

		public ImageBuffer LoadImage(string path)
		{
			ImageBuffer temp = new ImageBuffer();
			LoadImage(path, temp);

			return temp;
		}

		public Stream OpenStream(string path)
		{
			return File.OpenRead(MapPath(path));
		}

		public string[] ReadAllLines(string path)
		{
			return File.ReadLines(MapPath(path)).ToArray();
		}

		public string ReadAllText(string path)
		{
			var allText = File.ReadAllText(MapPath(path));
            return allText;
		}

		public string MapPath(string path)
		{
			return Path.GetFullPath(Path.Combine(RootPath, path));
		}
	}
}
