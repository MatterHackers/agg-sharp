using System;
using System.Diagnostics;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.RasterizerScanline;

namespace MatterHackers.Agg
{
    public class gouraud_application : GuiWidget
    {
        double[] m_x = new double[3];
        double[] m_y = new double[3];
        double m_dx;
        double m_dy;
        int    m_idx;

        MatterHackers.Agg.UI.Slider m_dilation;
        MatterHackers.Agg.UI.Slider m_gamma;
        MatterHackers.Agg.UI.Slider m_alpha;

        Stopwatch stopwatch = new Stopwatch();

        public gouraud_application()
        {
            AnchorAll();
            m_idx = (-1);
            m_dilation = new MatterHackers.Agg.UI.Slider(5, 5,    400-5, 11);
            m_dilation.ValueChanged += new EventHandler(SliderValueChanged);
            m_gamma = new MatterHackers.Agg.UI.Slider(5, 5 + 15, 400 - 5, 11 + 15);
            m_gamma.ValueChanged += new EventHandler(SliderValueChanged);
            m_alpha = new MatterHackers.Agg.UI.Slider(5, 5 + 30, 400 - 5, 11 + 30);
            m_alpha.ValueChanged += new EventHandler(SliderValueChanged);
            m_x[0] = 57; m_y[0] = 60;
            m_x[1] = 369;   m_y[1] = 170;
            m_x[2] = 143;   m_y[2] = 310;

            AddChild(m_dilation);
            AddChild(m_gamma);
            AddChild(m_alpha);

            m_dilation.Text = "Dilation={0:F2}";
            m_gamma.Text = "Linear gamma={0:F2}";
            m_alpha.Text = "Opacity={0:F2}";

            m_dilation.Value = 0.175;
            m_gamma.Value = 0.809;
            m_alpha.Value = 1.0;
        }

        void SliderValueChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        //template<class Scanline, class Ras> 
        public void render_gouraud(IImageByte backBuffer, IScanlineCache sl, IRasterizer ras)
        {
            double alpha = m_alpha.Value;
            double brc = 1;
            Graphics2D graphics2D = NewGraphics2D();

#if SourceDepth24
            pixfmt_alpha_blend_rgb pf = new pixfmt_alpha_blend_rgb(backBuffer, new blender_bgr());
#else
            ImageBuffer image = new ImageBuffer();
            image.Attach(backBuffer, new BlenderBGRA());
#endif
            ImageClippingProxy ren_base = new ImageClippingProxy(image);

            MatterHackers.Agg.span_allocator span_alloc = new span_allocator();
            span_gouraud_rgba span_gen = new span_gouraud_rgba();

            ras.gamma(new gamma_linear(0.0, m_gamma.Value));

            double d = m_dilation.Value;

            // Six triangles
            double xc = (m_x[0] + m_x[1] + m_x[2]) / 3.0;
            double yc = (m_y[0] + m_y[1] + m_y[2]) / 3.0;

            double x1 = (m_x[1] + m_x[0]) / 2 - (xc - (m_x[1] + m_x[0]) / 2);
            double y1 = (m_y[1] + m_y[0]) / 2 - (yc - (m_y[1] + m_y[0]) / 2);

            double x2 = (m_x[2] + m_x[1]) / 2 - (xc - (m_x[2] + m_x[1]) / 2);
            double y2 = (m_y[2] + m_y[1]) / 2 - (yc - (m_y[2] + m_y[1]) / 2);

            double x3 = (m_x[0] + m_x[2]) / 2 - (xc - (m_x[0] + m_x[2]) / 2);
            double y3 = (m_y[0] + m_y[2]) / 2 - (yc - (m_y[0] + m_y[2]) / 2);

            span_gen.colors(new RGBA_Floats(1, 0, 0, alpha),
                            new RGBA_Floats(0, 1, 0, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[0], m_y[0], m_x[1], m_y[1], xc, yc, d);
            ras.add_path(span_gen);
            ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);


            span_gen.colors(new RGBA_Floats(0, 1, 0, alpha),
                            new RGBA_Floats(0, 0, 1, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[1], m_y[1], m_x[2], m_y[2], xc, yc, d);
            ras.add_path(span_gen);
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);


            span_gen.colors(new RGBA_Floats(0, 0, 1, alpha),
                            new RGBA_Floats(1, 0, 0, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[2], m_y[2], m_x[0], m_y[0], xc, yc, d);
            ras.add_path(span_gen);
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);


            brc = 1-brc;
            span_gen.colors(new RGBA_Floats(1, 0, 0, alpha),
                            new RGBA_Floats(0, 1, 0, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[0], m_y[0], m_x[1], m_y[1], x1, y1, d);
            ras.add_path(span_gen);
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);


            span_gen.colors(new RGBA_Floats(0, 1, 0, alpha),
                            new RGBA_Floats(0, 0, 1, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[1], m_y[1], m_x[2], m_y[2], x2, y2, d);
            ras.add_path(span_gen);
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);


            span_gen.colors(new RGBA_Floats(0, 0, 1, alpha),
                            new RGBA_Floats(1, 0, 0, alpha),
                            new RGBA_Floats(brc, brc, brc, alpha));
            span_gen.triangle(m_x[2], m_y[2], m_x[0], m_y[0], x3, y3, d);
            ras.add_path(span_gen);
            scanlineRenderer.GenerateAndRender(ras, sl, ren_base, span_alloc, span_gen);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

            IImageByte backBuffer = widgetsSubImage;
#if SourceDepth24
            pixfmt_alpha_blend_rgb pf = new pixfmt_alpha_blend_rgb(backBuffer, new blender_bgr());
#else
            ImageBuffer pf = new ImageBuffer();
            pf.Attach(backBuffer, new BlenderBGRA());
#endif
            ImageClippingProxy ren_base = new ImageClippingProxy(pf);
            ren_base.clear(new RGBA_Floats(1.0, 1.0, 1.0));

            scanline_unpacked_8 sl = new scanline_unpacked_8();
            ScanlineRasterizer ras = new ScanlineRasterizer();
#if true
            render_gouraud(backBuffer, sl, ras);
#else
            agg.span_allocator span_alloc = new span_allocator();
            span_gouraud_rgba span_gen = new span_gouraud_rgba(new rgba8(255, 0, 0, 255), new rgba8(0, 255, 0, 255), new rgba8(0, 0, 255, 255), 320, 220, 100, 100, 200, 100, 0);
            span_gouraud test_sg = new span_gouraud(new rgba8(0, 0, 0, 255), new rgba8(0, 0, 0, 255), new rgba8(0, 0, 0, 255), 320, 220, 100, 100, 200, 100, 0);
            ras.add_path(test_sg);
            renderer_scanlines.render_scanlines_aa(ras, sl, ren_base, span_alloc, span_gen);
            //renderer_scanlines.render_scanlines_aa_solid(ras, sl, ren_base, new rgba8(0, 0, 0, 255));
#endif


            ras.gamma(new gamma_none());
            //m_dilation.Render(ras, sl, ren_base);
            //m_gamma.Render(ras, sl, ren_base);
            //m_alpha.Render(ras, sl, ren_base);
            base.OnDraw(graphics2D);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            int i;
            if (mouseEvent.Button == MouseButtons.Right)
            {
                scanline_unpacked_8 sl = new scanline_unpacked_8();
                ScanlineRasterizer ras = new ScanlineRasterizer();
                stopwatch.Restart();
                for (i = 0; i < 100; i++)
                {
                    //render_gouraud(sl, ras);
                }

                stopwatch.Stop();
                string buf;
                buf = "Time=" + stopwatch.ElapsedMilliseconds.ToString() + "ms";
                throw new NotImplementedException();
                //guiSurface.ShowSystemMessage(buf);
            }

            if (mouseEvent.Button == MouseButtons.Left)
            {
                double x = mouseEvent.X;
                double y = mouseEvent.Y;

                for (i = 0; i < 3; i++)
                {
                    if (Math.Sqrt((x - m_x[i]) * (x - m_x[i]) + (y - m_y[i]) * (y - m_y[i])) < 10.0)
                    {
                        m_dx = x - m_x[i];
                        m_dy = y - m_y[i];
                        m_idx = (int)i;
                        break;
                    }
                }
                if (i == 3)
                {
                    if (agg_math.point_in_triangle(m_x[0], m_y[0],
                                              m_x[1], m_y[1],
                                              m_x[2], m_y[2],
                                              x, y))
                    {
                        m_dx = x - m_x[0];
                        m_dy = y - m_y[0];
                        m_idx = 3;
                    }

                }
            }
            
            base.OnMouseDown(mouseEvent);
        }


        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            double x = mouseEvent.X;
            double y = mouseEvent.Y;
            if (mouseEvent.Button == MouseButtons.Left)
            {
                if (m_idx == 3)
                {
                    double dx = x - m_dx;
                    double dy = y - m_dy;
                    m_x[1] -= m_x[0] - dx;
                    m_y[1] -= m_y[0] - dy;
                    m_x[2] -= m_x[0] - dx;
                    m_y[2] -= m_y[0] - dy;
                    m_x[0] = dx;
                    m_y[0] = dy;
                    Invalidate();
                }
                else if (m_idx >= 0)
                {
                    m_x[m_idx] = x - m_dx;
                    m_y[m_idx] = y - m_dy;
                    Invalidate();
                }
            }
            else
            {
                OnMouseUp(mouseEvent);
            }

