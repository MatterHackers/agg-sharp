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

			return true;
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
				var temp = SixLabors.ImageSharp.Image.Load(fileName);
				return ConvertImageToImageBuffer(destImage, temp);
			}
			else
			{
				throw new Exception(string.Format("Image file not found: {0}", fileName));
			}
		}

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

		// allocate a set of lockers to use when accessing files for saving
		private static readonly object[] Lockers = new object[] { new object(), new object(), new object(), new object() };

		public static bool SaveImageData(string filename, IImageByte sourceImage)
		{
			try
			{
				using (var tgaSave = new MemoryStream())
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
					image2.Save(filename);

					/*
					ImageTgaIO.Save((ImageBuffer)sourceImage, tgaSave);
					tgaSave.Seek(0, SeekOrigin.Begin);
					var image = SixLabors.ImageSharp.Image.Load(tgaSave);
					if (Path.GetExtension(filename).ToLower() == ".png")
					{
						image.Save(filename);
						image.Save("c:\\temp\\temp2.png");
					}
					else
					{
						image.Save(filename);
					}
					*/
				}

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
