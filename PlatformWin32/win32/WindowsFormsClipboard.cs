using MatterHackers.Agg.Image;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;

namespace MatterHackers.Agg.UI
{
	public class WindowsFormsClipboard : ISystemClipboard
	{
		public string GetText()
		{
			return System.Windows.Forms.Clipboard.GetText();
		}

		public void SetText(string text)
		{
			System.Windows.Forms.Clipboard.SetText(text);
		}

		public bool ContainsText
		{
			get
			{
				try
				{
					return System.Windows.Forms.Clipboard.ContainsText();
				}
				catch
				{
					return false;
				}
			}
		}

		public bool ContainsImage
		{
			get
			{
				try
				{
					return System.Windows.Forms.Clipboard.ContainsImage();
				}
				catch
				{
					return false;
				}
			}
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
				byte[] destBuffer = destImage.GetBuffer(out int offset);
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

		public static bool ConvertBitmapToImage(ImageBuffer destImage, Bitmap bitmap)
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
								byte[] destBuffer = destImage.GetBuffer(out int offset);
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
								byte[] destBuffer = destImage.GetBuffer(out int offset);
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

		public ImageBuffer GetImage()
		{
			try
			{
				var bitmap = new Bitmap(System.Windows.Forms.Clipboard.GetImage());
				var image = new ImageBuffer();
				if (ConvertBitmapToImage(image, bitmap))
				{
					return image;
				}
			}
			catch
			{
			}

			return null;
		}

		public static Bitmap ConvertImageToBitmap(ImageBuffer sourceImage)
		{
			var bitmap = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);

			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

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

			bitmap.UnlockBits(bitmapData);

			return bitmap;
		}

		public void SetImage(ImageBuffer imageBuffer)
		{
			System.Windows.Forms.Clipboard.SetImage(ConvertImageToBitmap(imageBuffer));
		}

		public bool ContainsFileDropList => System.Windows.Forms.Clipboard.ContainsFileDropList();

		public StringCollection GetFileDropList()
		{
			return System.Windows.Forms.Clipboard.GetFileDropList();
		}
	}
}