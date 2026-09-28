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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's LionView (lion.rs): the AGG lion fitted to the widget on a panel-coloured card. Left or
	/// middle drag rotates and scales it about the widget centre relative to the press (so it does not jump
	/// when the drag starts, unlike C++ lion.cpp), right drag skews it (cursor / 1000, as lion.cpp), the wheel
	/// zooms exponentially and a two-position (touch) move pinches, twists and pans it.
	/// </summary>
	/// <remarks>
	/// Drawn through Graphics2D.Render, which on the GPU backbuffer is the halo-AA fill path (no MSAA) - the
	/// same proof agg-gui's window makes.
	/// </remarks>
	public class LionView : GuiWidget
	{
		private readonly LionShape lion = new LionShape();

		private readonly DemoTheme demoTheme;

		// The left-drag grip: the press's polar coords about the centre and the angle/scale it started from.
		private bool rotating;

		private bool skewing;

		private double gripAngle;

		private double gripDistance;

		private double startAngle;

		private double startScale;

		// The previous two touch positions while a pinch is in flight.
		private Vector2[] lastTouches;

		public LionView(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>Rotation in radians added to the lion's upright pose.</summary>
		public double Angle { get; set; }

		/// <summary>The user's zoom on top of the fit-to-widget scale.</summary>
		public double MouseScale { get; set; } = 1;

		/// <summary>Skew in lion.cpp's units: the right-drag cursor position, divided by 1000 in the transform.</summary>
		public double SkewX { get; set; }

		/// <summary>See <see cref="SkewX"/>.</summary>
		public double SkewY { get; set; }

		/// <summary>The two-finger pan in widget pixels, applied after rotate and skew.</summary>
		public Vector2 Offset { get; set; }

		/// <summary>The lion's opacity, 0 to 1 (agg-gui's Alpha slider).</summary>
		public double Alpha { get; set; } = 1;

		/// <summary>The scale fitting the lion's bounds into the widget less a 10 pixel pad (lion.rs fit_scale).</summary>
		public double FitScale()
		{
			double pad = 10 * DeviceScale;
			double sx = (this.Width - (pad * 2)) / Math.Max(this.lion.Bounds.Width, 1e-6);
			double sy = (this.Height - (pad * 2)) / Math.Max(this.lion.Bounds.Height, 1e-6);
			return Math.Max(Math.Min(sx, sy), 0.01);
		}

		/// <summary>The lion's full transform for the current size and gesture state.</summary>
		public Affine GetTransform()
		{
			// GetDemoTransform centres on the bounds' half-extent (lion.cpp); agg-gui centres on the bounds'
			// middle, so shift by the difference first. Its own move to the view centre integer-halves, so it
			// gets a 0 x 0 view and the exact centre plus the pan is added after.
			Vector2 boundsCentre = this.lion.Bounds.Center;
			Affine transform = Affine.NewTranslation(this.lion.Center.X - boundsCentre.X, this.lion.Center.Y - boundsCentre.Y);
			transform *= this.lion.GetDemoTransform(0, 0, this.Angle, this.FitScale() * this.MouseScale, this.SkewX, this.SkewY);
			transform *= Affine.NewTranslation((this.Width / 2) + this.Offset.X, (this.Height / 2) + this.Offset.Y);
			return transform;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.Width >= 4 && this.Height >= 4)
			{
				graphics2D.FillRectangle(this.LocalBounds, this.demoTheme.Palette.PanelFill);
				this.lion.Render(graphics2D, this.GetTransform(), (byte)Math.Round(Math.Clamp(this.Alpha, 0, 1) * 255));
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Middle as well as left: touch shells send a one-finger drag as a middle drag (lion.rs).
			if (mouseEvent.Button == MouseButtons.Left || mouseEvent.Button == MouseButtons.Middle)
			{
				this.rotating = true;
				this.BeginRotateGrip(mouseEvent.Position);
			}
			else if (mouseEvent.Button == MouseButtons.Right)
			{
				this.skewing = true;
				this.ApplySkew(mouseEvent.Position);
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.NumPositions >= 2)
			{
				// A pinch replaces the one-finger rotate while it lasts.
				this.FoldTouches(mouseEvent.GetPosition(0), mouseEvent.GetPosition(1));
			}
			else
			{
				this.lastTouches = null;
				if (this.rotating)
				{
					this.ApplyRotate(mouseEvent.Position);
				}
				else if (this.skewing)
				{
					this.ApplySkew(mouseEvent.Position);
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.rotating = false;
			this.skewing = false;
			this.lastTouches = null;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.FromTrackpadPinch)
			{
				// The pinch's two-finger moves zoom and turn the lion (FoldTouches); the wheel copy would zoom twice.
				mouseEvent.Handled = true;
				base.OnMouseWheel(mouseEvent);
				return;
			}

			// Exponential zoom, so in and out are symmetric and never cross zero; one notch (120) is lion.rs's
			// one wheel line of 0.1.
			this.MouseScale = Math.Clamp(this.MouseScale * Math.Exp(mouseEvent.WheelDelta / 120.0 * 0.1), 0.05, 50);
			if (this.rotating)
			{
				// Re-anchor so the next move does not undo the wheel.
				this.BeginRotateGrip(mouseEvent.Position);
			}

			mouseEvent.Handled = true;
			this.Invalidate();
			base.OnMouseWheel(mouseEvent);
		}

		private void BeginRotateGrip(Vector2 position)
		{
			Vector2 delta = position - new Vector2(this.Width / 2, this.Height / 2);
			this.gripAngle = Math.Atan2(delta.Y, delta.X);
			this.gripDistance = delta.Length;
			this.startAngle = this.Angle;
			this.startScale = this.MouseScale;
		}

		private void ApplyRotate(Vector2 position)
		{
			Vector2 delta = position - new Vector2(this.Width / 2, this.Height / 2);
			this.Angle = this.startAngle + (Math.Atan2(delta.Y, delta.X) - this.gripAngle);

			// A press on the centre has no radius to scale against, so the scale stays put.
			if (this.gripDistance > 1e-3)
			{
				this.MouseScale = this.startScale * (delta.Length / this.gripDistance);
			}

			this.Invalidate();
		}

		private void ApplySkew(Vector2 position)
		{
			this.SkewX = position.X;
			this.SkewY = position.Y;
			this.Invalidate();
		}

		/// <summary>lion.rs fold_multi_touch from raw positions: the spread ratio zooms, the twist rotates and the
		/// midpoint's motion pans, each a delta since the previous two-position move.</summary>
		private void FoldTouches(Vector2 a, Vector2 b)
		{
			if (this.lastTouches != null)
			{
				Vector2 was = this.lastTouches[1] - this.lastTouches[0];
				Vector2 now = b - a;
				if (was.Length > 1e-3)
				{
					this.MouseScale = Math.Clamp(this.MouseScale * (now.Length / was.Length), 0.05, 50);
					this.Angle += Math.Atan2(now.Y, now.X) - Math.Atan2(was.Y, was.X);
				}

				this.Offset += ((a + b) / 2) - ((this.lastTouches[0] + this.lastTouches[1]) / 2);
				this.Invalidate();
			}

			this.lastTouches = new[] { a, b };
			this.rotating = false;
		}
	}
}
