using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class perspective_application : GuiWidget
	{
		private MatterHackers.Agg.ScanlineRasterizer g_rasterizer = new ScanlineRasterizer();
		private ScanlineCachePacked8 g_scanline = new ScanlineCachePacked8();

		private UI.PolygonEditWidget quadPolygonControl;
		private UI.RadioButtonGroup transformationTypeRadioButton;
		private LionShape lionShape = new LionShape();

		public perspective_application()
		{
			AnchorAll();
			quadPolygonControl = new MatterHackers.Agg.UI.PolygonEditWidget(4, 5.0);
			transformationTypeRadioButton = new MatterHackers.Agg.UI.RadioButtonGroup(new Vector2(420, 5.0), new Vector2(130.0, 50.0));
			transformationTypeRadioButton.SelectionChanged += new EventHandler(NeedsRedraw);
			quadPolygonControl.SetXN(0, lionShape.Bounds.Left);
			quadPolygonControl.SetYN(0, lionShape.Bounds.Top);
			quadPolygonControl.SetXN(1, lionShape.Bounds.Right);
			quadPolygonControl.SetYN(1, lionShape.Bounds.Top);
			quadPolygonControl.SetXN(2, lionShape.Bounds.Right);
			quadPolygonControl.SetYN(2, lionShape.Bounds.Bottom);
			quadPolygonControl.SetXN(3, lionShape.Bounds.Left);
			quadPolygonControl.SetYN(3, lionShape.Bounds.Bottom);

			transformationTypeRadioButton.AddRadioButton("Bilinear");
			transformationTypeRadioButton.AddRadioButton("Perspective");
			transformationTypeRadioButton.SelectedIndex = 0;
			AddChild(transformationTypeRadioButton);
		}

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public void OnInitialize()
		{
			double dx = Width / 2.0 - (quadPolygonControl.GetXN(1) - quadPolygonControl.GetXN(0)) / 2.0;
			double dy = Height / 2.0 - (quadPolygonControl.GetYN(0) - quadPolygonControl.GetYN(2)) / 2.0;
			quadPolygonControl.AddXN(0, dx);
			quadPolygonControl.AddYN(0, dy);
			quadPolygonControl.AddXN(1, dx);
			quadPolygonControl.AddYN(1, dy);
			quadPolygonControl.AddXN(2, dx);
			quadPolygonControl.AddYN(2, dy);
			quadPolygonControl.AddXN(3, dx);
			quadPolygonControl.AddYN(3, dy);
		}

		private bool didInit = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			IImageByte backBuffer = widgetsSubImage;

			if (!didInit)
			{
				didInit = true;
				OnInitialize();
			}
			ImageBuffer image;
			if (backBuffer.BitDepth == 32)
			{
				image = new ImageBuffer();
				image.Attach(backBuffer, new BlenderBGRA());
			}
			else
			{
				if (backBuffer.BitDepth != 24)
				{
					throw new System.NotSupportedException();
				}
				image = new ImageBuffer();
				image.Attach(backBuffer, new BlenderBGR());
			}
			ImageClippingProxy clippingProxy = new ImageClippingProxy(image);
			clippingProxy.clear(new RGBA_Floats(1, 1, 1));

			g_rasterizer.SetVectorClipBox(0, 0, Width, Height);

			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			if (transformationTypeRadioButton.SelectedIndex == 0)
			{
				Bilinear tr = new Bilinear(lionShape.Bounds.Left, lionShape.Bounds.Bottom, lionShape.Bounds.Right, lionShape.Bounds.Top, quadPolygonControl.polygon());
				if (tr.is_valid())
				{
					//--------------------------
					// Render transformed lion
					//
					VertexSourceApplyTransform trans = new VertexSourceApplyTransform(lionShape.Path, tr);

					scanlineRenderer.RenderSolidAllPaths(clippingProxy, g_rasterizer, g_scanline, trans, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);
					//--------------------------

					//--------------------------
					// Render transformed ellipse
					//
					VertexSource.Ellipse ell = new MatterHackers.Agg.VertexSource.Ellipse((lionShape.Bounds.Left + lionShape.Bounds.Right) * 0.5, (lionShape.Bounds.Bottom + lionShape.Bounds.Top) * 0.5,
									 (lionShape.Bounds.Right - lionShape.Bounds.Left) * 0.5, (lionShape.Bounds.Top - lionShape.Bounds.Bottom) * 0.5,
									 200);
					Stroke ell_stroke = new Stroke(ell);
					ell_stroke.width(3.0);
					VertexSourceApplyTransform trans_ell = new VertexSourceApplyTransform(ell, tr);

					VertexSourceApplyTransform trans_ell_stroke = new VertexSourceApplyTransform(ell_stroke, tr);

					g_rasterizer.add_path(trans_ell);
					scanlineRenderer.render_scanlines_aa_solid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0.5, 0.3, 0.0, 0.3));

					g_rasterizer.add_path(trans_ell_stroke);
					scanlineRenderer.render_scanlines_aa_solid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0.0, 0.3, 0.2, 1.0));
				}
			}
			else
			{
				Perspective tr = new Perspective(lionShape.Bounds.Left, lionShape.Bounds.Bottom, lionShape.Bounds.Right, lionShape.Bounds.Top, quadPolygonControl.polygon());
				if (tr.is_valid())
				{
					// Render transformed lion
					VertexSourceApplyTransform trans = new VertexSourceApplyTransform(lionShape.Path, tr);

					scanlineRenderer.RenderSolidAllPaths(clippingProxy, g_rasterizer, g_scanline, trans, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);

					// Render transformed ellipse
					VertexSource.Ellipse FilledEllipse = new MatterHackers.Agg.VertexSource.Ellipse((lionShape.Bounds.Left + lionShape.Bounds.Right) * 0.5, (lionShape.Bounds.Bottom + lionShape.Bounds.Top) * 0.5,
									 (lionShape.Bounds.Right - lionShape.Bounds.Left) * 0.5, (lionShape.Bounds.Top - lionShape.Bounds.Bottom) * 0.5,
									 200);

					Stroke EllipseOutline = new Stroke(FilledEllipse);
					EllipseOutline.width(3.0);
					VertexSourceApplyTransform TransformedFilledEllipse = new VertexSourceApplyTransform(FilledEllipse, tr);

					VertexSourceApplyTransform TransformedEllipesOutline = new VertexSourceApplyTransform(EllipseOutline, tr);

					g_rasterizer.add_path(TransformedFilledEllipse);
					scanlineRenderer.render_scanlines_aa_solid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0.5, 0.3, 0.0, 0.3));

					g_rasterizer.add_path(TransformedEllipesOutline);
					scanlineRenderer.render_scanlines_aa_solid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0.0, 0.3, 0.2, 1.0));
				}
			}

			//--------------------------
			// Render the "quad" tool and controls
			g_rasterizer.add_path(quadPolygonControl);
			scanlineRenderer.render_scanlines_aa_solid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0, 0.3, 0.5, 0.6));
			//m_trans_type.Render(g_rasterizer, g_scanline, clippingProxy);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				quadPolygonControl.OnMouseDown(mouseEvent);
				if (MouseCaptured)
				{
					Invalidate();
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				quadPolygonControl.OnMouseMove(mouseEvent);
				if (MouseCaptured)
				{
					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			quadPolygonControl.OnMouseUp(mouseEvent);
			if (MouseCaptured)
			{
				Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new PerspectiveFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class PerspectiveFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new perspective_application();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Vector",
				"Perspective Rendering",
				"Perspective and bilinear transformations. In general, these classes can transform an arbitrary quadrangle "
			+ " to another arbitrary quadrangle (with some restrictions). The example demonstrates how to transform "
			+ "a rectangle to a quadrangle defined by 4 vertices. You can drag the 4 corners of the quadrangle, "
			+ "as well as its boundaries. Note, that the perspective transformations don't work correctly if "
			+ "the destination quadrangle is concave. Bilinear thansformations give a different result, but "
			+ "remain valid with any shape of the destination quadrangle.",
				600,
				600);

			return appWidgetInfo;
		}
	}
}