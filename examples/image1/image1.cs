using System;

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
    public class image1Widget : GuiWidget
    {
        MatterHackers.Agg.UI.Slider drawAngle;
        MatterHackers.Agg.UI.Slider drawScale;
        public ImageBuffer sourceImage = new ImageBuffer();

        Vector2 orignialSize;
        public Point2D WindowSize = new Point2D(10, 10);

        public image1Widget()
        {
            AnchorAll();
            string img_name = "spheres.bmp";
            if (!ImageBMPIO.LoadImageData(img_name, sourceImage))
            {
                string buf;
                buf = "File not found: "
                    + img_name
                    + ".bmp"
                    + ". Download http://www.antigrain.com/" + img_name + ".bmp" + "\n"
                    + "or copy it from another directory if available.";
                MessageBox.ShowMessageBox(buf, "Missing Files");
            }
            else
            {
                WindowSize.x = sourceImage.Width + 20;
                WindowSize.y = sourceImage.Height + 40 + 20;
            }

            drawAngle = new MatterHackers.Agg.UI.Slider(new Vector2(5, 5+15), new Vector2(295, 7));
            drawScale = new MatterHackers.Agg.UI.Slider(new Vector2(5,  5+55), new Vector2(295, 7));
            drawAngle.ValueChanged += new EventHandler(NeedsRedraw);
            drawScale.ValueChanged += new EventHandler(NeedsRedraw);

            AddChild(drawAngle);
            AddChild(drawScale);
            //drawAngle.Text = "Angle={0:F2}";
            //drawScale.Text = "Scale={0:F2}";
            drawAngle.SetRange(-180.0, 180.0);
            drawAngle.Value = 0.0;
            drawScale.SetRange(0.1, 5.0);
            drawScale.Value = 1.0;
        }

        void NeedsRedraw(object sender, EventArgs e)
        {
            Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

            if (orignialSize.x == 0)
            {
                orignialSize.x = WindowSize.x;
                orignialSize.y = WindowSize.y;
            }

            ImageBuffer destImageWithPreMultBlender = new ImageBuffer();
            switch (widgetsSubImage.BitDepth)
            {
                case 24:
                    destImageWithPreMultBlender.Attach(widgetsSubImage, new BlenderPreMultBGR());
                    break;

                case 32:
                    destImageWithPreMultBlender.Attach(widgetsSubImage, new BlenderPreMultBGRA());
                    break;

                default:
                    throw new Exception("Unknown bit depth");
            }

            ImageClippingProxy clippingProxy_pre = new ImageClippingProxy(destImageWithPreMultBlender);

            clippingProxy_pre.clear(new RGBA_Floats(1.0, 1.0, 1.0));

            Affine src_mtx = Affine.NewIdentity();
            src_mtx *= Affine.NewTranslation(-orignialSize.x / 2 - 10, -orignialSize.y / 2 - 20 - 10);
            src_mtx *= Affine.NewRotation(drawAngle.Value * Math.PI / 180.0);
            src_mtx *= Affine.NewScaling(drawScale.Value);
            src_mtx *= Affine.NewTranslation(orignialSize.x / 2, orignialSize.y / 2 + 20);

            Affine img_mtx = Affine.NewIdentity();
            img_mtx *= Affine.NewTranslation(-orignialSize.x / 2 + 10, -orignialSize.y / 2 + 20 + 10);
            img_mtx *= Affine.NewRotation(drawAngle.Value * Math.PI / 180.0);
            img_mtx *= Affine.NewScaling(drawScale.Value);
            img_mtx *= Affine.NewTranslation(orignialSize.x / 2, orignialSize.y / 2 + 20);
            img_mtx.invert();

            MatterHackers.Agg.span_allocator sa = new span_allocator();

            span_interpolator_linear interpolator = new span_interpolator_linear(img_mtx);

            span_image_filter sg;
            switch(sourceImage.BitDepth)
            {
                case 24:
                    {
                        ImageBufferAccessorClip source = new ImageBufferAccessorClip(sourceImage, RGBA_Floats.rgba_pre(0, 0, 0, 0).GetAsRGBA_Bytes());
                        sg = new span_image_filter_rgb_bilinear_clip(source, RGBA_Floats.rgba_pre(0, 0.4, 0, 0.5), interpolator);
                    }
                    break;

                case 32:
                    {
                        ImageBufferAccessorClip source = new ImageBufferAccessorClip(sourceImage, RGBA_Floats.rgba_pre(0, 0, 0, 0).GetAsRGBA_Bytes());
                        sg = new span_image_filter_rgba_bilinear_clip(source, RGBA_Floats.rgba_pre(0, 0.4, 0, 0.5), interpolator);
                    }
                    break;

                default:
                    throw new Exception("Bad sourc depth");
            }

            ScanlineRasterizer ras = new ScanlineRasterizer();
            ras.SetVectorClipBox(0, 0, Width, Height);
            ScanlineCachePacked8 sl = new ScanlineCachePacked8();
            //scanline_unpacked_8 sl = new scanline_unpacked_8();

            double r = orignialSize.x;
            if (orignialSize.y - 60 < r)
            {
                r = orignialSize.y - 60;
            }

            VertexSource.Ellipse ell = new VertexSource.Ellipse(orignialSize.x / 2.0 + 10,
                orignialSize.y / 2.0 + 20 + 10, 
                r / 2.0 + 16.0, 
                r / 2.0 + 16.0, 200);
            
            VertexSourceApplyTransform tr = new VertexSourceApplyTransform(ell, src_mtx);

            ras.add_path(tr);
            //clippingProxy_pre.SetClippingBox(30, 0, (int)width(), (int)height());
            ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
            scanlineRenderer.GenerateAndRender(ras, sl, clippingProxy_pre, sa, sg); 

            base.OnDraw(graphics2D);
        }
    }

    public class Image1Factory : AppWidgetFactory
    {
        public override GuiWidget NewWidget()
        {
            return new image1Widget();
        }

        public override AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
                "Bitmap",
                "Image Rotate & Scale",
                @"This is the first example of the image transformation algorithms. 
The example allows you to rotate and scale the image with respect to 
its center. Also, the image is scaled when resizing the window.",
                                           350,
                                           400);

            return appWidgetInfo;
        }
    }
}
