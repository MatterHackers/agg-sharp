using MatterHackers.Agg.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace MatterHackers.Agg.PlatformAbstract
{
	public interface IStaticData
	{
		bool DirectoryExists(string pathToManufacturers);

		bool FileExists(string path);

		IEnumerable<string> GetDirectories(string pathToManufacturers);

		IEnumerable<string> GetFiles(string path);

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <returns>An ImageBuffer initialized with data from the given file</returns>
		ImageBuffer LoadIcon(string path);

		ImageBuffer LoadIcon(string path, int width, int height);

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="buffer">The ImageBuffer to populate with data from the given file</param>
		void LoadIcon(string path, ImageBuffer buffer);

		void LoadImage(string path, ImageBuffer destImage);

		ImageBuffer LoadImage(string path);

		string MapPath(string path);

		Stream OpenSteam(string path);

		string[] ReadAllLines(string path);

		string ReadAllText(string path);

		void LoadImageData(Stream stream, ImageBuffer unScaledImage);

		void LoadSequence(string path, ImageSequence sequence);
	}

	public static class StaticData
	{
		public static IStaticData Instance { get; set; }

		/// <summary>
		/// Load an image from the web and update it when done.
		/// The downloaded image will be scaled to the size of the ImageBuffer it is being loaded into.
		/// </summary>
		/// <param name="uri"></param>
		public static void DownloadToImageAsync(ImageBuffer imageToLoadInto, string uriToLoad, bool sizeToDownloadedImage, IRecieveBlenderByte scalingBlender = null)
		{
			if(scalingBlender == null)
			{
				scalingBlender = new BlenderBGRA();
			}

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					// scale the loaded image to the size of the target image
					byte[] raw = e.Result;
					Stream stream = new MemoryStream(raw);
					ImageBuffer unScaledImage = new ImageBuffer(10, 10);
					if (!sizeToDownloadedImage)
					{
						StaticData.Instance.LoadImageData(stream, unScaledImage);
						// If the source image (the one we downloaded) is more than twice as big as our dest image.
						while (unScaledImage.Width > imageToLoadInto.Width * 2)
						{
							// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
							// So we scale as many times as we need to to get the Image to be the right size.
							// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
							ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, scalingBlender);
							halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
							unScaledImage = halfImage;
						}
						imageToLoadInto.NewGraphics2D().Render(unScaledImage, 0, 0, 0, imageToLoadInto.Width / (double)unScaledImage.Width, imageToLoadInto.Height / (double)unScaledImage.Height);
					}
					else
					{
						StaticData.Instance.LoadImageData(stream, imageToLoadInto);
					}
					imageToLoadInto.MarkImageChanged();
				}
				catch
				{
				}
			};
			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{ }
		}
	}
}