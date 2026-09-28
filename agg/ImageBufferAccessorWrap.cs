//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007, 2026 Lars Brubaker
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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ image_accessor_wrap: every read, however far outside the image, is folded back into it by one wrap mode
	/// per axis, so a span generator reading through it tiles the image over the plane (repeated or mirrored).
	/// </summary>
	public sealed class ImageBufferAccessorWrap : IImageBufferAccessor
	{
		private readonly IImageByte sourceImage;

		private readonly IWrapMode wrapX;

		private readonly IWrapMode wrapY;

		private int x;

		private int rowY;

		/// <summary>Wraps x by <paramref name="wrapX"/> and y by <paramref name="wrapY"/>, each built for the image's size.</summary>
		public ImageBufferAccessorWrap(IImageByte sourceImage, IWrapMode wrapX, IWrapMode wrapY)
		{
			this.sourceImage = sourceImage;
			this.wrapX = wrapX;
			this.wrapY = wrapY;
		}

		public IImageByte SourceImage => this.sourceImage;

		public byte[] span(int x, int y, int len, out int bufferIndex)
		{
			this.x = x;
			this.rowY = this.wrapY.Wrap(y);
			bufferIndex = this.sourceImage.GetBufferOffsetXY(this.wrapX.Wrap(x), this.rowY);
			return this.sourceImage.GetBuffer();
		}

		public byte[] next_x(out int bufferByteOffset)
		{
			bufferByteOffset = this.sourceImage.GetBufferOffsetXY(this.wrapX.Next(), this.rowY);
			return this.sourceImage.GetBuffer();
		}

		public byte[] next_y(out int bufferByteOffset)
		{
			this.rowY = this.wrapY.Next();
			bufferByteOffset = this.sourceImage.GetBufferOffsetXY(this.wrapX.Wrap(this.x), this.rowY);
			return this.sourceImage.GetBuffer();
		}
	}

	/// <summary>
	/// C++'s wrap_mode_* classes: fold a coordinate into [0, size). <see cref="Wrap"/> is C++'s call operator (sets
	/// the position) and <see cref="Next"/> its prefix increment (steps one on from the last position, cheaper
	/// than wrapping the next coordinate afresh).
	/// </summary>
	public interface IWrapMode
	{
		int Wrap(int v);

		int Next();
	}

	/// <summary>C++ wrap_mode_repeat: v modulo size, negative v included.</summary>
	public sealed class WrapModeRepeat : IWrapMode
	{
		private readonly int size;

		// A multiple of size large enough that v + add is positive for any coordinate a span reaches.
		private readonly int add;

		private int value;

		public WrapModeRepeat(int size)
		{
			this.size = size;
			this.add = size * (0x3FFFFFFF / size);
		}

		public int Wrap(int v) => this.value = (v + this.add) % this.size;

		public int Next()
		{
			if (++this.value >= this.size)
			{
				this.value = 0;
			}

			return this.value;
		}
	}

	/// <summary>
	/// C++ wrap_mode_repeat_pow2: v masked to the largest power of two not above size, so a size that is not a
	/// power of two repeats only its first power-of-two pixels.
	/// </summary>
	public sealed class WrapModeRepeatPow2 : IWrapMode
	{
		private readonly int mask;

		private int value;

		public WrapModeRepeatPow2(int size)
		{
			this.mask = 1;
			while (this.mask < size)
			{
				this.mask = (this.mask << 1) | 1;
			}

			this.mask >>= 1;
		}

		public int Wrap(int v) => this.value = v & this.mask;

		public int Next()
		{
			if (++this.value > this.mask)
			{
				this.value = 0;
			}

			return this.value;
		}
	}

	/// <summary>C++ wrap_mode_repeat_auto_pow2: <see cref="WrapModeRepeat"/>, masking instead of dividing for a power-of-two size.</summary>
	public sealed class WrapModeRepeatAutoPow2 : IWrapMode
	{
		private readonly int size;

		private readonly int add;

		private readonly int mask;

		private int value;

		public WrapModeRepeatAutoPow2(int size)
		{
			this.size = size;
			this.add = size * (0x3FFFFFFF / size);
			this.mask = (size & (size - 1)) != 0 ? 0 : size - 1;
		}

		public int Wrap(int v) => this.value = this.mask != 0 ? v & this.mask : (v + this.add) % this.size;

		public int Next()
		{
			if (++this.value >= this.size)
			{
				this.value = 0;
			}

			return this.value;
		}
	}

	/// <summary>C++ wrap_mode_reflect: the image, then its mirror, repeating (each edge pixel appears twice).</summary>
	public sealed class WrapModeReflect : IWrapMode
	{
		private readonly int size;

		private readonly int size2;

		private readonly int add;

		private int value;

		public WrapModeReflect(int size)
		{
			this.size = size;
			this.size2 = size * 2;
			this.add = this.size2 * (0x3FFFFFFF / this.size2);
		}

		public int Wrap(int v)
		{
			this.value = (v + this.add) % this.size2;
			return this.Folded();
		}

		public int Next()
		{
			if (++this.value >= this.size2)
			{
				this.value = 0;
			}

			return this.Folded();
		}

		private int Folded() => this.value >= this.size ? this.size2 - this.value - 1 : this.value;
	}

	/// <summary>
	/// C++ wrap_mode_reflect_pow2: reflects over the smallest power of two at least size, so a size that is not a
	/// power of two reads past the image's end (as C++ does; use it only for power-of-two images).
	/// </summary>
	public sealed class WrapModeReflectPow2 : IWrapMode
	{
		private readonly int size;

		private readonly int mask;

		private int value;

		public WrapModeReflectPow2(int size)
		{
			this.mask = 1;
			this.size = 1;
			while (this.mask < size)
			{
				this.mask = (this.mask << 1) | 1;
				this.size <<= 1;
			}
		}

		public int Wrap(int v)
		{
			this.value = v & this.mask;
			return this.Folded();
		}

		public int Next()
		{
			this.value = (this.value + 1) & this.mask;
			return this.Folded();
		}

		private int Folded() => this.value >= this.size ? this.mask - this.value : this.value;
	}

	/// <summary>C++ wrap_mode_reflect_auto_pow2: <see cref="WrapModeReflect"/>, masking instead of dividing when twice the size is a power of two.</summary>
	public sealed class WrapModeReflectAutoPow2 : IWrapMode
	{
		private readonly int size;

		private readonly int size2;

		private readonly int add;

		private readonly int mask;

		private int value;

		public WrapModeReflectAutoPow2(int size)
		{
			this.size = size;
			this.size2 = size * 2;
			this.add = this.size2 * (0x3FFFFFFF / this.size2);
			this.mask = (this.size2 & (this.size2 - 1)) != 0 ? 0 : this.size2 - 1;
		}

		public int Wrap(int v)
		{
			this.value = this.mask != 0 ? v & this.mask : (v + this.add) % this.size2;
			return this.Folded();
		}

		public int Next()
		{
			if (++this.value >= this.size2)
			{
				this.value = 0;
			}

			return this.Folded();
		}

		private int Folded() => this.value >= this.size ? this.size2 - this.value - 1 : this.value;
	}
}
