//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026, Lars Brubaker
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
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
    /// <param name="FillEvenOdd">When true, the rasterizer uses the even-odd fill rule instead of
    /// non-zero winding. Use this for compound paths where sub-path overlaps should create
    /// transparent cutouts (e.g. a circle with a hole punched through it).</param>
    public record ColoredVertexSource(IVertexSource VertexSource, Color Color, bool FillEvenOdd = false);

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

        /// <summary>
        /// The CPU back buffer this Graphics2D rasterizes into, or null when there is none.
        /// </summary>
        /// <remarks>
        /// Virtual because a GPU surface has no CPU back buffer of its own and has to make one on demand -
        /// see <c>Graphics2DGpu.DestImage</c>, which is what lets the agg demos that rasterize by hand
        /// (aa_demo, gouraud, blur, image_resample) run on a GPU window at all.
        /// </remarks>
        public virtual IImageByte DestImage
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

        public abstract void Clear(RectangleDouble rect, IColorType color);

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
            Color color = default,
            bool drawFromHintedCach = false,
            Color backgroundColor = default,
            bool bold = false)
        {
            DrawString(text, position.X, position.Y, pointSize, justification, baseline, color, drawFromHintedCach, backgroundColor, bold);
        }

        /// <summary>
        /// Draws a string on a typeface printer object with various optional styling parameters.
        /// </summary>
        /// <param name="text">The string text to be drawn.</param>
        /// <param name="x">The x-coordinate where the string starts.</param>
        /// <param name="y">The y-coordinate where the string starts.</param>
        /// <param name="pointSize">The size of the point in pixels. Default is 12.</param>
        /// <param name="justification">Defines the justification of the string, i.e., the alignment of the text. It can be left, right, or center. Default is 'Left'.</param>
        /// <param name="baseline">Defines the baseline alignment of the text, i.e., the vertical alignment of the text. It can be 'Text', 'Ideographic', etc. Default is 'Text'.</param>
        /// <param name="color">Defines the color of the text. Default is 'Black' if not specified.</param>
        /// <param name="drawFromHintedCach">A boolean flag to indicate if the rendered string should be drawn from hinted cache. Default is 'false'.</param>
        /// <param name="backgroundColor">Defines the background color of the text. No background color is applied if not specified.</param>
        /// <param name="bold">A boolean flag to indicate if the text should be bold. Default is 'false'.</param>
        /// <returns>Returns a TypeFacePrinter object that holds the rendered string and drawing settings.</returns>
        /// <example>
        /// TypeFacePrinter printer = DrawString("Hello World", 50, 50, 14, Justification.Center, Baseline.Text, Color.Red, true, Color.White, true);
        /// </example>
        /// <remarks>
        /// If the 'color' parameter's alpha value is zero, the function will interpret it as the color black.
        /// If the 'backgroundColor' parameter's alpha value is not zero, a rectangle of that color will be drawn as a background behind the string.
        /// </remarks>
        public TypeFacePrinter DrawString(string text,
            double x,
            double y,
            double pointSize = 12,
            Justification justification = Justification.Left,
            Baseline baseline = Baseline.Text,
            Color color = default,
            bool drawFromHintedCach = false,
            Color backgroundColor = default,
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

            return stringPrinter;
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
        
        public IVertexSource GetLine(double x1, double y1, double x2, double y2, double strokeWidth = -1)
        {
            if (strokeWidth == -1)
            {
                strokeWidth = 1 * DeviceScale;
            }

            var lineToDraw = new VertexStorage();
            lineToDraw.Clear();
            lineToDraw.MoveTo(x1, y1);
            lineToDraw.LineTo(x2, y2);

            return new Stroke(lineToDraw, strokeWidth);
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
            this.Render(GetLine(x1, y1, x2, y2, strokeWidth), color);
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

        /// <summary>
        /// Fills <paramref name="vertexSource"/> with <paramref name="colorType"/> under the current
        /// transform - the ordinary vector chokepoint every fill in agg-sharp goes through.
        /// </summary>
        /// <remarks>
        /// It is also where the LCD subpixel path is offered, to sources that can name their own geometry
        /// (<see cref="IVertexSourceRenderIdentity"/>) and only when every gate allows it - see
        /// <see cref="TryRenderThroughLcd"/>. A source that says nothing, or any refused gate, reaches
        /// <see cref="RenderVertexSource"/> and the bytes it always produced.
        /// </remarks>
        public void Render(IVertexSource vertexSource, IColorType colorType)
        {
            if (!TryRenderThroughLcd(vertexSource, colorType))
            {
                RenderVertexSource(vertexSource, colorType);
            }
        }

        /// <summary>
        /// The destination's own scanline fill, as it was before the LCD path existed. Reached from
        /// <see cref="Render"/> for everything the LCD path does not take.
        /// </summary>
        protected abstract void RenderVertexSource(IVertexSource vertexSource, IColorType colorType);

        /// <summary>
        /// Whether this destination can composite per-channel LCD coverage. False means
        /// <see cref="RenderLcd"/> silently falls back to <see cref="Render"/> on this Graphics2D, so a
        /// caller never has to know which backend it is talking to.
        /// </summary>
        /// <remarks>
        /// Mirrors the reference's <c>DrawCtx::has_lcd_mask_composite</c> (<c>draw_ctx.rs:424</c>), which
        /// defaults false for the same reason: a backend that cannot write three independent channel
        /// coverages must receive ordinary anti-aliased fills rather than a mask it would have to flatten.
        /// </remarks>
        public virtual bool CanCompositeLcd => false;

        /// <summary>
        /// Set by whoever knows this destination is an offscreen surface that will be blended onto something
        /// else later - a widget's transparent backbuffer, a compositing layer - rather than the final opaque
        /// surface. It turns subpixel chroma off (see <see cref="LcdChromaAllowed"/>) and stops this
        /// destination accepting a per-channel <see cref="LcdBuffer"/> (see
        /// <see cref="CanCompositeLcdBuffer"/>).
        /// </summary>
        /// <remarks>
        /// A property rather than an override because the same <see cref="Graphics2D"/> class draws both
        /// kinds of surface: <see cref="Image.ImageGraphics2D"/> is the final window surface in one call and a
        /// widget's transparent backbuffer in the next, and only the caller that allocated the buffer knows
        /// which. It is the plumbing half of the reference's validity gate (<c>text_render.rs:56-62</c>),
        /// whose Rust equivalent is knowing whether the active target is a layer.
        /// </remarks>
        public bool IsTransparentCompositingLayer { get; set; }

        /// <summary>
        /// Whether subpixel chroma is valid on this destination. True (the default) takes the LCD filter;
        /// false takes the chroma-free collapse (<see cref="LcdMaskBuilder.FinalizeGray"/>) - the same raster,
        /// the same layout and the same composite, with r == g == b coverage everywhere.
        /// </summary>
        /// <remarks>
        /// The reference's validity gate (<c>text_render.rs:56-62</c>): LCD geometry is only meaningful
        /// against the final opaque surface, because a transparent compositing layer's pixels get blended
        /// again later, against content the R/G/B phase knew nothing about. Separate from
        /// <see cref="CanCompositeLcd"/> - <b>can</b> this destination take a per-channel mask at all, versus
        /// <b>should</b> that mask carry chroma - and only consulted when that one is true.
        /// <para>
        /// The default reads <see cref="IsTransparentCompositingLayer"/>, but the only destination that ever
        /// reaches this check with that flag set is <see cref="LcdCoverage.LcdBufferGraphics2D"/>, whose two
        /// planes can carry gray coverage perfectly well. A transparent <see cref="Image.ImageGraphics2D"/>
        /// never gets here at all: it answers <see cref="CanCompositeLcd"/> false and takes the ordinary
        /// anti-aliased fill instead (see that override for why a single-alpha layer needs a composite that
        /// writes alpha, which the gray mask still would not do).
        /// </para>
        /// </remarks>
        protected virtual bool LcdChromaAllowed => !this.IsTransparentCompositingLayer;

        /// <summary>
        /// The fill rule an LCD mask must be rasterized under to cover the same pixels this destination's own
        /// fill would cover, or null when this destination cannot say - which refuses the LCD path outright.
        /// </summary>
        /// <remarks>
        /// The mask is rasterized here, by <see cref="LcdMaskBuilder"/>, rather than by whatever the
        /// destination fills with, so the rule has to be asked for rather than inherited. The default reads
        /// the scanline rasterizer, which is where a caller's <c>filling_rule</c> lands (see
        /// <see cref="RenderInRect"/>) - and is null when there is no rasterizer at all, because a mask built
        /// under a guessed rule would paint a different shape than <see cref="RenderVertexSource"/> would have.
        /// <para>
        /// A destination that fills by other means overrides it with the rule its own filler uses -
        /// <c>Graphics2DGpu</c> tessellates and has no rasterizer to read. That is the whole reason this is a
        /// property and not a field read: before it existed, the missing rasterizer silently kept every GL
        /// destination off the LCD path no matter what the user had turned on.
        /// </para>
        /// </remarks>
        protected virtual Util.filling_rule_e? LcdFillingRule => this.rasterizer?.FillingRule;

        /// <summary>
        /// Whether this destination can take a whole two-plane <see cref="LcdBuffer"/> without collapsing it -
        /// the buffer-level twin of <see cref="CanCompositeLcd"/>, and the gate a widget consults before
        /// choosing an LCD-coverage backbuffer at all.
        /// </summary>
        /// <remarks>
        /// False by default, so a backend that has not been taught the per-channel composite keeps the
        /// behaviour it always had rather than silently receiving a collapsed blit. The destinations that
        /// override it to true are the ones with a real per-channel composite to offer:
        /// <see cref="Image.ImageGraphics2D"/> through the software <see cref="LcdBuffer.CompositeOnto"/>,
        /// <c>Graphics2DGpu</c> through three color-masked GL passes, and
        /// <see cref="LcdCoverage.LcdBufferGraphics2D"/> - the nested case - through
        /// <see cref="LcdBuffer.CompositeBuffer"/>, which needs no collapse because its destination is already
        /// two planes.
        /// </remarks>
        public virtual bool CanCompositeLcdBuffer => false;

        /// <summary>
        /// Paints a finished LCD-coverage buffer onto this destination with its bottom-left pixel at
        /// (<paramref name="destX"/>, <paramref name="destY"/>) - whole destination pixels, and
        /// <b>not</b> transformed by the current transform.
        /// </summary>
        /// <param name="buffer">The source planes. Pixels with no coverage in any channel leave the
        /// destination untouched.</param>
        /// <param name="destX">Destination x of the buffer's left column.</param>
        /// <param name="destY">Destination y of the buffer's bottom row; both are Y-up, so there is no flip.</param>
        /// <remarks>
        /// The default collapses to a single alpha (<see cref="LcdBuffer.ToImageBufferCollapsed"/>, Rec.709
        /// weighted) and blits that through the ordinary image path, which is lossy of chroma wherever the
        /// three channel alphas diverge but preserves luminance. That is the reference's arrangement exactly:
        /// <c>DrawCtx::draw_lcd_backbuffer_arc</c> (<c>draw_ctx.rs:553-574</c>) has a live collapsing body and
        /// the backends that can do better override it - here <see cref="Image.ImageGraphics2D"/>, with the
        /// per-channel <see cref="LcdBuffer.CompositeOnto"/>.
        /// <para>
        /// The placement is integer and untransformed because the planes are finished pixels: resampling them
        /// would smear each channel's phase into its neighbours and destroy the subpixel geometry. The
        /// transform is neutralized rather than merely ignored so the collapsed blit lands where the caller
        /// asked whatever transform happened to be current.
        /// </para>
        /// </remarks>
        public virtual void CompositeLcdBuffer(LcdBuffer buffer, int destX, int destY)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            PushTransform();
            try
            {
                SetTransform(Affine.NewIdentity());
                Render(buffer.ToImageBufferCollapsed(), destX, destY);
            }
            finally
            {
                PopTransform();
            }
        }

        /// <summary>
        /// Fills <paramref name="vertexSource"/> through the LCD subpixel pipeline when every gate allows
        /// it, and through the ordinary <see cref="Render"/> path when any of them does not.
        /// </summary>
        /// <param name="vertexSource">Any vertex source; the current transform is applied, as
        /// <see cref="Render"/> does.</param>
        /// <param name="colorType">The fill color. Applied at composite time, per channel - the mask itself
        /// carries no color.</param>
        /// <param name="pathCacheKey">Optional caller-supplied identity of the geometry, letting the mask be
        /// reused across draws; null (the default) rasterizes every call. See <see cref="LcdMaskKey"/> for
        /// what makes a valid identity - notably, not a mutable path object.</param>
        /// <remarks>
        /// This is the general vector chokepoint the LCD design is built on, not a text feature: rect fills,
        /// strokes, icons and glyph runs all reach the same pipeline through here, and text is simply the
        /// caller that benefits most. It mirrors the reference's <c>draw_lcd_mask</c> hook and the gates its
        /// callers apply before reaching for it.
        /// <para>
        /// The gates, in the order this method checks them - all three have to pass, so the order is about
        /// cost and nothing else:
        /// <list type="number">
        /// <item><description>destination validity (<see cref="CanCompositeLcd"/>, plus a fill rule to
        /// rasterize the mask under - <see cref="LcdFillingRule"/>), which stays local to this
        /// object;</description></item>
        /// <item><description>the effective-scale cap, which overrides the user toggle
        /// (<see cref="LcdRenderSettings.EffectiveScaleAllowsLcd"/>);</description></item>
        /// <item><description>the toggle itself (<see cref="LcdRenderSettings.Enabled"/>), a lock-free
        /// volatile read.</description></item>
        /// </list>
        /// The last two are in the reference's order (the cap is evaluated first precisely because it
        /// overrides the toggle rather than being overridden by it). Any of them failing takes
        /// <see cref="Render"/> - which cannot come back here, since it offers the cached LCD path behind
        /// these same gates and they have just refused - and is a real fill, not a degraded one, just without
        /// subpixel chroma.
        /// That fallback is itself a deliberate divergence: the reference's text path always goes through a
        /// mask, taking <see cref="LcdMaskBuilder.FinalizeGray"/> when chroma is invalid, but agg-sharp's
        /// disabled path has to stay byte-identical to the pre-LCD renderer, so a refused fill goes back to
        /// the scanline renderer rather than to a gray mask.
        /// </para>
        /// <para>
        /// <b>Chroma, separately from LCD-or-not.</b> A destination that can composite a mask but must not
        /// carry subpixel chroma - a transparent compositing layer, where the R/G/B phase would be blended
        /// against unknown pixels later - reports <see cref="LcdChromaAllowed"/> false and gets the
        /// chroma-free collapse through this same bounded-mask, cache and composite path, which is
        /// <c>text_render.rs:56-62</c>'s structure.
        /// </para>
        /// <para>
        /// <b>The clip is whole-pixel granular here.</b> A mask has no unit finer than a pixel to enforce a
        /// clip in: <see cref="BoundedMaskBuilder"/> rounds its clip outward - floor on left and bottom, ceil
        /// on right and top, as the reference's <c>rect_to_pixel_clip</c> does - and the rect it gets from
        /// <see cref="GetClippingRect"/> has already lost its fraction, because the rasterizer reports its
        /// 24.8 clip box back through an integer divide. The two roundings compose to a floor on every edge,
        /// so a clip edge falling inside a pixel snaps: at the left and bottom edge that pixel is painted in
        /// full (up to a whole pixel of ink the caller did not ask for), at the right and top edge it is
        /// dropped in full (up to a whole pixel of ink the caller did ask for). <see cref="Render"/> clips
        /// the same geometry to 1/256 of a pixel and anti-aliases that pixel instead. The granularity is the
        /// reference's and is deliberate, not a rounding bug (which way each edge snaps is agg-sharp's, from
        /// that integer divide); both are pinned by test. It does mean a caller that needs an exact fractional
        /// clip has to snap its clip to whole pixels itself or stay off this path.
        /// </para>
        /// <para>
        /// The composite origin is whole pixels because <see cref="BoundedMaskBuilder"/> reports whole
        /// pixels, which discharges the rounding obligation the reference meets with an explicit
        /// <c>sx.round()</c> in <c>draw_lcd_mask</c> (<c>gfx_ctx\draw_impl.rs:605</c>). It is unconditional:
        /// sub-pixel placement of a finished mask smears each channel's phase into its neighbours and
        /// destroys the subpixel geometry, independent of any baseline-snapping setting.
        /// </para>
        /// <para>
        /// <b>Known limitation.</b> The clip is read through <see cref="GetClippingRect"/>, which for image
        /// destinations reports the rasterizer's vector clip box - and that box reads as empty when no clip
        /// was ever set, because the rasterizer keeps "clipping is off" in a flag it does not expose. Every
        /// destination built by <see cref="Image.ImageBuffer.NewGraphics2D"/> sets the box to the buffer
        /// bounds, so this only bites a hand-constructed <see cref="ImageGraphics2D"/> that never called
        /// <see cref="SetClippingRect"/>: it paints nothing here, where <see cref="Render"/> would paint
        /// unclipped. Distinguishing the two needs the rasterizer to report whether clipping is active.
        /// </para>
        /// </remarks>
        public void RenderLcd(IVertexSource vertexSource, IColorType colorType, object pathCacheKey = null)
        {
            if (vertexSource == null)
            {
                throw new ArgumentNullException(nameof(vertexSource));
            }

            Affine transform = GetTransform();
            if (!this.CanCompositeLcd
                || !(this.LcdFillingRule is Util.filling_rule_e fillingRule)
                || !LcdRenderSettings.IsEnabledAtScale(LcdRenderSettings.EffectiveScaleOf(transform)))
            {
                Render(vertexSource, colorType);
                return;
            }

            // The clip comes from the destination the same way every other fill's does: GetClippingRect is
            // the rasterizer's vector clip box for image destinations, which is what SetClippingRect (and
            // through it GuiWidget's clipping) writes. BoundedMaskBuilder trims the mask to it, rounded out
            // to whole pixels - see the remarks on the granularity that costs.
            RectangleDouble clip = GetClippingRect();

            if (!LcdMaskCache.TryGetBoundedMask(
                pathCacheKey,
                this.Width,
                this.Height,
                vertexSource,
                transform,
                out LcdMask mask,
                out int originX,
                out int originY,
                clip,
                fillingRule,
                LcdRenderSettings.PrimaryWeight,
                LcdRenderSettings.Gamma,
                !this.LcdChromaAllowed))
            {
                // Nothing to paint: off the destination, entirely clipped away, or an empty path. The
                // ordinary path would have painted nothing too, so there is nothing to fall back to.
                return;
            }

            CompositeLcdMask(mask, colorType.ToColor(), originX, originY);
        }

        /// <summary>
        /// Largest translation, in device pixels, that the cached LCD path will split into a whole-pixel
        /// origin and a phase. Past it the whole half stops fitting an <see cref="int"/> comfortably, and a
        /// fill that far off screen is not worth the arithmetic to find out.
        /// </summary>
        private const double MaxLcdPlacementInPixels = 1e7;

        /// <summary>
        /// Paints <paramref name="vertexSource"/> through the <b>cached</b> LCD subpixel pipeline, or returns
        /// false to say it did not - in which case <see cref="Render"/> paints it the ordinary way, byte for
        /// byte as it did before this path existed.
        /// </summary>
        /// <remarks>
        /// This is the generic half of the LCD design, and the reason nothing above it has to know about LCD
        /// at all: a source that can name its own geometry (<see cref="IVertexSourceRenderIdentity"/>) gets
        /// its raster cached, whether it is a glyph run, an icon or anything else. The reference reaches the
        /// same place from the other direction, by having its text renderer call a cached mask builder
        /// (<c>rasterize_text_mask_cached</c>, <c>mask.rs:119-246</c>); putting the decision in the fill
        /// keeps the text side a plain caller of <see cref="Render"/>.
        /// <para>
        /// <b>Cost when the feature is off.</b> The first thing checked is whether the source names itself,
        /// which is a type test that fails for every rect, stroke and icon in the library. Nothing is
        /// allocated and no lock is taken at any point on the refusing path - the toggle is a volatile field
        /// read (<see cref="LcdRenderSettings.Enabled"/>) - and the identity itself is not even asked for
        /// until the gates have passed.
        /// </para>
        /// <para>
        /// <b>The placement split.</b> A cached mask must not know where it was drawn, so the device
        /// placement is separated into two halves that add back up to it exactly: the whole part of the
        /// transform's translation becomes the composite origin, and its fraction stays in the transform the
        /// mask is rasterized with, so the fill keeps its true sub-pixel position. Two draws a whole number
        /// of pixels apart therefore share one mask and differ only in where it lands. The reference goes
        /// further and rounds the fraction away too (<c>sx.round()</c>, <c>draw_impl.rs:605</c>); keeping it
        /// costs a cache entry per distinct fraction and preserves the horizontal sub-pixel positioning
        /// agg-sharp's text has always had.
        /// </para>
        /// <para>
        /// <b>The linear part is not split off</b> - scale and rotation go into the mask transform, so the
        /// geometry is rasterized at its physical size and shape. That is the reference's HiDPI rule
        /// (<c>logical x ctm_scale</c>, <c>gfx_ctx.rs:635-639</c>) generalized, and a strict improvement on
        /// it for rotation, which its axis-aligned mask blit does not handle at all.
        /// </para>
        /// <para>
        /// <b>Floating point.</b> Splitting the translation makes the device position
        /// <c>(v + t - floor(t)) + floor(t)</c> where the ordinary path computes <c>v + t</c>. The two are
        /// equal in exact arithmetic and can differ by one ulp in doubles, which is enough to move a coverage
        /// value by one byte where an edge lands exactly on a 1/256 rasterizer boundary. It is deterministic
        /// - the same fill always splits the same way, so a cached mask is never wrong about itself - and it
        /// is why LCD text is compared against the LCD vector path rather than against the ordinary one.
        /// </para>
        /// <para>
        /// Same gates and same integer-origin composite as <see cref="RenderLcd"/>; the difference is
        /// entirely in what gets cached. <see cref="RenderLcd"/> trims its mask to the destination and the
        /// clip, which bakes the fill's position into the coverage bytes; this one uses the untrimmed build
        /// (<see cref="LcdMaskCache.GetUnclippedMask"/>) so the bytes depend only on the geometry and its
        /// phase, and applies the clip at composite time instead. That is the difference between one cache
        /// entry per shape and one per shape per position - and it is why an untrimmed mask has a size bound
        /// (<see cref="BoundedMaskBuilder.MaxUnclippedMaskExtentInPixels"/>) that sends anything absurd back
        /// to the ordinary path.
        /// </para>
        /// <para>
        /// <b>Known limitation, inherited from <see cref="RenderLcd"/>.</b> The clip comes from
        /// <see cref="GetClippingRect"/>, which for image destinations reports the rasterizer's vector clip
        /// box - and that box reads as empty when nobody ever called <c>SetVectorClipBox</c> on it, because
        /// the rasterizer keeps "clipping is off" in a flag it does not expose. Such a destination
        /// would paint nothing here where <see cref="RenderVertexSource"/> would paint unclipped. Nothing
        /// in-tree can reach it: every <see cref="ImageGraphics2D"/> comes from
        /// <see cref="Image.ImageBuffer.NewGraphics2D"/>, which sets the box to the buffer bounds. It is
        /// called out here because this is the universal fill chokepoint - a hand-built
        /// <see cref="Graphics2D"/> that skips the clip box now silently loses identified fills rather than
        /// only the explicit <see cref="RenderLcd"/> calls it never made. Since <c>GuiWidget.DrawChild</c>
        /// intersects every child's clip with the clip already in force on the surface, the cost is worse
        /// than lost fills: an empty surface clip makes every child intersect to nothing, so a whole widget
        /// tree painted onto such a surface skips every child and renders nothing at all.
        /// </para>
        /// </remarks>
        private bool TryRenderThroughLcd(IVertexSource vertexSource, IColorType colorType)
        {
            if (!TryUnwrapIdentifiableSource(vertexSource, out IVertexSourceRenderIdentity identifiedSource, out Affine transform))
            {
                return false;
            }

            if (!this.CanCompositeLcd
                || !(this.LcdFillingRule is Util.filling_rule_e fillingRule)
                || !LcdRenderSettings.IsEnabledAtScale(LcdRenderSettings.EffectiveScaleOf(transform)))
            {
                return false;
            }

            object identity = identifiedSource.RenderIdentity;
            if (identity == null)
            {
                return false;
            }

            // Written as negated comparisons so a NaN placement takes the ordinary path rather than the
            // floor below.
            if (!(Math.Abs(transform.tx) < MaxLcdPlacementInPixels)
                || !(Math.Abs(transform.ty) < MaxLcdPlacementInPixels))
            {
                return false;
            }

            int compositeOffsetX = (int)Math.Floor(transform.tx);
            int compositeOffsetY = (int)Math.Floor(transform.ty);

            // tx and ty are the transform's final translation, so replacing them with their own fraction is
            // exactly "the same transform, with the whole-pixel placement taken out".
            Affine maskTransform = transform;
            maskTransform.tx = NormalizeZeroPhase(transform.tx - compositeOffsetX);
            maskTransform.ty = NormalizeZeroPhase(transform.ty - compositeOffsetY);

            switch (LcdMaskCache.GetUnclippedMask(
                identity,
                identifiedSource,
                maskTransform,
                out LcdMask mask,
                out int originX,
                out int originY,
                fillingRule,
                LcdRenderSettings.PrimaryWeight,
                LcdRenderSettings.Gamma,
                !this.LcdChromaAllowed))
            {
                case UnclippedMaskResult.Built:
                    // The clip is enforced here rather than by trimming the mask, because a trimmed mask
                    // could not be shared across positions - see LcdMaskCache.GetUnclippedMask.
                    CompositeLcdMask(mask, colorType.ToColor(), originX + compositeOffsetX, originY + compositeOffsetY, GetClippingRect());
                    return true;

                case UnclippedMaskResult.Empty:
                    // Nothing to paint. The LCD path handled it - by painting nothing, which is what the
                    // ordinary path would have done with the same geometry.
                    return true;

                default:
                    // Too large to rasterize into a mask of its own. Still a real fill, so the ordinary path
                    // has to paint it.
                    return false;
            }
        }

        /// <summary>
        /// Collapses a sub-pixel phase of -0.0 onto +0.0, leaving every other value exactly as it is.
        /// </summary>
        /// <remarks>
        /// The two are the same placement, but <see cref="LcdMaskKey"/> compares its doubles by bit pattern,
        /// so an unnormalized -0.0 files a second entry holding bytes identical to the first one's. It takes a
        /// transform whose translation is already -0.0 to get here - subtracting the whole part from any other
        /// value yields +0.0 - and the affine multiply in <see cref="TryUnwrapIdentifiableSource"/> turns most
        /// of those into +0.0 on the way past, so this is a guard rather than a fix for an observed duplicate.
        /// It is one comparison on a path that is about to rasterize, which is a fair price for not having to
        /// reason about which mirrored or sheared transform survives that multiply with its sign intact.
        /// <para>
        /// Written as a comparison rather than <c>+ 0.0</c> because that trick is only a no-op for every value
        /// <i>other</i> than -0.0, and relying on a compiler not to fold away an addition it is entitled to
        /// consider redundant is the kind of thing that stops being true silently.
        /// </para>
        /// </remarks>
        private static double NormalizeZeroPhase(double phase)
        {
            // -0.0 == 0 is true, so this catches both zeros and hands back the positive one.
            return phase == 0 ? 0.0 : phase;
        }

        /// <summary>
        /// Finds the source that names its own geometry inside <paramref name="vertexSource"/> and the full
        /// path-space-to-device transform that applies to it, or answers false when there is no such source.
        /// </summary>
        /// <param name="vertexSource">The source handed to <see cref="Render"/>, possibly wrapped.</param>
        /// <param name="identifiedSource">The source that names itself.</param>
        /// <param name="transform">Everything between that source's own vertices and device pixels: the
        /// wrappers' transforms followed by the current transform.</param>
        /// <remarks>
        /// <b>How identity and wrappers compose.</b> A <see cref="VertexSourceApplyTransform"/> holding an
        /// <see cref="Affine"/> contributes placement, not shape - it moves vertices without changing which
        /// vertices they are - so it joins the transform and leaves the identity underneath it intact. That
        /// is what makes the split work for text: <see cref="Font.TypeFacePrinter"/> hands
        /// <see cref="Render"/> either itself or itself wrapped in the whole-device-pixel baseline nudge, and
        /// the two are the same run at two placements rather than two runs.
        /// <para>
        /// Every other proxy - a <see cref="Stroke"/>, a curve flattener - produces different vertices than
        /// the source it wraps, and cannot claim that source's identity. The walk stops at the first one, and
        /// the fill is rendered the ordinary way. So does a non-affine <see cref="ITransform"/>, whose effect
        /// is not a matrix that can be folded into the current one.
        /// </para>
        /// </remarks>
        private bool TryUnwrapIdentifiableSource(IVertexSource vertexSource, out IVertexSourceRenderIdentity identifiedSource, out Affine transform)
        {
            identifiedSource = null;
            transform = default;

            IVertexSource source = vertexSource;
            Affine wrappers = Affine.NewIdentity();
            while (source is VertexSourceApplyTransform applyTransform
                && applyTransform.TransformToApply is Affine affine
                && applyTransform.VertexSource != null)
            {
                // agg-sharp's operator * is a post-multiply ("a then b"), and a wrapper found further in is
                // applied before every wrapper already collected outside it.
                wrappers = affine * wrappers;
                source = applyTransform.VertexSource;
            }

            if (!(source is IVertexSourceRenderIdentity identifiable))
            {
                return false;
            }

            identifiedSource = identifiable;
            transform = wrappers * GetTransform();
            return true;
        }

        /// <summary>
        /// Composites a finished coverage mask onto this destination, applying <paramref name="color"/> per
        /// channel, with the mask's bottom-left pixel at (<paramref name="originX"/>,
        /// <paramref name="originY"/>) - both whole pixels, always.
        /// </summary>
        /// <remarks>
        /// The destination-specific half of <see cref="RenderLcd"/>, mirroring the reference's
        /// <c>DrawCtx::draw_lcd_mask</c>. Only reached when <see cref="CanCompositeLcd"/> is true, hence the
        /// throwing default rather than the reference's silent no-op: in C# an override pair that disagrees
        /// with itself is a bug worth hearing about, where in Rust the no-op default exists to let callers
        /// that skip the capability check degrade instead of failing to compile.
        /// <para>
        /// <paramref name="mask"/> may be shared with the mask cache and must be treated as read-only.
        /// </para>
        /// <para>
        /// <paramref name="clip"/> is null when the mask was already trimmed to the clip as it was built
        /// (<see cref="RenderLcd"/>) and carries the clip rect when it was not
        /// (<see cref="TryRenderThroughLcd"/>, whose mask is cached and therefore cannot be trimmed).
        /// </para>
        /// </remarks>
        protected virtual void CompositeLcdMask(LcdMask mask, Color color, int originX, int originY, RectangleDouble? clip = null)
        {
            throw new NotSupportedException(
                $"{this.GetType().Name} reports CanCompositeLcd but does not implement CompositeLcdMask.");
        }

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

        /// <summary>
        /// Draws a run of individually coloured vertices in this surface's own coordinate space.
        /// </summary>
        /// <remarks>
        /// The 2D escape hatch for the handful of widgets that need a gradient across a primitive - a
        /// hue ring, a saturation/value triangle - which no vertex source plus single colour can express.
        /// Before this existed those widgets downcast to the GPU <c>Graphics2D</c> and emitted raw
        /// immediate mode, which is exactly the coupling the wgpu port is removing.
        /// <para>
        /// The base implementation draws nothing. That is not an oversight: it preserves what those
        /// widgets already do on a non-GPU surface (they tested for the GPU type and skipped otherwise),
        /// and a software fallback for per-vertex-interpolated primitives is real work nobody has asked
        /// for. Surfaces that can do it override this.
        /// </para>
        /// </remarks>
        /// <param name="topology">How the vertices assemble into primitives.</param>
        /// <param name="vertices">The vertices, in surface coordinates.</param>
        public virtual void DrawColoredPrimitives(DrawTopology topology, ReadOnlySpan<PosColorVertex> vertices)
        {
        }

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
                    scale = Math.Min(fitRect.Width / totalBounds.Width, fitRect.Height / totalBounds.Height);
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

                if (colorVertices.FillEvenOdd)
                    this.Rasterizer.filling_rule(Util.filling_rule_e.fill_even_odd);
                this.Render(flattened, colorVertices.Color);
                if (colorVertices.FillEvenOdd)
                    this.Rasterizer.filling_rule(Util.filling_rule_e.fill_non_zero);
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