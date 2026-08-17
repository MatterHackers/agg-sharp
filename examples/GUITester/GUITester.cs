using System;
using System.Diagnostics;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;

namespace MatterHackers.Agg
{
	public class FontInfoWidget : GuiWidget
	{
		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			LiberationSansFont.Instance.ShowDebugInfo(graphics2D);
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			AnchorAll();
		}
	}

	public class GuiTester : GuiWidget, IDemoApp
	{
		private TabControl mainNavigationTabControl;

		public GuiTester()
		{
			mainNavigationTabControl = new TabControl(ThemeConfig.DefaultTheme(), Orientation.Vertical);

			
			mainNavigationTabControl.AddTab(new GridControlPage(), "GridControl");
#if true
			mainNavigationTabControl.AddTab(new MenuPage(), "MenuPage");
			mainNavigationTabControl.AddTab(new TextEditPage(), "TextEditPage");
			mainNavigationTabControl.AddTab(new SplitterPage(), "SplitterPage");
			mainNavigationTabControl.AddTab(new LayoutPage(), "LayoutPage");
			mainNavigationTabControl.AddTab(new ButtonsPage(), "ButtonsPage");

			mainNavigationTabControl.AddTab(new ScrollableWidgetTestPage(), "ScrollableWidgetTestPage");
			mainNavigationTabControl.AddTab(new AnchorCenterButtonsTestPAge(), "AnchorCenterButtonsTestPAge");
			mainNavigationTabControl.AddTab(new TabPagesPage(), "TabPagesPage");
			mainNavigationTabControl.AddTab(new ListBoxPage(), "ListBoxPage");
			mainNavigationTabControl.AddTab(new ButtonAnchorTestPage(), "ButtonAnchorTestPage");

			mainNavigationTabControl.AddTab(new AnchorTestsPage(), "AnchorTestsPage");
			mainNavigationTabControl.AddTab(new WindowPage(), "WindowPage");

			mainNavigationTabControl.AddTab(new SliderControlsPage(), "SliderControlsPage");
			mainNavigationTabControl.AddTab(new TabPage(new FontInfoWidget(), "Fonts"), "Fonts");
			mainNavigationTabControl.AddTab(new TabPage(new FontHintWidget(), "Font Hinting"), "Font Hinting");
#if WINDOWS
			// AForge captures through DirectShow, so this page only exists in the Windows build.
			try
			{
				mainNavigationTabControl.AddTab(new TabPage(new WebCamWidget(), "Web Cam"), "WebCam");
			}
			catch (Exception ex)
			{
				// AForge's capture dialog loads its icons through BinaryFormatter, which modern .NET
				// refuses outright, so on many machines constructing this page kills the whole demo before
				// a window ever opens. One unavailable page is not worth the other twenty.
				Console.WriteLine($"GUITester: skipping the Web Cam page ({ex.Message})");
			}
#endif
#endif
			this.AddChild(mainNavigationTabControl);

			AnchorAll();
		}

		public string Title { get; } = "GUI Tester";

		public string DemoCategory { get; } = "GUI";

		public string DemoDescription { get; } = "Shows a tabbed page of the windows controls that are available in ";

		private bool putUpDiagnostics = false;
		private Stopwatch totalTime = new Stopwatch();

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!putUpDiagnostics)
			{
				//DiagnosticWidget diagnosticView = DiagnosticWidget.Show(this);
				putUpDiagnostics = true;
			}
			this.NewGraphics2D().Clear(new Color(255, 255, 255));

			base.OnDraw(graphics2D);


			long milliseconds = totalTime.ElapsedMilliseconds;
			graphics2D.DrawString("ms: " + milliseconds.ToString() + "  ", Width, Height - 14, justification: Justification.Right, backgroundColor: Color.White);
			totalTime.Restart();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			// The clipboard implementation lives in the platform assembly, and the two do not share a type
			// name, so this is one of the few spots a demo has to know which OS it was built for.
#if WINDOWS
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
#else
			Clipboard.SetSystemClipboard(new MacClipboard());
#endif

			var demoWidget = new GuiTester();

			var systemWindow = new SystemWindow(800, 600);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}