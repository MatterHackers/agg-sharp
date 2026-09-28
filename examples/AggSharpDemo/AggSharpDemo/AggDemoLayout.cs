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
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// Where a demo's frame sits inside the view that shows it: the largest whole-number scale at which the
	/// demo fits, centered, on whole screen pixels. Both render modes of <see cref="AggDemoView"/> and its
	/// mouse mapping use this one placement, so switching modes never moves the demo under the pointer.
	/// </summary>
	/// <remarks>
	/// Whole-number scale and whole-pixel offset because every demo pixel then covers an exact square of view
	/// pixels: that is what keeps the software reference image unblurred when it is shown, and what keeps the
	/// GPU path's pixel grid aligned with the demo's. A view smaller than the demo shows it at scale 1,
	/// centered, with the overflow clipped - there is no whole-number scale below 1.
	/// <para>Whole pixels on the screen, not in the view: at a display scale like 1.25 a view can sit at a
	/// fractional screen position, and an offset whole in view coordinates would then land the frame between
	/// screen pixels. The view passes that fraction in, and the offset absorbs it.</para>
	/// </remarks>
	public readonly struct AggDemoLayout
	{
		/// <param name="viewOriginOnScreen">Where the view's origin is on the screen. Only its fractional
		/// part matters; default zero means the view already sits on whole pixels.</param>
		public AggDemoLayout(double viewWidth, double viewHeight, int demoWidth, int demoHeight, Vector2 viewOriginOnScreen = default)
		{
			this.DemoWidth = demoWidth;
			this.DemoHeight = demoHeight;

			int fitX = (int)Math.Floor(viewWidth / demoWidth);
			int fitY = (int)Math.Floor(viewHeight / demoHeight);
			this.Scale = Math.Max(1, Math.Min(fitX, fitY));

			double fractionX = viewOriginOnScreen.X - Math.Floor(viewOriginOnScreen.X);
			double fractionY = viewOriginOnScreen.Y - Math.Floor(viewOriginOnScreen.Y);
			this.Offset = new Vector2(
				Math.Floor((viewWidth - demoWidth * this.Scale) / 2 + fractionX) - fractionX,
				Math.Floor((viewHeight - demoHeight * this.Scale) / 2 + fractionY) - fractionY);
		}

		public int DemoWidth { get; }

		public int DemoHeight { get; }

		/// <summary>View pixels per demo pixel, always at least 1.</summary>
		public int Scale { get; }

		/// <summary>Where the demo's bottom-left corner lands in the view; the view's screen origin plus this is
		/// a whole screen pixel.</summary>
		public Vector2 Offset { get; }

		/// <summary>The demo's frame in view coordinates.</summary>
		public RectangleDouble DemoBoundsInView => new RectangleDouble(
			this.Offset.X,
			this.Offset.Y,
			this.Offset.X + this.DemoWidth * this.Scale,
			this.Offset.Y + this.DemoHeight * this.Scale);

		/// <summary>
		/// The transform from demo pixels (origin bottom-left, y up) to view coordinates. Both are y up, so
		/// it is a scale then a translation - no flip.
		/// </summary>
		public Affine DemoToView => Affine.NewScaling(this.Scale) * Affine.NewTranslation(this.Offset);

		/// <summary>Maps a point in view coordinates back to demo pixels (y up). Not clamped to the demo.</summary>
		public Vector2 ViewToDemo(Vector2 viewPosition)
		{
			return (viewPosition - this.Offset) / this.Scale;
		}

		/// <summary>
		/// The whole demo pixel under a view point, as C++ AGG reports mouse positions: floored, so the pixel
		/// whose bottom-left corner is (x, y). Not clamped to the demo.
		/// </summary>
		public Point2D ViewToDemoPixel(Vector2 viewPosition)
		{
			Vector2 demo = this.ViewToDemo(viewPosition);
			return new Point2D((int)Math.Floor(demo.X), (int)Math.Floor(demo.Y));
		}

		/// <summary>True when the pixel is one of the demo's: 0..Width-1 by 0..Height-1.</summary>
		public bool ContainsDemoPixel(Point2D pixel)
		{
			return pixel.x >= 0 && pixel.x < this.DemoWidth && pixel.y >= 0 && pixel.y < this.DemoHeight;
		}
	}
}
