using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.VectorMath;

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

namespace MatterHackers.Agg.Image
{
	public class ImageBufferFloat : IImageFloat
	{
		public const int OrderB = 0;
		public const int OrderG = 1;
		public const int OrderR = 2;
		public const int OrderA = 3;

		internal class InternalImageGraphics2D : ImageGraphics2D
		{
			private ImageBufferFloat m_Owner;

			internal InternalImageGraphics2D(ImageBufferFloat owner)
				: base()
			{
				m_Owner = owner;

				ScanlineRasterizer rasterizer = new ScanlineRasterizer();
				ImageClippingProxyFloat imageClippingProxy = new ImageClippingProxyFloat(owner);

				Initialize(imageClippingProxy, rasterizer);
				ScanlineCache = new ScanlineCachePacked8();
			}
		};

		protected int[] m_yTable;
		protected int[] m_xTable;
		private float[] m_FloatBuffer;
		private int m_BufferOffset; // the beginning of the image in this buffer
		private int m_BufferFirstPixel; // Pointer to first pixel depending on strideInFloats and image position

		private int m_Width;  // Width in pixels
		private int m_Height; // Height in pixels
		private int m_StrideInFloats; // Number of bytes per row. Can be < 0
		private int m_DistanceInFloatsBetweenPixelsInclusive;
		private int m_BitDepth;
		private Vector2 m_OriginOffset = new Vector2(0, 0);

		private IRecieveBlenderFloat m_Blender;

		private int changedCount = 0;

		public int ChangedCount { get { return changedCount; } }

		public void MarkImageChanged()
		{
			// mark this unchecked as we don't want to throw an exception if this rolls over.
			unchecked
			{
				changedCount++;
			}
		}

		public ImageBufferFloat()
		{
		}

		public ImageBufferFloat(IRecieveBlenderFloat blender)
		{
			SetRecieveBlender(blender);
		}

		public ImageBufferFloat(IImageFloat sourceImage, IRecieveBlenderFloat blender)
		{
			SetDimmensionAndFormat(sourceImage.Width, sourceImage.Height, sourceImage.StrideInFloats(), sourceImage.BitDepth, sourceImage.GetFloatsBetweenPixelsInclusive());
			int offset = sourceImage.GetBufferOffsetXY(0, 0);
			float[] buffer = sourceImage.GetBuffer();
			float[] newBuffer = new float[buffer.Length];
			agg_basics.memcpy(newBuffer, offset, buffer, offset, buffer.Length - offset);
			SetBuffer(newBuffer, offset);
			SetRecieveBlender(blender);
		}

		public ImageBufferFloat(int width, int height, int bitsPerPixel, IRecieveBlenderFloat blender)
		{
			Allocate(width, height, width * (bitsPerPixel / 32), bitsPerPixel);
			SetRecieveBlender(blender);
		}

#if false
        public ImageBuffer(IImageFloat image, IBlenderFloat blender, GammaLookUpTable gammaTable)
        {
            unsafe
            {
                AttachBuffer(image.GetBuffer(), image.Width, image.Height, image.StrideInBytes(), image.BitDepth, image.GetDistanceBetweenPixelsInclusive());
            }

            SetRecieveBlender(blender);
        }
#endif

		public ImageBufferFloat(IImageFloat sourceImageToCopy, IRecieveBlenderFloat blender, int distanceBetweenPixelsInclusive, int bufferOffset, int bitsPerPixel)
		{
			SetDimmensionAndFormat(sourceImageToCopy.Width, sourceImageToCopy.Height, sourceImageToCopy.StrideInFloats(), bitsPerPixel, distanceBetweenPixelsInclusive);
			int offset = sourceImageToCopy.GetBufferOffsetXY(0, 0);
			float[] buffer = sourceImageToCopy.GetBuffer();
			float[] newBuffer = new float[buffer.Length];
			throw new NotImplementedException();
			//agg_basics.memcpy(newBuffer, offset, buffer, offset, buffer.Length - offset);
			//SetBuffer(newBuffer, offset + bufferOffset);
			//SetRecieveBlender(blender);
		}

