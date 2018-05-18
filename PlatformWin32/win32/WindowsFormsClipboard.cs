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

		public bool ContainsText => System.Windows.Forms.Clipboard.ContainsText();

		public bool ContainsImage => System.Windows.Forms.Clipboard.ContainsImage();
		public ImageBuffer GetImage()
		{
			var bitmap = new System.Drawing.Bitmap(System.Windows.Forms.Clipboard.GetImage());
			var image = new ImageBuffer();
			if (ImageIOWindowsPlugin.ConvertBitmapToImage(image, bitmap))
			{
				return image;
			}

			return null;
		}

		public bool ContainsFileDropList => System.Windows.Forms.Clipboard.ContainsFileDropList();
		public StringCollection GetFileDropList()
		{
			return System.Windows.Forms.Clipboard.GetFileDropList();
		}
	}
}