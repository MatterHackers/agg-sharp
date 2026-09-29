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

using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The element context-fill and context-stroke refer to while its content draws: a &lt;use&gt; for what it
	/// references, a marked path for its markers. A paint server named through it is laid over this element's box, in
	/// its user space, as usvg does - not over the shape that says context-fill.
	/// </summary>
	internal sealed class SvgContextElement
	{
		private SvgContextElement(SvgPaint fill, SvgPaint stroke, RectangleDouble bounds, Affine transform)
		{
			this.Fill = fill;
			this.Stroke = stroke;
			this.Bounds = bounds;
			this.Transform = transform;
		}

		public SvgPaint Fill { get; }

		public SvgPaint Stroke { get; }

		/// <summary>The element's bounding box in its own user space.</summary>
		public RectangleDouble Bounds { get; }

		/// <summary>The element's user space to pixels.</summary>
		public Affine Transform { get; }

		/// <summary>
		/// The context an element of <paramref name="style"/> makes for its content. Its own context-fill or
		/// context-stroke is taken from <paramref name="outer"/> (none without one); a colour loses its alpha, as
		/// usvg keeps only the paint and not its opacity; and a stroke of no width is none.
		/// </summary>
		public static SvgContextElement Resolve(SvgContextElement outer, SvgStyle style, RectangleDouble bounds, Affine transform)
		{
			SvgPaint Own(SvgPaint paint)
			{
				if (paint.Context != SvgContextPaintKind.None)
				{
					return outer?.Paint(paint.Context) ?? SvgPaint.None;
				}

				return !paint.IsNone && paint.ServerId == null ? SvgPaint.FromColor(new Color(paint.Color, 255)) : paint;
			}

			return new SvgContextElement(Own(style.Fill), style.StrokeWidth > 0 ? Own(style.Stroke) : SvgPaint.None, bounds, transform);
		}

		/// <summary>The paint context-fill (<see cref="SvgContextPaintKind.Fill"/>) or context-stroke stands for here.</summary>
		public SvgPaint Paint(SvgContextPaintKind kind) => kind == SvgContextPaintKind.Fill ? this.Fill : this.Stroke;
	}
}
