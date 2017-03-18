/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using Newtonsoft.Json;

namespace MatterHackers.Agg
{
	public class FileSystemStaticData : IStaticData
	{
		private static Dictionary<string, ImageBuffer> cachedImages = new Dictionary<string, ImageBuffer>();

		private string basePath;

		public FileSystemStaticData()
		{
			string appPathAndFile = Assembly.GetExecutingAssembly().Location;
			string pathToAppFolder = Path.GetDirectoryName(appPathAndFile);
			string localStaticDataPath = Path.Combine(pathToAppFolder, "StaticData");

			this.basePath = localStaticDataPath;

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
		/// <returns>An ImageBuffer initialized with data from the given file</returns>
		public ImageBuffer LoadIcon(string path)
		{
			return LoadImage(Path.Combine("Icons", path));
		}

		/// <summary>
		/// Load the specified file from the StaticData/Icons path and scale it to the given size,
		/// adjusting for the device scale in GuiWidget
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		public ImageBuffer LoadIcon(string path, int width, int height)
		{
			int deviceWidth = (int)(width * GuiWidget.DeviceScale);
			int deviceHeight = (int)(height * GuiWidget.DeviceScale);
			ImageBuffer scaledImage = LoadIcon(path);
			scaledImage.SetRecieveBlender(new BlenderPreMultBGRA());
			scaledImage = ImageBuffer.CreateScaledImage(scaledImage, deviceWidth, deviceHeight);

			return scaledImage;
		}

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="buffer">The ImageBuffer to populate with data from the given file</param>
		public void LoadIcon(string path, ImageBuffer buffer)
		{
			LoadImage(Path.Combine("Icons", path), buffer);
		}

		public void LoadSequence(string pathToImages, ImageSequence sequence)
		{
			if (DirectoryExists(pathToImages))
			{
				string propertiesPath = Path.Combine(pathToImages, "properties.json");
				if (FileExists(propertiesPath))
				{
					string jsonData = ReadAllText(propertiesPath);

					var properties = JsonConvert.DeserializeObject<ImageSequence.Properties>(jsonData);
					sequence.FramePerSecond = properties.FramePerFrame;
					sequence.Looping = properties.Looping;
				}

				var pngFiles = GetFiles(pathToImages).Where(fileName => Path.GetExtension(fileName).ToUpper() == ".PNG").OrderBy(s => s);
				foreach (string path in pngFiles)
				{
					ImageBuffer image = LoadImage(path);
					sequence.AddImage(image);
				}
			}
		}

		public void LoadImageData(Stream imageStream, ImageBuffer destImage)
		{
			using (var bitmap = new Bitmap(imageStream))
			{
				ImageIOWindowsPlugin.ConvertBitmapToImage(destImage, bitmap);
			}
		}

		static object locker = new object();
		public void LoadImage(string path, ImageBuffer destImage)
		{
			lock (locker)
			{
				ImageBuffer cachedImage;
				if (!cachedImages.TryGetValue(path, out cachedImage))
				{
					using (var imageStream = OpenSteam(path))
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

				destImage.CopyFrom(cachedImage);
			}
		}

		public ImageBuffer LoadImage(string path)
		{
			ImageBuffer temp = new ImageBuffer();
			LoadImage(path, temp);

			return temp;
		}

		public Stream OpenSteam(string path)
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
			string fullPath = Path.GetFullPath(Path.Combine(this.basePath, path));
			return fullPath;
		}
	}
}