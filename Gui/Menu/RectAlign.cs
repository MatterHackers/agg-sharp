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
using MatterHackers.VectorMath;

// Anchor relative popup placement, ported from agg-gui (agg-gui/src/widgets/popup/align.rs, its answer to egui's
// RectAlign). MatePoint can only put a popup's edge against an anchor's edge; RectAlign names any of nine points on
// the anchor and on the popup, adds a gap, and AnchoredPopup flips to a mirror image when the first choice
// overflows. Coordinates are agg's Y-up, so Max on Y is the top edge.
namespace MatterHackers.Agg.UI
{
	/// <summary>Where on one axis a point sits: the start (0), middle (0.5) or end (1). On Y, Min is the bottom.</summary>
	public enum AxisAlign
	{
		Min,
		Center,
		Max
	}

	/// <summary>A point on a rectangle, named by its horizontal and vertical <see cref="AxisAlign"/>.</summary>
	public readonly struct Align2 : IEquatable<Align2>
	{
		public static readonly Align2 LeftTop = new Align2(AxisAlign.Min, AxisAlign.Max);
		public static readonly Align2 CenterTop = new Align2(AxisAlign.Center, AxisAlign.Max);
		public static readonly Align2 RightTop = new Align2(AxisAlign.Max, AxisAlign.Max);
		public static readonly Align2 LeftCenter = new Align2(AxisAlign.Min, AxisAlign.Center);
		public static readonly Align2 Center = new Align2(AxisAlign.Center, AxisAlign.Center);
		public static readonly Align2 RightCenter = new Align2(AxisAlign.Max, AxisAlign.Center);
		public static readonly Align2 LeftBottom = new Align2(AxisAlign.Min, AxisAlign.Min);
		public static readonly Align2 CenterBottom = new Align2(AxisAlign.Center, AxisAlign.Min);
		public static readonly Align2 RightBottom = new Align2(AxisAlign.Max, AxisAlign.Min);

		/// <summary>The nine points in agg-gui's (and egui's) combo box order, with the labels they show.</summary>
		public static readonly IReadOnlyList<(Align2 Align, string Label)> All = new[]
		{
			(LeftTop, "LEFT_TOP"),
			(LeftCenter, "LEFT_CENTER"),
			(LeftBottom, "LEFT_BOTTOM"),
			(CenterTop, "CENTER_TOP"),
			(Center, "CENTER_CENTER"),
			(CenterBottom, "CENTER_BOTTOM"),
			(RightTop, "RIGHT_TOP"),
			(RightCenter, "RIGHT_CENTER"),
			(RightBottom, "RIGHT_BOTTOM"),
		};

		public Align2(AxisAlign x, AxisAlign y)
		{
			this.X = x;
			this.Y = y;
		}

		public AxisAlign X { get; }

		public AxisAlign Y { get; }

		/// <summary>This point's index in <see cref="All"/>.</summary>
		public int AllIndex
		{
			get
			{
				for (int i = 0; i < All.Count; i++)
				{
					if (All[i].Align == this)
					{
						return i;
					}
				}

				return 0;
			}
		}

		/// <summary>The point this names on <paramref name="rect"/>.</summary>
		public Vector2 PointIn(RectangleDouble rect)
		{
			return new Vector2(rect.Left + rect.Width * Fraction(this.X), rect.Bottom + rect.Height * Fraction(this.Y));
		}

		public Align2 FlipX() => new Align2(Flip(this.X), this.Y);

		public Align2 FlipY() => new Align2(this.X, Flip(this.Y));

		public Align2 Flip() => new Align2(Flip(this.X), Flip(this.Y));

		public static double Fraction(AxisAlign align) => align == AxisAlign.Min ? 0 : align == AxisAlign.Center ? 0.5 : 1;

		/// <summary>Which way a gap pushes away from this edge: -1 off the start, +1 off the end, 0 from the middle.</summary>
		public static double Sign(AxisAlign align) => align == AxisAlign.Min ? -1 : align == AxisAlign.Center ? 0 : 1;

		public static AxisAlign Flip(AxisAlign align) => align == AxisAlign.Min ? AxisAlign.Max : align == AxisAlign.Max ? AxisAlign.Min : AxisAlign.Center;

		public static bool operator ==(Align2 a, Align2 b) => a.Equals(b);

		public static bool operator !=(Align2 a, Align2 b) => !a.Equals(b);

		public bool Equals(Align2 other) => this.X == other.X && this.Y == other.Y;

		public override bool Equals(object obj) => obj is Align2 other && this.Equals(other);

		public override int GetHashCode() => ((int)this.X * 3) + (int)this.Y;

		public override string ToString() => All[this.AllIndex].Label;
	}

