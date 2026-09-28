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

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// Four lengths edited together unless "same" is off: margins as left, right, top, bottom and corner radii
	/// as north-west, north-east, south-west, south-east (agg-gui's FourVal, egui's Margin / CornerRadius).
	/// </summary>
	public sealed class FrameEdges
	{
		private readonly double[] values = new double[4];

		public FrameEdges(double value)
		{
			this.SetAll(value);
		}

		/// <summary>Raised when a value or <see cref="Same"/> changes.</summary>
		public event EventHandler Changed;

		/// <summary>True while one value edits all four.</summary>
		public bool Same { get; private set; } = true;

		public double this[int index]
		{
			get => this.values[index];
			set
			{
				if (this.values[index] != value)
				{
					this.values[index] = value;
					this.Changed?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Sets all four to <paramref name="value"/> and turns <see cref="Same"/> on.</summary>
		public void SetAll(double value)
		{
			for (int i = 0; i < 4; i++)
			{
				this.values[i] = value;
			}

			this.Same = true;
			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Turns "same" on or off. Turning it on collapses the four to their average, as agg-gui does, so
		/// the one value shown is the one in use.
		/// </summary>
		public void SetSame(bool same)
		{
			if (same)
			{
				this.SetAll((this.values[0] + this.values[1] + this.values[2] + this.values[3]) / 4);
			}
			else if (this.Same)
			{
				this.Same = false;
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	/// <summary>
	/// Everything the Frame window edits, with egui FrameDemo's defaults (frame_demo/core.rs FrameState).
	/// Lengths are design units; the shadow offset is egui's, y growing downwards.
	/// </summary>
	public sealed class FrameState
	{
		public const double DefaultCornerRadius = 14;
		public const double DefaultInnerMargin = 12;
		public const double DefaultOuterMargin = 24;
		public const double DefaultStrokeWidth = 1;
		public const double DefaultShadowX = 8;
		public const double DefaultShadowY = 12;
		public const double DefaultShadowBlur = 16;
		public const double DefaultShadowSpread = 0;

		public static readonly Color DefaultFill = new Color(97, 0, 255, 128);
		public static readonly Color DefaultStrokeColor = new Color(128, 128, 128);
		public static readonly Color DefaultShadowColor = new Color(0, 0, 0, 180);

		private double shadowX = DefaultShadowX;
		private double shadowY = DefaultShadowY;
		private double shadowBlur = DefaultShadowBlur;
		private double shadowSpread = DefaultShadowSpread;
		private Color shadowColor = DefaultShadowColor;
		private Color fill = DefaultFill;
		private double strokeWidth = DefaultStrokeWidth;
		private Color strokeColor = DefaultStrokeColor;

		public FrameState()
		{
			this.InnerMargin.Changed += (s, e) => this.RaiseChanged();
			this.OuterMargin.Changed += (s, e) => this.RaiseChanged();
			this.CornerRadius.Changed += (s, e) => this.RaiseChanged();
		}

		/// <summary>Raised whenever anything here changes.</summary>
		public event EventHandler Changed;

		/// <summary>Left, right, top, bottom: the gap between the frame's edge and its content.</summary>
		public FrameEdges InnerMargin { get; } = new FrameEdges(DefaultInnerMargin);

		/// <summary>Left, right, top, bottom: the gap around the frame.</summary>
		public FrameEdges OuterMargin { get; } = new FrameEdges(DefaultOuterMargin);

		/// <summary>North-west, north-east, south-west, south-east.</summary>
		public FrameEdges CornerRadius { get; } = new FrameEdges(DefaultCornerRadius);

		public double ShadowX { get => this.shadowX; set => this.Set(ref this.shadowX, value); }

		/// <summary>egui's shadow y offset: positive moves the shadow down.</summary>
		public double ShadowY { get => this.shadowY; set => this.Set(ref this.shadowY, value); }

		public double ShadowBlur { get => this.shadowBlur; set => this.Set(ref this.shadowBlur, value); }

		public double ShadowSpread { get => this.shadowSpread; set => this.Set(ref this.shadowSpread, value); }

		public Color ShadowColor { get => this.shadowColor; set => this.Set(ref this.shadowColor, value); }

		public Color Fill { get => this.fill; set => this.Set(ref this.fill, value); }

		public double StrokeWidth { get => this.strokeWidth; set => this.Set(ref this.strokeWidth, value); }

		public Color StrokeColor { get => this.strokeColor; set => this.Set(ref this.strokeColor, value); }

		/// <summary>Puts every value back to egui's defaults, "same" on everywhere.</summary>
		public void Reset()
		{
			this.InnerMargin.SetAll(DefaultInnerMargin);
			this.OuterMargin.SetAll(DefaultOuterMargin);
			this.CornerRadius.SetAll(DefaultCornerRadius);
			this.ShadowX = DefaultShadowX;
			this.ShadowY = DefaultShadowY;
			this.ShadowBlur = DefaultShadowBlur;
			this.ShadowSpread = DefaultShadowSpread;
			this.ShadowColor = DefaultShadowColor;
			this.Fill = DefaultFill;
			this.StrokeWidth = DefaultStrokeWidth;
			this.StrokeColor = DefaultStrokeColor;
		}

		private void Set<T>(ref T field, T value)
		{
			if (!Equals(field, value))
			{
				field = value;
				this.RaiseChanged();
			}
		}

		private void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);
	}
}
