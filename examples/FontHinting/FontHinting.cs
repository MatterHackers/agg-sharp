using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class FontHinter : GuiWidget
	{
		private Slider pixelSizeSlider;
		private Slider gammaSlider;

		public FontHinter()
		{
			AnchorAll();
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			pixelSizeSlider = new Slider(new Vector2(30, 30), 600 - 60);
			gammaSlider = new Slider(new Vector2(30, 70), 600 - 60);

			pixelSizeSlider.Text = "Pixel size={0:F3}";
			pixelSizeSlider.SetRange(2, 20);
			pixelSizeSlider.NumTicks = 23;
			pixelSizeSlider.Value = 6;

			gammaSlider.Text = "Gamma={0:F3}";
			gammaSlider.SetRange(0.0, 3.0);
			gammaSlider.Value = 1.0;

			topToBottom.AddChild(pixelSizeSlider);
			topToBottom.AddChild(gammaSlider);

			AddChild(topToBottom);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GammaLookUpTable gamma = new GammaLookUpTable(gammaSlider.Value);
			IRecieveBlenderByte NormalBlender = new BlenderBGR();
			IRecieveBlenderByte GammaBlender = new BlenderGammaBGR(gamma);
			ImageBuffer rasterNormal = new ImageBuffer();
			rasterNormal.Attach(graphics2D.DestImage, NormalBlender);
			ImageBuffer rasterGamma = new ImageBuffer();
			rasterGamma.Attach(graphics2D.DestImage, GammaBlender);
			ImageClippingProxy clippingProxyNormal = new ImageClippingProxy(rasterNormal);
			ImageClippingProxy clippingProxyGamma = new ImageClippingProxy(rasterGamma);

			clippingProxyNormal.clear(new RGBA_Floats(1, 1, 1));

			ScanlineRasterizer ras = new ScanlineRasterizer();
			scanline_unpacked_8 sl = new scanline_unpacked_8();

			int size_mul = (int)pixelSizeSlider.Value;

			renderer_enlarged ren_en = new renderer_enlarged(size_mul);

			StyledTypeFace type = new StyledTypeFace(LiberationSansFont.Instance, 12);
			IVertexSource character = type.GetGlyphForCharacter('E');
			character.rewind(0);
			ras.reset();
			ras.add_path(character);
			ren_en.RenderSolid(clippingProxyGamma, ras, sl, RGBA_Bytes.Black);

			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(clippingProxyGamma, ras, sl, RGBA_Bytes.Black);

			ras.gamma(new gamma_none());

			PathStorage ps = new PathStorage();
			Stroke pg = new Stroke(ps);
			pg.width(2);

			DrawBigA(graphics2D);

			base.OnDraw(graphics2D);
		}

		private void DrawBigA(Graphics2D graphics2D)
		{
			ScanlineRasterizer m_ras = new ScanlineRasterizer();
			m_ras.SetVectorClipBox(0, 0, Width, Height);
			TypeFacePrinter bigAPrinter = new TypeFacePrinter("a", 150);
			FlattenCurves flattenedBigA = new FlattenCurves(bigAPrinter);
			VertexSourceApplyTransform scaleAndTranslate = new VertexSourceApplyTransform(flattenedBigA, Affine.NewTranslation(155, 55));
			ScanlineCachePacked8 m_sl = new ScanlineCachePacked8();
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();

#if false
            ImageProxySubpxelLcd24 clippingProxy = new ImageProxySubpxelLcd24(graphics2D.DestImage, new lcd_distribution_lut());
            VertexSourceApplyTransform scaledWide = new VertexSourceApplyTransform(scaleAndTranslate, Affine.NewScaling(3, 1));
            m_ras.add_path(scaledWide);
            scanlineRenderer.render_scanlines_aa_solid(clippingProxy, m_ras, m_sl, RGBA_Bytes.Black);
#else
			m_ras.add_path(scaleAndTranslate);
			ImageClippingProxy clippingProxy = new ImageClippingProxy(graphics2D.DestImage);
			scanlineRenderer.RenderSolid(clippingProxy, m_ras, m_sl, RGBA_Bytes.Black);
#endif
		}

		[STAThread]
		public static void Main(string[] args)
		{
			MatterHackers.Agg.UI.Tests.UnitTests.Run();
			AppWidgetFactory appWidget = new BlurFactory();
			appWidget.CreateWidgetAndRunInWindow(SystemWindow.PixelTypes.Depth24);
		}
	}

	public class BlurFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new FontHinter();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Bitmap",
				"Font Hinter",
				@"",
				440,
				330);

			return appWidgetInfo;
		}
	}

	public class lcd_distribution_lut
	{
		private byte[] m_primary = new byte[256];
		private byte[] m_secondary = new byte[256];
		private byte[] m_tertiary = new byte[256];

		public lcd_distribution_lut(double prim = 1.0/3.0, double second = 2.0/9.0, double tert = 1.0/9.0)
		{
			double norm = 1.0 / (prim + second * 2 + tert * 2);
			prim *= norm;
			second *= norm;
			tert *= norm;
			for (int i = 0; i < 256; i++)
			{
				m_primary[i] = (byte)Math.Floor(prim * i);
				m_secondary[i] = (byte)Math.Floor(second * i);
				m_tertiary[i] = (byte)Math.Floor(tert * i);
			}
		}

		public uint primary(uint v)
		{
			return m_primary[v];
		}

		public uint secondary(uint v)
		{
			return m_secondary[v];
		}

		public uint tertiary(uint v)
		{
			return m_tertiary[v];
		}
	}

	public class ImageProxySubpxelLcd24 : ImageProxy
	{
		private lcd_distribution_lut m_lut;

		public ImageProxySubpxelLcd24(IImageByte bufferToProxy, lcd_distribution_lut lut)
			: base(bufferToProxy)
		{
			m_lut = lut;
		}

		public override int Width
		{
			get
			{
				return base.Width * 3;
			}
		}

		public override void blend_hline(int x1, int y, int x2, RGBA_Bytes c, byte cover)
		{
			byte[] buffer = linkedImage.GetBuffer();
			int index = linkedImage.GetBufferOffsetY(y) + x1;// +x1 + x1;
			int alpha = (int)cover * c.alpha;
			int len = x2 - x1;
			len /= 3;
			do
			{
				buffer[index + ImageBuffer.OrderR] = (byte)((((c.red - buffer[index + ImageBuffer.OrderR]) * alpha) + (buffer[index + ImageBuffer.OrderR] << 16)) >> 16);
				buffer[index + ImageBuffer.OrderG] = (byte)((((c.green - buffer[index + ImageBuffer.OrderG]) * alpha) + (buffer[index + ImageBuffer.OrderG] << 16)) >> 16);
				buffer[index + ImageBuffer.OrderB] = (byte)((((c.blue - buffer[index + ImageBuffer.OrderB]) * alpha) + (buffer[index + ImageBuffer.OrderB] << 16)) >> 16);
				index += 3;
			}
			while (--len > 0);
		}

		private byte[] c3 = new byte[2048 * 3];

		public override void blend_solid_hspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			if (c3.Length < len + 4)
			{
				c3 = new byte[len + 4];
			}

			agg_basics.memset(c3, 0, 0, len + 4);

			int i;
			for (i = 0; i < len; i++)
			{
				c3[i + 0] += (byte)m_lut.tertiary(covers[coversIndex + i]);
				c3[i + 1] += (byte)m_lut.secondary(covers[coversIndex + i]);
				c3[i + 2] += (byte)m_lut.primary(covers[coversIndex + i]);
				c3[i + 3] += (byte)m_lut.secondary(covers[coversIndex + i]);
				c3[i + 4] += (byte)m_lut.tertiary(covers[coversIndex + i]);
			}

			x -= 2;
			len += 4;

			if (x < 0)
			{
				len -= x;
				x = 0;
			}

			i = x % 3;
			int c3Index = 0;

			byte[] rgb = new byte[] { c.blue, c.green, c.red };
			byte[] buffer = linkedImage.GetBuffer();
			int index = linkedImage.GetBufferOffsetY(y) + x;

			do
			{
				int alpha = c3[c3Index++] * c.alpha;
				if (alpha > 0)
				{
					if (alpha == 255 * 255)
					{
						buffer[index] = (byte)rgb[i];
					}
					else
					{
						buffer[index] = (byte)((((rgb[i] - buffer[index]) * alpha) + (buffer[index] << 16)) >> 16);
					}
				}
				index++;
				i++;
				if (i >= 3)
				{
					i = 0;
				}
			}
			while (--len > 0);
		}
	}

	public class square
	{
		private double m_size;

		public square(double size)
		{
			m_size = size;
		}

		public void draw(ScanlineRasterizer ras, IScanlineCache sl, IImageByte destImage, RGBA_Bytes color,
				  double x, double y)
		{
			ras.reset();
			ras.move_to_d(x * m_size, y * m_size);
			ras.line_to_d(x * m_size + m_size, y * m_size);
			ras.line_to_d(x * m_size + m_size, y * m_size + m_size);
			ras.line_to_d(x * m_size, y * m_size + m_size);
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(destImage, ras, sl, color);
		}
	}

	internal class renderer_enlarged : ScanlineRenderer
	{
		private double m_size;
		private square m_square;
		private scanline_unpacked_8 m_sl = new scanline_unpacked_8();

		public renderer_enlarged(double size)
		{
			m_size = size;
			m_square = new square(size);
		}

		protected override void RenderSolidSingleScanLine(IImageByte destImage, IScanlineCache scanLineCache, RGBA_Bytes color)
		{
			int y = scanLineCache.y();
			int num_spans = scanLineCache.num_spans();
			ScanlineSpan scanlineSpan = scanLineCache.begin();

			byte[] ManagedCoversArray = scanLineCache.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				int num_pix = scanlineSpan.len;
				int coverIndex = scanlineSpan.cover_index;

				do
				{
					int a = (ManagedCoversArray[coverIndex++] * color.Alpha0To255) >> 8;
					m_square.draw(destImage.NewGraphics2D().Rasterizer, m_sl, destImage,
									new RGBA_Bytes(color.Red0To255, color.Green0To255, color.Blue0To255, a),
									x, y);
					++x;
				}
				while (--num_pix > 0);
				if (--num_spans == 0) break;
				scanlineSpan = scanLineCache.GetNextScanlineSpan();
			}
		}
	}
}