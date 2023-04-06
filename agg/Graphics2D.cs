//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
    public record ColoredVertexSource(IVertexSource VertexSource, Color Color);

    public interface IStyleHandler
    {
        Color color(int style);

        void GenerateSpan(Color[] span, int spanIndex, int x, int y, int len, int style);

        bool IsSolid(int style);
    };

    public abstract class Graphics2D
    {
        protected Stack<Affine> affineTransformStack = new Stack<Affine>();
        protected IImageByte destImageByte;
        protected IImageFloat destImageFloat;
        protected ScanlineRasterizer rasterizer;
        protected Stroke StrockedText;
        private const int cover_full = 255;

        public Graphics2D()
        {
            affineTransformStack.Push(Affine.NewIdentity());
        }

        public Graphics2D(IImageByte destImage, ScanlineRasterizer rasterizer)
            : this()
        {
            Initialize(destImage, rasterizer);
        }

        public enum Alignment
        {
            Left,
            Center,
            Right
        }

        public enum TransformQuality
        {
            Fastest,
            Best
        }

        public IImageByte DestImage
        {
            get
            {
                return destImageByte;
            }
        }

        public IImageFloat DestImageFloat
        {
            get
            {
                return destImageFloat;
            }
        }

        public double DeviceScale { get; set; } = 1;
        public abstract int Height { get; }
        public TransformQuality ImageRenderQuality { get; set; } = TransformQuality.Fastest;

        public ScanlineRasterizer Rasterizer
        {
            get { return rasterizer; }
        }

        public abstract IScanlineCache ScanlineCache
        {
            get;
            set;
        }

        public int TransformStackCount
        {
            get { return affineTransformStack.Count; }
        }

        public abstract int Width { get; }

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }

        public static double GetScallingBaseOnMaxSize(ImageBuffer image, Vector2 maxSize, out Vector2 size)
        {
            double ratio = 1;
            size = new Vector2(image.Width, image.Height);
            if (size.X > maxSize.X)
            {
                size.X = maxSize.X;
                ratio = size.X / image.Width;
                size.Y = image.Height * ratio;
            }

            if (size.Y > maxSize.Y)
            {
                size.Y = maxSize.Y;
                ratio = size.Y / image.Height;
                size.X = image.Width * ratio;
            }

            return ratio;
        }

        public void Circle(Vector2 origin, double radius, Color color)
        {
            Circle(origin.X, origin.Y, radius, color);
        }

        public void Circle(double x, double y, double radius, Color color)
        {
            Ellipse elipse = new Ellipse(x, y, radius, radius);
            Render(elipse, color);
        }

        public abstract void Clear(IColorType color);

        /// <summary>
        /// Draws an arc representing a portion of an ellipse specified by a Rectangle structure.
        /// </summary>
        /// <param name="color">The color to draw in.</param>
        /// <param name="rect">Structure that defines the boundaries of the ellipse.</param>
        /// <param name="startAngle">Angle in degrees measured clockwise from the x-axis to the starting point of the arc.</param>
        /// <param name="sweepAngle">Angle in degrees measured clockwise from the startAngle parameter to ending point of the arc.</param>
        public void DrawArc(Color color, RectangleDouble rect, int startAngle, int sweepAngle)
        {
            throw new NotImplementedException();
        }

        public void DrawLine(Color color, Vector2 start, Vector2 end)
        {
            Line(start, end, color);
        }

        public void DrawString(string text,
            Vector2 position,
            double pointSize = 12,
            Justification justification = Justification.Left,
            Baseline baseline = Baseline.Text,
            Color color = default(Color),
            bool drawFromHintedCach = false,
            Color backgroundColor = default(Color),
            bool bold = false)
        {
            DrawString(text, position.X, position.Y, pointSize, justification, baseline, color, drawFromHintedCach, backgroundColor, bold);
        }

        public void DrawString(string text,
            double x,
            double y,
            double pointSize = 12,
            Justification justification = Justification.Left,
            Baseline baseline = Baseline.Text,
            Color color = default(Color),
            bool drawFromHintedCach = false,
            Color backgroundColor = default(Color),
            bool bold = false)
        {
            TypeFacePrinter stringPrinter = new TypeFacePrinter(text, pointSize, new Vector2(x, y), justification, baseline, bold);
            if (color.Alpha0To255 == 0)
            {
                color = Color.Black;
            }

            if (backgroundColor.Alpha0To255 != 0)
            {
                FillRectangle(stringPrinter.LocalBounds, backgroundColor);
            }

            stringPrinter.DrawFromHintedCache = drawFromHintedCach;
            stringPrinter.Render(this, color);
        }

        public void FillRectangle(RectangleDouble rect, IColorType fillColor)
        {
            FillRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, fillColor);
        }

        public void FillRectangle(RectangleInt rect, IColorType fillColor)
        {
            FillRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, fillColor);
        }

        public void FillRectangle(Vector2 leftBottom, Vector2 rightTop, IColorType fillColor)
        {
            FillRectangle(leftBottom.X, leftBottom.Y, rightTop.X, rightTop.Y, fillColor);
        }

        public abstract void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor);

        public abstract RectangleDouble GetClippingRect();

        public Affine GetTransform()
        {
            return affineTransformStack.Peek();
        }

        public void Initialize(IImageByte destImage, ScanlineRasterizer rasterizer)
        {
            destImageByte = destImage;
            destImageFloat = null;
            this.rasterizer = rasterizer;
        }

        public void Initialize(IImageFloat destImage, ScanlineRasterizer rasterizer)
        {
            destImageByte = null;
            destImageFloat = destImage;
            this.rasterizer = rasterizer;
        }

        /// <summary>
        /// Render a line
        /// </summary>
        /// <param name="start">start position</param>
        /// <param name="end">end position</param>
        /// <param name="color">line color</param>
        /// <param name="strokeWidth">The width in pixels, -1 will render 1 pixel scaled to device units</param>
        public void Line(Vector2 start, Vector2 end, Color color, double strokeWidth = -1)
        {
            if (strokeWidth == -1)
            {
                strokeWidth = 1 * DeviceScale;
            }

            Line(start.X, start.Y, end.X, end.Y, color, strokeWidth);
        }

        /// <summary>
        /// Render a line
        /// </summary>
        /// <param name="x1">x start</param>
        /// <param name="y1">y start</param>
        /// <param name="x2">x end</param>
        /// <param name="y2">y end</param>
        /// <param name="color">color of the line</param>
        /// <param name="strokeWidth">The width in pixels, -1 will render 1 pixel scaled to device units</param>
        public virtual void Line(double x1, double y1, double x2, double y2, Color color, double strokeWidth = -1)
        {
            if (strokeWidth == -1)
            {
                strokeWidth = 1 * DeviceScale;
            }

            var lineToDraw = new VertexStorage();
            lineToDraw.Clear();
            lineToDraw.MoveTo(x1, y1);
            lineToDraw.LineTo(x2, y2);

            this.Render(
                new Stroke(lineToDraw, strokeWidth),
                color);
        }

        public Affine PopTransform()
        {
            if (affineTransformStack.Count == 1)
            {
                throw new System.Exception("You cannot remove the last transform from the stack.");
            }

            return affineTransformStack.Pop();
        }

        public void PushTransform()
        {
            if (affineTransformStack.Count > 1000)
            {
                throw new System.Exception("You seem to be leaking transforms.  You should be popping some of them at some point.");
            }

            affineTransformStack.Push(affineTransformStack.Peek());
        }

        public abstract void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth = -1);

        public void Rectangle(RectangleDouble rect, Color color, double strokeWidth = -1)
        {
            if (strokeWidth == -1)
            {
                strokeWidth = 1 * DeviceScale;
            }

            Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, color, strokeWidth);
        }

        public void Rectangle(RectangleInt rect, Color color)
        {
            Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, color);
        }

        public abstract void Render(IVertexSource vertexSource, IColorType colorType);

        public void Render(IImageByte imageSource, Point2D position)
        {
            Render(imageSource, position.x, position.y);
        }

        public void Render(IImageByte imageSource, Vector2 position)
        {
            Render(imageSource, position.X, position.Y);
        }

        public void Render(IImageByte imageSource, Vector2 position, double width, double height)
        {
            Render(imageSource, position.X, position.Y, width, height);
        }

        public void Render(IImageByte imageSource, double x, double y)
        {
            Render(imageSource, x, y, 0, 1, 1);
        }

        public void Render(IImageByte imageSource, double x, double y, double width, double height)
        {
            Render(imageSource, x, y, 0, width / imageSource.Width, height / imageSource.Height);
        }

        public abstract void Render(IImageByte imageSource,
            double x,
            double y,
            double angleRadians,
            double scaleX,
            double scaleY);

        public abstract void Render(IImageFloat imageSource,
            double x,
            double y,
            double angleRadians,
            double scaleX,
            double scaleY);

        public void Render(IVertexSource vertexSource, double x, double y, IColorType color)
        {
            Render(new VertexSourceApplyTransform(vertexSource, Affine.NewTranslation(x, y)), color);
        }

        public void Render(IVertexSource vertexSource, Vector2 position, IColorType color)
        {
            Render(new VertexSourceApplyTransform(vertexSource, Affine.NewTranslation(position.X, position.Y)), color);
        }

        public void RenderMaxSize(ImageBuffer image, Vector2 position, Vector2 maxSize)
        {
            var zero = Vector2.Zero;
            RenderMaxSize(image, position, maxSize, ref zero, out _);
        }

        public void RenderMaxSize(ImageBuffer image, Vector2 position, Vector2 maxSize, ref Vector2 origin)
        {
            RenderMaxSize(image, position, maxSize, ref origin, out _);
        }

        /// <summary>
        /// Renders the given image at the given position scaling down if bigger than maxSize
        /// </summary>
        /// <param name="image">The image to render</param>
        /// <param name="position">The postion to render it at</param>
        /// <param name="maxSize">The max size to allow it to render to. Will be scaled down to fit.</param>
        /// <param name="origin">The postion in the sourc to hold at the 'positon'</param>
        /// <param name="size"></param>
        public void RenderMaxSize(ImageBuffer image, Vector2 position, Vector2 maxSize, ref Vector2 origin, out Vector2 size)
        {
            var ratio = GetScallingBaseOnMaxSize(image, maxSize, out size);
            origin *= ratio;

            if (size.X != image.Width)
            {
                this.Render(image.CreateScaledImage(size.X / image.Width), position.X - origin.X, position.Y - origin.Y, size.X, size.Y);
            }
            else
            {
                this.Render(image, position - origin);
            }
        }

        public void RenderInRect(string text,
            double pointSize,
            RectangleDouble fitRect,
            out RectangleDouble renderedBounds,
            double xPositionRatio = 0,
            double yPositionRatio = 0,
            double debugBoundsWidth = 0)
        {
            RenderInRect(text, AggContext.DefaultFont, pointSize, fitRect, out renderedBounds, xPositionRatio, yPositionRatio, debugBoundsWidth);
        }

        public void RenderInRect(string text,
            TypeFace font,
            double pointSize,
            RectangleDouble fitRect,
            out RectangleDouble renderedBounds,
            double xPositionRatio = 0,
            double yPositionRatio = 0,
            double debugBoundsWidth = 0)
        {
            var styledTypeFace = new StyledTypeFace(font, pointSize * 300 / 72);
            var typeFacePrinter = new TypeFacePrinter(text, styledTypeFace);
            RenderInRect(new ColoredVertexSource[] { new ColoredVertexSource(typeFacePrinter, Color.Black) }, fitRect, out renderedBounds, xPositionRatio, yPositionRatio, debugBoundsWidth);
        }

        /// <summary>
        /// Renders the given vector source making scaled to fit the given rect. Scalling will remain proportional.
        /// If the vector source is smaller in one dimension it will be offset based on the position ratio
        /// </summary>
        /// <param name="source">The vector source to render</param>
        /// <param name="fitRect">The rect to scale to fit within</param>
        /// <param name="xPositionRatio">The ratio of the width to offset in x if not fully utilized</param>
        /// <param name="yPositionRatio">The ratio of the height to offset in y if not fully utilized</param>
        /// <param name="debugShowBounds">Render an outline of the total rectangle</param>
        public void RenderInRect(IEnumerable<ColoredVertexSource> source,
            RectangleDouble fitRect,
            out RectangleDouble renderedBounds,
            double xPositionRatio = 0,
            double yPositionRatio = 0,
            double debugBoundsWidth = 0)
        {
            renderedBounds = RectangleDouble.ZeroIntersection;

            xPositionRatio = Math.Max(0, Math.Min(1, xPositionRatio));
            yPositionRatio = Math.Max(0, Math.Min(1, yPositionRatio));

            RectangleDouble totalBounds = RectangleDouble.ZeroIntersection;
            foreach (var colorVertices in source)
            {
                var bounds = colorVertices.VertexSource.GetBounds();
                totalBounds.ExpandToInclude(bounds);
            }
            
            foreach (var colorVertices in source)
            {
                double scale;
                if (totalBounds.Width > fitRect.Width
                    || totalBounds.Height > fitRect.Height)
                {
                    // we need to scale down
                    scale = Math.Min(fitRect.Width / totalBounds.Width, fitRect.Height / totalBounds.Height);
                }
                else
                {
                    // we need to scale up
                    scale = Math.Max(fitRect.Width / totalBounds.Width, fitRect.Height / totalBounds.Height);
                }

                // zero out the offset
                var transform = Affine.NewTranslation(-totalBounds.Left, -totalBounds.Bottom);
                // scale
                transform *= Affine.NewScaling(scale);
                // offset to the fit rect
                transform *= Affine.NewTranslation(fitRect.Left, fitRect.Bottom);

                // do we need to move it to account for position ratios
                var scaledBounds = totalBounds * scale;
                transform *= Affine.NewTranslation((fitRect.Width - scaledBounds.Width) * xPositionRatio, (fitRect.Height - scaledBounds.Height) * yPositionRatio);
                var flattened = new FlattenCurves(new VertexSourceApplyTransform(colorVertices.VertexSource, transform));
                renderedBounds.ExpandToInclude(flattened.GetBounds());

                this.Render(flattened, colorVertices.Color);
            }

            if (debugBoundsWidth > 0)
            {
                this.Rectangle(fitRect, Color.Red, debugBoundsWidth);
            }
        }

        public void RenderScale(IImageByte image, double x, double y, double sizeX)
        {
            var ratio = sizeX / image.Width;
            var sizeY = image.Height * ratio;
            this.Render(image, x, y, sizeX, sizeY);
        }

        public abstract void SetClippingRect(RectangleDouble rect_d);

        public void SetTransform(Affine value)
        {
            affineTransformStack.Pop();
            affineTransformStack.Push(value);
        }
    }

    public static class ColoredVertexSourceExtensions
    {
        public static RectangleDouble GetBounds(this IEnumerable<ColoredVertexSource> source)
        {
            RectangleDouble totalBounds = RectangleDouble.ZeroIntersection;
            foreach (var colorVertices in source)
            {
                var bounds = colorVertices.VertexSource.GetBounds();
                totalBounds.ExpandToInclude(bounds);
            }

            return totalBounds;
        }
    }
}