		public void AttachBuffer(float[] buffer, int bufferOffset, int width, int height, int strideInBytes, int bitDepth, int distanceInBytesBetweenPixelsInclusive)
		{
			m_FloatBuffer = null;
			SetDimmensionAndFormat(width, height, strideInBytes, bitDepth, distanceInBytesBetweenPixelsInclusive);
			SetBuffer(buffer, bufferOffset);
		}

		public void Attach(IImageFloat sourceImage, IRecieveBlenderFloat blender, int distanceBetweenPixelsInclusive, int bufferOffset, int bitsPerPixel)
		{
			SetDimmensionAndFormat(sourceImage.Width, sourceImage.Height, sourceImage.StrideInFloats(), bitsPerPixel, distanceBetweenPixelsInclusive);
			int offset = sourceImage.GetBufferOffsetXY(0, 0);
			float[] buffer = sourceImage.GetBuffer();
			SetBuffer(buffer, offset + bufferOffset);
			SetRecieveBlender(blender);
		}

		public void Attach(IImageFloat sourceImage, IRecieveBlenderFloat blender)
		{
			Attach(sourceImage, blender, sourceImage.GetFloatsBetweenPixelsInclusive(), 0, sourceImage.BitDepth);
		}

		public bool Attach(IImageFloat sourceImage, int x1, int y1, int x2, int y2)
		{
			m_FloatBuffer = null;
			DettachBuffer();

			if (x1 > x2 || y1 > y2)
			{
				throw new Exception("You need to have your x1 and y1 be the lower left corner of your sub image.");
			}
			RectangleInt boundsRect = new RectangleInt(x1, y1, x2, y2);
			if (boundsRect.clip(new RectangleInt(0, 0, (int)sourceImage.Width - 1, (int)sourceImage.Height - 1)))
			{
				SetDimmensionAndFormat(boundsRect.Width, boundsRect.Height, sourceImage.StrideInFloats(), sourceImage.BitDepth, sourceImage.GetFloatsBetweenPixelsInclusive());
				int bufferOffset = sourceImage.GetBufferOffsetXY(boundsRect.Left, boundsRect.Bottom);
				float[] buffer = sourceImage.GetBuffer();
				SetBuffer(buffer, bufferOffset);
				return true;
			}

			return false;
		}

		public void SetAlpha(byte value)
		{
			if (BitDepth != 32)
			{
				throw new Exception("You don't have alpha channel to set.  Your image has a bit depth of " + BitDepth.ToString() + ".");
			}
			int numPixels = Width * Height;
			int offset;
			float[] buffer = GetBuffer(out offset);
			for (int i = 0; i < numPixels; i++)
			{
				buffer[offset + i * 4 + 3] = value;
			}
		}

		private void Deallocate()
		{
			m_FloatBuffer = null;
			SetDimmensionAndFormat(0, 0, 0, 32, 4);
		}

		public void Allocate(int inWidth, int inHeight, int inScanWidthInFloats, int bitsPerPixel)
		{
			if (bitsPerPixel != 128 && bitsPerPixel != 96 && bitsPerPixel != 32)
			{
				throw new Exception("Unsupported bits per pixel.");
			}
			if (inScanWidthInFloats < inWidth * (bitsPerPixel / 32))
			{
				throw new Exception("Your scan width is not big enough to hold your width and height.");
			}
			SetDimmensionAndFormat(inWidth, inHeight, inScanWidthInFloats, bitsPerPixel, bitsPerPixel / 32);

			m_FloatBuffer = new float[m_StrideInFloats * m_Height];

			SetUpLookupTables();
		}

		public Graphics2D NewGraphics2D()
		{
			InternalImageGraphics2D imageRenderer = new InternalImageGraphics2D(this);

			imageRenderer.Rasterizer.SetVectorClipBox(0, 0, Width, Height);

			return imageRenderer;
		}

