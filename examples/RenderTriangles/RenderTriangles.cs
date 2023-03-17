using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class TriangleRenderer : GuiWidget
    {
		public static List<Color> colors = null;
        public bool DrawNumbers { get; set; }

        public TriangleRenderer()
        {
			this.DrawNumbers = DrawNumbers;

			if (colors == null)
            {
				colors = new List<Color>();
				var count = 20;
				for(int i=0; i<count; i++)
                {
					colors.Add(ColorF.FromHSL(i/(double)count, .7, .6).ToColor());
				}
            }

			BackgroundColor = Color.LightBlue;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
			// get the bounds of all the points
			var bounds = RectangleDouble.ZeroIntersection;

			foreach (var poly in Polygons)
			{
				foreach (var point in poly)
				{
					bounds.ExpandToInclude(point);
				}
			}

			// prep for scaling
			var transform = Affine.NewTranslation(-bounds.Left, -bounds.Bottom);
			// scale to window
			var scale = Math.Min(Width / bounds.Width, Height / bounds.Height);
			transform *= Affine.NewScaling(scale);
			// scale for boarder
			transform *= Affine.NewScaling(.9);
			// move for boarder
			transform *= Affine.NewTranslation(Width / 20, Height / 20);

			// draw all the triangles
			var colorIndex = 0;
			foreach (var poly in Polygons)
			{
				var numPoints = poly.Count;
				if (numPoints > 2)
				{
					var first = true;
					var vertexStorage = new VertexStorage();
					for (int i = 0; i < numPoints; i++)
					{
						var p0 = poly[i];
						if (first)
						{
							vertexStorage.MoveTo(p0);
							first = false;
						}
						else
						{
							vertexStorage.LineTo(p0);
						}
					}
				
					graphics2D.Render(new VertexSourceApplyTransform(vertexStorage, transform), 0, 0, colors[colorIndex++].WithAlpha(190));
				}
				else if (numPoints > 1)
				{
					var p0 = poly[numPoints - 1];
					var p1 = poly[numPoints - 2];
					graphics2D.Line(transform.Transform(p0), transform.Transform(p1), Color.Green);
				}
				else if (numPoints == 1)
				{
                    var p0 = poly[numPoints - 1];
                    graphics2D.Circle(transform.Transform(p0), 5, Color.Red);
                }
			}

			if (DrawNumbers)
			{
				// draw all the numbers
                foreach (var poly in Polygons.Where(poly => poly.Count > 1))
				{
                    var numPoints = poly.Count;
                    for (int i = 0; i < numPoints; i++)
					{
                        var p0 = poly[i];
                        graphics2D.DrawString(i.ToString(), transform.Transform(p0));
                    }

                    colorIndex++;
                }
			}

			base.OnDraw(graphics2D);
        }

		public List<List<Vector2>> Polygons { get; set; } = new List<List<Vector2>>();
    }

    public class RenderTriangles : GuiWidget, IDemoApp
	{
        private ThemedTextEditWidget textWidget;

        string CurrentFile()
		{
			return "Polygons_0.txt";
		}

		string LoadFile()
		{
			if (File.Exists(CurrentFile()))
			{
				return File.ReadAllText(CurrentFile());
			}

			return "";
		}

		void SaveFile()
		{
			File.WriteAllText(CurrentFile(), textWidget.Text);
		}

		public RenderTriangles()
		{
			var spliter = new Splitter()
			{
				SplitterBackground = Color.Gray,
				SplitterDistance = 200
			};

			AddChild(spliter);

			var leftSideTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
            {
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
            };
			spliter.Panel1.AddChild(leftSideTopToBottom);

			textWidget = new ThemedTextEditWidget("", ThemeConfig.DefaultTheme(),
				multiLine: true,
                messageWhenEmptyAndNotSelected: "Each line should have a comma separated\n x, y. Add as many lines as you want.\nAdd an empty line to creat a new polygon.",
				pixelWidth: 200)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				Margin = 3,
			};

			textWidget.ActualTextEditWidget.VAnchor = VAnchor.Stretch;
			textWidget.ActualTextEditWidget.HAnchor = HAnchor.Stretch;

			var showNumbers = new CheckBox("Show Numbers")
			{
				Margin = new BorderDouble(7),
				HAnchor = HAnchor.Left
			};

            leftSideTopToBottom.AddChild(textWidget);

            leftSideTopToBottom.AddChild(showNumbers);

            spliter.AddChild(new VerticalLine());

			var triangleRenderer = new TriangleRenderer()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			showNumbers.CheckedStateChanged += (s, e) =>
			{
				triangleRenderer.DrawNumbers = showNumbers.Checked;
			};

            spliter.Panel2.AddChild(triangleRenderer);

			textWidget.TextChanged += (sender, e) =>
			{
				var poly = new List<Vector2>();
				var polygons = new List<List<Vector2>>();
				polygons.Add(poly);

				var lines = textWidget.Text.Split('\n');
				foreach (var line in lines)
				{
					if (string.IsNullOrEmpty(line))
					{
						poly = new List<Vector2>();
						polygons.Add(poly);
					}
					else
					{
						var parts = line.Split(',');
						if (parts.Length == 2)
						{
							if (double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y))
							{
								poly.Add(new Vector2(x, y));
							}
						}
					}
				}

				triangleRenderer.Polygons = polygons;
				SaveFile();
				Invalidate();
            };

			textWidget.Text = LoadFile();

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