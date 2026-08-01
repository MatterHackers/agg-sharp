/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.RenderGl.OpenGl;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The LCD coverage arm of the GL destination: compositing a two plane <see cref="LcdBuffer"/> onto the
	/// framebuffer through three color masked passes, and the per-channel pass images those passes sample.
	/// </summary>
	public partial class Graphics2DGpu
	{
        // The three per-channel pass images an LcdBuffer composites through. Like aATextureImages these are
        // cpu side ImageBuffers with no gl affinity, so one set is shared by every context and the per
        // context part is left to ImageTexturePlugin, which already keys its textures by (pixel buffer,
        // context) and re-uploads on InvalidateGlCaches through MarkAllImagesNeedRefresh.
        // Weak on the buffer so a widget's planes take their pass images with them when the widget goes.
        private static readonly ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages> lcdChannelImages = new ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages>();
        private static readonly object lcdChannelImagesLock = new object();

        // The same arrangement for a single mask's three pass images. Weak on the mask because a mask lives in
        // LcdMaskCache, which is LRU bounded - an evicted mask has to be able to take its textures with it, and
        // a strong table here would pin every glyph run the process ever drew.
        // No change stamp, where the buffer table needs one: a mask is finished when it is built and is handed
        // out read only (see Graphics2D.CompositeLcdMask), so one pack per mask is all there ever is.
        private static readonly ConditionalWeakTable<LcdMask, ImageBuffer[]> lcdMaskChannelImages = new ConditionalWeakTable<LcdMask, ImageBuffer[]>();
        private static readonly object lcdMaskChannelImagesLock = new object();

        /// <summary>
        /// True: this destination composites a two plane <see cref="LcdBuffer"/> per channel, through three
        /// color masked passes (see <see cref="CompositeLcdBuffer"/>). This is the gate
        /// <c>GuiWidget.ResolveBackbufferMode</c> consults, so turning it on is what lets a GPU rendered
        /// widget choose an LCD coverage backbuffer at all.
        /// </summary>
        /// <remarks>
        /// False without a context - <c>D3D11SystemWindow</c> builds a Graphics2DGpu before the device
        /// exists and again after teardown, and such an instance cannot draw anything - and false on a
        /// transparent compositing layer, where the base class's rule applies: subpixel geometry computed
        /// against pixels that get blended again later is geometry against unknown content.
        /// </remarks>
        public override bool CanCompositeLcdBuffer => this.gl != null && !this.IsTransparentCompositingLayer;

        /// <summary>
        /// True: this destination composites a single <see cref="LcdMask"/> per channel, through the same
        /// three color masked passes (see <see cref="CompositeLcdMask"/>). This is the gate every ordinary
        /// vector fill consults (<c>Graphics2D.TryRenderThroughLcd</c>), so turning it on is what makes the
        /// user's LCD setting visible in a GPU rendered window at all - text included, since text is only ever
        /// a caller of the vector path.
        /// </summary>
        /// <remarks>
        /// The same two refusals as <see cref="CanCompositeLcdBuffer"/> directly above, for the same reasons:
        /// no context means nothing can be drawn, and a transparent compositing layer's pixels get blended
        /// again later against content the subpixel phase knew nothing about.
        /// <para>
        /// And a third: a surface of no size. The two argument constructor leaves
        /// <see cref="Graphics2DGpu.Width"/> and <see cref="Graphics2DGpu.Height"/> at zero, and it is used in
        /// production - such an instance would push a degenerate <c>glOrtho(0, 0, 0, 0)</c> and enforce a clip
        /// nothing has set. Nothing routes a fill to one of those today, so this is a latent case made
        /// explicit rather than a bug being fixed.
        /// </para>
        /// </remarks>
        public override bool CanCompositeLcd => this.gl != null
            && this.Width > 0
            && this.Height > 0
            && !this.IsTransparentCompositingLayer;

        /// <summary>
        /// Non-zero, always - the rule this class's own fills use.
        /// </summary>
        /// <remarks>
        /// The base class reads the fill rule off its <see cref="ScanlineRasterizer"/>, and this class has
        /// none: it fills by tessellation (<c>VertexSourceToTesselator</c>), whose
        /// <see cref="Tesselate.Tesselator.WindingRule"/> is left at its <c>NonZero</c> default everywhere in
        /// the render path - nothing in RenderGl ever sets it. So the mask is rasterized under exactly the rule
        /// the tesselated fill it replaces would have used, which is the property that matters: the LCD path
        /// must cover the pixels the ordinary path covered, only with per-channel coverage.
        /// </remarks>
        protected override filling_rule_e? LcdFillingRule => filling_rule_e.fill_non_zero;

        /// <summary>
        /// Composites a finished LCD coverage backbuffer onto the framebuffer at whole pixel
        /// (<paramref name="destX"/>, <paramref name="destY"/>), preserving per-channel coverage.
        /// </summary>
        /// <remarks>
        /// <b>The mechanism.</b> Per channel source alpha needs three different alphas for one fragment,
        /// which is dual-source blending's job - not portable, and not expressible in fixed function GL at
        /// all. The reference's answer is to draw the same quad three times, each pass writing only one color
        /// channel and taking that channel's coverage as the source alpha
        /// (<c>demo-wgpu\src\pipelines.rs</c> <c>lcb_r</c> / <c>lcb_g</c> / <c>lcb_b</c>, blend
        /// One / OneMinusSrcAlpha because the color plane is premultiplied). Ported here as
        /// <see cref="OpenGl.GL.ColorMask"/> per pass over the standard premultiplied blend this class
        /// already uses.
        /// <para>
        /// Where the reference selects the channel in a fragment shader from a uniform, this selects it in
        /// the pixels: <see cref="LcdBufferChannelImages"/> pre-reduces the planes to one ordinary
        /// premultiplied BGRA image per pass, holding that channel's color in its own slot and that channel's
        /// coverage in alpha. The texture then <i>is</i> the shader's output, so the whole composite runs on
        /// fixed function texturing with no shader support required - which matters, because this is the
        /// legacy immediate mode GL path and has none.
        /// </para>
        /// <para>
        /// <b>Destination alpha is never written</b>, by all three passes leaving the alpha write mask off.
        /// That is the reference's behaviour exactly (its <c>ColorWrites::RED</c> and friends exclude alpha),
        /// and a deliberate divergence from the software
        /// <see cref="LcdBuffer.CompositeOnto(ImageBuffer, int, int, double, RectangleInt?)"/>, which sets
        /// destination alpha to <c>max</c> over the three channel alphas. There is no third pixel format to
        /// write it in here: this runs against the window's framebuffer, whose alpha is not read by anything
        /// downstream, and a fourth pass to maintain it would cost a full quad for a channel nobody samples.
        /// </para>
        /// <para>
        /// No transform is applied, matching the base class - the planes are finished pixels and resampling
        /// them would smear each channel's phase into its neighbours. Clipping still applies: the caller's
        /// clip rect is already live as the GL scissor (see <see cref="SetClippingRect"/>).
        /// </para>
        /// </remarks>
        public override void CompositeLcdBuffer(LcdBuffer buffer, int destX, int destY)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!this.CanCompositeLcdBuffer)
            {
                // Nothing to composite through; take the base class's collapse so a caller that reached here
                // anyway still gets its pixels.
                base.CompositeLcdBuffer(buffer, destX, destY);
                return;
            }

            if (buffer.Width <= 0 || buffer.Height <= 0)
            {
                return;
            }

            var channelImages = GetLcdChannelImages(buffer);

            PushOrthoProjection();
            gl.Disable(EnableCap.Lighting);
            gl.Enable(EnableCap.Texture2D);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);

            // Premultiplied source over, because the color plane is premultiplied per channel:
            // dst_c = src_c + dst_c * (1 - src_alpha_c), with src_alpha_c coming from the pass image's alpha.
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            gl.Translate(destX, destY, 0);

            // White under the default modulate texture environment, so each pass emits its texel unchanged.
            gl.Color4(Color.White);

            try
            {
                for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
                {
                    gl.ColorMask(channel == 0, channel == 1, channel == 2, false);
                    ImageTexturePlugin.GetImageTexturePlugin(gl, channelImages[channel], false).DrawToGL();
                }
            }
            finally
            {
                // Restored rather than left set, and restored on the way out of a throw as well: every other
                // draw on this context expects to be able to write all four channels, and a mask stuck at one
                // channel would silently turn the rest of the frame - every frame - monochrome. A pass can
                // throw (the texture uploader allocates, and the D3D backing of gl raises on a lost device),
                // and D3D11SystemWindow.OnPaint swallows that, so without the finally one bad frame would
                // leave the context permanently miscolored.
                gl.ColorMask(true, true, true, true);
            }

            PopOrthoProjection();
        }

        /// <summary>
        /// Composites a finished coverage mask onto the framebuffer at whole pixel
        /// (<paramref name="originX"/>, <paramref name="originY"/>), applying <paramref name="color"/> per
        /// channel.
        /// </summary>
        /// <remarks>
        /// <b>The mechanism is <see cref="CompositeLcdBuffer"/>'s</b> - three passes of the same quad, each
        /// writing one color channel with <see cref="OpenGl.GL.ColorMask"/> and taking that channel's coverage
        /// as its source alpha - and the differences are all about where the color comes from. A buffer carries
        /// its own color per pixel; a mask carries coverage only and is handed a color per draw, which is what
        /// lets one mask serve every color and position it is ever drawn at (see <c>LcdMaskCache</c>). So the
        /// color arrives as the draw color and the default modulate texture environment multiplies it into the
        /// pass texture, leaving the textures a pure function of the mask.
        /// <para>
        /// <b>The premultiplication choice.</b> Pass <c>c</c>'s texture is white premultiplied by that
        /// channel's coverage - all four bytes are the mask byte - and the blend is
        /// One / OneMinusSrcAlpha, matching the buffer composite above. The reference's mask pipeline states
        /// the same thing as straight white over SrcAlpha / OneMinusSrcAlpha, which is <i>not</i> available
        /// here: <see cref="ImageTexturePlugin"/> blits every image it uploads through the image's own blender
        /// onto a transparent destination, and that blit turns a straight
        /// (<see cref="BlenderBGRA"/>) white into a premultiplied one - so a straight pass image would arrive
        /// at the driver already multiplied and SrcAlpha would multiply the coverage in a second time. A
        /// premultiplied image survives that blit byte for byte (see <see cref="LcdBufferChannelImages"/>).
        /// </para>
        /// <para>
        /// The draw color is therefore <b>premultiplied</b> too, which is what makes the arithmetic come out
        /// at <see cref="LcdCoverage.LcdComposite"/>'s: modulate gives
        /// <c>src_c = mask_c * color_c * color_a</c> and <c>src_a = mask_c * color_a</c>, and One /
        /// OneMinusSrcAlpha then lands <c>color_c * cov + dst_c * (1 - cov)</c> with
        /// <c>cov = mask_c * color_a</c>. It is byte identical to the software composite for an opaque color,
        /// which is what text draws with; a translucent one pays up to one byte level of rounding, because
        /// <see cref="OpenGl.GL.Color4(Color)"/> takes bytes and the premultiplied color has to be quantized
        /// into them. Making that exact would need the color inside the texture, which would key the texture
        /// cache by color and defeat the mask cache behind it.
        /// </para>
        /// <para>
        /// <b>Destination alpha is never written</b> and no transform is applied, both exactly as
        /// <see cref="CompositeLcdBuffer"/> - see its remarks.
        /// </para>
        /// </remarks>
        /// <param name="clip">
        /// Not applied here, and not ignored either: on this destination it is
        /// <see cref="GetClippingRect"/>'s rect, which <see cref="SetClippingRect"/> has already installed as
        /// the GL scissor, live for every pass - so re-clipping would be the same rectangle enforced twice.
        /// The buffer composite above relies on the identical arrangement.
        /// <para>
        /// The two are the same rectangle because a widget clip is whole pixels by the time it gets here
        /// (<c>GuiWidget.DrawChild</c> floors and ceils all four edges first), <b>not</b> because the two roundings
        /// agree in general: the scissor takes <c>floor(left)</c> and <c>ceil(width)</c>, where a mask clip
        /// takes <c>floor(left)</c> and <c>ceil(right)</c>, and those part company on a fractional left edge -
        /// <c>floor(0.5) + ceil(1.0)</c> reaches x = 1, <c>ceil(1.5)</c> reaches x = 2. A caller that sets a
        /// fractional clip would get the scissor's answer, which is the same answer every other GL draw on
        /// this destination already gives it.
        /// </para>
        /// </param>
        protected override void CompositeLcdMask(LcdMask mask, Color color, int originX, int originY, RectangleDouble? clip = null)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (!this.CanCompositeLcd)
            {
                // Reports the capability disagreeing with itself, as the base class does. Nothing here can
                // paint without a context, and the caller only got here by asking whether it could.
                base.CompositeLcdMask(mask, color, originX, originY, clip);
                return;
            }

            if (mask.Width <= 0 || mask.Height <= 0)
            {
                return;
            }

            ImageBuffer[] channelImages = GetLcdMaskChannelImages(mask);

            PushOrthoProjection();
            gl.Disable(EnableCap.Lighting);
            gl.Enable(EnableCap.Texture2D);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);

            // Premultiplied source over, because both the pass images and the draw color below are
            // premultiplied: dst_c = src_c + dst_c * (1 - src_alpha_c).
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            gl.Translate(originX, originY, 0);

            gl.Color4(Premultiply(color));

            try
            {
                for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
                {
                    gl.ColorMask(channel == 0, channel == 1, channel == 2, false);
                    ImageTexturePlugin.GetImageTexturePlugin(gl, channelImages[channel], false).DrawToGL();
                }
            }
            finally
            {
                // Restored on the way out of a throw as well - see CompositeLcdBuffer for what a mask left
                // stuck on one channel does to every frame after it.
                gl.ColorMask(true, true, true, true);
            }

            PopOrthoProjection();
        }

        /// <summary>
        /// This mask's three per-channel pass images: channel <c>c</c>'s coverage as white premultiplied by
        /// itself, built once and then shared by every draw of that mask.
        /// </summary>
        /// <remarks>
        /// The pack runs outside the lock, so two threads that both miss can both build - the loser's images
        /// are simply dropped, and only the published set is ever drawn with. That is the same trade the buffer
        /// table above makes: holding a process wide lock across an O(width * height) pass would park every
        /// other context behind a glyph run's repack.
        /// </remarks>
        private static ImageBuffer[] GetLcdMaskChannelImages(LcdMask mask)
        {
            lock (lcdMaskChannelImagesLock)
            {
                if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] cached))
                {
                    return cached;
                }
            }

            ImageBuffer[] built = PackLcdMaskChannelImages(mask);

            lock (lcdMaskChannelImagesLock)
            {
                if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] published))
                {
                    return published;
                }

                lcdMaskChannelImages.Add(mask, built);
                return built;
            }
        }

        /// <summary>
        /// Reduces <paramref name="mask"/> to one ordinary premultiplied BGRA image per pass, each holding
        /// channel <c>c</c>'s coverage in all four bytes.
        /// </summary>
        /// <remarks>
        /// White premultiplied by the coverage, rather than the coverage in alpha alone: see
        /// <see cref="CompositeLcdMask"/> for why the image has to be premultiplied to survive the texture
        /// uploader, and <see cref="LcdBufferChannelImages"/> for why a valid premultiplied image
        /// (<c>color &lt;= alpha</c>, trivially true here) makes that blit lossless. The two color channels the
        /// pass's write mask discards are white too, which costs nothing and keeps the image a plain
        /// interpretation of itself - a coverage image - rather than a channel-selecting one, because unlike
        /// the buffer form there is no per-channel color to select.
        /// <para>
        /// Row <c>y</c> in, row <c>y</c> out. Both the mask and the image are Y-up and agg-sharp's GL texture
        /// path is Y-up end to end, so there is no flip anywhere in this composite.
        /// </para>
        /// </remarks>
        private static ImageBuffer[] PackLcdMaskChannelImages(LcdMask mask)
        {
            var images = new ImageBuffer[LcdBufferChannelImages.ChannelCount];

            for (int channel = 0; channel < images.Length; channel++)
            {
                var image = new ImageBuffer(mask.Width, mask.Height, 32, new BlenderPreMultBGRA());
                byte[] pixels = image.GetBuffer();
                int bytesPerPixel = image.GetBytesBetweenPixelsInclusive();

                for (int y = 0; y < mask.Height; y++)
                {
                    int rowOffset = image.GetBufferOffsetXY(0, y);
                    int source = mask.PixelOffset(0, y) + channel;

                    for (int x = 0; x < mask.Width; x++, source += 3)
                    {
                        byte coverage = mask.Data[source];
                        int offset = rowOffset + (x * bytesPerPixel);
                        pixels[offset + ImageBuffer.OrderR] = coverage;
                        pixels[offset + ImageBuffer.OrderG] = coverage;
                        pixels[offset + ImageBuffer.OrderB] = coverage;
                        pixels[offset + ImageBuffer.OrderA] = coverage;
                    }
                }

                images[channel] = image;
            }

            return images;
        }

        /// <summary>
        /// The draw color with its color channels multiplied by its own alpha, rounding half up.
        /// </summary>
        /// <remarks>
        /// Half up rather than truncating because the whole point is to land the software composite's byte:
        /// truncation would darken every translucent fill by up to a full level instead of half of one.
        /// </remarks>
        private static Color Premultiply(Color color)
        {
            if (color.alpha == 255)
            {
                return color;
            }

            return new Color(
                (byte)(((color.red * color.alpha) + 127) / 255),
                (byte)(((color.green * color.alpha) + 127) / 255),
                (byte)(((color.blue * color.alpha) + 127) / 255),
                color.alpha);
        }

        /// <summary>
        /// This buffer's three per-channel pass images, repacked if the buffer has been painted since they
        /// were last built.
        /// </summary>
        /// <remarks>
        /// The lock covers only the table, not the repack: the repack writes into images owned by this
        /// buffer, and a buffer is painted and composited by the one thread that owns it, so two threads
        /// racing here would already be racing over the planes themselves. Holding a process wide lock across
        /// an O(width * height) pass over a full window backbuffer, on the other hand, would park every other
        /// context behind it.
        /// </remarks>
        private static LcdBufferChannelImages GetLcdChannelImages(LcdBuffer buffer)
        {
            LcdBufferChannelImages images;
            lock (lcdChannelImagesLock)
            {
                if (!lcdChannelImages.TryGetValue(buffer, out images)
                    || images.Width != buffer.Width
                    || images.Height != buffer.Height)
                {
                    images = new LcdBufferChannelImages(buffer.Width, buffer.Height);
                    lcdChannelImages.AddOrUpdate(buffer, images);
                }
            }

            images.UpdateFrom(buffer);
            return images;
        }
	}
}
