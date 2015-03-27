using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;

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

    public class GuiTester : GuiWidget
    {
        TabControl mainNavigationTabControl;
		
        public GuiTester()
        {
            mainNavigationTabControl = new TabControl(Orientation.Vertical);

            mainNavigationTabControl.AddTab(new MenuPage(), "MenuPage");
#if true
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
#endif
            this.AddChild(mainNavigationTabControl);

            AnchorAll();
        }

        bool putUpDiagnostics = false;
        Stopwatch totalTime = new Stopwatch();
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
            MatterHackers.Agg.Image.UnitTests.Run();
            MatterHackers.Agg.Font.UnitTests.Run();
            Agg.UI.Tests.UnitTests.Run();
            MatterHackers.Agg.Tests.UnitTests.Run();

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