	/// <summary>
	/// How a popup attaches to its anchor: the popup's <see cref="Child"/> point is put on the anchor's
	/// <see cref="Parent"/> point, then pushed <c>gap</c> outward on each axis where the two differ.
	/// </summary>
	public readonly struct RectAlign : IEquatable<RectAlign>
	{
		public static readonly RectAlign BottomStart = new RectAlign(Align2.LeftBottom, Align2.LeftTop);
		public static readonly RectAlign Bottom = new RectAlign(Align2.CenterBottom, Align2.CenterTop);
		public static readonly RectAlign BottomEnd = new RectAlign(Align2.RightBottom, Align2.RightTop);
		public static readonly RectAlign TopStart = new RectAlign(Align2.LeftTop, Align2.LeftBottom);
		public static readonly RectAlign Top = new RectAlign(Align2.CenterTop, Align2.CenterBottom);
		public static readonly RectAlign TopEnd = new RectAlign(Align2.RightTop, Align2.RightBottom);
		public static readonly RectAlign RightStart = new RectAlign(Align2.RightTop, Align2.LeftTop);
		public static readonly RectAlign Right = new RectAlign(Align2.RightCenter, Align2.LeftCenter);
		public static readonly RectAlign RightEnd = new RectAlign(Align2.RightBottom, Align2.LeftBottom);
		public static readonly RectAlign LeftStart = new RectAlign(Align2.LeftTop, Align2.RightTop);
		public static readonly RectAlign Left = new RectAlign(Align2.LeftCenter, Align2.RightCenter);
		public static readonly RectAlign LeftEnd = new RectAlign(Align2.LeftBottom, Align2.RightBottom);

		/// <summary>The twelve named placements, in agg-gui's preset combo order.</summary>
		public static readonly IReadOnlyList<(RectAlign Align, string Label)> Presets = new[]
		{
			(TopStart, "TOP_START"),
			(Top, "TOP"),
			(TopEnd, "TOP_END"),
			(RightStart, "RIGHT_START"),
			(Right, "RIGHT"),
			(RightEnd, "RIGHT_END"),
			(BottomStart, "BOTTOM_START"),
			(Bottom, "BOTTOM"),
			(BottomEnd, "BOTTOM_END"),
			(LeftStart, "LEFT_START"),
			(Left, "LEFT"),
			(LeftEnd, "LEFT_END"),
		};

		/// <summary>The fallbacks tried, in order, when a placement and its mirror images all overflow (egui's MENU_ALIGNS).</summary>
		public static readonly IReadOnlyList<RectAlign> MenuAligns = new[]
		{
			BottomStart, BottomEnd, TopStart, TopEnd, RightEnd, RightStart, LeftEnd, LeftStart, Top, Right, Bottom, Left,
		};

		/// <summary>How far a clamped popup is kept from the edge of the viewport, as agg-gui's popups are.</summary>
		public const double ViewportMargin = 4;

		public RectAlign(Align2 parent, Align2 child)
		{
			this.Parent = parent;
			this.Child = child;
		}

		/// <summary>The point on the anchor.</summary>
		public Align2 Parent { get; }

		/// <summary>The point on the popup that is put on <see cref="Parent"/>.</summary>
		public Align2 Child { get; }

		/// <summary>The index of this placement in <see cref="Presets"/>, or -1 for a pair that has no name.</summary>
		public int PresetIndex
		{
			get
			{
				for (int i = 0; i < Presets.Count; i++)
				{
					if (Presets[i].Align == this)
					{
						return i;
					}
				}

				return -1;
			}
		}

		/// <summary>The preset name, or null for a pair that has none.</summary>
		public string PresetLabel => this.PresetIndex < 0 ? null : Presets[this.PresetIndex].Label;

		public RectAlign FlipX() => new RectAlign(this.Parent.FlipX(), this.Child.FlipX());

		public RectAlign FlipY() => new RectAlign(this.Parent.FlipY(), this.Child.FlipY());

		public RectAlign Flip() => new RectAlign(this.Parent.Flip(), this.Child.Flip());

		/// <summary>The mirror images tried when this placement overflows: across X, across Y, then both.</summary>
		public RectAlign[] Symmetries() => new[] { this.FlipX(), this.FlipY(), this.Flip() };

		/// <summary>Where a popup of <paramref name="size"/> goes against <paramref name="parent"/>, before any clamping.</summary>
		public RectangleDouble PlaceChild(RectangleDouble parent, Vector2 size, double gap)
		{
			Vector2 anchor = this.Parent.PointIn(parent);
			double gapX = this.Parent.X != this.Child.X ? Align2.Sign(this.Parent.X) : 0;
			double gapY = this.Parent.Y != this.Child.Y ? Align2.Sign(this.Parent.Y) : 0;
			double left = anchor.X + gap * gapX - size.X * Align2.Fraction(this.Child.X);
			double bottom = anchor.Y + gap * gapY - size.Y * Align2.Fraction(this.Child.Y);
			return new RectangleDouble(left, bottom, left + size.X, bottom + size.Y);
		}

		/// <summary>
		/// The first of <paramref name="candidates"/> whose placement fits inside <paramref name="content"/>, or the
		/// first candidate when none does (egui's Popup::get_best_align).
		/// </summary>
		public static RectAlign FindBestAlign(IEnumerable<RectAlign> candidates, RectangleDouble content, RectangleDouble parent, double gap, Vector2 size)
		{
			RectAlign? first = null;
			foreach (RectAlign align in candidates)
			{
				first ??= align;
				RectangleDouble placed = align.PlaceChild(parent, size, gap);
				if (placed.Left >= content.Left && placed.Bottom >= content.Bottom
					&& placed.Right <= content.Right && placed.Top <= content.Top)
				{
					return align;
				}
			}

			return first ?? BottomStart;
		}

		/// <summary>Moves <paramref name="rect"/> into a viewport of size <paramref name="viewport"/>, <see cref="ViewportMargin"/> in from its edges.</summary>
		public static RectangleDouble Clamp(RectangleDouble rect, Vector2 viewport)
		{
			double maxX = Math.Max(viewport.X - rect.Width - ViewportMargin, ViewportMargin);
			double maxY = Math.Max(viewport.Y - rect.Height - ViewportMargin, ViewportMargin);
			double left = Math.Clamp(rect.Left, ViewportMargin, maxX);
			double bottom = Math.Clamp(rect.Bottom, ViewportMargin, maxY);
			return new RectangleDouble(left, bottom, left + rect.Width, bottom + rect.Height);
		}

		public static bool operator ==(RectAlign a, RectAlign b) => a.Equals(b);

		public static bool operator !=(RectAlign a, RectAlign b) => !a.Equals(b);

		public bool Equals(RectAlign other) => this.Parent == other.Parent && this.Child == other.Child;

		public override bool Equals(object obj) => obj is RectAlign other && this.Equals(other);

		public override int GetHashCode() => (this.Parent.GetHashCode() * 9) + this.Child.GetHashCode();

		public override string ToString() => this.PresetLabel ?? $"{this.Parent} / {this.Child}";
	}
}
