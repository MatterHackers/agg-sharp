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
using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class ImageGraphics2D : Graphics2D
	{
		private IScanlineCache scanlineCache;
		private readonly VertexStorage drawImageRectPath = new VertexStorage();
		private readonly span_allocator destImageSpanAllocatorCache = new span_allocator();
		private readonly ScanlineCachePacked8 drawImageScanlineCache = new ScanlineCachePacked8();
		private readonly ScanlineRenderer scanlineRenderer = new ScanlineRenderer();

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
			Rasterizer.SetVectorClipBox(clippingRect);
		}

		public override RectangleDouble GetClippingRect()
		{
			return Rasterizer.GetVectorClipBox();
		}

		public override void Render(IVertexSource vertexSource, IColorType colorBytes)
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
				scanlineRenderer.RenderSolid(destImageByte, rasterizer, scanlineCache, colorBytes.ToColor());
				DestImage.MarkImageChanged();
			}
			else
			{
				scanlineRenderer.RenderSolid(destImageFloat, rasterizer, scanlineCache, colorBytes.ToColorF());
				destImageFloat.MarkImageChanged();
			}
		}

		private void DrawImageGetDestBounds(IImageByte sourceImage,
			double destX,
			double destY,
			double hotspotOffsetX,
			double hotspotOffsetY,
			double scaleX,
			double scaleY,
			double angleRad,
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

			int sourceBufferWidth = (int)sourceImage.Width;
			int sourceBufferHeight = (int)sourceImage.Height;

			drawImageRectPath.remove_all();

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
			Affine graphicsTransform = GetTransform();

			// exit early if the dest and source bounds don't touch.
			// TODO: <BUG> make this do rotation and scaling
			RectangleInt sourceBounds = source.GetBounds();
			RectangleInt destBounds = this.destImageByte.GetBounds();
			sourceBounds.Offset((int)(destX + graphicsTransform.tx), (int)(destY + graphicsTransform.ty));

			if (!RectangleInt.DoIntersect(sourceBounds, destBounds))
			{
				if (inScaleX != 1 || inScaleY != 1 || angleRadians != 0)
				{
					//throw new NotImplementedException();
				}

				//return;
			}

			double scaleX = inScaleX;
			double scaleY = inScaleY;

			if (!graphicsTransform.is_identity())
			{
				if (scaleX != 1 || scaleY != 1 || angleRadians != 0)
				{
					//throw new NotImplementedException();
				}

				graphicsTransform.transform(ref destX, ref destY);
			}

#if false // this is an optimization that eliminates the drawing of images that have their alpha set to all 0 (happens with generated images like explosions).
	        MaxAlphaFrameProperty maxAlphaFrameProperty = MaxAlphaFrameProperty::GetMaxAlphaFrameProperty(source);

	        if((maxAlphaFrameProperty.GetMaxAlpha() * color.A_Byte) / 256 <= ALPHA_CHANNEL_BITS_DIVISOR)
	        {
		        m_OutFinalBlitBounds.SetRect(0,0,0,0);
	        }
#endif
			bool isScaled = scaleX != 1 || scaleY != 1;

			bool isRotated = true;
			if (Math.Abs(angleRadians) < (0.1 * MathHelper.Tau / 360))
			{
				isRotated = false;
				angleRadians = 0;
			}

			// bool IsMipped = false;
			double sourceOriginOffsetX = source.OriginOffset.X;
			double sourceOriginOffsetY = source.OriginOffset.Y;
			bool canUseMipMaps = isScaled;
			if (scaleX > 0.5 || scaleY > 0.5)
			{
				canUseMipMaps = false;
			}

			bool renderRequriesSourceSampling = isScaled || isRotated || destX != (int)destX || destY != (int)destY;

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
							DrawImageGetDestBounds(source, destX, destY, sourceOriginOffsetX, sourceOriginOffsetY, scaleX, scaleY, angleRadians, out Affine destRectTransform);

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
							DrawImageGetDestBounds(source, destX, destY, sourceOriginOffsetX, sourceOriginOffsetY, scaleX, scaleY, angleRadians, out Affine destRectTransform);

							var sourceRectTransform = new Affine(destRectTransform);
							// We invert it because it is the transform to make the image go to the same position as the polygon. LBB [2/24/2004]
							sourceRectTransform.invert();

							var interpolator = new span_interpolator_linear(sourceRectTransform);
							var sourceAccessor = new ImageBufferAccessorClip(source, ColorF.rgba_pre(0, 0, 0, 0).ToColor());

							// spanImageFilter = new span_image_filter_rgba_bilinear_clip(sourceAccessor, RGBA_Floats.rgba_pre(0, 0, 0, 0), interpolator);

							IImageFilterFunction filterFunction = null;
							filterFunction = new image_filter_blackman(4);
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
				DrawImageGetDestBounds(source, destX, destY, sourceOriginOffsetX, sourceOriginOffsetY, scaleX, scaleY, angleRadians, out Affine destRectTransform);

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
			throw new NotImplementedException();
		}

		public override void Clear(IColorType iColor)
		{
			RectangleDouble clippingRect = GetClippingRect();
			var clippingRectInt = new RectangleInt((int)clippingRect.Left, (int)clippingRect.Bottom, (int)clippingRect.Right, (int)clippingRect.Top);

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
								int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRect.Left, y);
								int bytesBetweenPixels = DestImage.GetBytesBetweenPixelsInclusive();
								for (int x = 0; x < clippingRectInt.Width; x++)
								{
									buffer[bufferOffset] = color.blue;
									bufferOffset += bytesBetweenPixels;
								}
							}
						}

						break;

					case 24:
						for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
						{
							int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRect.Left, y);
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
						{
							for (int y = clippingRectInt.Bottom; y < clippingRectInt.Top; y++)
							{
								int bufferOffset = DestImage.GetBufferOffsetXY((int)clippingRect.Left, y);
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
		}
	}
}