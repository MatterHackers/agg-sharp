using System.Collections.Specialized;
using GLFW;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	public class GlfwClipboard : ISystemClipboard
	{
		private Window glfwWindow;

		public GlfwClipboard(Window glfwWindow)
		{
			this.glfwWindow = glfwWindow;
		}

		public bool ContainsFileDropList => false;

		public bool ContainsImage => false;

		public bool ContainsText => !string.IsNullOrEmpty(Glfw.GetClipboardString(glfwWindow));

		public StringCollection GetFileDropList() => null;

		public ImageBuffer GetImage() => null;

		public string GetText() => Glfw.GetClipboardString(glfwWindow);

		public void SetImage(ImageBuffer imageBuffer)
		{
		}

		public void SetText(string text)
		{
			Glfw.SetClipboardString(glfwWindow, text);
		}
	}
}
