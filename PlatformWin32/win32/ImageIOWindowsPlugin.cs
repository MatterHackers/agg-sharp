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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Image
{
	public class ImageIOWindowsPlugin : IImageIOProvider
	{
		public bool LoadImageData(Stream stream, ImageSequence destImageSequence)
		{
			var gifImg = System.Drawing.Image.FromStream(stream);
			if (gifImg != null)
			{
				FrameDimension dimension = new FrameDimension(gifImg.FrameDimensionsList[0]);
				// Number of frames
				int frameCount = gifImg.GetFrameCount(dimension);

				for (int i = 0; i < frameCount; i++)
				{
					// Return an Image at a certain index
					gifImg.SelectActiveFrame(dimension, i);

					using (var bitmap = new Bitmap(gifImg))
					{
						var frame = new ImageBuffer();
						ConvertBitmapToImage(frame, bitmap);
						destImageSequence.AddImage(frame);
					}
				}

				try
				{
					PropertyItem item = gifImg.GetPropertyItem(0x5100); // FrameDelay in libgdiplus
					// Time is in 1/100th of a second
					destImageSequence.SecondsPerFrame = (item.Value[0] + item.Value[1] * 256) / 100.0;
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					destImageSequence.SecondsPerFrame = 2;
				}

				return true;
			}

			return false;
		}

		public bool LoadImageData(Stream stream, ImageBuffer destImage)
		{
			using (var bitmap = new Bitmap(stream))
			{
				return ConvertBitmapToImage(destImage, bitmap);
			}
		}

		public bool LoadImageData(string fileName, ImageBuffer destImage)
		{
			if (System.IO.File.Exists(fileName))
			{
				using (var bitmap = new Bitmap(fileName))
				{
					return ConvertBitmapToImage(destImage, bitmap);
				}
			}
			else
			{
				throw new System.Exception(string.Format("Image file not found: {0}", fileName));
			}
		}

		internal static bool ConvertBitmapToImage(ImageBuffer destImage, Bitmap bitmap)
		{
			if (bitmap != null)
			{
				switch (bitmap.PixelFormat)
				{
					case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
						{
							destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 32);
							if (destImage.GetRecieveBlender() == null)
							{
								destImage.SetRecieveBlender(new BlenderBGRA());
							}

							BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
							int sourceIndex = 0;
							int destIndex = 0;
							unsafe
							{
								int offset;
								byte[] destBuffer = destImage.GetBuffer(out offset);
								byte* pSourceBuffer = (byte*)bitmapData.Scan0;
								for (int y = 0; y < destImage.Height; y++)
								{
									destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
									for (int x = 0; x < destImage.Width; x++)
									{
#if true
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
#else
                                            Color notPreMultiplied = new Color(pSourceBuffer[sourceIndex + 0], pSourceBuffer[sourceIndex + 1], pSourceBuffer[sourceIndex + 2], pSourceBuffer[sourceIndex + 3]);
                                            sourceIndex += 4;
                                            Color preMultiplied = notPreMultiplied.ToColorF().premultiply().ToColor();
                                            destBuffer[destIndex++] = preMultiplied.blue;
                                            destBuffer[destIndex++] = preMultiplied.green;
                                            destBuffer[destIndex++] = preMultiplied.red;
                                            destBuffer[destIndex++] = preMultiplied.alpha;
#endif
									}
								}
							}

							bitmap.UnlockBits(bitmapData);

							return true;
						}

					case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
						{
							destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 32);
							if (destImage.GetRecieveBlender() == null)
							{
								destImage.SetRecieveBlender(new BlenderBGRA());
							}

							BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
							int sourceIndex = 0;
							int destIndex = 0;
							unsafe
							{
								int offset;
								byte[] destBuffer = destImage.GetBuffer(out offset);
								byte* pSourceBuffer = (byte*)bitmapData.Scan0;
								for (int y = 0; y < destImage.Height; y++)
								{
									sourceIndex = y * bitmapData.Stride;
									destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
									for (int x = 0; x < destImage.Width; x++)
									{
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = 255;
									}
								}
							}

							bitmap.UnlockBits(bitmapData);
							return true;
						}

					case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
						{
							Copy8BitDataToImage(destImage, bitmap);
							return true;
						}

					default:
						// let this code fall through and return false
						break;
				}
			}
			return false;
		}

		private static void Copy8BitDataToImage(ImageBuffer destImage, Bitmap bitmap)
		{
			destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 32);
			if (destImage.GetRecieveBlender() == null)
			{
				destImage.SetRecieveBlender(new BlenderBGRA());
			}

			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
			int sourceIndex = 0;
			int destIndex = 0;
			unsafe
			{
				int offset;
				byte[] destBuffer = destImage.GetBuffer(out offset);
				byte* pSourceBuffer = (byte*)bitmapData.Scan0;

				System.Drawing.Color[] colors = bitmap.Palette.Entries;

				for (int y = 0; y < destImage.Height; y++)
				{
					sourceIndex = y * bitmapData.Stride;
					destIndex = destImage.GetBufferOffsetY(destImage.Height - 1 - y);
					for (int x = 0; x < destImage.Width; x++)
					{
						System.Drawing.Color color = colors[pSourceBuffer[sourceIndex++]];
						destBuffer[destIndex++] = color.B;
						destBuffer[destIndex++] = color.G;
						destBuffer[destIndex++] = color.R;
						destBuffer[destIndex++] = color.A;
					}
				}
			}

			bitmap.UnlockBits(bitmapData);
		}

		public bool SaveImageData(string filename, IImageByte sourceImage)
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

			if (!System.IO.File.Exists(filename))
			{
				if (sourceImage.BitDepth == 32)
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
				else if (sourceImage.BitDepth == 8 && format == ImageFormat.Png)
				{
					using (Bitmap bitmapToSave = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format8bppIndexed))
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
					throw new System.NotImplementedException();
				}
			}

			return false;
		}

		public bool LoadImageData(String filename, ImageBufferFloat destImage)
		{
			if (System.IO.File.Exists(filename))
			{
				var bitmap = new Bitmap(filename);
				if (bitmap != null)
				{
					switch (bitmap.PixelFormat)
					{
						case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
							destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 128);
							break;

						default:
							throw new System.NotImplementedException();
					}

					BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
					int sourceIndex = 0;
					int destIndex = 0;
					unsafe
					{
						int offset;
						float[] destBuffer = destImage.GetBuffer(out offset);
						byte* pSourceBuffer = (byte*)bitmapData.Scan0;
						for (int y = 0; y < destImage.Height; y++)
						{
							destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
							for (int x = 0; x < destImage.Width; x++)
							{
								destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
								destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
								destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
								destBuffer[destIndex++] = 1.0f;
							}
						}
					}

					bitmap.UnlockBits(bitmapData);

					return true;
				}
			}

			return false;
		}
	}
}
