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
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// A write-only <see cref="IImageByte"/> whose blended pixels and spans become rectangles on a
	/// <see cref="Graphics2D"/>, so the pixel renderers - <see cref="RendererMarkers"/>, <see cref="RendererPrimitives"/>,
	/// <see cref="OutlineRenderer"/>, <see cref="ImageLineRenderer"/> - draw their exact pixels on any surface, the
	/// GPU included. Pixel (x, y) is the unit square from (x, y) to (x + 1, y + 1) in the graphics' coordinates,
	/// and a pixel's cover scales its color's alpha.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only the blends are supported: a copy would replace what is under it, which a rectangle drawn source-over
	/// cannot, and nothing can be read back. Wrap it in an <see cref="ImageClippingProxy"/> to clip to
	/// <see cref="Width"/> by <see cref="Height"/>, as a renderer over an image would be.
	/// </para>
	/// <para>
	/// The renderers write a pixel or a short span at a time, and a rectangle each would be a draw call each. So
	/// every row inside <see cref="Height"/> holds one pending run, which a pixel of the same color and cover
	/// just to its right extends; anything else on that row issues the run first. Rows never share a pixel, so
	/// issuing them in any order is exact. The pending runs are handed to <see cref="Graphics2D.DeferDraws"/>,
	/// so any other draw, transform or clip change on the graphics issues them first; after the last span,
	/// call <see cref="Flush"/> (or draw anything else) before the frame ends.
	/// </para>
	/// </remarks>
	public class Graphics2DSpanImage : IImageByte
	{
		private readonly Graphics2D graphics;

		private readonly Action issueForGraphics;

		// Per row: the pending run's [left, right) and its cover-scaled color; right == left when empty.
		private readonly int[] runLeft;

		private readonly int[] runRight;

		private readonly Color[] runColor;

		// The rows with a run, each listed once however often its run is issued and restarted.
		private readonly int[] pendingRows;

		private readonly bool[] rowListed;

		private int pendingRowCount;

		private bool issuing;

		// On a surface that can draw coloured primitives, the issued rectangles as two triangles each, in issue
		// order, drawn as one call by Flush (or the next other draw): a draw call per rectangle is what made
		// the GPU slow. Within one draw, blending follows the triangles' order, so overlaps stay exact.
		private readonly List<PosColorVertex> batch;

		public Graphics2DSpanImage(Graphics2D graphics, int width, int height)
		{
			this.graphics = graphics;
			this.Width = width;
			this.Height = height;
			this.issueForGraphics = this.IssueForGraphics;
			this.runLeft = new int[Math.Max(height, 0)];
			this.runRight = new int[Math.Max(height, 0)];
			this.runColor = new Color[Math.Max(height, 0)];
			this.pendingRows = new int[Math.Max(height, 0)];
			this.rowListed = new bool[Math.Max(height, 0)];
			this.batch = graphics.CanDrawColoredPrimitives ? new List<PosColorVertex>() : null;
		}

		public Vector2 OriginOffset { get; set; }

		public int BitDepth => 32;

		public int Width { get; }

		public int Height { get; }

		public RectangleInt GetBounds() => new RectangleInt(0, 0, this.Width, this.Height);

		public void BlendPixel(int x, int y, Color sourceColor, byte cover)
		{
			this.Fill(x, y, x + 1, y + 1, sourceColor, cover);
		}

		/// <summary>The pixels from x to <paramref name="x2"/> inclusive, in either order.</summary>
		public void blend_hline(int x, int y, int x2, Color sourceColor, byte cover)
		{
			this.Fill(Math.Min(x, x2), y, Math.Max(x, x2) + 1, y + 1, sourceColor, cover);
		}

		/// <summary>The pixels from y1 to <paramref name="y2"/> inclusive, in either order.</summary>
		public void blend_vline(int x, int y1, int y2, Color sourceColor, byte cover)
		{
			this.Fill(x, Math.Min(y1, y2), x + 1, Math.Max(y1, y2) + 1, sourceColor, cover);
		}

		/// <summary>One rectangle per run of equal cover.</summary>
		public void blend_solid_hspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex)
		{
			int runStart = 0;
			for (int i = 1; i <= len; i++)
			{
				if (i == len || covers[coversIndex + i] != covers[coversIndex + runStart])
				{
					this.Fill(x + runStart, y, x + i, y + 1, sourceColor, covers[coversIndex + runStart]);
					runStart = i;
				}
			}
		}

		/// <summary>One rectangle per run of equal cover.</summary>
		public void blend_solid_vspan(int x, int y, int len, Color sourceColor, byte[] covers, int coversIndex)
		{
			int runStart = 0;
			for (int i = 1; i <= len; i++)
			{
				if (i == len || covers[coversIndex + i] != covers[coversIndex + runStart])
				{
					this.Fill(x, y + runStart, x + 1, y + i, sourceColor, covers[coversIndex + runStart]);
					runStart = i;
				}
			}
		}

		public void MarkImageChanged()
		{
		}

		public Graphics2D NewGraphics2D() => this.graphics;

		public int GetBufferOffsetY(int y) => throw NotPixels();

		public int GetBufferOffsetXY(int x, int y) => throw NotPixels();

		public int StrideInBytes() => throw NotPixels();

		public int StrideInBytesAbs() => throw NotPixels();

		public IRecieveBlenderByte GetRecieveBlender() => throw NotPixels();

		public void SetRecieveBlender(IRecieveBlenderByte value) => throw NotPixels();

		public int GetBytesBetweenPixelsInclusive() => throw NotPixels();

		public byte[] GetBuffer() => throw NotPixels();

		public Color GetPixel(int x, int y) => throw NotPixels();

		public void copy_pixel(int x, int y, byte[] c, int ByteOffset) => throw NotPixels();

		public void CopyFrom(IImageByte sourceImage) => throw NotPixels();

		public void CopyFrom(IImageByte sourceImage, RectangleInt sourceImageRect, int destXOffset, int destYOffset) => throw NotPixels();

		public void SetPixel(int x, int y, Color color) => throw NotPixels();

		public void copy_hline(int x, int y, int len, Color sourceColor) => throw NotPixels();

		public void copy_vline(int x, int y, int len, Color sourceColor) => throw NotPixels();

		public void copy_color_hspan(int x, int y, int len, Color[] colors, int colorIndex) => throw NotPixels();

		public void copy_color_vspan(int x, int y, int len, Color[] colors, int colorIndex) => throw NotPixels();

		/// <summary>A pixel each, which the row's run merges where neighbors share color and cover.</summary>
		public void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			for (int i = 0; i < len; i++)
			{
				byte cover = covers == null ? (byte)255 : covers[coversIndex + (firstCoverForAll ? 0 : i)];
				this.Fill(x + i, y, x + i + 1, y + 1, colors[colorsIndex + i], cover);
			}
		}

		/// <summary>A pixel each, on its own row.</summary>
		public void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			for (int i = 0; i < len; i++)
			{
				byte cover = covers == null ? (byte)255 : covers[coversIndex + (firstCoverForAll ? 0 : i)];
				this.Fill(x, y + i, x + 1, y + i + 1, colors[colorsIndex + i], cover);
			}
		}

		private static NotSupportedException NotPixels()
		{
			return new NotSupportedException("Graphics2DSpanImage only blends solid pixels and spans onto its Graphics2D; it has no pixels to copy or read.");
		}

		/// <summary>Draws every pending run now. Call it after the last span, unless something else is drawn after.</summary>
		public void Flush()
		{
			for (int i = 0; i < this.pendingRowCount; i++)
			{
				this.IssueRow(this.pendingRows[i]);
				this.rowListed[this.pendingRows[i]] = false;
			}

			this.pendingRowCount = 0;

			if (this.batch?.Count > 0)
			{
				this.issuing = true;
				try
				{
					this.graphics.DrawColoredPrimitives(DrawTopology.TriangleList, CollectionsMarshal.AsSpan(this.batch));
				}
				finally
				{
					this.issuing = false;
					this.batch.Clear();
				}
			}
		}

		/// <summary>
		/// The graphics' deferred-draw callback. Our own rectangle reaches it too, mid-issue; the other rows'
		/// runs stay pending then, so they re-register rather than flush.
		/// </summary>
		private void IssueForGraphics()
		{
			if (this.issuing)
			{
				this.graphics.DeferDraws(this.issueForGraphics);
			}
			else
			{
				this.Flush();
			}
		}

		private void Fill(int left, int bottom, int right, int top, Color color, byte cover)
		{
			if (cover == 0 || color.alpha == 0 || right <= left || top <= bottom)
			{
				return;
			}

			if (cover != 255)
			{
				// Rounded as the software blenders scale by cover, so the two agree to the unit.
				color.alpha = (byte)Rgba8Math.Multiply(color.alpha, cover);
			}

			if (top == bottom + 1 && bottom >= 0 && bottom < this.Height)
			{
				this.AddToRow(bottom, left, right, color);
				return;
			}

			// A taller rectangle goes now, after the runs pending on the pixels it covers.
			for (int y = Math.Max(bottom, 0); y < Math.Min(top, this.Height); y++)
			{
				if (this.runRight[y] > left && this.runLeft[y] < right)
				{
					this.IssueRow(y);
				}
			}

			this.Issue(left, bottom, right, top, color);
		}

		private void AddToRow(int y, int left, int right, Color color)
		{
			if (this.runRight[y] != this.runLeft[y])
			{
				if (this.runRight[y] == left && this.runColor[y] == color)
				{
					this.runRight[y] = right;
					return;
				}

				this.IssueRow(y);
			}
			else if (!this.rowListed[y])
			{
				this.rowListed[y] = true;
				this.pendingRows[this.pendingRowCount++] = y;
			}

			this.runLeft[y] = left;
			this.runRight[y] = right;
			this.runColor[y] = color;
			this.graphics.DeferDraws(this.issueForGraphics);
		}

		/// <summary>Draws row y's run, if it has one, and leaves the row empty (still listed as pending).</summary>
		private void IssueRow(int y)
		{
			int left = this.runLeft[y];
			int right = this.runRight[y];
			if (right != left)
			{
				this.runRight[y] = left;
				this.Issue(left, y, right, y + 1, this.runColor[y]);
			}
		}

		private void Issue(int left, int bottom, int right, int top, Color color)
		{
			if (this.batch != null)
			{
				// The primitives are in surface coordinates, so they take the graphics' transform here, as
				// FillRectangle would; a transform change flushes first, so it is the one they were blended under.
				Affine transform = this.graphics.GetTransform();
				double x0 = left, y0 = bottom, x1 = right, y1 = top;
				transform.Transform(ref x0, ref y0);
				transform.Transform(ref x1, ref y1);
				var leftBottom = new PosColorVertex(new Vector2(x0, y0), color);
				var rightTop = new PosColorVertex(new Vector2(x1, y1), color);
				this.batch.Add(leftBottom);
				this.batch.Add(new PosColorVertex(new Vector2(x1, y0), color));
				this.batch.Add(rightTop);
				this.batch.Add(leftBottom);
				this.batch.Add(rightTop);
				this.batch.Add(new PosColorVertex(new Vector2(x0, y1), color));
				this.graphics.DeferDraws(this.issueForGraphics);
				return;
			}

			this.issuing = true;
			try
			{
				this.graphics.FillRectangle(left, bottom, right, top, color);
			}
			finally
			{
				this.issuing = false;
			}
		}
	}
}
