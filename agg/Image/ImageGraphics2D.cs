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
using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class ImageGraphics2D : Graphics2D, IPatternFillGraphics, IGradientFillGraphics, IImageFilterGraphics
	{
		private IScanlineCache scanlineCache;
		private readonly VertexStorage drawImageRectPath = new VertexStorage();
		private readonly span_allocator destImageSpanAllocatorCache = new span_allocator();
		private readonly ScanlineCachePacked8 drawImageScanlineCache = new ScanlineCachePacked8();
		private readonly ScanlineRenderer scanlineRenderer = new ScanlineRenderer();

		// StraightOverDestination's chain, kept while the destination buffer stays the same so a fill allocates nothing.
		private ImageBuffer straightOverBuffer;
		private ImageClippingProxy straightOverClipping;

		/// <summary>
		/// Whether a Best-quality image draw reads past the source edge as the edge pixel (clamp) rather than as
		/// transparent (clip). Clip is right for placing an image on a canvas; resizing an image into its own
		/// bounds (<see cref="ImageBufferExtensionMethods.CreateScaledImage(ImageBuffer, int, int)"/>) needs clamp,
		/// or an opaque image comes out with a see-through frame.
		/// </summary>
		internal bool ExtendImageEdges { get; set; }

		public ImageGraphics2D()
		{
		}

		public ImageGraphics2D(IImageByte destImage, ScanlineRasterizer rasterizer, IScanlineCache scanlineCache)
			: base(destImage, rasterizer)
		{
			this.scanlineCache = scanlineCache;
		}

		public override IScanlineCache ScanlineCache
		{
			get { return scanlineCache; }
			set { scanlineCache = value; }
		}

		public override int Width => destImageByte.Width;

		public override int Height => destImageByte.Height;

		public override void SetClippingRect(RectangleDouble clippingRect)
		{
			this.FlushDeferredDraws();
			Rasterizer.SetVectorClipBox(clippingRect);
		}

		/// <summary>The rasterizer's clip box, or the whole canvas when it has none (it clips to the canvas anyway).</summary>
		public override RectangleDouble GetClippingRect()
		{
			return Rasterizer.HasVectorClipBox ? Rasterizer.GetVectorClipBox() : new RectangleDouble(0, 0, Width, Height);
		}

		/// <inheritdoc/>
		protected override void RenderVertexSource(IVertexSource vertexSource, IColorType colorBytes)
		{
			rasterizer.reset();
			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
			}

			rasterizer.add_path(vertexSource);
			if (destImageByte != null)
			{
				// See StraightOverDestination: a widget backbuffer takes a solid fill as straight source-over.
				scanlineRenderer.RenderSolid(StraightOverDestination() ?? destImageByte, rasterizer, scanlineCache, colorBytes.ToColor());
				DestImage.MarkImageChanged();
			}
			else
			{
				scanlineRenderer.RenderSolid(destImageFloat, rasterizer, scanlineCache, colorBytes.ToColorF());
				destImageFloat.MarkImageChanged();
			}
		}

		/// <summary>
		/// The destination to fill a solid colour into when it is a 32 bit <see cref="ImageBuffer"/> labelled
		/// <see cref="BlenderPreMultBGRA"/>: a clipping proxy with the destination's clip box over a
		/// <see cref="StraightOverImageProxy"/> on that buffer. Null - fill through the destination as is - otherwise.
		/// </summary>
		/// <remarks>
		/// <b>The rule: a widget backbuffer (and any image labelled premultiplied that the app draws into) holds
		/// straight colour.</b> Every consumer reads it that way - the GPU image blit (see the note in
		/// Graphics2DGpu.Render(IImageByte, ...)), the CPU blit onto a straight surface, and MatterCAD's icons - so
		/// a solid fill blends into it with straight source-over (<see cref="BlenderStraightOverBGRA"/>, C++
		/// <c>blender_rgba_plain</c>): (c, a) over a transparent pixel, the straight lerp over an opaque one.
		/// Graphics2D colours are straight. Handing one to the label's premultiplied blender added the whole colour
		/// on top of the background, so a translucent fill over opaque content came out too light - a faint one
		/// clamped to white (ImageGraphics2DTranslucentFillTests, MarkdownTableGridTests' table stripes) - and
		/// over a transparent pixel it stored (c * cover, cover) at an anti-aliased edge, which the straight-reading
		/// composite darkened by the cover again (PartlyCoveredEdgeOnTransparentBackbufferCompositesStraight).
		/// ImageBuffer's own solid blends keep the C++ premultiplied contract (ImageBufferSolidBlendTests); the
		/// straight blend lives only in the proxy, so the buffer and its blender are never changed. A genuinely premultiplied target (the SVG
		/// renderer's layers) draws through <see cref="ScanlineRenderer"/> directly with premultiplied colour.
		/// Only a real ImageBuffer is asked: other IImageByte destinations (Graphics2DSpanImage) have no blender.
		/// The chain is rebuilt only through plain <see cref="ImageClippingProxy"/> wrappers, whose boxes intersect
		/// into one; any other proxy (an <see cref="ImageMultiClipProxy"/>) keeps the labelled blend rather than
		/// lose its clipping.
		/// </remarks>
		private IImageByte StraightOverDestination()
		{
			int left = int.MinValue, bottom = int.MinValue, right = int.MaxValue, top = int.MaxValue;
			IImageByte image = destImageByte;
			while (image is ImageProxy proxy)
			{
				if (proxy.GetType() != typeof(ImageClippingProxy))
				{
					return null;
				}

				RectangleInt box = ((ImageClippingProxy)proxy).clip_box();
				left = Math.Max(left, box.Left);
				bottom = Math.Max(bottom, box.Bottom);
				right = Math.Min(right, box.Right);
				top = Math.Min(top, box.Top);
				image = proxy.LinkedImage;
			}

			if (!(image is ImageBuffer buffer && buffer.BitDepth == 32 && buffer.GetRecieveBlender() is BlenderPreMultBGRA))
			{
				return null;
			}

			if (straightOverBuffer != buffer)
			{
				straightOverBuffer = buffer;
				straightOverClipping = new ImageClippingProxy(new StraightOverImageProxy(buffer));
			}

			// Copied as is (an empty box stays empty): SetClippingBox would normalize an inverted, invisible box.
			if (left == int.MinValue)
			{
				straightOverClipping.reset_clipping(true);
			}
			else
			{
				straightOverClipping.clip_box_naked(left, bottom, right, top);
			}

			return straightOverClipping;
		}

		/// <summary>
		/// True for a 32 bit-per-pixel <see cref="ImageBuffer"/> destination, reached either directly or
		/// through <see cref="ImageClippingProxy"/> wrappers - which is what
		/// <see cref="ImageBuffer.NewGraphics2D"/> hands this class, never the buffer itself.
		/// </summary>
		/// <remarks>
		/// The limits are all real, not conservatism:
		/// <list type="bullet">
		/// <item><description>32 bits per pixel, because per-channel coverage has nothing to write into an
		/// 8 or 24 bit destination's missing channels;</description></item>
		/// <item><description>a concrete <see cref="ImageBuffer"/>, because
		/// <see cref="LcdComposite.Composite"/> writes bytes directly - per-channel coverage cannot travel
		/// through <see cref="IRecieveBlenderByte"/>, which is the whole reason this is a mask pipeline
		/// rather than a blender;</description></item>
		/// <item><description>and <see cref="ImageClippingProxy"/> as the only proxy walked through, because
		/// it is the only one whose byte writes land unchanged on its linked buffer. Every other
		/// <see cref="ImageProxy"/> exists precisely to reinterpret them - <see cref="FormatTransposer"/>
		/// swaps the axes, <see cref="AlphaMaskAdaptor"/> multiplies in a mask - so a direct write behind one
		/// would land in the wrong place or skip its effect. Those report false and take the ordinary fill
		/// through the proxy, which is the answer that stays correct;</description></item>
		/// <item><description>float destinations are out, since there is no float composite.</description></item>
		/// </list>
		/// <para>
		/// <b>A transparent compositing layer is refused outright</b>, rather than being handed the
		/// chroma-free mask the validity gate calls for on a per-channel target. The reason is specific to a
		/// single-alpha destination: <see cref="LcdComposite"/> writes colour and deliberately leaves
		/// destination alpha alone, which is right on an opaque surface and useless on a layer whose alpha is
		/// still zero - the paint would be invisible the moment the layer was blended onto anything. Such a
		/// destination takes the ordinary anti-aliased fill, which writes alpha, and so renders exactly as it
		/// did before the LCD path existed. The gray arm is still the right answer for a target that carries
		/// alpha per channel; that is <see cref="LcdBufferGraphics2D"/>, which keeps this true and switches
		/// <see cref="Graphics2D.LcdChromaAllowed"/> instead. The caller declares the layer through
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/>; opacity is not measured here.
		/// </para>
		/// <para>
		/// <b>The reference does not share this hazard, and the divergence is worth naming.</b> Rust's
		/// framebuffer composite writes the destination alpha too, by a max rule over the three channel
		/// coverages (<c>gfx_ctx\draw_impl.rs:568-570</c>), so a layer it paints into comes out with alpha and
		/// survives being blended onward. agg-sharp ported the other composite - the straight-RGBA one from
		/// <c>mask.rs</c>, which leaves destination alpha untouched - and the refusal above is the honest
		/// consequence of that choice rather than a limit of the design. Teaching
		/// <see cref="LcdComposite"/> the max-alpha write is what would close the gap, and would let a
		/// transparent single-alpha layer take the gray mask like every other target.
		/// </para>
		/// </remarks>
		public override bool CanCompositeLcd => !this.IsTransparentCompositingLayer && ResolveLcdDestination() != null;

		/// <inheritdoc/>
		protected override void CompositeLcdMask(LcdMask mask, Color color, int originX, int originY, RectangleDouble? clip = null)
		{
			ImageBuffer destination = this.CanCompositeLcd ? ResolveLcdDestination() : null;
			if (destination == null)
			{
				base.CompositeLcdMask(mask, color, originX, originY, clip);
				return;
			}

			LcdComposite.Composite(destination, mask, color, originX, originY, clip);
			destImageByte.MarkImageChanged();
		}

		/// <summary>
		/// True on the same destinations <see cref="CanCompositeLcd"/> accepts, minus the ones flagged
		/// <see cref="Graphics2D.IsTransparentCompositingLayer"/>.
		/// </summary>
		/// <remarks>
		/// The extra condition is the validity gate, applied one level up from a mask: per-channel planes are
		/// only meaningful against pixels nothing will blend again later, and a transparent layer's pixels are
		/// blended again by definition. A widget asking this question is deciding whether to keep its whole
		/// backbuffer in LCD coverage, so answering false here sends it to the ordinary RGBA backbuffer rather
		/// than producing chroma that would be collapsed - or worse, composited against unknown pixels - on
		/// the way to the screen.
		/// <para>
		/// <b>Unchecked precondition: the destination must hold premultiplied colour.</b> Saying true here is
		/// what routes a widget's buffer into <see cref="LcdBuffer.CompositeOnto"/>, whose per-channel
		/// <c>color_c + dest_c * (1 - alpha_c)</c> is source-over only against a premultiplied destination -
		/// a straight-alpha one (<see cref="BlenderBGRA"/>) blends visibly wrong. Nothing verifies it, and a
		/// blender test here would be wrong rather than merely absent: it would refuse the opaque final
		/// surface, where the two conventions coincide and the composite is correct either way. The two
		/// destinations this is reached on both satisfy it - a widget backbuffer is
		/// <see cref="BlenderPreMultBGRA"/> by construction, and an opaque window surface has no partial
		/// alpha to get wrong.
		/// </para>
		/// <para>
		/// The precondition became load-bearing when <c>GuiWidget.ResolveBackbufferMode</c> stopped requiring
		/// the widget to be opaque: while every channel alpha was 255 the <c>dest_c * (1 - alpha_c)</c> term
		/// vanished and the destination's convention could not matter. The same change exposed a divergence in
		/// the other direction - the buffered path writes <c>dest.alpha := max(alpha_c)</c> where the
		/// unbuffered <see cref="LcdComposite"/> leaves destination alpha untouched. Harmless on the opaque
		/// surfaces above, and the reason a buffered render is compared to a direct one within a tolerance
		/// rather than byte for byte.
		/// </para>
		/// </remarks>
		public override bool CanCompositeLcdBuffer => !this.IsTransparentCompositingLayer && ResolveLcdDestination() != null;

		/// <inheritdoc/>
		/// <remarks>
		/// The per-channel override of the base class's collapsing default: each subpixel's alpha drives its
		/// own source-over, so a cached LCD backbuffer keeps its chroma all the way onto this surface. The
		/// destination is treated as premultiplied, which it is where this is reached - a widget backbuffer
		/// (<see cref="BlenderPreMultBGRA"/>), or an opaque final surface, where premultiplied and straight
		/// coincide.
		/// <para>
		/// The clipping rect is honoured, because the widget layer sets it to the child's screen clipping
		/// before compositing and a partially scrolled-out widget must not paint over its siblings. Like <see cref="LcdComposite"/>, the placement is in raw buffer pixels and takes
		/// no account of <see cref="ImageBuffer.OriginOffset"/>.
		/// </para>
		/// <para>
		/// The premultiplied-or-opaque precondition is the caller's to keep. A widget backbuffer flush keeps it
		/// by construction; an SVG icon's composite from <see cref="Render(IImageByte, double, double, double, double, double)"/>
		/// checks it per draw (<see cref="TakesLcdImageOver"/>), since an icon can be drawn into any image.
		/// </para>
		/// </remarks>
		public override void CompositeLcdBuffer(LcdBuffer buffer, int destX, int destY)
		{
			this.FlushDeferredDraws();
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			ImageBuffer destination = this.CanCompositeLcdBuffer ? ResolveLcdDestination() : null;
			if (destination == null)
			{
				base.CompositeLcdBuffer(buffer, destX, destY);
				return;
			}

			buffer.CompositeOnto(destination, destX, destY, 1.0, LcdBuffer.ToPixelClip(GetClippingRect()));
			destImageByte.MarkImageChanged();
		}

		/// <summary>
		/// Whether an image's LCD composite may land on <paramref name="footprint"/> of this destination: it must
		/// hold premultiplied colour, or be opaque everywhere the image lands.
		/// </summary>
		/// <remarks>
		/// This checks, for images, the precondition <see cref="CanCompositeLcdBuffer"/> leaves unchecked.
		/// <see cref="LcdBuffer.CompositeOnto"/> is premultiplied source-over, so on a straight-alpha
		/// destination with partial alpha - an ad-hoc <c>new ImageBuffer(w, h)</c> an icon is being drawn into -
		/// it would bake premultiplied, fringed edges into pixels that are later blended as straight alpha. A
		/// widget backbuffer is <see cref="BlenderPreMultBGRA"/> and passes on the blender. The software window
		/// surface (<c>WindowsFormsDXBackedGui</c>) is straight <see cref="BlenderBGRA"/> but opaque once its
		/// background is painted, where the two conventions coincide; a blender-only gate would cost it its LCD
		/// icons, so a straight destination passes when every pixel under the image is fully opaque. That scan
		/// is the image's own size, the size of the composite it guards.
		/// </remarks>
		private bool TakesLcdImageOver(RectangleInt footprint)
		{
			ImageBuffer destination = ResolveLcdDestination();
			if (destination == null)
			{
				return false;
			}

			if (destination.GetRecieveBlender() is BlenderPreMultBGRA)
			{
				return true;
			}

			if (!footprint.IntersectWithRectangle(new RectangleInt(0, 0, destination.Width, destination.Height)))
			{
				// Entirely off the destination: nothing to composite, and the plain blit draws nothing either.
				return false;
			}

			byte[] buffer = destination.GetBuffer();
			for (int y = footprint.Bottom; y < footprint.Top; y++)
			{
				for (int x = footprint.Left; x < footprint.Right; x++)
				{
					if (buffer[destination.GetBufferOffsetXY(x, y) + ImageBuffer.OrderA] != 255)
					{
						return false;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// The <see cref="ImageBuffer"/> behind <c>destImageByte</c>, unwrapping clipping proxies only, or
		/// null when this destination cannot take an LCD composite. See <see cref="CanCompositeLcd"/> for why
		/// the walk stops at any other proxy.
		/// </summary>
		private ImageBuffer ResolveLcdDestination()
		{
			IImageByte image = destImageByte;
			while (image is ImageClippingProxy clippingProxy)
			{
				image = clippingProxy.LinkedImage;
			}

			return image is ImageBuffer buffer && buffer.BitDepth == 32 ? buffer : null;
		}

		/// <summary>
		/// Builds the image-to-surface transform the <see cref="Graphics2D.Render(IImageByte, double, double, double, double, double)"/>
		/// contract describes - hotspot, scale, rotation, placement, then the whole graphics transform - and the
		/// image's rectangle in its own pixels, which <see cref="DrawImage"/> rasterizes through it.
		/// </summary>
		private void DrawImageGetDestBounds(IImageByte sourceImage,
			double destX,
			double destY,
			double hotspotOffsetX,
			double hotspotOffsetY,
			double scaleX,
			double scaleY,
			double angleRad,
			Affine graphicsTransform,
			out Affine destRectTransform)
		{
			destRectTransform = Affine.NewIdentity();

			if (hotspotOffsetX != 0.0f || hotspotOffsetY != 0.0f)
			{
				destRectTransform *= Affine.NewTranslation(-hotspotOffsetX, -hotspotOffsetY);
			}

			if (scaleX != 1 || scaleY != 1)
			{
				destRectTransform *= Affine.NewScaling(scaleX, scaleY);
			}

			if (angleRad != 0)
			{
				destRectTransform *= Affine.NewRotation(angleRad);
			}

			if (destX != 0 || destY != 0)
			{
				destRectTransform *= Affine.NewTranslation(destX, destY);
			}

			destRectTransform *= graphicsTransform;

			int sourceBufferWidth = (int)sourceImage.Width;
			int sourceBufferHeight = (int)sourceImage.Height;

			drawImageRectPath.Clear();

			drawImageRectPath.MoveTo(0, 0);
			drawImageRectPath.LineTo(sourceBufferWidth, 0);
			drawImageRectPath.LineTo(sourceBufferWidth, sourceBufferHeight);
			drawImageRectPath.LineTo(0, sourceBufferHeight);
			drawImageRectPath.ClosePolygon();
		}

		private void DrawImage(ISpanGenerator spanImageFilter, Affine destRectTransform)
		{
			if (destImageByte.OriginOffset.X != 0 || destImageByte.OriginOffset.Y != 0)
			{
				destRectTransform *= Affine.NewTranslation(-destImageByte.OriginOffset.X, -destImageByte.OriginOffset.Y);
			}

			var transformedRect = new VertexSourceApplyTransform(drawImageRectPath, destRectTransform);
			Rasterizer.add_path(transformedRect);
			{
				var destImageWithClipping = new ImageClippingProxy(destImageByte);
				scanlineRenderer.GenerateAndRender(Rasterizer, drawImageScanlineCache, destImageWithClipping, destImageSpanAllocatorCache, spanImageFilter);
			}
		}

		public override void Render(IImageByte source,
			double destX,
			double destY,
			double angleRadians,
			double inScaleX,
			double inScaleY)
		{
			this.FlushDeferredDraws();

			// An SVG icon landing 1:1 on whole pixels composites with its subpixel coverage, as text does. Only
			// with no destination origin offset: the LCD composite places in raw buffer pixels and ignores it.
			if (destImageByte.OriginOffset.X == 0
				&& destImageByte.OriginOffset.Y == 0
				&& LcdImageComposite.TryRender(this, source, destX, destY, angleRadians, inScaleX, inScaleY, this.TakesLcdImageOver))
			{
				return;
			}

			Affine graphicsTransform = GetTransform();

			double scaleX = inScaleX;
			double scaleY = inScaleY;

#if false // this is an optimization that eliminates the drawing of images that have their alpha set to all 0 (happens with generated images like explosions).
	        MaxAlphaFrameProperty maxAlphaFrameProperty = MaxAlphaFrameProperty::GetMaxAlphaFrameProperty(source);

	        if((maxAlphaFrameProperty.GetMaxAlpha() * color.A_Byte) / 256 <= ALPHA_CHANNEL_BITS_DIVISOR)
	        {
		        m_OutFinalBlitBounds.SetRect(0,0,0,0);
	        }
#endif
			bool isScaled = scaleX != 1 || scaleY != 1;

			if (Math.Abs(angleRadians) < (0.1 * MathHelper.Tau / 360))
			{
				angleRadians = 0;
			}

			// Only a whole-pixel translation of the image can skip resampling; any scale, turn or shear - the
			// image's own or the graphics transform's - has to be sampled.
			DrawImageGetDestBounds(source, destX, destY, source.OriginOffset.X, source.OriginOffset.Y, scaleX, scaleY, angleRadians, graphicsTransform, out Affine imageToDest);
			bool isTranslationOnly = imageToDest.sx == 1 && imageToDest.sy == 1 && imageToDest.shx == 0 && imageToDest.shy == 0;

			// bool IsMipped = false;
			double sourceOriginOffsetX = source.OriginOffset.X;
			double sourceOriginOffsetY = source.OriginOffset.Y;
			bool canUseMipMaps = isScaled;
			if (scaleX > 0.5 || scaleY > 0.5)
			{
				canUseMipMaps = false;
			}

			bool renderRequriesSourceSampling = !isTranslationOnly || imageToDest.tx != (int)imageToDest.tx || imageToDest.ty != (int)imageToDest.ty;

			// this is the fast drawing path
			if (renderRequriesSourceSampling)
			{
#if false // if the scaling is small enough the results can be improved by using mip maps
	        if(CanUseMipMaps)
	        {
		        CMipMapFrameProperty* pMipMapFrameProperty = CMipMapFrameProperty::GetMipMapFrameProperty(source);
		        double OldScaleX = scaleX;
		        double OldScaleY = scaleY;
		        const CFrameInterface* pMippedFrame = pMipMapFrameProperty.GetMipMapFrame(ref scaleX, ref scaleY);
		        if(pMippedFrame != source)
		        {
			        IsMipped = true;
			        source = pMippedFrame;
			        sourceOriginOffsetX *= (OldScaleX / scaleX);
			        sourceOriginOffsetY *= (OldScaleY / scaleY);
		        }

			    HotspotOffsetX *= (inScaleX / scaleX);
			    HotspotOffsetY *= (inScaleY / scaleY);
	        }
#endif
				switch (ImageRenderQuality)
				{
					case TransformQuality.Fastest:
						{
							var destRectTransform = new Affine(imageToDest);

							var sourceRectTransform = new Affine(destRectTransform);
							// We invert it because it is the transform to make the image go to the same position as the polygon. LBB [2/24/2004]
							sourceRectTransform.invert();

							span_image_filter spanImageFilter;
							var interpolator = new span_interpolator_linear(sourceRectTransform);
							var sourceAccessor = new ImageBufferAccessorClip(source, ColorF.rgba_pre(0, 0, 0, 0).ToColor());

							spanImageFilter = new span_image_filter_rgba_bilinear_clip(sourceAccessor, ColorF.rgba_pre(0, 0, 0, 0), interpolator);

							DrawImage(spanImageFilter, destRectTransform);
						}

						break;

					case TransformQuality.Best:
						{
							var destRectTransform = new Affine(imageToDest);

							var sourceRectTransform = new Affine(destRectTransform);
							// We invert it because it is the transform to make the image go to the same position as the polygon. LBB [2/24/2004]
							sourceRectTransform.invert();

							var interpolator = new span_interpolator_linear(sourceRectTransform);
							IImageBufferAccessor sourceAccessor = this.ExtendImageEdges
								? new ImageBufferAccessorClamp(source)
								: new ImageBufferAccessorClip(source, ColorF.rgba_pre(0, 0, 0, 0).ToColor());

							// spanImageFilter = new span_image_filter_rgba_bilinear_clip(sourceAccessor, RGBA_Floats.rgba_pre(0, 0, 0, 0), interpolator);

							IImageFilterFunction filterFunction = null;
							filterFunction = new image_filter_spline16();
							var filter = new ImageFilterLookUpTable();
							filter.calculate(filterFunction, true);

							span_image_filter spanGenerator = new span_image_filter_rgba(sourceAccessor, interpolator, filter);

							DrawImage(spanGenerator, destRectTransform);
						}

						break;
				}
#if false // this is some debug you can enable to visualize the dest bounding box
		        LineFloat(BoundingRect.left, BoundingRect.top, BoundingRect.right, BoundingRect.top, WHITE);
		        LineFloat(BoundingRect.right, BoundingRect.top, BoundingRect.right, BoundingRect.bottom, WHITE);
		        LineFloat(BoundingRect.right, BoundingRect.bottom, BoundingRect.left, BoundingRect.bottom, WHITE);
		        LineFloat(BoundingRect.left, BoundingRect.bottom, BoundingRect.left, BoundingRect.top, WHITE);
#endif
			}
			else // TODO: this can be even faster if we do not use an intermediate buffer
			{
				var destRectTransform = new Affine(imageToDest);

				var sourceRectTransform = new Affine(destRectTransform);
				// We invert it because it is the transform to make the image go to the same position as the polygon. LBB [2/24/2004]
				sourceRectTransform.invert();

				var interpolator = new span_interpolator_linear(sourceRectTransform);
				var sourceAccessor = new ImageBufferAccessorClip(source, ColorF.rgba_pre(0, 0, 0, 0).ToColor());

				span_image_filter spanImageFilter = null;
				switch (source.BitDepth)
				{
					case 32:
						spanImageFilter = new span_image_filter_rgba_nn_stepXby1(sourceAccessor, interpolator);
						break;

					case 24:
						spanImageFilter = new span_image_filter_rgb_nn_stepXby1(sourceAccessor, interpolator);
						break;

					case 8:
						spanImageFilter = new span_image_filter_gray_nn_stepXby1(sourceAccessor, interpolator);
						break;

					default:
						throw new NotImplementedException();
				}

				// spanImageFilter = new span_image_filter_rgba_nn(sourceAccessor, interpolator);

				DrawImage(spanImageFilter, destRectTransform);
			}

			DestImage.MarkImageChanged();
		}

		/// <inheritdoc/>
		public void FillPathWithImage(IVertexSource path, IImageByte image, Affine imageToScreen, ImageWrapMode wrapX, ImageWrapMode wrapY)
		{
			this.FlushDeferredDraws();
			ImagePatternFill.Fill(this, path, image, imageToScreen, wrapX, wrapY);
		}

		/// <inheritdoc/>
		public void FillPathWithGradient(IVertexSource path, GradientFill gradient, GradientFill alphaGradient = null)
		{
			this.FlushDeferredDraws();
			ImageGradientFill.Fill(this, path, gradient, alphaGradient);
		}

		/// <inheritdoc/>
		public void FillPathWithFilteredImage(IVertexSource path, IImageByte image, ImageFilterFill fill)
		{
			this.FlushDeferredDraws();
			ImageFilteredFill.Fill(this, path, image, fill);
		}

		public override void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth)
		{
			var rect = new RoundedRect(left + .5, bottom + .5, right - .5, top - .5, 0);
			var rectOutline = new Stroke(rect, strokeWidth);

			Render(rectOutline, color);
		}

		public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
		{
			var rect = new RoundedRect(left, bottom, right, top, 0);
			Render(rect, fillColor.ToColor());
		}

		public override void Render(IImageFloat source,
			double x,
			double y,
			double angleDegrees,
			double inScaleX,
			double inScaleY)
		{
			this.FlushDeferredDraws();
			throw new NotImplementedException();
		}

		public override void Clear(RectangleDouble bounds, IColorType iColor)
		{
			this.FlushDeferredDraws();
			var intBounds = new RectangleInt(bounds);
			var clippingRect = GetClippingRect();
			var clippingRectInt = new RectangleInt(clippingRect);
			// find the intersection of the clipping rect and the bounds
			clippingRectInt.IntersectWithRectangle(intBounds);
			if (clippingRectInt.Width == 0
				|| clippingRectInt.Height == 0)
			{
				return;
			}

            if (DestImage != null)
            {
                var color = iColor.ToColor();
                byte[] buffer = DestImage.GetBuffer();
                switch (DestImage.BitDepth)
                {
                    case 8:
                        {
                            for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
                            {
                                int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRectInt.Left, y);
                                int bytesBetweenPixels = DestImage.GetBytesBetweenPixelsInclusive();
                                for (int x = 0; x < clippingRectInt.Width; x++)
                                {
                                    buffer[bufferOffset] = color.blue;
                                    bufferOffset += bytesBetweenPixels;
                                }
                            }
                        }

                        break;

                    case 16:
                        // A packed 16-bit pixel's layout is its blender's, so clear through it (C++ renderer_base::clear).
                        for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
                        {
                            DestImage.copy_hline(clippingRectInt.Left, y, clippingRectInt.Width, color);
                        }

                        break;

                    case 24:
                        for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
                        {
                            int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRectInt.Left, y);
                            int bytesBetweenPixels = DestImage.GetBytesBetweenPixelsInclusive();
                            for (int x = 0; x < clippingRectInt.Width; x++)
                            {
                                buffer[bufferOffset + 0] = color.blue;
                                buffer[bufferOffset + 1] = color.green;
                                buffer[bufferOffset + 2] = color.red;
                                bufferOffset += bytesBetweenPixels;
                            }
                        }

                        break;

                    case 32:
                        if (DestImage.GetBytesBetweenPixelsInclusive() == 4)
                        {
                            unsafe
                            {
                                fixed (byte* pBufferIn = buffer)
                                {
                                    uint colorValue = (uint)color.Alpha0To255 << 24 | (uint)color.Red0To255 << 16 | (uint)color.Green0To255 << 8 | (uint)color.Blue0To255;
                                    ulong colorValue2 = (ulong)colorValue << 32 | colorValue;

                                    var widthDiv2 = clippingRectInt.Width / 2;

                                    for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
                                    {
                                        byte* pBuffer = pBufferIn + DestImage.GetBufferOffsetXY((int)clippingRectInt.Left, y);
                                        for (int x = 0; x < widthDiv2; x++)
                                        {
                                            // Convert the buffer offset to a pointer to the location where we want to copy the color.
                                            // Copy the color value into the destination buffer in one operation.
                                            *(ulong*)pBuffer = colorValue2;
                                            pBuffer += 8;
                                        }

                                        if (clippingRectInt.Width % 2 == 1)
                                        {
                                            // there is one more pixel to draw. Fill it with colorValue
                                            *(uint*)pBuffer = colorValue;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
                            {
                                int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRectInt.Left, y);
                                int bytesBetweenPixels = DestImage.GetBytesBetweenPixelsInclusive();
                                for (int x = 0; x < clippingRectInt.Width; x++)
                                {
                                    buffer[bufferOffset + 0] = color.blue;
                                    buffer[bufferOffset + 1] = color.green;
                                    buffer[bufferOffset + 2] = color.red;
                                    buffer[bufferOffset + 3] = color.alpha;
                                    bufferOffset += bytesBetweenPixels;
                                }
                            }
                        }

                        break;

                    default:
                        throw new NotImplementedException();
                }

                DestImage.MarkImageChanged();
            }
            else // it is a float
            {
                if (DestImageFloat == null)
                {
                    throw new Exception("You have to have either a byte or float DestImage.");
                }

                var color = iColor.ToColorF();
                int height = DestImageFloat.Height;
                float[] buffer = DestImageFloat.GetBuffer();
                switch (DestImageFloat.BitDepth)
                {
                    case 128:
                        for (int y = 0; y < height; y++)
                        {
                            int bufferOffset = DestImageFloat.GetBufferOffsetXY(clippingRectInt.Left, y);
                            int bytesBetweenPixels = DestImageFloat.GetFloatsBetweenPixelsInclusive();
                            for (int x = 0; x < clippingRectInt.Width; x++)
                            {
                                buffer[bufferOffset + 0] = color.blue;
                                buffer[bufferOffset + 1] = color.green;
                                buffer[bufferOffset + 2] = color.red;
                                buffer[bufferOffset + 3] = color.alpha;
                                bufferOffset += bytesBetweenPixels;
                            }
                        }

                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            //Rectangle(bounds, Color.Black);
        }


        public override void Clear(IColorType iColor)
		{
			Clear(GetClippingRect(), iColor);
		}
	}
}