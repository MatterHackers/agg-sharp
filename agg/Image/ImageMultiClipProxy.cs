//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
// Copyright (C) 2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using System.Collections.Generic;
using MatterHackers.Agg;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ AGG's renderer_mclip: an image clipped to any number of boxes. Every drawing call is replayed once per
	/// box, with the inherited single clip box set to that box, so what is drawn is what falls in any of them.
	/// </summary>
	/// <remarks>
	/// Start with <c>reset_clipping(false)</c> (nothing visible) and add boxes with <see cref="add_clip_box"/>;
	/// with no boxes, drawing goes through the inherited clip box alone, so <c>reset_clipping(true)</c> makes the
	/// whole image visible again. Boxes that overlap are drawn twice where they overlap, as in C++.
	/// </remarks>
	public class ImageMultiClipProxy : ImageClippingProxy
	{
		private readonly List<RectangleInt> clipBoxes = new List<RectangleInt>();

		private RectangleInt bounds;

		private int currentBox;

		public ImageMultiClipProxy(IImageByte image)
			: base(image)
		{
			bounds = clip_box();
		}

		/// <summary>
		/// The union of the boxes. As in C++ it starts from the clip box reset_clipping leaves, so after
		/// reset_clipping(false) - whose empty box is (1, 1, 0, 0) - its left and bottom are at most 1.
		/// </summary>
		public override RectangleInt bounding_clip_box() => bounds;

		/// <summary>Makes the whole image visible (true) or none of it (false), and drops every added box.</summary>
		public override void reset_clipping(bool visibility)
		{
			base.reset_clipping(visibility);
			clipBoxes.Clear();
			currentBox = 0;
			bounds = clip_box();
		}

		/// <summary>Adds a box (inclusive corners) to draw in, cut to the image; a box wholly off it is dropped.</summary>
		public void add_clip_box(int x1, int y1, int x2, int y2)
		{
			var box = new RectangleInt(x1, y1, x2, y2);
			box.normalize();
			if (box.clip(new RectangleInt(0, 0, Width - 1, Height - 1)))
			{
				clipBoxes.Add(box);
				if (box.Left < bounds.Left) bounds.Left = box.Left;
				if (box.Bottom < bounds.Bottom) bounds.Bottom = box.Bottom;
				if (box.Right > bounds.Right) bounds.Right = box.Right;
				if (box.Top > bounds.Top) bounds.Top = box.Top;
			}
		}

		public override void copy_pixel(int x, int y, byte[] c, int byteOffset)
		{
			FirstClipBox();
			do
			{
				if (inbox(x, y))
				{
					base.copy_pixel(x, y, c, byteOffset);
					break;
				}
			}
			while (NextClipBox());
		}

		public override void BlendPixel(int x, int y, Color sourceColor, byte cover)
		{
			FirstClipBox();
			do
			{
				if (inbox(x, y))
				{
					base.BlendPixel(x, y, sourceColor, cover);
					break;
				}
			}
			while (NextClipBox());
		}

		public override Color GetPixel(int x, int y)
		{
			FirstClipBox();
			do
			{
				if (inbox(x, y))
				{
					return base.GetPixel(x, y);
				}
			}
			while (NextClipBox());
			return new Color();
		}

		/// <summary>Copies a run of <paramref name="len"/> pixels (a length, as IImageByte declares it) into every box.</summary>
		public override void copy_hline(int x, int y, int len, Color c)
		{
			FirstClipBox();
			do { base.copy_hline(x, y, len, c); } while (NextClipBox());
		}

		/// <summary>Copies a column of <paramref name="len"/> pixels (a length) into every box.</summary>
		public override void copy_vline(int x, int y, int len, Color c)
		{
			FirstClipBox();
			do { base.copy_vline(x, y, len, c); } while (NextClipBox());
		}

		public override void blend_hline(int x1, int y, int x2, Color c, byte cover)
		{
			FirstClipBox();
			do { base.blend_hline(x1, y, x2, c, cover); } while (NextClipBox());
		}

		public override void blend_vline(int x, int y1, int y2, Color c, byte cover)
		{
			FirstClipBox();
			do { base.blend_vline(x, y1, y2, c, cover); } while (NextClipBox());
		}

		public override void blend_solid_hspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
			FirstClipBox();
			do { base.blend_solid_hspan(x, y, len, c, covers, coversIndex); } while (NextClipBox());
		}

		public override void blend_solid_vspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
			FirstClipBox();
			do { base.blend_solid_vspan(x, y, len, c, covers, coversIndex); } while (NextClipBox());
		}

		public override void copy_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			FirstClipBox();
			do { base.copy_color_hspan(x, y, len, colors, colorsIndex); } while (NextClipBox());
		}

		public override void copy_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			FirstClipBox();
			do { base.copy_color_vspan(x, y, len, colors, colorsIndex); } while (NextClipBox());
		}

		public override void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			FirstClipBox();
			do { base.blend_color_hspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll); } while (NextClipBox());
		}

		public override void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			FirstClipBox();
			do { base.blend_color_vspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll); } while (NextClipBox());
		}

		public override void CopyFrom(IImageByte sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset)
		{
			FirstClipBox();
			do { base.CopyFrom(sourceImage, sourceImageRect, destXOffset, destYOffset); } while (NextClipBox());
		}

		/// <summary>
		/// C++ first_clip_box: clip to the first box. With no boxes the inherited clip box is left as
		/// reset_clipping set it.
		/// </summary>
		private void FirstClipBox()
		{
			currentBox = 0;
			if (clipBoxes.Count > 0)
			{
				RectangleInt box = clipBoxes[0];
				clip_box_naked(box.Left, box.Bottom, box.Right, box.Top);
			}
		}

		/// <summary>C++ next_clip_box: clip to the next box, or false when every box has been drawn through.</summary>
		private bool NextClipBox()
		{
			if (++currentBox < clipBoxes.Count)
			{
				RectangleInt box = clipBoxes[currentBox];
				clip_box_naked(box.Left, box.Bottom, box.Right, box.Top);
				return true;
			}

			return false;
		}
	}
}
