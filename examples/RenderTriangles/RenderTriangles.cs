using System;
using System.Collections.Generic;
using System.Diagnostics;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class TriangleRenderer : GuiWidget
    {
		public TriangleRenderer()
        {
			BackgroundColor = Color.LightBlue;
        }


		private List<Vector2> _points = new List<Vector2>();

        public override void OnDraw(Graphics2D graphics2D)
        {
            // draw all the triangles
            var count = Points.Count;
            for (int i = 0; i < count / 3; i++)
			{
				var p0 = Points[i*3];
                var p1 = Points[i*3+1];
				var p2 = Points[i*3+2];
				var vertexStorage = new VertexStorage();
				vertexStorage.MoveTo(p0);
				vertexStorage.LineTo(p1);
                vertexStorage.LineTo(p2);
                graphics2D.Render(vertexStorage, 0, 0, Color.Red.WithAlpha(160));
			}

            var leftovers = (count % 3);
			if (leftovers == 2)
			{                 
				var p0 = Points[count - 1];
				var p1 = Points[count - 2];
				graphics2D.Line(p0, p1, Color.Red);
			}

			base.OnDraw(graphics2D);
        }

        public List<Vector2> Points
		{
			get => _points;
			
			set
			{
				_points = value;
				Invalidate();
			}
		}
    }

    public class RenderTriangles : GuiWidget, IDemoApp
	{
		public RenderTriangles()
		{
			var leftToRight = AddChild(new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			});

			var textWidget = new TextEditWidget(pixelWidth: 200)
			{
				VAnchor = VAnchor.Stretch,
				Multiline = true,
			};

			leftToRight.AddChild(textWidget);

			leftToRight.AddChild(new VerticalLine());

			var triangleRenderer = new TriangleRenderer()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			leftToRight.AddChild(triangleRenderer);

			textWidget.TextChanged += (sender, e) =>
			{
				var lines = textWidget.Text.Split('\n');
				var points = new List<Vector2>();
				foreach (var line in lines)
				{
					var parts = line.Split(',');
					if (parts.Length == 2)
					{
						if (double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y))
						{
							points.Add(new Vector2(x, y));
						}
					}
				}

				triangleRenderer.Points = points;
            };
		

			triangleRenderer.Invalidate();

			AnchorAll();
		}

		public string Title { get; } = "RenderTriangles";

		public string DemoCategory { get; } = "RenderTriangles";

        public string DemoDescription { get; } = "Easy way to render triangles in comma separated lines";

		public override void OnDraw(Graphics2D graphics2D)
		{
			this.NewGraphics2D().Clear(new Color(255, 255, 255));

			base.OnDraw(graphics2D);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

			var demoWidget = new RenderTriangles();

			var systemWindow = new SystemWindow(800, 600);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}