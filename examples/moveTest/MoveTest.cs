using System;
using System.Diagnostics;

using MatterHackers.Agg.UI;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class blur : RectangleWidget
    {
        public blur()
        {
            FlowLayoutWidget allButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                FlowLayoutWidget topButtonBar = new FlowLayoutWidget();
                {
                    Button button1 = new Button("button1");
                    topButtonBar.AddChild(button1);
                }
                allButtons.AddChild(topButtonBar);

                FlowLayoutWidget bottomButtonBar = new FlowLayoutWidget();
                {
                    Button button2 = new Button("wide button2");
                    bottomButtonBar.AddChild(button2);
                }
                allButtons.AddChild(bottomButtonBar);

                AddChild(allButtons);
            }
        }

        public override void OnLayout()
        {
            AnchorFlags = AnchorFlags.All;
            base.OnLayout();
        }

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new BlurFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

    public class BlurFactory : AppWidgetFactory
    {
		public override GUIWidget NewWidget()
        {
            return new blur();
        }

		public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
                "Bitmap",
                "Gaussian and Stack Blur",
                @"Now you can blur rendered images rather fast! There two algorithms are used: 
Stack Blur by Mario Klingemann and Fast Recursive Gaussian Filter, described 
here and here (PDF). The speed of both methods does not depend on the filter radius. 
Mario's method works 3-5 times faster; it doesn't produce exactly Gaussian response, 
but pretty fair for most practical purposes. The recursive filter uses floating 
point arithmetic and works slower. But it is true Gaussian filter, with theoretically 
infinite impulse response. The radius (actually 2*sigma value) can be fractional 
and the filter produces quite adequate result.",
                                           300,
                                           200);

            return appWidgetInfo;
        }
    }
}