using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class FloodFillDemo : GuiWidget
	{
		private ImageBuffer imageToFillOn;

		private Slider m_slider1;
		private Slider m_slider2;

		private Point2D imageOffset = new Point2D(20, 60);

		public FloodFillDemo()
		{
			BackgroundColor = Color.White;
			imageToFillOn = new ImageBuffer(400, 300, 32, new BlenderBGRA());
			Graphics2D imageToFillGraphics = imageToFillOn.NewGraphics2D();
			imageToFillGraphics.Clear(Color.White);
			imageToFillGraphics.DrawString("Click to fill", 20, 30);
			imageToFillGraphics.Circle(new Vector2(200, 150), 35, Color.Black);
			imageToFillGraphics.Circle(new Vector2(200, 150), 30, Color.Green);
			imageToFillGraphics.Rectangle(20, 50, 210, 280, Color.Black);
			imageToFillGraphics.Rectangle(imageToFillOn.GetBounds(), Color.Blue);

			Random rand = new Random();
			for (int i = 0; i < 20; i++)
			{
				Ellipse elipse = new Ellipse(rand.Next(imageToFillOn.Width), rand.Next(imageToFillOn.Height), rand.Next(10, 60), rand.Next(10, 60));
				Stroke outline = new Stroke(elipse);
				imageToFillGraphics.Render(outline, Color.Black);
			}

			m_slider1 = new Slider(new Vector2(80, 10), 510);
			m_slider2 = new Slider(new Vector2(80, 10 + 20), 510);

			m_slider1.ValueChanged += new EventHandler(NeedsRedraw);
			m_slider2.ValueChanged += new EventHandler(NeedsRedraw);

			AddChild(m_slider1);
			AddChild(m_slider2);

			m_slider1.Text = "Pixel size={0:F3}";
			m_slider1.SetRange(8, 100);
			m_slider1.NumTicks = 23;
			m_slider1.Value = 32;

			m_slider2.Text = "gamma={0:F3}";
			m_slider2.SetRange(0.0, 3.0);
			m_slider2.Value = 1.0;
		}

		public string Title { get; } = "Flood Fill";

		public string DemoCategory { get; } = "Bitmap";

		public string DemoDescription { get; } = "Demonstration of a flood filling algorithm.";

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(imageToFillOn, imageOffset);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (mouseEvent.Button == MouseButtons.Left)
			{
				int x = (int)mouseEvent.X - imageOffset.x;
				int y = (int)mouseEvent.Y - imageOffset.y;

				FloodFill filler = new FloodFill(Color.Red);
				filler.Fill(imageToFillOn, x, y);

				Invalidate();
			}
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var demoWidget = new FloodFillDemo();

			var systemWindow = new SystemWindow(600, 400);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}