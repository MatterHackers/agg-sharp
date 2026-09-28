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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>
	/// The canvas of the Multi Touch window (agg-gui's MultiTouchView in text_demos/multi_touch.rs, a port of
	/// egui's): an arrow that two or more fingers pinch, turn and slide, thickened by their pressure, which
	/// drifts back home half a second after the fingers lift.
	/// </summary>
	/// <remarks>
	/// Zoom, rotation and translation accumulate in normalised units - the shorter side spans -1 to 1 - so the
	/// arrow keeps its place whatever size the window is.
	/// </remarks>
	public class MultiTouchView : GuiWidget
	{
		/// <summary>How long a released arrow holds still before it starts home (egui's 0.5 s).</summary>
		public const double ResetDelaySeconds = 0.5;

		private readonly DemoTheme demoTheme;

		private readonly Stopwatch stopwatch = Stopwatch.StartNew();

		private readonly MultiTouchGesture gesture = new MultiTouchGesture();

		private double? lastTouchSeconds;

		private double? previousFrameSeconds;

		private bool gestureThisFrame;

		private bool frameRequested;

		public MultiTouchView(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>Raised when <see cref="InputSource"/> may have changed.</summary>
		public event EventHandler InputSourceChanged;

		/// <summary>Seconds on the view's clock; replaceable so a test can step time.</summary>
		public Func<double> Clock { get; set; }

		/// <summary>Multiplicative zoom; 1 is home.</summary>
		public double Zoom { get; private set; } = 1;

		/// <summary>Radians, counter-clockwise.</summary>
		public double Rotation { get; private set; }

		/// <summary>In units of half the shorter side.</summary>
		public Vector2 Translation { get; private set; }

		/// <summary>The latest gesture's pressure, 0 when none.</summary>
		public double Force { get; private set; }

		/// <summary>The latest gesture's finger count, 0 when none.</summary>
		public int NumTouches { get; private set; }

		/// <summary>Whether the arrow has been moved by a wheel or trackpad scroll rather than fingers.</summary>
		public bool RelativePointerGesture { get; private set; }

		/// <summary>agg-gui's status line: what is driving the arrow.</summary>
		public string InputSource => this.gesture.Current is MultiTouchInfo info
			? $"Input source: {info.NumTouches}-finger touch"
			: this.RelativePointerGesture ? "Input source: cursor" : "Input source: none";

		/// <summary>Pixels per normalised unit: half the shorter side.</summary>
		public double UnitScale => Math.Min(this.Width, this.Height) / 2;

		private double Now => this.Clock?.Invoke() ?? this.stopwatch.Elapsed.TotalSeconds;

		/// <summary>Folds one gesture frame into the arrow, as agg-gui's Event::MultiTouch handler.</summary>
		public void Integrate(MultiTouchInfo info)
		{
			this.Zoom *= info.ZoomDelta;
			this.Rotation += info.RotationDelta;
			if (this.UnitScale > 0)
			{
				this.Translation += info.TranslationDelta / this.UnitScale;
			}

			this.Force = info.Force;
			this.NumTouches = info.NumTouches;
			this.lastTouchSeconds = this.Now;
			this.gestureThisFrame = true;
			this.InputSourceChanged?.Invoke(this, EventArgs.Empty);
			this.Invalidate();
		}

		/// <summary>
		/// Drifts the arrow toward home once the fingers have been off it for <see cref="ResetDelaySeconds"/>:
		/// a half-life decay whose half-life itself shrinks to nothing over the next half second (egui's
		/// slowly_reset).
		/// </summary>
		/// <returns>Whether the arrow is still on its way and wants another frame.</returns>
		public bool SlowlyReset(double now, double dt)
		{
			if (this.lastTouchSeconds is not double last)
			{
				return false;
			}

			double sinceLast = now - last;
			if (sinceLast < ResetDelaySeconds)
			{
				return true;
			}

			double t = Math.Clamp((sinceLast - ResetDelaySeconds) / (1 - ResetDelaySeconds), 0, 1);
			double halfLife = Math.Pow(1 - t, 4);
			if (halfLife <= 1e-3)
			{
				this.Zoom = 1;
				this.Rotation = 0;
				this.Translation = Vector2.Zero;
				this.lastTouchSeconds = null;
				return false;
			}

			double factor = Math.Exp(-Math.Log(2) / halfLife * dt);
			this.Zoom = 1 + ((this.Zoom - 1) * factor);
			this.Rotation *= factor;
			this.Translation *= factor;
			return true;
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (this.gesture.Update(mouseEvent) is MultiTouchInfo info)
			{
				this.Integrate(info);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool hadGesture = this.gesture.Current != null;
			this.gesture.Reset();
			if (hadGesture)
			{
				this.InputSourceChanged?.Invoke(this, EventArgs.Empty);
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.FromTrackpadPinch)
			{
				// A trackpad pinch also arrives as two-finger moves, which the gesture reads as a real multi-touch
				// zoom; taken as a scroll here it would slide the arrow as well.
				mouseEvent.WheelDelta = 0;
				base.OnMouseWheel(mouseEvent);
				return;
			}

			// egui's trackpad fallback: ctrl (or cmd) scroll zooms, a plain scroll slides. One notch (120) is
			// taken as agg-gui's 50 px line.
			double deltaY = mouseEvent.WheelDelta / 120.0 * 50;
			double deltaX = mouseEvent.WheelDeltaX / 120.0 * 50;
			if (Keyboard.IsKeyDown(Keys.ControlKey) || Keyboard.IsKeyDown(Keys.LWin))
			{
				this.Zoom *= Math.Clamp(1 + (deltaY * 0.002), 0.2, 5);
			}
			else if (this.UnitScale > 0)
			{
				this.Translation += new Vector2(deltaX, deltaY) / this.UnitScale;
			}

			this.lastTouchSeconds = this.Now;
			this.RelativePointerGesture = true;
			this.InputSourceChanged?.Invoke(this, EventArgs.Empty);
			this.Invalidate();
			mouseEvent.WheelDelta = 0;
			base.OnMouseWheel(mouseEvent);
		}

		/// <summary>The arrow's tail and tip in the view's pixels.</summary>
		public (Vector2 Tail, Vector2 Tip) ArrowEnds()
		{
			double scale = this.UnitScale;
			var center = new Vector2(this.Width / 2, this.Height / 2);
			double sin = Math.Sin(this.Rotation);
			double cos = Math.Cos(this.Rotation);
			Vector2 RotateScale(double x, double y) => new Vector2(this.Zoom * ((x * cos) - (y * sin)), this.Zoom * ((x * sin) + (y * cos)));

			// egui's (-0.5, 0.5) to (0.5, -0.5) is Y-down; bottom-left to top-right in agg's Y-up.
			Vector2 tail = center + ((this.Translation + RotateScale(-0.5, -0.5)) * scale);
			return (tail, tail + (RotateScale(1, 1) * scale));
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double now = this.Now;
			double dt = this.previousFrameSeconds is double previous ? Math.Clamp(now - previous, 0, 0.25) : 1.0 / 60;
			this.previousFrameSeconds = now;

			double strokeWidth = 1;
			bool animating;
			if (this.gestureThisFrame)
			{
				strokeWidth += 10 * this.Force;
				this.gestureThisFrame = false;
				animating = true;
			}
			else
			{
				this.NumTouches = 0;
				this.Force = 0;
				animating = this.SlowlyReset(now, dt);
			}

			DemoPalette palette = this.demoTheme.Palette;
			graphics2D.FillRectangle(this.LocalBounds, palette.PanelFill);

			(Vector2 tail, Vector2 tip) = this.ArrowEnds();
			Color color = palette.TextColor;
			graphics2D.Line(tail, tip, color, strokeWidth * DeviceScale);

			double length = (tip - tail).Length;
			double headLength = length * 0.12;
			if (length > 1 && headLength > 0.5)
			{
				Vector2 along = (tip - tail) / length;
				const double HeadHalfAngle = 0.45;
				Vector2 left = tip - (headLength * Rotate(along, HeadHalfAngle));
				Vector2 right = tip - (headLength * Rotate(along, -HeadHalfAngle));
				var head = new VertexStorage();
				head.MoveTo(tip);
				head.LineTo(left);
				head.LineTo(right);
				head.ClosePolygon();
				graphics2D.Render(head, color);
			}

			base.OnDraw(graphics2D);

			if (animating)
			{
				this.RequestNextFrame();
			}
		}

		private static Vector2 Rotate(Vector2 v, double angle)
			=> new Vector2((v.X * Math.Cos(angle)) - (v.Y * Math.Sin(angle)), (v.X * Math.Sin(angle)) + (v.Y * Math.Cos(angle)));

		/// <summary>One more frame on the next idle while the arrow is still going home, as the Dancing Strings view.</summary>
		private void RequestNextFrame()
		{
			if (this.frameRequested || this.Clock != null)
			{
				return;
			}

			this.frameRequested = true;
			UiThread.RunOnIdle(() =>
			{
				this.frameRequested = false;
				if (!this.HasBeenClosed && this.ActuallyVisibleOnScreen())
				{
					this.Invalidate();
				}
			});
		}
	}
}
