using System;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;

namespace MatterHackers.Agg
{
	internal class DemoRunner : GuiWidget
	{
		public DemoRunner()
		{
			var appWidgetFinder = PluginFinder.CreateInstancesOf<IDemoApp>().OrderBy(a => a.Title).ToList();

			TabControl tabControl = new TabControl(Orientation.Vertical);
			AddChild(tabControl);
			tabControl.AnchorAll();

			int count = appWidgetFinder.Count;
			for (int i = 0; i < count; i++)
			{
				TabPage tabPage = new TabPage(appWidgetFinder[i].Title);
				tabPage.AddChild(appWidgetFinder[i] as GuiWidget);
				tabControl.AddTab(tabPage, tabPage.Text);
			}

			AnchorAll();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

			var systemWindow = new SystemWindow(640, 480);
			systemWindow.Title = "Demo Runner";
			systemWindow.AddChild(new DemoRunner());
			systemWindow.ShowAsSystemWindow();
		}
	}
}