		public void CopyFrom(IImageFloat sourceImage)
		{
			CopyFrom(sourceImage, sourceImage.GetBounds(), 0, 0);
		}

		protected void CopyFromNoClipping(IImageFloat sourceImage, RectangleInt clippedSourceImageRect, int destXOffset, int destYOffset)
		{
			if (GetFloatsBetweenPixelsInclusive() != BitDepth / 32
				|| sourceImage.GetFloatsBetweenPixelsInclusive() != sourceImage.BitDepth / 32)
			{
				throw new Exception("WIP we only support packed pixel formats at this time.");
			}

			if (BitDepth == sourceImage.BitDepth)
			{
				int lengthInFloats = clippedSourceImageRect.Width * GetFloatsBetweenPixelsInclusive();

				int sourceOffset = sourceImage.GetBufferOffsetXY(clippedSourceImageRect.Left, clippedSourceImageRect.Bottom);
				float[] sourceBuffer = sourceImage.GetBuffer();
				int destOffset;
				float[] destBuffer = GetPixelPointerXY(clippedSourceImageRect.Left + destXOffset, clippedSourceImageRect.Bottom + destYOffset, out destOffset);

				for (int i = 0; i < clippedSourceImageRect.Height; i++)
				{
					agg_basics.memmove(destBuffer, destOffset, sourceBuffer, sourceOffset, lengthInFloats);
					sourceOffset += sourceImage.StrideInFloats();
					destOffset += StrideInFloats();
				}
			}
			else
			{
				bool haveConversion = true;
				switch (sourceImage.BitDepth)
				{
					case 24:
						switch (BitDepth)
						{
							case 32:
								{
									int numPixelsToCopy = clippedSourceImageRect.Width;
									for (int i = clippedSourceImageRect.Bottom; i < clippedSourceImageRect.Top; i++)
									{
										int sourceOffset = sourceImage.GetBufferOffsetXY(clippedSourceImageRect.Left, clippedSourceImageRect.Bottom + i);
										float[] sourceBuffer = sourceImage.GetBuffer();
										int destOffset;
										float[] destBuffer = GetPixelPointerXY(
											clippedSourceImageRect.Left + destXOffset,
											clippedSourceImageRect.Bottom + i + destYOffset,
											out destOffset);
										for (int x = 0; x < numPixelsToCopy; x++)
										{
											destBuffer[destOffset++] = sourceBuffer[sourceOffset++];
											destBuffer[destOffset++] = sourceBuffer[sourceOffset++];
											destBuffer[destOffset++] = sourceBuffer[sourceOffset++];
											destBuffer[destOffset++] = 255;
										}
									}
								}
								break;

							default:
								haveConversion = false;
								break;
						}
						break;

					default:
						haveConversion = false;
						break;
				}

				if (!haveConversion)
				{
					throw new NotImplementedException("You need to write the " + sourceImage.BitDepth.ToString() + " to " + BitDepth.ToString() + " conversion");
				}
			}
		}

		public void CopyFrom(IImageFloat sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset)
		{
			RectangleInt sourceImageBounds = sourceImage.GetBounds();
			RectangleInt clippedSourceImageRect = new RectangleInt();
			if (clippedSourceImageRect.IntersectRectangles(sourceImageRect, sourceImageBounds))
			{
				RectangleInt destImageRect = clippedSourceImageRect;
				destImageRect.Offset(destXOffset, destYOffset);
				RectangleInt destImageBounds = GetBounds();
				RectangleInt clippedDestImageRect = new RectangleInt();
				if (clippedDestImageRect.IntersectRectangles(destImageRect, destImageBounds))
				{
					// we need to make sure the source is also clipped to the dest. So, we'll copy this back to source and offset it.
					clippedSourceImageRect = clippedDestImageRect;
					clippedSourceImageRect.Offset(-destXOffset, -destYOffset);
					CopyFromNoClipping(sourceImage, clippedSourceImageRect, destXOffset, destYOffset);
				}
			}
		}

