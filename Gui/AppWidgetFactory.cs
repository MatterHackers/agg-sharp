namespace MatterHackers.Agg.UI
{
	public class AppWidgetInfo
	{
		public string category;
		public string title;
		public string description;
		public int width;
		public int height;

		public AppWidgetInfo(string category, string caption, string description, int width, int height)
		{
			this.category = category;
			this.title = caption;
			this.description = description;
			this.width = width;
			this.height = height;
		}
	}

	public abstract class AppWidgetFactory
	{
		public abstract AppWidgetInfo GetAppParameters();

		public abstract GuiWidget NewWidget();

		public enum RenderSurface { Bitmap, OpenGL };

		public void CreateWidgetAndRunInWindow(SystemWindow.PixelTypes bitDepth = SystemWindow.PixelTypes.Depth32, RenderSurface surfaceType = RenderSurface.Bitmap)
		{
			AppWidgetInfo appWidgetInfo = GetAppParameters();
			SystemWindow systemWindow = new SystemWindow(appWidgetInfo.width, appWidgetInfo.height);
			systemWindow.PixelType = bitDepth;
			systemWindow.Title = appWidgetInfo.title;
			if (surfaceType == RenderSurface.OpenGL)
			{
				systemWindow.UseOpenGL = true;
			}
			systemWindow.AddChild(NewWidget());
			systemWindow.ShowAsSystemWindow();
		}
	}
}