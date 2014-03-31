using System;

using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
    public class ImageWidget : GuiWidget
    {
        ImageBuffer image;

        public bool ForcePixelAlignment { get; set; }

        public ImageWidget(int width, int height)
        {
            ForcePixelAlignment = true;
            LocalBounds = new RectangleDouble(0, 0, width, height);
        }

        public ImageWidget(ImageBuffer initialImage)
            : this(initialImage.Width, initialImage.Height)
        {
            Image = initialImage;
        }

        public ImageBuffer Image
        {
            get
            {
                return image;
            }

            set
            {
                image = value;
                LocalBounds = new RectangleDouble(0, 0, image.Width, image.Height);
            }
        }

        static NamedExecutionTimer drawTimer = new NamedExecutionTimer("ImgWdgt");
        public override void OnDraw(Graphics2D graphics2D)
        {
            drawTimer.Start();
            if (image != null)
            {
                RectangleDouble screenBounds = TransformRectangleToScreenSpace(LocalBounds);
                double pixelAlignXAdjust = 0;
                double pixelAlignYAdjust = 0;
                if (ForcePixelAlignment)
                {
                    pixelAlignXAdjust = screenBounds.Left - (int)screenBounds.Left;
                    pixelAlignYAdjust = screenBounds.Bottom - (int)screenBounds.Bottom;
                }
                graphics2D.Render(image, -pixelAlignXAdjust, -pixelAlignYAdjust);
            }
            base.OnDraw(graphics2D);
            drawTimer.Stop();
        }
    }
}