		public Vector2 OriginOffset
		{
			get { return m_OriginOffset; }
			set { m_OriginOffset = value; }
		}

		public int Width { get { return m_Width; } }

		public int Height { get { return m_Height; } }

		public int StrideInFloats()
		{
			return m_StrideInFloats;
		}

		public int StrideInFloatsAbs()
		{
			return System.Math.Abs(m_StrideInFloats);
		}

		public int GetFloatsBetweenPixelsInclusive()
		{
			return m_DistanceInFloatsBetweenPixelsInclusive;
		}

		public int BitDepth
		{
			get { return m_BitDepth; }
		}

		public virtual RectangleInt GetBounds()
		{
			return new RectangleInt(-(int)m_OriginOffset.x, -(int)m_OriginOffset.y, Width - (int)m_OriginOffset.x, Height - (int)m_OriginOffset.y);
		}

		public IRecieveBlenderFloat GetBlender()
		{
			return m_Blender;
		}

		public void SetRecieveBlender(IRecieveBlenderFloat value)
		{
			if (value != null && value.NumPixelBits != BitDepth)
			{
				throw new NotSupportedException("The blender has to support the bit depth of this image.");
			}
			m_Blender = value;
		}

		private void SetUpLookupTables()
		{
			m_yTable = new int[m_Height];
			for (int i = 0; i < m_Height; i++)
			{
				m_yTable[i] = i * m_StrideInFloats;
			}

			m_xTable = new int[m_Width];
			for (int i = 0; i < m_Width; i++)
			{
				m_xTable[i] = i * m_DistanceInFloatsBetweenPixelsInclusive;
			}
		}

		public void FlipY()
		{
			m_StrideInFloats *= -1;
			m_BufferFirstPixel = m_BufferOffset;
			if (m_StrideInFloats < 0)
			{
				int addAmount = -((int)((int)m_Height - 1) * m_StrideInFloats);
				m_BufferFirstPixel = addAmount + m_BufferOffset;
			}

			SetUpLookupTables();
		}

		public void SetBuffer(float[] floatBuffer, int bufferOffset)
		{
			if (floatBuffer.Length < m_Height * m_StrideInFloats)
			{
				throw new Exception("Your buffer does not have enough room for your height and strideInBytes.");
			}
			m_FloatBuffer = floatBuffer;
			m_BufferOffset = m_BufferFirstPixel = bufferOffset;
			if (m_StrideInFloats < 0)
			{
				int addAmount = -((int)((int)m_Height - 1) * m_StrideInFloats);
				m_BufferFirstPixel = addAmount + m_BufferOffset;
			}
			SetUpLookupTables();
		}

		private void SetDimmensionAndFormat(int width, int height, int strideInFloats, int bitDepth, int distanceInFloatsBetweenPixelsInclusive)
		{
			if (m_FloatBuffer != null)
			{
				throw new Exception("You already have a buffer set. You need to set dimensions before the buffer.  You may need to clear the buffer first.");
			}
			m_Width = width;
			m_Height = height;
			m_StrideInFloats = strideInFloats;
			m_BitDepth = bitDepth;
			if (distanceInFloatsBetweenPixelsInclusive > 4)
			{
				throw new System.Exception("It looks like you are passing bits per pixel rather than distance in Floats.");
			}
			if (distanceInFloatsBetweenPixelsInclusive < (bitDepth / 32))
			{
				throw new Exception("You do not have enough room between pixels to support your bit depth.");
			}
			m_DistanceInFloatsBetweenPixelsInclusive = distanceInFloatsBetweenPixelsInclusive;
			if (strideInFloats < distanceInFloatsBetweenPixelsInclusive * width)
			{
				throw new Exception("You do not have enough strideInFloats to hold the width and pixel distance you have described.");
			}
		}

		public void DettachBuffer()
		{
			m_FloatBuffer = null;
			m_Width = m_Height = m_StrideInFloats = m_DistanceInFloatsBetweenPixelsInclusive = 0;
		}

