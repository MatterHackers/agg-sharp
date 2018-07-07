using System;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.VectorMath;

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

			// HACK: force width/height/color/position/spacing on default tab controls
			double maxWidth = tabControl.TabBar.Children.Select(c => c.Width).Max();
			foreach (var child in tabControl.TabBar.Children)
			{
				if (child is TextTab textTab)
				{
					foreach(var viewWidget in textTab.Children)
					{
						viewWidget.BackgroundColor = new Color(viewWidget.BackgroundColor, 180);
						viewWidget.HAnchor = HAnchor.Absolute;
						viewWidget.VAnchor = VAnchor.Fit;
						viewWidget.Margin = 0;
						viewWidget.Padding = 6;
						viewWidget.Position = Vector2.Zero;
						viewWidget.Width = maxWidth;
					}
				}
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
