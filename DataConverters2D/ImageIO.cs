/*
Copyright (c) 2014, Lars Brubaker
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
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MatterHackers.Agg.Image
{
	public static class ImageIO
	{
		public static bool LoadImageData(Stream stream, ImageSequence sequence)
		{
			// if (AggContext.OperatingSystem == OSType.Mac)
#if OSX
			SixLabors.ImageSharp.Image image;
			try
			{
				image = SixLabors.ImageSharp.Image.Load(stream);
			}
			catch
			{
				return false;
			}

			sequence.Frames.Clear();
			sequence.FrameTimesMs.Clear();

			if (image.Frames.Count > 1)
			{
				var minFrameTimeMs = int.MaxValue;
				for (var i = 0; i < image.Frames.Count; i++)
				{
					// Return an Image at a certain index
					var imageBuffer = new ImageBuffer();
					ConvertImageToImageBuffer(imageBuffer, image.Frames.CloneFrame(i));

					var frameData = image.Frames[i].Metadata.GetGifMetadata();

					var frameDelay = frameData.FrameDelay * 10;

					sequence.AddImage(imageBuffer, frameDelay);
					minFrameTimeMs = Math.Max(10, Math.Min(frameDelay, minFrameTimeMs));
				}

				sequence.SecondsPerFrame = minFrameTimeMs / 1000.0;
			}
			else
			{
				var imageBuffer = new ImageBuffer();
				if (ImageIO.ConvertImageToImageBuffer(imageBuffer, image))
				{
					sequence.AddImage(imageBuffer);
				}
			}

			Configuration.Default.MemoryAllocator.ReleaseRetainedResources();

			return true;
#else
			Image<Rgba32> image;
			try
			{
				image = Image<Rgba32>.Load<Rgba32>(stream);
			}
			catch
			{
				return false;
			}

			sequence.Frames.Clear();
			sequence.FrameTimesMs.Clear();

			if (image.Frames.Count > 1)
			{
				var minFrameTimeMs = int.MaxValue;
				for (var i = 0; i < image.Frames.Count; i++)
				{
					// Return an Image at a certain index
					ImageBuffer imageBuffer = new ImageBuffer();
					ConvertImageToImageBuffer(imageBuffer, image.Frames[i]);

					var frameData = image.Frames[i].Metadata.GetGifMetadata();

					var frameDelay = frameData.FrameDelay * 10;

					sequence.AddImage(imageBuffer, frameDelay);
					minFrameTimeMs = Math.Max(10, Math.Min(frameDelay, minFrameTimeMs));
				}

				sequence.SecondsPerFrame = minFrameTimeMs / 1000.0;
			}
			else
			{
				ImageBuffer imageBuffer = new ImageBuffer();
				if (ImageIO.ConvertImageToImageBuffer(imageBuffer, image))
				{
					sequence.AddImage(imageBuffer);
				}
			}

			return true;

#endif
		}

		public static bool LoadImageData(Stream stream, ImageBuffer destImage)
		{
			using (var bitmap = Image<Rgba32>.Load<Rgba32>(stream))
			{
				return ConvertImageToImageBuffer(destImage, bitmap);
			}
		}

		public static bool LoadImageData(string fileName, ImageBuffer destImage)
		{
			if (File.Exists(fileName))
			{
				var temp = SixLabors.ImageSharp.Image.Load<Rgba32>(fileName);
				return ConvertImageToImageBuffer(destImage, temp);
			}
			else
			{
				throw new Exception(string.Format("Image file not found: {0}", fileName));
			}
		}

#if OSX
		private static bool ConvertImageToImageBuffer(ImageBuffer destImage, SixLabors.ImageSharp.Image imageIn)
		{
			var tgaSave = new MemoryStream();
			var encoder = new SixLabors.ImageSharp.Formats.Tga.TgaEncoder();
			encoder.BitsPerPixel = SixLabors.ImageSharp.Formats.Tga.TgaBitsPerPixel.Pixel32;
			encoder.Compression = SixLabors.ImageSharp.Formats.Tga.TgaCompression.None;
			imageIn.SaveAsTga(tgaSave, encoder);
			tgaSave.Seek(0, SeekOrigin.Begin);
			if (ImageTgaIO.LoadImageData(destImage, tgaSave, 32))
			{
				return true;
			}

			return false;
		}
#else
		private static bool ConvertImageToImageBuffer(ImageBuffer imageBuffer, ImageFrame<Rgba32> imageFrame)
		{
			Rgba32[] pixelArray = new Rgba32[imageFrame.Width * imageFrame.Height];
			imageFrame.CopyPixelDataTo(pixelArray);
			return ConvertImageToImageBuffer(imageBuffer, imageFrame.Width, imageFrame.Height, pixelArray);
		}

		private static bool ConvertImageToImageBuffer(ImageBuffer destImage, Image<Rgba32> image)
		{
			Rgba32[] pixelArray = new Rgba32[image.Width * image.Height];
			image.CopyPixelDataTo(pixelArray);
			return ConvertImageToImageBuffer(destImage, image.Width, image.Height, pixelArray);
		}

		public static bool ConvertImageToImageBuffer(ImageBuffer destImage, int width, int height, Rgba32[] pixelArray)
		{
			if (pixelArray != null)
			{
				destImage.Allocate(width, height, width * 4, 32);
				if (destImage.GetRecieveBlender() == null)
				{
					destImage.SetRecieveBlender(new BlenderBGRA());
				}

				int sourceIndex = 0;
				byte[] destBuffer = destImage.GetBuffer(out _);
				for (int y = 0; y < destImage.Height; y++)
				{
					int destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
					for (int x = 0; x < destImage.Width; x++)
					{
						destBuffer[destIndex++] = pixelArray[sourceIndex].B;
						destBuffer[destIndex++] = pixelArray[sourceIndex].G;
						destBuffer[destIndex++] = pixelArray[sourceIndex].R;
						destBuffer[destIndex++] = pixelArray[sourceIndex].A;
						sourceIndex++;
					}
				}

				return true;
			}

			return false;
		}
#endif

		public static bool SaveImageData(string filename, IImageByte sourceImage)
		{
			if (!File.Exists(filename))
			{
				using (var fs = new FileStream(filename, FileMode.CreateNew))
				{
					return SaveImageData(fs, Path.GetExtension(filename), sourceImage);
				}
			}

			return false;
		}

		private static Image<Rgba32> ImageBufferToImage32(IImageByte sourceImage)
		{
			var source = sourceImage.GetBuffer();
			var invertedBuffer = new byte[source.Length];
			int index = 0;
			for (int y = sourceImage.Height - 1; y >= 0; y--)
			{
				var line = sourceImage.GetBufferOffsetY(y);
				for (int x = 0; x < sourceImage.Width; x++)
				{
					var pix = x * 4;
					invertedBuffer[index++] = source[line + pix + 2];
					invertedBuffer[index++] = source[line + pix + 1];
					invertedBuffer[index++] = source[line + pix + 0];
					invertedBuffer[index++] = source[line + pix + 3];
				}
			}

			var image2 = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(invertedBuffer,
				sourceImage.Width,
				sourceImage.Height);
			return image2;
		}

		public static bool SaveImageData(Stream stream, string extension, IImageByte sourceImage)
		{
			try
			{
				Image<Rgba32> image2 = ImageBufferToImage32(sourceImage);
				var formatManager = new SixLabors.ImageSharp.Formats.ImageFormatManager();
				SixLabors.ImageSharp.Formats.IImageFormat imageFormat = null;
				switch(extension.ToLower())
				{
					case ".jpg":
					case ".jpeg":
						imageFormat = SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance;
						break;
					case ".png":
						imageFormat = SixLabors.ImageSharp.Formats.Png.PngFormat.Instance;
						break;
					case ".gif":
						imageFormat = SixLabors.ImageSharp.Formats.Gif.GifFormat.Instance;
						break;
					default:
						throw new NotImplementedException();
				}
				image2.Save(stream, imageFormat);

				return true;
			}
			catch { }

			return false;
		}

		public static ImageBuffer LoadImage(string path)
		{
			var temp = new ImageBuffer();
			LoadImageData(path, temp);

			return temp;
		}

		public static ImageBuffer LoadImage(Stream stream)
		{
			var temp = new ImageBuffer();
			LoadImageData(stream, temp);

			return temp;
		}
	}
}