		public float[] GetBuffer()
		{
			return m_FloatBuffer;
		}

		public float[] GetBuffer(out int bufferOffset)
		{
			bufferOffset = m_BufferOffset;
			return m_FloatBuffer;
		}

		public float[] GetPixelPointerY(int y, out int bufferOffset)
		{
			bufferOffset = m_BufferFirstPixel + m_yTable[y];
			//bufferOffset = GetBufferOffsetXY(0, y);
			return m_FloatBuffer;
		}

		public float[] GetPixelPointerXY(int x, int y, out int bufferOffset)
		{
			bufferOffset = GetBufferOffsetXY(x, y);
			return m_FloatBuffer;
		}

		public RGBA_Floats GetPixel(int x, int y)
		{
			return m_Blender.PixelToColorRGBA_Floats(m_FloatBuffer, GetBufferOffsetXY(x, y));
		}

		public virtual void SetPixel(int x, int y, RGBA_Floats color)
		{
			x -= (int)m_OriginOffset.x;
			y -= (int)m_OriginOffset.y;
			m_Blender.CopyPixels(GetBuffer(), GetBufferOffsetXY(x, y), color, 1);
		}

		public int GetBufferOffsetY(int y)
		{
			return m_BufferFirstPixel + m_yTable[y];
		}

		public int GetBufferOffsetXY(int x, int y)
		{
			return m_BufferFirstPixel + m_yTable[y] + m_xTable[x];
		}

		public void copy_pixel(int x, int y, float[] c, int ByteOffset)
		{
			throw new System.NotImplementedException();
			//byte* p = GetPixelPointerXY(x, y);
			//((int*)p)[0] = ((int*)c)[0];
			//p[OrderR] = c.r;
			//p[OrderG] = c.g;
			//p[OrderB] = c.b;
			//p[OrderA] = c.a;
		}

		public void BlendPixel(int x, int y, RGBA_Floats c, byte cover)
		{
			throw new System.NotImplementedException();
			/*
			cob_type::copy_or_blend_pix(
				(value_type*)m_rbuf->row_ptr(x, y, 1)  + x + x + x,
				c.r, c.g, c.b, c.a,
				cover);*/
		}

		public void SetPixelFromColor(float[] destPixel, IColorType c)
		{
			throw new System.NotImplementedException();
			//pDestPixel[OrderR] = (byte)c.R_Byte;
			//pDestPixel[OrderG] = (byte)c.G_Byte;
			//pDestPixel[OrderB] = (byte)c.B_Byte;
		}

		public void copy_hline(int x, int y, int len, RGBA_Floats sourceColor)
		{
			int bufferOffset;
			float[] buffer = GetPixelPointerXY(x, y, out bufferOffset);

			m_Blender.CopyPixels(buffer, bufferOffset, sourceColor, len);
		}

		public void copy_vline(int x, int y, int len, RGBA_Floats sourceColor)
		{
			throw new NotImplementedException();
#if false
            int scanWidth = StrideInBytes();
            byte* pDestBuffer = GetPixelPointerXY(x, y);
            do
            {
                m_Blender.CopyPixel(pDestBuffer, sourceColor);
                pDestBuffer = &pDestBuffer[scanWidth];
            }
            while (--len != 0);
#endif
		}

		public void blend_hline(int x1, int y, int x2, RGBA_Floats sourceColor, byte cover)
		{
			if (sourceColor.alpha != 0)
			{
				int len = x2 - x1 + 1;

				int bufferOffset;
				float[] buffer = GetPixelPointerXY(x1, y, out bufferOffset);

				float alpha = sourceColor.alpha * (cover * (1.0f / 255.0f));
				if (alpha == 1)
				{
					m_Blender.CopyPixels(buffer, bufferOffset, sourceColor, len);
				}
				else
				{
					do
					{
						m_Blender.BlendPixel(buffer, bufferOffset, new RGBA_Floats(sourceColor.red, sourceColor.green, sourceColor.blue, alpha));
						bufferOffset += m_DistanceInFloatsBetweenPixelsInclusive;
					}
					while (--len != 0);
				}
			}
		}