            base.OnMouseMove(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            m_idx = -1;
            base.OnMouseUp(mouseEvent);
        }

        public override void OnKeyDown(MatterHackers.Agg.UI.KeyEventArgs keyEvent)
        {
            double dx = 0;
            double dy = 0;
            switch(keyEvent.KeyCode)
            {
                case Keys.Left:  dx = -0.1; break;
                case Keys.Right: dx =  0.1; break;
                case Keys.Up:    dy =  0.1; break;
                case Keys.Down:  dy = -0.1; break;
            }
            m_x[0] += dx;
            m_y[0] += dy;
            m_x[1] += dx;
            m_y[1] += dy;
            Invalidate();
            base.OnKeyDown(keyEvent);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            AppWidgetFactory appWidget = new gouraud_application_Factory();
            appWidget.CreateWidgetAndRunInWindow();
        }
    }

    public class gouraud_application_Factory : AppWidgetFactory
    {
        public override GuiWidget NewWidget()
        {
            return new gouraud_application();
        }

        public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
                "Vector",
                "Gouraud Colors",
                "Gouraud shading. It's a simple method of interpolating colors in a triangle. There's no 'cube' drawn"
                + ", there're just 6 triangles. You define a triangle and colors in its vertices. When rendering, the "
                + "colors will be linearly interpolated. But there's a problem that appears when drawing adjacent "
                +"triangles with Anti-Aliasing. Anti-Aliased polygons do not 'dock' to each other correctly, there "
                +"visual artifacts at the edges appear. I call it “the problem of adjacent edges”. AGG has a simple"
                +" mechanism that allows you to get rid of the artifacts, just dilating the polygons and/or changing "
                +"the gamma-correction value. But it's tricky, because the values depend on the opacity of the polygons."
                +" In this example you can change the opacity, the dilation value and gamma. Also you can drag the "
                +"Red, Green and Blue corners of the “cube”.",
                400,
                320);

            return appWidgetInfo;
        }
    }
}




