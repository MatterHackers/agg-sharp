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
