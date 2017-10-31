using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class RandomFillWidget : GuiWidget
	{
		private ImageBuffer image;

		public RandomFillWidget(Point2D size)
		{
			LocalBounds = new RectangleDouble(0, 0, size.x, size.y);
			image = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());

			Random rand = new Random();
			Graphics2D imageGraphics = image.NewGraphics2D();
			for (int i = 0; i < 30; i++)
			{
				imageGraphics.Circle(rand.NextDouble() * image.Width, rand.NextDouble() * image.Height, rand.NextDouble() * 10 + 5, Color.Red);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(image, 0, 0);
			base.OnDraw(graphics2D);
		}
	}

	public class ScrollableWidgetTestPage : TabPage
	{
		public ScrollableWidgetTestPage()
			: base("Scroll Widget")
		{
			ScrollableWidget scrollWidgetLeft = new ScrollableWidget();
			scrollWidgetLeft.AutoScroll = true;
			scrollWidgetLeft.LocalBounds = new RectangleDouble(0, 0, 300, 400);
			//scrollWidgetLeft.DebugShowBounds = true;
			scrollWidgetLeft.OriginRelativeParent = new Vector2(30, 30);
			scrollWidgetLeft.AddChild(new RandomFillWidget(new Point2D(300, 600)));
			scrollWidgetLeft.AddChild(new Button("button1", 100, 100));

			scrollWidgetLeft.Margin = new BorderDouble(10);
			//scrollWidgetLeft.DebugShowBounds = true;

			AddChild(scrollWidgetLeft);

			ScrollableWidget scrollWidgetRight = new ScrollableWidget();
			scrollWidgetRight.LocalBounds = new RectangleDouble(0, 0, 250, 400);
			//scrollWidgetRight.DebugShowBounds = true;
			scrollWidgetRight.OriginRelativeParent = new Vector2(340, 30);
			AddChild(scrollWidgetRight);
		}
	}
}