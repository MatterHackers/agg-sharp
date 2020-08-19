using MatterHackers.Agg.Image;
using System.Collections.Specialized;

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

		public ImageBuffer GetImage()
		{
			try
			{
				var bitmap = new System.Drawing.Bitmap(System.Windows.Forms.Clipboard.GetImage());
				var image = new ImageBuffer();
				if (ImageIOWindowsPlugin.ConvertBitmapToImage(image, bitmap))
				{
					return image;
				}
			}
			catch
			{
			}

			return null;
		}

		public void SetImage(ImageBuffer imageBuffer)
		{
			System.Windows.Forms.Clipboard.SetImage(ImageIOWindowsPlugin.ConvertImageToBitmap(imageBuffer));
		}

		public bool ContainsFileDropList => System.Windows.Forms.Clipboard.ContainsFileDropList();

		public StringCollection GetFileDropList()
		{
			return System.Windows.Forms.Clipboard.GetFileDropList();
		}
	}
}