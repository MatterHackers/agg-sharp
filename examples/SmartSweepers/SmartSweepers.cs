using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI.Examples;

namespace SmartSweeper
{
	public class SmartSweepersApplication : Gaming.Game.GamePlatform, IDemoApp
	{
		private CController m_Controller;
		private static double rtri;                                              // Angle For The Triangle ( NEW )
		private static double rquad;                                             // Angle For The Quad ( NEW )
		private MatterHackers.Agg.UI.CheckBox m_SuperFast;

		public SmartSweepersApplication(double width, double height)
			: base(60, 5, width, height)
		{
			this.Title = "Smart Sweepers";
		}

		public string DemoCategory { get; } = "Game";

		public string DemoDescription { get; } = "Shows off a cool c# neral net framwork.";

		private bool firstTime = true;

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (firstTime)
			{
				firstTime = false;
				m_SuperFast = new MatterHackers.Agg.UI.CheckBox(10, 10, "Run Super Fast");
				AddChild(m_SuperFast);

				// The controller only ever wanted the play field size. It used to be handed a sub image of
				// Graphics2D.DestImage and read Width/Height off that, which on a GPU surface forced a
				// full screen CPU layer to be allocated and composited every frame for no picture at all.
				m_Controller = new CController((int)Width, (int)Height, 30, 40, .1, .7, .3, 4, 1, 2000);
			}

			graphics2D.Clear(new ColorF(1, 1, 1, 1));

			// No SetVectorClipBox here: a GPU Graphics2D has no ScanlineRasterizer, and the widget's own
			// clipping rectangle already bounds what this draws.
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
}