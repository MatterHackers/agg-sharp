using System;
using NeuralNet;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace SmartSweeper
{
    public class SmartSweepersApplication : Gaming.Game.GamePlatform
    {
        CController m_Controller;
        private static double rtri;                                              // Angle For The Triangle ( NEW )
        private static double rquad;                                             // Angle For The Quad ( NEW )
        MatterHackers.Agg.UI.CheckBox m_SuperFast;

        public SmartSweepersApplication(double width, double height)
            : base(60, 5, width, height)
        {
        }

        bool firstTime = true;
        public override void OnDraw(Graphics2D graphics2D)
        {
            ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

            IImageByte backBuffer = widgetsSubImage;
            
            if (firstTime)
            {
                firstTime = false;
                m_SuperFast = new MatterHackers.Agg.UI.CheckBox(10, 10, "Run Super Fast");
                AddChild(m_SuperFast);
                m_Controller = new CController(backBuffer, 30, 40, .1, .7, .3, 4, 1, 2000);
            }

            graphics2D.Clear(new RGBA_Floats(1, 1, 1, 1));
            graphics2D.Rasterizer.SetVectorClipBox(0, 0, (int)Width, (int)Height);
            m_Controller.FastRender(m_SuperFast.Checked);
            m_Controller.Render(graphics2D);
            //m_SuperFast.Render(graphics2D);
            base.OnDraw(graphics2D);
        }

        public override void OnUpdate(double NumSecondsPassed)
        {
            if (m_SuperFast.Checked)
            {
                for (int i = 0; i < 40; i++)
                {
                    m_Controller.Update();
                }
            }
            m_Controller.Update();
            rtri += 0.2f;                                                       // Increase The Rotation Variable For The Triangle ( NEW )
            rquad -= 0.15f;                                                     // Decrease The Rotation Variable For The Quad ( NEW )
            base.OnUpdate(NumSecondsPassed);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            SmartSweepersApplication smartSweepers = new SmartSweepersApplication(640, 480);

            smartSweepers.Title = "Smart Sweepers";
            smartSweepers.ShowAsSystemWindow();
        }
    }

    public class SmartSweepersFactory : AppWidgetFactory
    {
        public override GuiWidget NewWidget()
        {
            return new SmartSweepersApplication(640, 480);
        }

        public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
                "Game",
                "Smart Sweepers",
                "Shows off a cool c# neral net framwork.",
                640,
                480);

            return appWidgetInfo;
        }
    }
}
