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
			mainNavigationTabControl = new TabControl(Orientation.Vertical);

			
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
			mainNavigationTabControl.AddTab(new TabPage(new WebCamWidget(), "Web Cam"), "WebCam");
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
				//DiagnosticWidget diagnosticView = new DiagnosticWidget(this);
				putUpDiagnostics = true;
			}
			this.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255));

			base.OnDraw(graphics2D);


			long milliseconds = totalTime.ElapsedMilliseconds;
			graphics2D.DrawString("ms: " + milliseconds.ToString() + "  ", Width, Height - 14, justification: Justification.Right, backgroundColor: RGBA_Bytes.White);
			totalTime.Restart();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

			var demoWidget = new GuiTester();

			var systemWindow = new SystemWindow(800, 600);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}