		public void blend_vline(int x, int y1, int y2, RGBA_Floats sourceColor, byte cover)
		{
			throw new NotImplementedException();
#if false
            int ScanWidth = StrideInBytes();
            if (sourceColor.m_A != 0)
            {
                unsafe
                {
                    int len = y2 - y1 + 1;
                    byte* p = GetPixelPointerXY(x, y1);
                    sourceColor.m_A = (byte)(((int)(sourceColor.m_A) * (cover + 1)) >> 8);
                    if (sourceColor.m_A == base_mask)
                    {
                        byte cr = sourceColor.m_R;
                        byte cg = sourceColor.m_G;
                        byte cb = sourceColor.m_B;
                        do
                        {
                            m_Blender.CopyPixel(p, sourceColor);
                            p = &p[ScanWidth];
                        }
                        while (--len != 0);
                    }
                    else
                    {
                        if (cover == 255)
                        {
                            do
                            {
                                m_Blender.BlendPixel(p, sourceColor);
                                p = &p[ScanWidth];
                            }
                            while (--len != 0);
                        }
                        else
                        {
                            do
                            {
                                m_Blender.BlendPixel(p, sourceColor);
                                p = &p[ScanWidth];
                            }
                            while (--len != 0);
                        }
                    }
                }
            }
#endif
		}

		public void blend_solid_hspan(int x, int y, int len, RGBA_Floats sourceColor, byte[] covers, int coversIndex)
		{
			float colorAlpha = sourceColor.alpha;
			if (colorAlpha != 0)
			{
				unchecked
				{
					int bufferOffset;
					float[] buffer = GetPixelPointerXY(x, y, out bufferOffset);

					do
					{
						float alpha = colorAlpha * (covers[coversIndex] * (1.0f / 255.0f));
						if (alpha == 1)
						{
							m_Blender.CopyPixels(buffer, bufferOffset, sourceColor, 1);
						}
						else
						{
							m_Blender.BlendPixel(buffer, bufferOffset, new RGBA_Floats(sourceColor.red, sourceColor.green, sourceColor.blue, alpha));
						}
						bufferOffset += m_DistanceInFloatsBetweenPixelsInclusive;
						coversIndex++;
					}
					while (--len != 0);
				}
			}
		}

		public void blend_solid_vspan(int x, int y, int len, RGBA_Floats c, byte[] covers, int coversIndex)
		{
			throw new NotImplementedException();
#if false
            if (sourceColor.m_A != 0)
            {
                int ScanWidth = StrideInBytes();
                unchecked
                {
                    byte* p = GetPixelPointerXY(x, y);
                    do
                    {
                        byte oldAlpha = sourceColor.m_A;
                        sourceColor.m_A = (byte)(((int)(sourceColor.m_A) * ((int)(*covers++) + 1)) >> 8);
                        if (sourceColor.m_A == base_mask)
                        {
                            m_Blender.CopyPixel(p, sourceColor);
                        }
                        else
                        {
                            m_Blender.BlendPixel(p, sourceColor);
                        }
                        p = &p[ScanWidth];
                        sourceColor.m_A = oldAlpha;
                    }
                    while (--len != 0);
                }
            }
#endif
		}

		public void copy_color_hspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			do
			{
				m_Blender.CopyPixels(m_FloatBuffer, bufferOffset, colors[colorsIndex], 1);

				++colorsIndex;
				bufferOffset += m_DistanceInFloatsBetweenPixelsInclusive;
			}
			while (--len != 0);
		}

