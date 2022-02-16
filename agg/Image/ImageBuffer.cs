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
using System.Collections.Generic;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Image
{
	public class ImageBuffer : IImageByte
	{
		public const int OrderB = 0;
		public const int OrderG = 1;
		public const int OrderR = 2;
		public const int OrderA = 3;

		internal class InternalImageGraphics2D : ImageGraphics2D
		{
			internal InternalImageGraphics2D(ImageBuffer owner)
				: base()
			{
				var rasterizer = new ScanlineRasterizer();
				var imageClippingProxy = new ImageClippingProxy(owner);

				Initialize(imageClippingProxy, rasterizer);
				ScanlineCache = new ScanlineCachePacked8();
			}
		}

		private bool? _hasTransparency = null;

		public bool HasTransparency
		{
			get
			{
				if (BitDepth == 32)
				{
					if (_hasTransparency == null)
					{
						_hasTransparency = false;
						var buffer = GetBuffer();
						// check if the image has any alpha set to something other than 255
						for (int y = 0; y < Height; y++)
						{
							var yOffset = GetBufferOffsetY(y);
							for (int x = 0; x < Width; x++)
							{
								// get the alpha at this pixel
								if (buffer[yOffset + x * 4 + 3] < 255)
								{
									_hasTransparency = true;
									x = Width;
									y = Height;
								}
							}
						}
					}

					return _hasTransparency.Value;
				}

				throw new NotImplementedException();
			}
		}

		protected int[] yTableArray;
		protected int[] xTableArray;
		private byte[] m_ByteBuffer;
		private int bufferOffset; // the beginning of the image in this buffer
		private int bufferFirstPixel; // Pointer to first pixel depending on strideInBytes and image position

		private int strideInBytes; // Number of bytes per row. Can be < 0
		private int DistanceInBytesBetweenPixelsInclusive;

		private IRecieveBlenderByte recieveBlender;

		private const int base_mask = 255;

		public event EventHandler ImageChanged;

		public int ChangedCount { get; private set; }

		public void MarkImageChanged()
		{
			// mark this unchecked as we don't want to throw an exception if this rolls over.
			unchecked
			{
				ChangedCount++;
				_hasTransparency = null;
				ImageChanged?.Invoke(this, null);
			}
		}

		public ImageBuffer()
		{
		}

		public ImageBuffer(IRecieveBlenderByte recieveBlender)
		{
			SetRecieveBlender(recieveBlender);
		}

		public ImageBuffer(IImageByte sourceImage, IRecieveBlenderByte recieveBlender)
		{
			SetDimmensionAndFormat(sourceImage.Width, sourceImage.Height, sourceImage.StrideInBytes(), sourceImage.BitDepth, sourceImage.GetBytesBetweenPixelsInclusive(), true);
			int offset = sourceImage.GetBufferOffsetXY(0, 0);
			byte[] buffer = sourceImage.GetBuffer();
			byte[] newBuffer = new byte[buffer.Length];
			agg_basics.memcpy(newBuffer, offset, buffer, offset, buffer.Length - offset);
			SetBuffer(newBuffer, offset);
			SetRecieveBlender(recieveBlender);
		}

		public ImageBuffer(ImageBuffer sourceImage)
		{
			SetDimmensionAndFormat(sourceImage.Width, sourceImage.Height, sourceImage.StrideInBytes(), sourceImage.BitDepth, sourceImage.GetBytesBetweenPixelsInclusive(), true);
			int offset = sourceImage.GetBufferOffsetXY(0, 0);
			byte[] buffer = sourceImage.GetBuffer();
			byte[] newBuffer = new byte[buffer.Length];
			agg_basics.memcpy(newBuffer, offset, buffer, offset, buffer.Length - offset);
			SetBuffer(newBuffer, offset);
			SetRecieveBlender(sourceImage.GetRecieveBlender());
		}

		public ImageBuffer(int width, int height, int bitsPerPixel = 32)
		{
			Allocate(width, height, width * (bitsPerPixel / 8), bitsPerPixel);
			SetRecieveBlender(new BlenderBGRA());
		}

		public ImageBuffer(int width, int height, int bitsPerPixel, IRecieveBlenderByte recieveBlender)
		{
			Allocate(width, height, width * (bitsPerPixel / 8), bitsPerPixel);
			SetRecieveBlender(recieveBlender);
		}

		public void Allocate(int width, int height, int bitsPerPixel, IRecieveBlenderByte recieveBlender)
		{
			Allocate(width, height, width * (bitsPerPixel / 8), bitsPerPixel);
			SetRecieveBlender(recieveBlender);
		}

#if false
        public ImageBuffer(IImage image, IBlender blender, GammaLookUpTable gammaTable)
        {
            unsafe
            {
                AttachBuffer(image.GetBuffer(), image.Width(), image.Height(), image.StrideInBytes(), image.BitDepth, image.GetDistanceBetweenPixelsInclusive());
            }

            SetRecieveBlender(blender);
        }
#endif

		public ImageBuffer(IImageByte sourceImageToCopy, IRecieveBlenderByte blender, int distanceBetweenPixelsInclusive, int bufferOffset, int bitsPerPixel)
		{
			SetDimmensionAndFormat(sourceImageToCopy.Width, sourceImageToCopy.Height, sourceImageToCopy.StrideInBytes(), bitsPerPixel, distanceBetweenPixelsInclusive, true);
			int offset = sourceImageToCopy.GetBufferOffsetXY(0, 0);
			byte[] buffer = sourceImageToCopy.GetBuffer();
			byte[] newBuffer = new byte[buffer.Length];
			agg_basics.memcpy(newBuffer, offset, buffer, offset, buffer.Length - offset);
			SetBuffer(newBuffer, offset + bufferOffset);
			SetRecieveBlender(blender);
		}

		/// <summary>
		/// This will create a new ImageBuffer that references the same memory as the image that you took the sub image from.
		/// It will modify the original main image when you draw to it.
		/// </summary>
		/// <param name="imageContainingSubImage">The image to reference</param>
		/// <param name="subImageBounds">The bounds to use for the new image</param>
		/// <returns>The new image that references this images memory</returns>
		public static ImageBuffer NewSubImageReference(IImageByte imageContainingSubImage, RectangleDouble subImageBounds)
		{
			var subImage = new ImageBuffer();
			if (subImageBounds.Left < 0 || subImageBounds.Bottom < 0 || subImageBounds.Right > imageContainingSubImage.Width || subImageBounds.Top > imageContainingSubImage.Height
				|| subImageBounds.Left >= subImageBounds.Right || subImageBounds.Bottom >= subImageBounds.Top)
			{
				throw new ArgumentException("The subImageBounds must be on the image and valid.");
			}

			int left = Math.Max(0, (int)Math.Floor(subImageBounds.Left));
			int bottom = Math.Max(0, (int)Math.Floor(subImageBounds.Bottom));
			int width = Math.Min(imageContainingSubImage.Width - left, (int)subImageBounds.Width);
			int height = Math.Min(imageContainingSubImage.Height - bottom, (int)subImageBounds.Height);
			int bufferOffsetToFirstPixel = imageContainingSubImage.GetBufferOffsetXY(left, bottom);
			subImage.AttachBuffer(imageContainingSubImage.GetBuffer(), bufferOffsetToFirstPixel, width, height, imageContainingSubImage.StrideInBytes(), imageContainingSubImage.BitDepth, imageContainingSubImage.GetBytesBetweenPixelsInclusive());
			subImage.SetRecieveBlender(imageContainingSubImage.GetRecieveBlender());

			return subImage;
		}

		public void AttachBuffer(byte[] buffer, int bufferOffset, int width, int height, int strideInBytes, int bitDepth, int distanceInBytesBetweenPixelsInclusive)
		{
			m_ByteBuffer = null;
			SetDimmensionAndFormat(width, height, strideInBytes, bitDepth, distanceInBytesBetweenPixelsInclusive, false);
			SetBuffer(buffer, bufferOffset);
		}

		public void Attach(IImageByte sourceImage, IRecieveBlenderByte recieveBlender, int distanceBetweenPixelsInclusive, int bufferOffset, int bitsPerPixel)
		{
			SetDimmensionAndFormat(sourceImage.Width, sourceImage.Height, sourceImage.StrideInBytes(), bitsPerPixel, distanceBetweenPixelsInclusive, false);
			int offset = sourceImage.GetBufferOffsetXY(0, 0);
			byte[] buffer = sourceImage.GetBuffer();
			SetBuffer(buffer, offset + bufferOffset);
			SetRecieveBlender(recieveBlender);
		}

		public void Attach(IImageByte sourceImage, IRecieveBlenderByte recieveBlender)
		{
			Attach(sourceImage, recieveBlender, sourceImage.GetBytesBetweenPixelsInclusive(), 0, sourceImage.BitDepth);
		}

		public bool Attach(IImageByte sourceImage, int x1, int y1, int x2, int y2)
		{
			m_ByteBuffer = null;
			DettachBuffer();

			if (x1 > x2 || y1 > y2)
			{
				throw new Exception("You need to have your x1 and y1 be the lower left corner of your sub image.");
			}

			var boundsRect = new RectangleInt(x1, y1, x2, y2);
			if (boundsRect.clip(new RectangleInt(0, 0, (int)sourceImage.Width - 1, (int)sourceImage.Height - 1)))
			{
				SetDimmensionAndFormat(boundsRect.Width, boundsRect.Height, sourceImage.StrideInBytes(), sourceImage.BitDepth, sourceImage.GetBytesBetweenPixelsInclusive(), false);
				int bufferOffset = sourceImage.GetBufferOffsetXY(boundsRect.Left, boundsRect.Bottom);
				byte[] buffer = sourceImage.GetBuffer();
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
			byte[] buffer = GetBuffer(out int offset);
			for (int i = 0; i < numPixels; i++)
			{
				buffer[offset + i * 4 + 3] = value;
			}
		}

		private void Deallocate()
		{
			if (m_ByteBuffer != null)
			{
				m_ByteBuffer = null;
				SetDimmensionAndFormat(0, 0, 0, 32, 4, true);
			}
		}

		public void Allocate(int inWidth, int inHeight, int inScanWidthInBytes, int bitsPerPixel)
		{
			if (bitsPerPixel != 32 && bitsPerPixel != 24 && bitsPerPixel != 8)
			{
				throw new Exception("Unsupported bits per pixel.");
			}

			if (inScanWidthInBytes < inWidth * (bitsPerPixel / 8))
			{
				throw new Exception("Your scan width is not big enough to hold your width and height.");
			}

			SetDimmensionAndFormat(inWidth, inHeight, inScanWidthInBytes, bitsPerPixel, bitsPerPixel / 8, true);

			if (m_ByteBuffer == null || m_ByteBuffer.Length != strideInBytes * Height)
			{
				m_ByteBuffer = new byte[strideInBytes * Height];
				SetUpLookupTables();
			}

			if (yTableArray.Length != inHeight
				|| xTableArray.Length != inWidth)
			{
				throw new Exception("The yTable and xTable should be allocated correctly at this point. Figure out what happened."); // LBB, don't fix this if you don't understand what it's trying to do.
			}
		}

		public MatterHackers.Agg.Graphics2D NewGraphics2D()
		{
			var imageRenderer = new InternalImageGraphics2D(this);

			imageRenderer.Rasterizer.SetVectorClipBox(0, 0, Width, Height);

			return imageRenderer;
		}

		public void CopyFrom(IImageByte sourceImage)
		{
			if (this == sourceImage)
            {
				return;
            }

			Allocate(sourceImage.Width, sourceImage.Height, sourceImage.StrideInBytesAbs(), sourceImage.BitDepth);

			// make sure we make an exact copy
			SetRecieveBlender(new BlenderBGRAExactCopy());
			CopyFrom(sourceImage, sourceImage.GetBounds(), 0, 0);

			// then set the blender to what we expect
			SetRecieveBlender(sourceImage.GetRecieveBlender());
		
			// and finally let anything that cares know we change the image
			MarkImageChanged();
		}

		protected void CopyFromNoClipping(IImageByte sourceImage, RectangleInt clippedSourceImageRect, int destXOffset, int destYOffset)
		{
			if (GetBytesBetweenPixelsInclusive() != BitDepth / 8
				|| sourceImage.GetBytesBetweenPixelsInclusive() != sourceImage.BitDepth / 8)
			{
				throw new Exception("WIP we only support packed pixel formats at this time.");
			}

			if (BitDepth == sourceImage.BitDepth)
			{
				int lengthInBytes = clippedSourceImageRect.Width * GetBytesBetweenPixelsInclusive();

				int sourceOffset = sourceImage.GetBufferOffsetXY(clippedSourceImageRect.Left, clippedSourceImageRect.Bottom);
				byte[] sourceBuffer = sourceImage.GetBuffer();
				byte[] destBuffer = GetPixelPointerXY(clippedSourceImageRect.Left + destXOffset, clippedSourceImageRect.Bottom + destYOffset, out int destOffset);

				for (int i = 0; i < clippedSourceImageRect.Height; i++)
				{
					agg_basics.memmove(destBuffer, destOffset, sourceBuffer, sourceOffset, lengthInBytes);
					sourceOffset += sourceImage.StrideInBytes();
					destOffset += StrideInBytes();
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
										byte[] sourceBuffer = sourceImage.GetBuffer();
										byte[] destBuffer = GetPixelPointerXY(
											clippedSourceImageRect.Left + destXOffset,
											clippedSourceImageRect.Bottom + i + destYOffset,
											out int destOffset);
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

		public void CopyFrom(IImageByte sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset)
		{
			RectangleInt sourceImageBounds = sourceImage.GetBounds();
			var clippedSourceImageRect = default(RectangleInt);
			if (clippedSourceImageRect.IntersectRectangles(sourceImageRect, sourceImageBounds))
			{
				RectangleInt destImageRect = clippedSourceImageRect;
				destImageRect.Offset(destXOffset, destYOffset);
				RectangleInt destImageBounds = GetBounds();
				var clippedDestImageRect = default(RectangleInt);
				if (clippedDestImageRect.IntersectRectangles(destImageRect, destImageBounds))
				{
					// we need to make sure the source is also clipped to the dest. So, we'll copy this back to source and offset it.
					clippedSourceImageRect = clippedDestImageRect;
					clippedSourceImageRect.Offset(-destXOffset, -destYOffset);
					CopyFromNoClipping(sourceImage, clippedSourceImageRect, destXOffset, destYOffset);
				}
			}
		}

		public Vector2 OriginOffset { get; set; }

		/// <summary>
		/// Gets width in pixels
		/// </summary>
		public int Width { get; private set; }

		/// <summary>
		/// Gets height in pixels
		/// </summary>
		public int Height { get; private set; }

		public int StrideInBytes()
		{
			return strideInBytes;
		}

		public int StrideInBytesAbs()
		{
			return System.Math.Abs(strideInBytes);
		}

		public int GetBytesBetweenPixelsInclusive()
		{
			return DistanceInBytesBetweenPixelsInclusive;
		}

		public int BitDepth { get; private set; }

		public virtual RectangleInt GetBounds()
		{
			return new RectangleInt(-(int)OriginOffset.X, -(int)OriginOffset.Y, Width - (int)OriginOffset.X, Height - (int)OriginOffset.Y);
		}

		public IRecieveBlenderByte GetRecieveBlender()
		{
			return recieveBlender;
		}

		public void SetRecieveBlender(IRecieveBlenderByte value)
		{
			if (BitDepth != 0 && value != null && value.NumPixelBits != BitDepth)
			{
				throw new NotSupportedException("The blender has to support the bit depth of this image.");
			}

			recieveBlender = value;
		}

		private void SetUpLookupTables()
		{
			yTableArray = new int[Height];
			for (int i = 0; i < Height; i++)
			{
				yTableArray[i] = i * strideInBytes;
			}

			xTableArray = new int[Width];
			for (int i = 0; i < Width; i++)
			{
				xTableArray[i] = i * DistanceInBytesBetweenPixelsInclusive;
			}
		}

		public void InvertYLookupTable()
		{
			strideInBytes *= -1;
			bufferFirstPixel = bufferOffset;
			if (strideInBytes < 0)
			{
				int addAmount = -((int)((int)Height - 1) * strideInBytes);
				bufferFirstPixel = addAmount + bufferOffset;
			}

			SetUpLookupTables();
		}

		/// <summary>
		/// Flip pixels in the Y axis
		/// </summary>
		public void FlipY()
		{
			byte[] buffer = GetBuffer();
			for (int y = 0; y < Height / 2; y++)
			{
				int bottomBuffer = GetBufferOffsetY(y);
				int topBuffer = GetBufferOffsetY(Height - y - 1);
				for (int x = 0; x < StrideInBytes(); x++)
				{
					byte hold = buffer[bottomBuffer + x];
					buffer[bottomBuffer + x] = buffer[topBuffer + x];
					buffer[topBuffer + x] = hold;
				}
			}
		}

		/// <summary>
		/// Flip pixels in the X axis
		/// </summary>
		public void FlipX()
		{
			byte[] buffer = GetBuffer();

			// Iterate each row, swapping pixels in x from outer to midpoint
			for (int y = 0; y < this.Height; y++)
			{
				for (int x = 0; x < this.Width / 2; x++)
				{
					int leftOffset = GetBufferOffsetXY(x, y);
					int rightOffset = GetBufferOffsetXY(this.Width - x - 1, y);

					// Hold
					byte leftBuffer0 = buffer[leftOffset + 0];
					byte leftBuffer1 = buffer[leftOffset + 1];
					byte leftBuffer2 = buffer[leftOffset + 2];
					byte leftBuffer3 = buffer[leftOffset + 3];

					// Swap
					buffer[leftOffset + 0] = buffer[rightOffset + 0];
					buffer[leftOffset + 1] = buffer[rightOffset + 1];
					buffer[leftOffset + 2] = buffer[rightOffset + 2];
					buffer[leftOffset + 3] = buffer[rightOffset + 3];

					buffer[rightOffset + 0] = leftBuffer0;
					buffer[rightOffset + 1] = leftBuffer1;
					buffer[rightOffset + 2] = leftBuffer2;
					buffer[rightOffset + 3] = leftBuffer3;
				}
			}
		}

		public void SetBuffer(byte[] byteBuffer, int bufferOffset)
		{
			if (byteBuffer.Length < Height * strideInBytes)
			{
				throw new Exception("Your buffer does not have enough room for your height and strideInBytes.");
			}

			m_ByteBuffer = byteBuffer;
			this.bufferOffset = bufferFirstPixel = bufferOffset;
			if (strideInBytes < 0)
			{
				int addAmount = -((int)((int)Height - 1) * strideInBytes);
				bufferFirstPixel = addAmount + bufferOffset;
			}

			SetUpLookupTables();
		}

		private void DeallocateOrClearBuffer(int width, int height, int strideInBytes, int bitDepth, int distanceInBytesBetweenPixelsInclusive)
		{
			if (this.Width == width
				&& this.Height == height
				&& this.strideInBytes == strideInBytes
				&& this.BitDepth == bitDepth
				&& DistanceInBytesBetweenPixelsInclusive == distanceInBytesBetweenPixelsInclusive
				&& m_ByteBuffer != null)
			{
				for (int i = 0; i < m_ByteBuffer.Length; i++)
				{
					m_ByteBuffer[i] = 0;
				}

				return;
			}
			else
			{
				Deallocate();
			}
		}

		private void SetDimmensionAndFormat(int width, int height, int strideInBytes, int bitDepth, int distanceInBytesBetweenPixelsInclusive, bool doDeallocateOrClearBuffer)
		{
			if (doDeallocateOrClearBuffer)
			{
				DeallocateOrClearBuffer(width, height, strideInBytes, bitDepth, distanceInBytesBetweenPixelsInclusive);
			}

			this.Width = width;
			this.Height = height;
			this.strideInBytes = strideInBytes;
			this.BitDepth = bitDepth;
			if (distanceInBytesBetweenPixelsInclusive > 4)
			{
				throw new System.Exception("It looks like you are passing bits per pixel rather than distance in bytes.");
			}

			if (distanceInBytesBetweenPixelsInclusive < (bitDepth / 8))
			{
				throw new Exception("You do not have enough room between pixels to support your bit depth.");
			}

			DistanceInBytesBetweenPixelsInclusive = distanceInBytesBetweenPixelsInclusive;
			if (strideInBytes < distanceInBytesBetweenPixelsInclusive * width)
			{
				throw new Exception("You do not have enough strideInBytes to hold the width and pixel distance you have described.");
			}
		}

		public void DettachBuffer()
		{
			m_ByteBuffer = null;
			Width = Height = strideInBytes = DistanceInBytesBetweenPixelsInclusive = 0;
		}

		public byte[] GetBuffer()
		{
			return m_ByteBuffer;
		}

		public byte[] GetBuffer(out int bufferOffset)
		{
			bufferOffset = this.bufferOffset;
			return m_ByteBuffer;
		}

		public byte[] GetPixelPointerY(int y, out int bufferOffset)
		{
			bufferOffset = bufferFirstPixel + yTableArray[y];
			// bufferOffset = GetBufferOffsetXY(0, y);
			return m_ByteBuffer;
		}

		public byte[] GetPixelPointerXY(int x, int y, out int bufferOffset)
		{
			bufferOffset = GetBufferOffsetXY(x, y);
			return m_ByteBuffer;
		}

		public Color GetPixel(int x, int y)
		{
			return recieveBlender.PixelToColor(m_ByteBuffer, GetBufferOffsetXY(x, y));
		}

		public int GetBufferOffsetY(int y)
		{
			return bufferFirstPixel + yTableArray[y] + xTableArray[0];
		}

		public int GetBufferOffsetXY(int x, int y)
		{
			return bufferFirstPixel + yTableArray[y] + xTableArray[x];
		}

		public void copy_pixel(int x, int y, byte[] c, int ByteOffset)
		{
			throw new System.NotImplementedException();
			// byte* p = GetPixelPointerXY(x, y);
			// ((int*)p)[0] = ((int*)c)[0];
			// p[OrderR] = c.r;
			// p[OrderG] = c.g;
			// p[OrderB] = c.b;
			// p[OrderA] = c.a;
		}

		public void BlendPixel(int x, int y, Color c, byte cover)
		{
			throw new System.NotImplementedException();
			/*
			cob_type::copy_or_blend_pix(
				(value_type*)m_rbuf->row_ptr(x, y, 1)  + x + x + x,
				c.r, c.g, c.b, c.a,
				cover);*/
		}

		public void SetPixel(int x, int y, Color color)
		{
			x -= (int)OriginOffset.X;
			y -= (int)OriginOffset.Y;
			recieveBlender.CopyPixels(GetBuffer(), GetBufferOffsetXY(x, y), color, 1);
		}

		public void copy_hline(int x, int y, int len, Color sourceColor)
		{
			byte[] buffer = GetPixelPointerXY(x, y, out int bufferOffset);

			recieveBlender.CopyPixels(buffer, bufferOffset, sourceColor, len);
		}

		public void copy_vline(int x, int y, int len, Color sourceColor)
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

		public void blend_hline(int x1, int y, int x2, Color sourceColor, byte cover)
		{
			if (sourceColor.alpha != 0)
			{
				int len = x2 - x1 + 1;

				byte[] buffer = GetPixelPointerXY(x1, y, out int bufferOffset);

				int alpha = ((int)sourceColor.alpha * (cover + 1)) >> 8;
				if (alpha == base_mask)
				{
					recieveBlender.CopyPixels(buffer, bufferOffset, sourceColor, len);
				}
				else
				{
					do
					{
						recieveBlender.BlendPixel(buffer, bufferOffset, new Color(sourceColor.red, sourceColor.green, sourceColor.blue, alpha));
						bufferOffset += DistanceInBytesBetweenPixelsInclusive;
					}
					while (--len != 0);
				}
			}
		}

		public void blend_vline(int x, int y1, int y2, Color sourceColor, byte cover)
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

		public void blend_solid_hspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex)
		{
			int colorAlpha = sourceColor.alpha;
			if (colorAlpha != 0)
			{
				unchecked
				{
					byte[] buffer = GetPixelPointerXY(x, y, out int bufferOffset);

					do
					{
						int alpha = (colorAlpha * (covers[coversIndex] + 1)) >> 8;
						if (alpha == base_mask)
						{
							recieveBlender.CopyPixels(buffer, bufferOffset, sourceColor, 1);
						}
						else
						{
							recieveBlender.BlendPixel(buffer, bufferOffset, new Color(sourceColor.red, sourceColor.green, sourceColor.blue, alpha));
						}

						bufferOffset += DistanceInBytesBetweenPixelsInclusive;
						coversIndex++;
					}
					while (--len != 0);
				}
			}
		}

		public void blend_solid_vspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex)
		{
			if (sourceColor.alpha != 0)
			{
				int scanWidthInBytes = StrideInBytes();
				unchecked
				{
					int bufferOffset = GetBufferOffsetXY(x, y);
					do
					{
						byte oldAlpha = sourceColor.alpha;
						sourceColor.alpha = (byte)(((int)sourceColor.alpha * ((int)covers[coversIndex++] + 1)) >> 8);
						if (sourceColor.alpha == base_mask)
						{
							recieveBlender.CopyPixels(m_ByteBuffer, bufferOffset, sourceColor, 1);
						}
						else
						{
							recieveBlender.BlendPixel(m_ByteBuffer, bufferOffset, sourceColor);
						}

						bufferOffset += scanWidthInBytes;
						sourceColor.alpha = oldAlpha;
					}
					while (--len != 0);
				}
			}
		}

		public void copy_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			do
			{
				recieveBlender.CopyPixels(m_ByteBuffer, bufferOffset, colors[colorsIndex], 1);

				++colorsIndex;
				bufferOffset += DistanceInBytesBetweenPixelsInclusive;
			}
			while (--len != 0);
		}

		public void copy_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			do
			{
				recieveBlender.CopyPixels(m_ByteBuffer, bufferOffset, colors[colorsIndex], 1);

				++colorsIndex;
				bufferOffset += strideInBytes;
			}
			while (--len != 0);
		}

		public void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);
			recieveBlender.BlendPixels(m_ByteBuffer, bufferOffset, colors, colorsIndex, covers, coversIndex, firstCoverForAll, len);
		}

		public void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int bufferOffset = GetBufferOffsetXY(x, y);

			int scanWidth = StrideInBytesAbs();
			if (!firstCoverForAll)
			{
				do
				{
					DoCopyOrBlend.BasedOnAlphaAndCover(recieveBlender, m_ByteBuffer, bufferOffset, colors[colorsIndex], covers[coversIndex++]);
					bufferOffset += scanWidth;
					++colorsIndex;
				}
				while (--len != 0);
			}
			else
			{
				if (covers[coversIndex] == 255)
				{
					do
					{
						DoCopyOrBlend.BasedOnAlpha(recieveBlender, m_ByteBuffer, bufferOffset, colors[colorsIndex]);
						bufferOffset += scanWidth;
						++colorsIndex;
					}
					while (--len != 0);
				}
				else
				{
					do
					{
						DoCopyOrBlend.BasedOnAlphaAndCover(recieveBlender, m_ByteBuffer, bufferOffset, colors[colorsIndex], covers[coversIndex]);
						bufferOffset += scanWidth;
						++colorsIndex;
					}
					while (--len != 0);
				}
			}
		}

		public void ApplyGammaInv(GammaLookUpTable g)
		{
			throw new System.NotImplementedException();
			// for_each_pixel(apply_gamma_inv_rgba<color_type, order_type, GammaLut>(g));
		}

		private bool IsPixelVisible(int x, int y, Func<Color, bool> isVisible)
		{
			Color pixelValue = GetRecieveBlender().PixelToColor(m_ByteBuffer, GetBufferOffsetXY(x, y));
			return isVisible(pixelValue);
		}

		/// <summary>
		/// Calculate the visible bounds (set pixel) of a given images.
		/// </summary>
		/// <param name="visibleBounds">The returned bounds of the visible pixels</param>
		/// <param name="isVisible">An optional function for defining the criteria of visibility</param>
		public void GetVisibleBounds(out RectangleInt visibleBounds, Func<Color, bool> isVisible = null)
		{
			if (isVisible == null)
			{
				isVisible = (pixelValue) =>
				{
					return pixelValue.Alpha0To255 != 0
						|| pixelValue.Red0To255 != 0
						|| pixelValue.Green0To255 != 0
						|| pixelValue.Blue0To255 != 0;
				};
			}

			visibleBounds = new RectangleInt(0, 0, Width, Height);

			// trim the bottom
			bool aPixelsIsVisible = false;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (IsPixelVisible(x, y, isVisible))
					{
						visibleBounds.Bottom = y;
						y = Height;
						x = Width;
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

			// trim the top
			for (int y = Height - 1; y >= 0; y--)
			{
				for (int x = 0; x < Width; x++)
				{
					if (IsPixelVisible(x, y, isVisible))
					{
						visibleBounds.Top = y + 1;
						y = -1;
						x = Width;
					}
				}
			}

			// trim the left
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					if (IsPixelVisible(x, y, isVisible))
					{
						visibleBounds.Left = x;
						y = Height;
						x = Width;
					}
				}
			}

			// trim the right
			for (int x = Width - 1; x >= 0; x--)
			{
				for (int y = 0; y < Height; y++)
				{
					if (IsPixelVisible(x, y, isVisible))
					{
						visibleBounds.Right = x + 1;
						y = Height;
						x = -1;
					}
				}
			}
		}

		public void CropToVisibleInPlace(Func<Color, bool> isVisible = null, bool clearOriginOffset = true)
		{
			var cropped = this.CropToVisible(isVisible);
			if(clearOriginOffset)
            {
				cropped.OriginOffset = Vector2.Zero;
			}

			this.CopyFrom(cropped);
		}

		public ImageBuffer CropToVisible(Func<Color, bool> isVisible = null)
		{
			Vector2 oldOriginOffset = OriginOffset;

			// Move the HotSpot to 0, 0 so PPoint will work the way we want
			OriginOffset = new Vector2(0, 0);

			GetVisibleBounds(out RectangleInt visibleBounds, isVisible);

			if (visibleBounds.Width == Width
				&& visibleBounds.Height == Height)
			{
				OriginOffset = oldOriginOffset;
				return this;
			}

			// check if the Not0Rect has any size
			if (visibleBounds.Width > 0)
			{
				var tempImage = new ImageBuffer();

				// set TempImage equal to the Not0Rect
				tempImage.Initialize(this, visibleBounds);

				tempImage.OriginOffset = new Vector2(-visibleBounds.Left + oldOriginOffset.X, -visibleBounds.Bottom + oldOriginOffset.Y);

				return tempImage;
			}
			else
			{
				return new ImageBuffer();
			}
		}

		public static bool operator ==(ImageBuffer a, ImageBuffer b)
		{
			if (a is null || b is null)
			{
				if (a is null && b is null)
				{
					return true;
				}

				return false;
			}

			return a.Equals(b, 0);
		}

		public static bool operator !=(ImageBuffer a, ImageBuffer b)
		{
			bool areEqual = a == b;
			return !areEqual;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() == typeof(ImageBuffer))
			{
				return this == (ImageBuffer)obj;
			}

			return false;
		}

		public bool Equals(ImageBuffer b, int maxError)
		{
			if (Width == b.Width
				&& Height == b.Height
				&& BitDepth == b.BitDepth
				&& StrideInBytes() == b.StrideInBytes()
				&& OriginOffset == b.OriginOffset)
			{
				int bytesPerPixel = BitDepth / 8;
				int aDistanceBetweenPixels = GetBytesBetweenPixelsInclusive();
				int bDistanceBetweenPixels = b.GetBytesBetweenPixelsInclusive();
				byte[] aBuffer = GetBuffer();
				byte[] bBuffer = b.GetBuffer();
				for (int y = 0; y < Height; y++)
				{
					int aBufferOffset = GetBufferOffsetY(y);
					int bBufferOffset = b.GetBufferOffsetY(y);
					for (int x = 0; x < Width; x++)
					{
						for (int byteIndex = 0; byteIndex < bytesPerPixel; byteIndex++)
						{
							byte aByte = aBuffer[aBufferOffset + byteIndex];
							byte bByte = bBuffer[bBufferOffset + byteIndex];
							if (aByte < (bByte - maxError) || aByte > (bByte + maxError))
							{
								return false;
							}
						}

						aBufferOffset += aDistanceBetweenPixels;
						bBufferOffset += bDistanceBetweenPixels;
					}
				}

				return true;
			}

			return false;
		}

		public bool Contains(ImageBuffer imageToFind, int maxError = 0)
		{
			return Contains(imageToFind, out _, out _, maxError);
		}

		public bool Contains(ImageBuffer imageToFind, out int matchX, out int matchY, int maxError = 0)
		{
			matchX = 0;
			matchY = 0;
			if (Width >= imageToFind.Width
				&& Height >= imageToFind.Height
				&& BitDepth == imageToFind.BitDepth)
			{
				int bytesPerPixel = BitDepth / 8;
				int aDistanceBetweenPixels = GetBytesBetweenPixelsInclusive();
				int bDistanceBetweenPixels = imageToFind.GetBytesBetweenPixelsInclusive();
				byte[] thisBuffer = GetBuffer();
				byte[] containedBuffer = imageToFind.GetBuffer();
				for (matchY = 0; matchY <= Height - imageToFind.Height; matchY++)
				{
					for (matchX = 0; matchX <= Width - imageToFind.Width; matchX++)
					{
						bool foundBadMatch = false;
						for (int imageToFindY = 0; imageToFindY < imageToFind.Height; imageToFindY++)
						{
							int thisBufferOffset = GetBufferOffsetXY(matchX, matchY + imageToFindY);
							int imageToFindBufferOffset = imageToFind.GetBufferOffsetY(imageToFindY);
							for (int imageToFindX = 0; imageToFindX < imageToFind.Width; imageToFindX++)
							{
								for (int byteIndex = 0; byteIndex < bytesPerPixel; byteIndex++)
								{
									byte aByte = thisBuffer[thisBufferOffset + byteIndex];
									byte bByte = containedBuffer[imageToFindBufferOffset + byteIndex];
									if (aByte < (bByte - maxError) || aByte > (bByte + maxError))
									{
										foundBadMatch = true;
										byteIndex = bytesPerPixel;
										imageToFindX = imageToFind.Width;
										imageToFindY = imageToFind.Height;
									}
								}

								thisBufferOffset += aDistanceBetweenPixels;
								imageToFindBufferOffset += bDistanceBetweenPixels;
							}
						}

						if (!foundBadMatch)
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool FindLeastSquaresMatch(ImageBuffer imageToFind, double maxError)
		{
			return FindLeastSquaresMatch(imageToFind, out _, out _, maxError);
		}

		public bool FindLeastSquaresMatch(ImageBuffer imageToFind, out Vector2 bestPosition, out double bestLeastSquares, double maxError = double.MaxValue)
		{
			bestPosition = Vector2.Zero;
			bestLeastSquares = double.MaxValue;

			if (Width >= imageToFind.Width
				&& Height >= imageToFind.Height
				&& BitDepth == imageToFind.BitDepth)
			{
				int bytesPerPixel = BitDepth / 8;
				int aDistanceBetweenPixels = GetBytesBetweenPixelsInclusive();
				int bDistanceBetweenPixels = imageToFind.GetBytesBetweenPixelsInclusive();
				byte[] thisBuffer = GetBuffer();
				byte[] containedBuffer = imageToFind.GetBuffer();
				for (int matchY = 0; matchY <= Height - imageToFind.Height; matchY++)
				{
					for (int matchX = 0; matchX <= Width - imageToFind.Width; matchX++)
					{
						double currentLeastSquares = 0;

						for (int imageToFindY = 0; imageToFindY < imageToFind.Height; imageToFindY++)
						{
							int thisBufferOffset = GetBufferOffsetXY(matchX, matchY + imageToFindY);
							int imageToFindBufferOffset = imageToFind.GetBufferOffsetY(imageToFindY);
							for (int imageToFindX = 0; imageToFindX < imageToFind.Width; imageToFindX++)
							{
								for (int byteIndex = 0; byteIndex < bytesPerPixel; byteIndex++)
								{
									byte aByte = thisBuffer[thisBufferOffset + byteIndex];
									byte bByte = containedBuffer[imageToFindBufferOffset + byteIndex];
									int difference = (int)aByte - (int)bByte;
									currentLeastSquares += difference * difference;
								}

								thisBufferOffset += aDistanceBetweenPixels;
								imageToFindBufferOffset += bDistanceBetweenPixels;
								if (currentLeastSquares > maxError)
								{
									// stop checking we have too much error.
									imageToFindX = imageToFind.Width;
									imageToFindY = imageToFind.Height;
								}
							}
						}

						if (currentLeastSquares < bestLeastSquares)
						{
							bestPosition = new Vector2(matchX, matchY);
							bestLeastSquares = currentLeastSquares;
							if (maxError > currentLeastSquares)
							{
								maxError = currentLeastSquares;
								if (currentLeastSquares == 0)
								{
									return true;
								}
							}
						}
					}
				}
			}

			return bestLeastSquares <= maxError;
		}

		public override int GetHashCode()
		{
			// This might be hard to make fast and useful.
			return m_ByteBuffer.GetHashCode() ^ bufferOffset.GetHashCode() ^ bufferFirstPixel.GetHashCode();
		}

		public RectangleInt GetBoundingRect()
		{
			var boundingRect = new RectangleInt(0, 0, Width, Height);
			boundingRect.Offset((int)OriginOffset.X, (int)OriginOffset.Y);
			return boundingRect;
		}

		private void Initialize(ImageBuffer sourceImage)
		{
			RectangleInt sourceBoundingRect = sourceImage.GetBoundingRect();

			Initialize(sourceImage, sourceBoundingRect);
			OriginOffset = sourceImage.OriginOffset;
		}

		private void Initialize(ImageBuffer sourceImage, RectangleInt boundsToCopyFrom)
		{
			if (sourceImage == this)
			{
				throw new Exception("We do not create a temp buffer for this to work.  You must have a source distinct from the dest.");
			}

			Deallocate();
			Allocate(boundsToCopyFrom.Width, boundsToCopyFrom.Height, boundsToCopyFrom.Width * sourceImage.BitDepth / 8, sourceImage.BitDepth);
			SetRecieveBlender(sourceImage.GetRecieveBlender());

			if (Width != 0 && Height != 0)
			{
				// The first thing we need to do is make sure the frame is cleared. LBB [3/15/2004]
				MatterHackers.Agg.Graphics2D graphics2D = NewGraphics2D();
				graphics2D.Clear(new Color(0, 0, 0, 0));

				int x = -boundsToCopyFrom.Left - (int)sourceImage.OriginOffset.X;
				int y = -boundsToCopyFrom.Bottom - (int)sourceImage.OriginOffset.Y;

				graphics2D.Render(sourceImage, x, y, 0, 1, 1);
			}
		}

		public void SetPixel32(int p, int p_2, uint p_3)
		{
			throw new NotImplementedException();
		}

		public uint GetPixel32(double p, double p_2)
		{
			throw new NotImplementedException();
		}
	}

	public static class ImageBufferExtensionMethods
	{
		/// <summary>
		/// Flip pixels in the X axis
		/// </summary>
		public static ImageBuffer MirrorX(this ImageBuffer input)
		{
			var mirrored = new ImageBuffer(input);
			mirrored.FlipX();
			return mirrored;
		}

		/// <summary>
		/// Flip pixels in the Y axis
		/// </summary>
		public static ImageBuffer MirrorY(this ImageBuffer input)
		{
			var mirrored = new ImageBuffer(input);
			mirrored.FlipY();
			return mirrored;
		}

		public static ImageBuffer CreateScaledImage(this ImageBuffer unscaledSourceImage, double width, double height)
		{
			return CreateScaledImage(unscaledSourceImage, (int)Math.Round(width), (int)Math.Round(height));
		}

		/// <summary>
		/// Get the weighted center (considering the amount that pixels are set) of the image
		/// </summary>
		/// <param name="imageBuffer">The image to get the center of</param>
		/// <param name="thresholdFunction">The function that describes if a pixel should be considered (> .5 will be accumulated).</param>
		/// <returns>The weighted center position of the image</returns>
		public static Vector2 GetWeightedCenter(this ImageBuffer imageBuffer, IThresholdFunction thresholdFunction)
		{
			double accumulatedCount = 0;
			Vector2 accumulatedPosition = Vector2.Zero;
			for (int y = 0; y < imageBuffer.Height; y++)
			{
				for (int x = 0; x < imageBuffer.Width; x++)
				{
					Color color = imageBuffer.GetPixel(x, y);
					if (thresholdFunction.Threshold(color) > .5)
					{
						accumulatedCount++;
						double px = x;
						double py = y;
						accumulatedPosition += new Vector2(px, py);
					}
				}
			}

			return accumulatedPosition / accumulatedCount;
		}

		public static Circle GetCircleBounds(this ImageBuffer image)
		{
			var outsidePoints = new List<Vector2>();

			// get the first pixel position for each y
			// get the last pixel position for each y

			return SmallestEnclosingCircle.MakeCircle(outsidePoints);
		}

		public static ImageBuffer CreateScaledImage(this ImageBuffer sourceImage, int width, int height)
		{
			var destImage = new ImageBuffer(width, height, 32, sourceImage.GetRecieveBlender());

			// If the source image is more than twice as big as our dest image.
			while (sourceImage.Width >= destImage.Width * 2)
			{
				// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
				// So we scale as many times as we need to get the Image to be the right size.
				// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
				var halfImage = new ImageBuffer(sourceImage.Width / 2, sourceImage.Height / 2, 32, sourceImage.GetRecieveBlender());
				halfImage.NewGraphics2D().Render(sourceImage, 0, 0, 0, halfImage.Width / (double)sourceImage.Width, halfImage.Height / (double)sourceImage.Height);
				sourceImage = halfImage;

				if (sourceImage.Width == width)
				{
					return sourceImage;
				}
			}

			Graphics2D renderGraphics = destImage.NewGraphics2D();
			renderGraphics.ImageRenderQuality = Graphics2D.TransformQuality.Best;

			renderGraphics.Render(sourceImage, 0, 0, 0, destImage.Width / (double)sourceImage.Width, destImage.Height / (double)sourceImage.Height);

			return destImage;
		}

		public static void ScaleImage(this ImageBuffer image, double ratio)
		{
			var scaled = CreateScaledImage(image, image.Width * ratio, image.Height * ratio);
			image.CopyFrom(scaled);
		}

		public static ImageBuffer CreateScaledImage(this ImageBuffer image, double ratio)
		{
			return CreateScaledImage(image, image.Width * ratio, image.Height * ratio);
		}

		public static ImageBuffer ToGrayscale(this ImageBuffer sourceImage)
		{
			var outputImage = new ImageBuffer(sourceImage.Width, sourceImage.Height, 8, new blender_gray(1));
			outputImage.NewGraphics2D().Render(sourceImage, 0, 0);

			return outputImage;
		}
	}

	public static class DoCopyOrBlend
	{
		private const byte base_mask = 255;

		public static void BasedOnAlpha(IRecieveBlenderByte recieveBlender, byte[] destBuffer, int bufferOffset, Color sourceColor)
		{
			// if (sourceColor.m_A != 0)
			{
#if false // we blend regardless of the alpha so that we can get Light Opacity working (used this way we have additive and faster blending in one blender) LBB
                if (sourceColor.m_A == base_mask)
                {
                    Blender.CopyPixel(pDestBuffer, sourceColor);
                }
                else
#endif
				{
					recieveBlender.BlendPixel(destBuffer, bufferOffset, sourceColor);
				}
			}
		}

		public static void BasedOnAlphaAndCover(IRecieveBlenderByte recieveBlender, byte[] destBuffer, int bufferOffset, Color sourceColor, int cover)
		{
			if (cover == 255)
			{
				BasedOnAlpha(recieveBlender, destBuffer, bufferOffset, sourceColor);
			}
			else
			{
				// if (sourceColor.m_A != 0)
				{
					sourceColor.alpha = (byte)((sourceColor.alpha * (cover + 1)) >> 8);
#if false // we blend regardless of the alpha so that we can get Light Opacity working (used this way we have additive and faster blending in one blender) LBB
                    if (sourceColor.m_A == base_mask)
                    {
                        Blender.CopyPixel(pDestBuffer, sourceColor);
                    }
                    else
#endif
					{
						recieveBlender.BlendPixel(destBuffer, bufferOffset, sourceColor);
					}
				}
			}
		}
	}
}