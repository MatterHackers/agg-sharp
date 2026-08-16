using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	internal class DemoRunner : GuiWidget
	{
		public DemoRunner()
		{
			string searchPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			// Load plugins from all assemblies the startup directory
			var dlls = Directory.GetFiles(searchPath, "*.dll");
			var allAssemblies = dlls.Concat(Directory.GetFiles(searchPath, "*.exe"));

			foreach (var file in allAssemblies)
			{
				try
				{
					PluginFinder.LoadTypesFromAssembly(System.Reflection.Assembly.LoadFile(file));
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading assembly: " + ex.Message);
				}
			}

			var appWidgetFinder = PluginFinder.CreateInstancesOf<IDemoApp>().OrderBy(a => a.Title).ToList();

			TabControl tabControl = new TabControl(ThemeConfig.DefaultTheme(), Orientation.Vertical);
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

			// Which demo opens first matters more than it looks: several of these draw straight into the
			// window's CPU pixel buffer and throw on a GPU window, so picking the starting tab is what
			// makes an unattended run on a GPU backend possible at all.
			string requestedTab = Environment.GetEnvironmentVariable("AGG_DEMO_TAB");
			if (!string.IsNullOrWhiteSpace(requestedTab))
			{
				var match = appWidgetFinder.FirstOrDefault(
					a => a.Title.IndexOf(requestedTab, StringComparison.OrdinalIgnoreCase) >= 0);
				if (match != null)
				{
					tabControl.SelectTab(appWidgetFinder.IndexOf(match));
				}
				else
				{
					Console.WriteLine($"DemoRunner: no demo title contains '{requestedTab}'.");
				}
			}

			AnchorAll();
		}

		/// <summary>
		/// The demo browser's entry point. It had none of its own and ran on the one the TUnit source
		/// generator emitted through a stale test-project reference, so launching DemoRunner ran the test
		/// suite rather than the demos.
		/// </summary>
		/// <param name="args">Unused.</param>
		[STAThread]
		public static void Main(string[] args)
		{
			var demoRunner = new DemoRunner();

			var systemWindow = new SystemWindow(800, 600);
			systemWindow.Title = "Agg Demo Runner";
			systemWindow.AddChild(demoRunner);
			systemWindow.ShowAsSystemWindow();
		}
	}
}
