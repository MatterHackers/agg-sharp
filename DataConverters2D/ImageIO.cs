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
					ConvertBitmapToImage(imageBuffer, image.Frames[i]);

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
				if (ImageIO.ConvertBitmapToImage(imageBuffer, image))
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
				return ConvertBitmapToImage(destImage, bitmap);
			}
		}

		public static bool LoadImageData(string fileName, ImageBuffer destImage)
		{
			if (File.Exists(fileName))
			{
				return ConvertBitmapToImage(destImage, Image<Rgba32>.Load<Rgba32>(fileName));
			}
			else
			{
				throw new Exception(string.Format("Image file not found: {0}", fileName));
			}
		}

		private static bool ConvertBitmapToImage(ImageBuffer imageBuffer, ImageFrame<Rgba32> imageFrame)
		{
			if (imageFrame.TryGetSinglePixelSpan(out var pixelSpan))
			{
				Rgba32[] pixelArray = pixelSpan.ToArray();

				return ConvertBitmapToImage(imageBuffer, imageFrame.Width, imageFrame.Height, pixelArray);
			}

			return false;
		}

		private static bool ConvertBitmapToImage(ImageBuffer destImage, Image<Rgba32> image)
		{
			if (image.TryGetSinglePixelSpan(out var pixelSpan))
			{
				Rgba32[] pixelArray = pixelSpan.ToArray();

				return ConvertBitmapToImage(destImage, image.Width, image.Height, pixelArray);
			}

			return false;
		}

		public static bool ConvertBitmapToImage(ImageBuffer destImage, int width, int height, Rgba32[] pixelArray)
		{
			if (pixelArray != null)
			{
				destImage.Allocate(width, height, width * 4, 32);
				if (destImage.GetRecieveBlender() == null)
				{
					destImage.SetRecieveBlender(new BlenderBGRA());
				}

				int sourceIndex = 0;
				int destIndex = 0;
				unsafe
				{
					byte[] destBuffer = destImage.GetBuffer(out int offset);
					for (int y = 0; y < destImage.Height; y++)
					{
						destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
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
			}

			return false;
		}

		// allocate a set of lockers to use when accessing files for saving
		private static readonly object[] Lockers = new object[] { new object(), new object(), new object(), new object() };

		public static bool SaveImageData(string filename, IImageByte sourceImage)
		{
#if true
			throw new NotImplementedException();
#else
			// Get a lock index base on the hash of the file name
			int lockerIndex = Math.Abs(filename.GetHashCode()) % Lockers.Length; // mod the hash code by the count to get an index

			// lock on the index that this file name selects
			lock (Lockers[lockerIndex])
			{
				if (File.Exists(filename))
				{
					File.Delete(filename);
				}

				ImageFormat format = ImageFormat.Jpeg;
				if (filename.ToLower().EndsWith(".png"))
				{
					format = ImageFormat.Png;
				}
				else if (!filename.ToLower().EndsWith(".jpg") && !filename.ToLower().EndsWith(".jpeg"))
				{
					filename += ".jpg";
				}

				if (!File.Exists(filename))
				{
					if (sourceImage.BitDepth == 32)
					{
						try
						{
							using (var bitmapToSave = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb))
							{
								BitmapData bitmapData = bitmapToSave.LockBits(new Rectangle(0, 0, bitmapToSave.Width, bitmapToSave.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmapToSave.PixelFormat);
								int destIndex = 0;
								unsafe
								{
									byte[] sourceBuffer = sourceImage.GetBuffer();
									byte* pDestBuffer = (byte*)bitmapData.Scan0;
									int scanlinePadding = bitmapData.Stride - bitmapData.Width * 4;
									for (int y = 0; y < sourceImage.Height; y++)
									{
										int sourceIndex = sourceImage.GetBufferOffsetXY(0, sourceImage.Height - 1 - y);
										for (int x = 0; x < sourceImage.Width; x++)
										{
											pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
											pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
											pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
											pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
										}

										destIndex += scanlinePadding;
									}
								}

								bitmapToSave.UnlockBits(bitmapData);
								bitmapToSave.Save(filename, format);
							}

							return true;
						}
						catch (Exception ex)
						{
							Console.WriteLine("Error saving file: " + ex.Message);
							return false;
						}
					}
					else if (sourceImage.BitDepth == 8 && format == ImageFormat.Png)
					{
						using (var bitmapToSave = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format8bppIndexed))
						{
							ColorPalette palette = bitmapToSave.Palette;
							for (int i = 0; i < palette.Entries.Length; i++)
							{
								palette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
							}

							bitmapToSave.Palette = palette;
							BitmapData bitmapData = bitmapToSave.LockBits(new Rectangle(0, 0, bitmapToSave.Width, bitmapToSave.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmapToSave.PixelFormat);
							int destIndex = 0;
							unsafe
							{
								byte[] sourceBuffer = sourceImage.GetBuffer();
								byte* pDestBuffer = (byte*)bitmapData.Scan0;
								for (int y = 0; y < sourceImage.Height; y++)
								{
									int sourceIndex = sourceImage.GetBufferOffsetXY(0, sourceImage.Height - 1 - y);
									for (int x = 0; x < sourceImage.Width; x++)
									{
										pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
									}
								}
							}
							bitmapToSave.Save(filename, format);
							bitmapToSave.UnlockBits(bitmapData);

							return true;
						}
					}
					else
					{
						throw new NotImplementedException();
					}
				}

				return false;
			}
#endif
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
