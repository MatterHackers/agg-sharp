using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Diagnostics;

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

	public class FontHintWidget : GuiWidget
	{
		string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		double yOffsetUpper = -.1;
		double ySizeUpper = .2;

		double yOffsetLower = 0;
		double ySizeLower = -.5;

		public override void OnDraw(Graphics2D graphics2D)
		{
			double textHeight = 20;
			double textY = 200;

			base.OnDraw(graphics2D);

			graphics2D.DrawString("YOffset = {0:0.00}".FormatWith(yOffsetUpper), 20, Height - 20);
			graphics2D.DrawString("YScale = {0:0.00}".FormatWith(ySizeUpper), 140, Height - 20);

			graphics2D.DrawString("YOffset = {0:0.00}".FormatWith(yOffsetLower), 20, Height - 40);
			graphics2D.DrawString("YScale = {0:0.00}".FormatWith(ySizeLower), 140, Height - 40);

			graphics2D.DrawString(alphabet, 20, textY);
			graphics2D.DrawString(alphabet.ToLower(), 310, textY);

			TypeFacePrinter upperPrinter = new TypeFacePrinter(alphabet);
			TypeFacePrinter lowerPrinter = new TypeFacePrinter(alphabet.ToLower());

			graphics2D.Render(new VertexSourceApplyTransform(upperPrinter, Affine.NewScaling(1, (12 + ySizeUpper)/12)), 20, textY - textHeight + yOffsetUpper, RGBA_Bytes.Black);
			graphics2D.Render(new VertexSourceApplyTransform(lowerPrinter, Affine.NewScaling(1, (12 + ySizeLower) / 12)), 310, textY - textHeight + yOffsetLower, RGBA_Bytes.Black);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if(keyEvent.KeyCode == Keys.Up)
			{
				yOffsetUpper += .1;
				if (yOffsetUpper > .5) yOffsetUpper = .5;
			}
			else if (keyEvent.KeyCode == Keys.Down)
			{
				yOffsetUpper -= .1;
				if (yOffsetUpper < -.5) yOffsetUpper = -.5;
			}
			if (keyEvent.KeyCode == Keys.Right)
			{
				ySizeUpper += .1;
				if (ySizeUpper > .5) ySizeUpper = .5;
			}
			else if (keyEvent.KeyCode == Keys.Left)
			{
				ySizeUpper -= .1;
				if (ySizeUpper < -.5) ySizeUpper = -.5;
			}


			if (keyEvent.KeyCode == Keys.Home)
			{
				yOffsetLower += .1;
				if (yOffsetLower > .5) yOffsetLower = .5;
			}
			else if (keyEvent.KeyCode == Keys.End)
			{
				yOffsetLower -= .1;
				if (yOffsetLower < -.5) yOffsetLower = -.5;
			}
			if (keyEvent.KeyCode == Keys.PageDown)
			{
				ySizeLower += .1;
				if (ySizeLower > .5) ySizeLower = .5;
			}
			else if (keyEvent.KeyCode == Keys.Delete)
			{
				ySizeLower -= .1;
				if (ySizeLower < -.5) ySizeLower = -.5;
			}
			Invalidate();

			base.OnKeyDown(keyEvent);
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			AnchorAll();
		}
	}

	public class GuiTester : GuiWidget
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
			mainNavigationTabControl.AddTab(new TabPage(new FontHintWidget(), "Font Hinting"), "Fonts");
#endif
			this.AddChild(mainNavigationTabControl);

			AnchorAll();
		}

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
			graphics2D.DrawString("ms: ", Width - 60, Height - 14);
			graphics2D.DrawString(milliseconds.ToString() + "  ", Width, Height - 14, justification: Justification.Right);
			totalTime.Restart();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

			AppWidgetFactory appWidget = new GuiTesterFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class GuiTesterFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new GuiTester();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
			"GUI",
			"GUI Tester",
			"Shows a tabed page of the windows controls that are available in ",
			800,
			600);

			return appWidgetInfo;
		}
	}
}