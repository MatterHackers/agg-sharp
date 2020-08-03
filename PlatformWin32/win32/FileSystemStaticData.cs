/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using Newtonsoft.Json;

namespace MatterHackers.Agg
{
	public class FileSystemStaticData : IStaticData
	{
		private static Dictionary<string, ImageBuffer> cachedImages = new Dictionary<string, ImageBuffer>();
		private static Dictionary<(string, int, int), ImageBuffer> cachedIcons = new Dictionary<(string, int, int), ImageBuffer>();

		private readonly string basePath;

		public FileSystemStaticData()
		{
			string appPathAndFile = Assembly.GetExecutingAssembly().Location;
			string pathToAppFolder = Path.GetDirectoryName(appPathAndFile);

			this.basePath = Path.Combine(pathToAppFolder, "StaticData");

#if DEBUG
			// In debug builds, use the StaticData folder up two directories from bin\debug, which should be MatterControl\StaticData
			if (!Directory.Exists(this.basePath))
			{
				this.basePath = Path.GetFullPath(Path.Combine(pathToAppFolder, "..", "..", "StaticData"));
			}
#endif
		}

		public FileSystemStaticData(string overridePath)
		{
			Console.WriteLine("   Overriding StaticData: " + Path.GetFullPath(overridePath));
			this.basePath = overridePath;
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

		public IEnumerable<string> GetFiles(string path)
		{
			return Directory.GetFiles(MapPath(path)).Select(p => p.Substring(p.IndexOf("StaticData") + 11));
		}

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="invertImage">describes if the icon should be inverted</param>
		/// <returns>An ImageBuffer initialized with data from the given file</returns>
		public ImageBuffer LoadIcon(string path, bool invertImage = false)
		{
			var icon = LoadImage(Path.Combine("Icons", path), invertImage);
			return (GuiWidget.DeviceScale == 1) ? icon : icon.CreateScaledImage(GuiWidget.DeviceScale);
		}

		/// <summary>
		/// Load the specified file from the StaticData/Icons path and scale it to the given size,
		/// adjusting for the device scale in GuiWidget
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="width">Width to scale to</param>
		/// <param name="height">Height to scale to</param>
		/// <param name="invertImage">describes if the icon should be inverted</param>
		/// <returns>The image buffer at the right scale</returns>
		public ImageBuffer LoadIcon(string path, int width, int height, bool invertImage = false)
		{
			int deviceWidth = (int)(width * GuiWidget.DeviceScale);
			int deviceHeight = (int)(height * GuiWidget.DeviceScale);

			lock (locker)
			{
				if (!cachedIcons.TryGetValue((path, width, height), out ImageBuffer cachedIcon))
				{
					cachedIcon = LoadIcon(path);
					cachedIcon.SetRecieveBlender(new BlenderPreMultBGRA());

					// Scale if required
					if (cachedIcon.Width != width || cachedIcon.Height != height)
					{
						cachedIcon = cachedIcon.CreateScaledImage(deviceWidth, deviceHeight);
					}

					// only cache relatively small images
					if (cachedIcon.Width < 200 && cachedIcon.Height < 200)
					{
						cachedIcons.Add((path, width, height), cachedIcon);
					}
				}
			}

			var cachedImage = new ImageBuffer(cachedIcons[(path, width, height)]);

			// Themed icons are black and need be inverted on dark themes, or when white icons are requested
			if (invertImage)
			{
				cachedImage = cachedImage.InvertLightness();
				cachedImage.SetRecieveBlender(new BlenderPreMultBGRA());
			}

			return cachedImage;
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
			using (var bitmap = new Bitmap(imageStream))
			{
				ImageIOWindowsPlugin.ConvertBitmapToImage(destImage, bitmap);
			}
		}

		public void LoadImageSequenceData(Stream stream, ImageSequence sequence)
		{
			lock (locker)
			{
				System.Drawing.Image image;
				try
				{
					image = System.Drawing.Image.FromStream(stream);
				}
				catch
				{
					return;
				}

				sequence.Frames.Clear();
				sequence.FrameTimesMs.Clear();

				var dimension = new System.Drawing.Imaging.FrameDimension(image.FrameDimensionsList[0]);
				// Number of frames
				int frameCount = image.GetFrameCount(dimension);

				if (frameCount > 1)
				{
					var minFrameTimeMs = int.MaxValue;
					for (var i = 0; i < frameCount; i++)
					{
						// Return an Image at a certain index
						image.SelectActiveFrame(dimension, i);
						ImageBuffer imageBuffer = new ImageBuffer();
						if (ImageIOWindowsPlugin.ConvertBitmapToImage(imageBuffer, new Bitmap(image)))
						{
							var frameDelay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, i * 4) * 10;

							sequence.AddImage(imageBuffer, frameDelay);
							minFrameTimeMs = Math.Max(10, Math.Min(frameDelay, minFrameTimeMs));
						}
					}

					var item = image.GetPropertyItem(0x5100); // FrameDelay in libgdiplus
															  // Time is in milliseconds
					sequence.SecondsPerFrame = minFrameTimeMs / 1000.0;
				}
				else
				{
					ImageBuffer imageBuffer = new ImageBuffer();
					if (ImageIOWindowsPlugin.ConvertBitmapToImage(imageBuffer, new Bitmap(image)))
					{
						sequence.AddImage(imageBuffer);
					}
				}
			}
		}

		private static object locker = new object();

		private void LoadImage(string path, ImageBuffer destImage, bool invertImage = false)
		{
			lock (locker)
			{
				if (!cachedImages.TryGetValue(path, out ImageBuffer cachedImage))
				{
					using (var imageStream = OpenStream(path))
					using (var bitmap = new Bitmap(imageStream))
					{
						cachedImage = new ImageBuffer();
						ImageIOWindowsPlugin.ConvertBitmapToImage(cachedImage, bitmap);
					}

					if (cachedImage.Width < 200 && cachedImage.Height < 200)
					{
						// only cache relatively small images
						cachedImages.Add(path, cachedImage);
					}
				}

				// Themed icons are black and need be inverted on dark themes, or when white icons are requested
				if (invertImage)
				{
					cachedImage = cachedImage.InvertLightness();
					cachedImage.SetRecieveBlender(new BlenderPreMultBGRA());
				}

				destImage.CopyFrom(cachedImage);
			}
		}

		public ImageBuffer LoadImage(string path)
		{
			return this.LoadImage(path, false);
		}

		public ImageBuffer LoadImage(string path, bool invertImage = false)
		{
			ImageBuffer temp = new ImageBuffer();
			LoadImage(path, temp, invertImage);

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
			return File.ReadAllText(MapPath(path));
		}

		public string MapPath(string path)
		{
			return Path.GetFullPath(Path.Combine(this.basePath, path));
		}
	}
}
