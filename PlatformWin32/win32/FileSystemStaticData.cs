using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

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
					ImageSequence.Properties properties = JsonConvert.DeserializeObject<ImageSequence.Properties>(jsonData);
					sequence.FramePerSecond = properties.FramePerFrame;
					sequence.Looping = properties.Looping;
				}

				string[] pngFilesIn = GetFiles(pathToImages).Where(fileName => Path.GetExtension(fileName).ToUpper() == ".PNG").ToArray();
				List<string> pngFiles = new List<string>(pngFilesIn);
				pngFiles.Sort();
				foreach (string pngFile in pngFiles)
				{
					ImageBuffer image = new ImageBuffer();
					LoadImage(pngFile, image);
					sequence.AddImage(image);
				}
			}
		}

		public void LoadImageData(Stream imageStream, ImageBuffer destImage)
		{
			var bitmap = new Bitmap(imageStream);
			ImageIOWindowsPlugin.ConvertBitmapToImage(destImage, bitmap);
		}

		static object locker = new object();
		public void LoadImage(string path, ImageBuffer destImage)
		{
			lock (locker)
			{
				ImageBuffer cachedImage = null;
				if (!cachedImages.TryGetValue(path, out cachedImage))
				{
					using (var imageStream = OpenSteam(path))
					{
						var bitmap = new Bitmap(imageStream);
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