		public void copy_color_vspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			do
			{
				m_Blender.CopyPixels(m_FloatBuffer, bufferOffset, colors[colorsIndex], 1);

				++colorsIndex;
				bufferOffset += m_StrideInFloats;
			}
			while (--len != 0);
		}

		public void blend_color_hspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);
			m_Blender.BlendPixels(m_FloatBuffer, bufferOffset, colors, colorsIndex, covers, coversIndex, firstCoverForAll, len);
		}

		public void blend_color_vspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			int ScanWidth = StrideInFloatsAbs();
			if (!firstCoverForAll)
			{
				do
				{
					DoCopyOrBlendFloat.BasedOnAlphaAndCover(m_Blender, m_FloatBuffer, bufferOffset, colors[colorsIndex], covers[coversIndex++]);
					bufferOffset += ScanWidth;
					++colorsIndex;
				}
				while (--len != 0);
			}
			else
			{
				if (covers[coversIndex] == 1)
				{
					do
					{
						DoCopyOrBlendFloat.BasedOnAlpha(m_Blender, m_FloatBuffer, bufferOffset, colors[colorsIndex]);
						bufferOffset += ScanWidth;
						++colorsIndex;
					}
					while (--len != 0);
				}
				else
				{
					do
					{
						DoCopyOrBlendFloat.BasedOnAlphaAndCover(m_Blender, m_FloatBuffer, bufferOffset, colors[colorsIndex], covers[coversIndex]);
						bufferOffset += ScanWidth;
						++colorsIndex;
					}
					while (--len != 0);
				}
			}
		}

		public void apply_gamma_inv(GammaLookUpTable g)
		{
			throw new System.NotImplementedException();
			//for_each_pixel(apply_gamma_inv_rgba<color_type, order_type, GammaLut>(g));
		}

		private bool IsPixelVisible(int x, int y)
		{
			RGBA_Floats pixelValue = GetBlender().PixelToColorRGBA_Floats(m_FloatBuffer, GetBufferOffsetXY(x, y));
			return (pixelValue.Alpha0To255 != 0 || pixelValue.Red0To255 != 0 || pixelValue.Green0To255 != 0 || pixelValue.Blue0To255 != 0);
		}

		public void GetVisibleBounds(out RectangleInt visibleBounds)
		{
			visibleBounds = new RectangleInt(0, 0, Width, Height);

			// trim the bottom
			bool aPixelsIsVisible = false;
			for (int y = 0; y < m_Height; y++)
			{
				for (int x = 0; x < m_Width; x++)
				{
					if (IsPixelVisible(x, y))
					{
						visibleBounds.Bottom = y;
						y = m_Height;
						x = m_Width;
						aPixelsIsVisible = true;
					}
				}
			}

			// if we don't run into any pixels set for the top trim than there are no pixels set at all
			if (!aPixelsIsVisible)
			{
				visibleBounds.SetRect(0, 0, 0, 0);
				return;
			}

			// trim the bottom
			for (int y = m_Height - 1; y >= 0; y--)
			{
				for (int x = 0; x < m_Width; x++)
				{
					if (IsPixelVisible(x, y))
					{
						visibleBounds.Top = y + 1;
						y = -1;
						x = m_Width;
					}
				}
			}

			// trim the left
			for (int x = 0; x < m_Width; x++)
			{
				for (int y = 0; y < m_Height; y++)
				{
					if (IsPixelVisible(x, y))
					{
						visibleBounds.Left = x;
						y = m_Height;
						x = m_Width;
					}
				}
			}

			// trim the right
			for (int x = m_Width - 1; x >= 0; x--)
			{
				for (int y = 0; y < m_Height; y++)
				{
					if (IsPixelVisible(x, y))
					{
						visibleBounds.Right = x + 1;
						y = m_Height;
						x = -1;
					}
				}
			}
		}

		public void CropToVisible()
		{
			Vector2 OldOriginOffset = OriginOffset;

			//Move the HotSpot to 0, 0 so PPoint will work the way we want
			OriginOffset = new Vector2(0, 0);

			RectangleInt visibleBounds;
			GetVisibleBounds(out visibleBounds);

			if (visibleBounds.Width == Width
				&& visibleBounds.Height == Height)
			{
				OriginOffset = OldOriginOffset;
				return;
			}

			// check if the Not0Rect has any size
			if (visibleBounds.Width > 0)
			{
				ImageBufferFloat TempImage = new ImageBufferFloat();

				// set TempImage equal to the Not0Rect
				TempImage.Initialize(this, visibleBounds);

				// set the frame equal to the TempImage
				Initialize(TempImage);

				OriginOffset = new Vector2(-visibleBounds.Left + OldOriginOffset.x, -visibleBounds.Bottom + OldOriginOffset.y);
			}
			else
			{
				Deallocate();
			}
		}

		public RectangleInt GetBoundingRect()
		{
			RectangleInt boundingRect = new RectangleInt(0, 0, Width, Height);
			boundingRect.Offset((int)OriginOffset.x, (int)OriginOffset.y);
			return boundingRect;
		}

		private void Initialize(ImageBufferFloat sourceImage)
		{
			RectangleInt sourceBoundingRect = sourceImage.GetBoundingRect();

			Initialize(sourceImage, sourceBoundingRect);
			OriginOffset = sourceImage.OriginOffset;
		}

		private void Initialize(ImageBufferFloat sourceImage, RectangleInt boundsToCopyFrom)
		{
			if (sourceImage == this)
			{
				throw new Exception("We do not create a temp buffer for this to work.  You must have a source distinct from the dest.");
			}
			Deallocate();
			Allocate(boundsToCopyFrom.Width, boundsToCopyFrom.Height, boundsToCopyFrom.Width * sourceImage.BitDepth / 8, sourceImage.BitDepth);
			SetRecieveBlender(sourceImage.GetBlender());

			if (m_Width != 0 && m_Height != 0)
			{
				RectangleInt DestRect = new RectangleInt(0, 0, boundsToCopyFrom.Width, boundsToCopyFrom.Height);
				RectangleInt AbsoluteSourceRect = boundsToCopyFrom;
				// The first thing we need to do is make sure the frame is cleared. LBB [3/15/2004]
				Graphics2D graphics2D = NewGraphics2D();
				graphics2D.Clear(new RGBA_Floats(0, 0, 0, 0));

				int x = -boundsToCopyFrom.Left - (int)sourceImage.OriginOffset.x;
				int y = -boundsToCopyFrom.Bottom - (int)sourceImage.OriginOffset.y;

				graphics2D.Render(sourceImage, x, y, 0, 1, 1);
			}
		}
	}

	public static class DoCopyOrBlendFloat
	{
		private const byte base_mask = 255;

		public static void BasedOnAlpha(IRecieveBlenderFloat Blender, float[] destBuffer, int bufferOffset, RGBA_Floats sourceColor)
		{
			//if (sourceColor.m_A != 0)
			{
#if false // we blend regardless of the alpha so that we can get Light Opacity working (used this way we have additive and faster blending in one blender) LBB
                if (sourceColor.m_A == base_mask)
                {
                    Blender.CopyPixel(pDestBuffer, sourceColor);
                }
                else
#endif
				{
					Blender.BlendPixel(destBuffer, bufferOffset, sourceColor);
				}
			}
		}

		public static void BasedOnAlphaAndCover(IRecieveBlenderFloat Blender, float[] destBuffer, int bufferOffset, RGBA_Floats sourceColor, int cover)
		{
			if (cover == 255)
			{
				BasedOnAlpha(Blender, destBuffer, bufferOffset, sourceColor);
			}
			else
			{
				//if (sourceColor.m_A != 0)
				{
					sourceColor.alpha = sourceColor.alpha * ((float)cover * (1 / 255));
#if false // we blend regardless of the alpha so that we can get Light Opacity working (used this way we have additive and faster blending in one blender) LBB
                    if (sourceColor.m_A == base_mask)
                    {
                        Blender.CopyPixel(pDestBuffer, sourceColor);
                    }
                    else
#endif
					{
						Blender.BlendPixel(destBuffer, bufferOffset, sourceColor);
					}
				}
			}
		}
	};
}