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

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Image
{
	public abstract class ImageProxy : IImageByte
	{
		protected IImageByte linkedImage;

		public IImageByte LinkedImage
		{
			get
			{
				return linkedImage;
			}

			set
			{
				linkedImage = value;
			}
		}

		public ImageProxy(IImageByte linkedImage)
		{
			this.linkedImage = linkedImage;
		}

		public virtual void LinkToImage(IImageByte linkedImage)
		{
			this.linkedImage = linkedImage;
		}

		public virtual Vector2 OriginOffset
		{
			get { return linkedImage.OriginOffset; }
			set { linkedImage.OriginOffset = value; }
		}

		public virtual int Width
		{
			get
			{
				return linkedImage.Width;
			}
		}

		public virtual int Height
		{
			get
			{
				return linkedImage.Height;
			}
		}

		public virtual int StrideInBytes()
		{
			return linkedImage.StrideInBytes();
		}

		public virtual int StrideInBytesAbs()
		{
			return linkedImage.StrideInBytesAbs();
		}

		public virtual RectangleInt GetBounds()
		{
			return linkedImage.GetBounds();
		}

		public Graphics2D NewGraphics2D()
		{
			return linkedImage.NewGraphics2D();
		}

		public IRecieveBlenderByte GetRecieveBlender()
		{
			return linkedImage.GetRecieveBlender();
		}

		public void SetRecieveBlender(IRecieveBlenderByte value)
		{
			linkedImage.SetRecieveBlender(value);
		}

		public virtual RGBA_Bytes GetPixel(int x, int y)
		{
			return linkedImage.GetPixel(x, y);
		}

		public virtual void copy_pixel(int x, int y, byte[] c, int ByteOffset)
		{
			linkedImage.copy_pixel(x, y, c, ByteOffset);
		}

		public virtual void CopyFrom(IImageByte sourceRaster)
		{
			linkedImage.CopyFrom(sourceRaster);
		}

		public virtual void CopyFrom(IImageByte sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset)
		{
			linkedImage.CopyFrom(sourceImage, sourceImageRect, destXOffset, destYOffset);
		}

		public virtual void SetPixel(int x, int y, RGBA_Bytes color)
		{
			linkedImage.SetPixel(x, y, color);
		}

		public virtual void BlendPixel(int x, int y, RGBA_Bytes sourceColor, byte cover)
		{
			linkedImage.BlendPixel(x, y, sourceColor, cover);
		}

		public virtual void copy_hline(int x, int y, int len, RGBA_Bytes sourceColor)
		{
			linkedImage.copy_hline(x, y, len, sourceColor);
		}

		public virtual void copy_vline(int x, int y, int len, RGBA_Bytes sourceColor)
		{
			linkedImage.copy_vline(x, y, len, sourceColor);
		}

		public virtual void blend_hline(int x1, int y, int x2, RGBA_Bytes sourceColor, byte cover)
		{
			linkedImage.blend_hline(x1, y, x2, sourceColor, cover);
		}

		public virtual void blend_vline(int x, int y1, int y2, RGBA_Bytes sourceColor, byte cover)
		{
			linkedImage.blend_vline(x, y1, y2, sourceColor, cover);
		}

		public virtual void blend_solid_hspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_hspan(x, y, len, c, covers, coversIndex);
		}

		public virtual void copy_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorIndex)
		{
			linkedImage.copy_color_hspan(x, y, len, colors, colorIndex);
		}

		public virtual void copy_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorIndex)
		{
			linkedImage.copy_color_vspan(x, y, len, colors, colorIndex);
		}

		public virtual void blend_solid_vspan(int x, int y, int len, RGBA_Bytes c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_vspan(x, y, len, c, covers, coversIndex);
		}

		public virtual void blend_color_hspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_hspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public virtual void blend_color_vspan(int x, int y, int len, RGBA_Bytes[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_vspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public byte[] GetBuffer()
		{
			return linkedImage.GetBuffer();
		}

		public int GetBufferOffsetXY(int x, int y)
		{
			return linkedImage.GetBufferOffsetXY(x, y);
		}

		public int GetBufferOffsetY(int y)
		{
			return linkedImage.GetBufferOffsetY(y);
		}

		public virtual int GetBytesBetweenPixelsInclusive()
		{
			return linkedImage.GetBytesBetweenPixelsInclusive();
		}

		public virtual int BitDepth
		{
			get
			{
				return linkedImage.BitDepth;
			}
		}

		public void MarkImageChanged()
		{
			linkedImage.MarkImageChanged();
		}
	}

	public abstract class ImageProxyFloat : IImageFloat
	{
		protected IImageFloat linkedImage;

		public ImageProxyFloat(IImageFloat linkedImage)
		{
			this.linkedImage = linkedImage;
		}

		public virtual void LinkToImage(IImageFloat linkedImage)
		{
			this.linkedImage = linkedImage;
		}

		public virtual Vector2 OriginOffset
		{
			get { return linkedImage.OriginOffset; }
			set { linkedImage.OriginOffset = value; }
		}

		public virtual int Width
		{
			get
			{
				return linkedImage.Width;
			}
		}

		public virtual int Height
		{
			get
			{
				return linkedImage.Height;
			}
		}

		public virtual int StrideInFloats()
		{
			return linkedImage.StrideInFloats();
		}

		public virtual int StrideInFloatsAbs()
		{
			return linkedImage.StrideInFloatsAbs();
		}

		public virtual RectangleInt GetBounds()
		{
			return linkedImage.GetBounds();
		}

		public Graphics2D NewGraphics2D()
		{
			return linkedImage.NewGraphics2D();
		}

		public IRecieveBlenderFloat GetBlender()
		{
			return linkedImage.GetBlender();
		}

		public void SetRecieveBlender(IRecieveBlenderFloat value)
		{
			linkedImage.SetRecieveBlender(value);
		}

		public virtual RGBA_Floats GetPixel(int x, int y)
		{
			return linkedImage.GetPixel(y, x);
		}

		public virtual void copy_pixel(int x, int y, float[] c, int FloatOffset)
		{
			linkedImage.copy_pixel(x, y, c, FloatOffset);
		}

		public virtual void CopyFrom(IImageFloat sourceRaster)
		{
			linkedImage.CopyFrom(sourceRaster);
		}

		public virtual void CopyFrom(IImageFloat sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset)
		{
			linkedImage.CopyFrom(sourceImage, sourceImageRect, destXOffset, destYOffset);
		}

		public virtual void SetPixel(int x, int y, RGBA_Floats color)
		{
			linkedImage.SetPixel(x, y, color);
		}

		public virtual void BlendPixel(int x, int y, RGBA_Floats sourceColor, byte cover)
		{
			linkedImage.BlendPixel(x, y, sourceColor, cover);
		}

		public virtual void copy_hline(int x, int y, int len, RGBA_Floats sourceColor)
		{
			linkedImage.copy_hline(x, y, len, sourceColor);
		}

		public virtual void copy_vline(int x, int y, int len, RGBA_Floats sourceColor)
		{
			linkedImage.copy_vline(x, y, len, sourceColor);
		}

		public virtual void blend_hline(int x1, int y, int x2, RGBA_Floats sourceColor, byte cover)
		{
			linkedImage.blend_hline(x1, y, x2, sourceColor, cover);
		}

		public virtual void blend_vline(int x, int y1, int y2, RGBA_Floats sourceColor, byte cover)
		{
			linkedImage.blend_vline(x, y1, y2, sourceColor, cover);
		}

		public virtual void blend_solid_hspan(int x, int y, int len, RGBA_Floats c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_hspan(x, y, len, c, covers, coversIndex);
		}

		public virtual void copy_color_hspan(int x, int y, int len, RGBA_Floats[] colors, int colorIndex)
		{
			linkedImage.copy_color_hspan(x, y, len, colors, colorIndex);
		}

		public virtual void copy_color_vspan(int x, int y, int len, RGBA_Floats[] colors, int colorIndex)
		{
			linkedImage.copy_color_vspan(x, y, len, colors, colorIndex);
		}

		public virtual void blend_solid_vspan(int x, int y, int len, RGBA_Floats c, byte[] covers, int coversIndex)
		{
			linkedImage.blend_solid_vspan(x, y, len, c, covers, coversIndex);
		}

		public virtual void blend_color_hspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_hspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public virtual void blend_color_vspan(int x, int y, int len, RGBA_Floats[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			linkedImage.blend_color_vspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public float[] GetBuffer()
		{
			return linkedImage.GetBuffer();
		}

		public int GetBufferOffsetY(int y)
		{
			return linkedImage.GetBufferOffsetY(y);
		}

		public int GetBufferOffsetXY(int x, int y)
		{
			return linkedImage.GetBufferOffsetXY(x, y);
		}

		public virtual int GetFloatsBetweenPixelsInclusive()
		{
			return linkedImage.GetFloatsBetweenPixelsInclusive();
		}

		public virtual int BitDepth
		{
			get
			{
				return linkedImage.BitDepth;
			}
		}

		public void MarkImageChanged()
		{
			linkedImage.MarkImageChanged();
		}
	}
}