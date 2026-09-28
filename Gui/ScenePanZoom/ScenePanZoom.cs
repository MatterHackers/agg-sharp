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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A pan/zoom container (agg-gui's widgets::Scene, itself egui's Scene): it hosts one content widget laid out in
	/// scene space and shows it through a <see cref="SceneTransform"/>. Dragging empty space (left or middle button)
	/// pans, the wheel zooms about the cursor, a two-finger move pinches, and a double-click on empty space resets to
	/// the fitted view. The content stays fully interactive: the pan/zoom is the content's ParentToChildTransform, so
	/// drawing, hit-testing and mouse positions all go through it.
	/// </summary>
	public class ScenePanZoom : GuiWidget
	{
		/// <summary>agg-gui's DEFAULT_ZOOM_RANGE.</summary>
		public const double DefaultMinZoom = 0.1;

		public const double DefaultMaxZoom = 2;

		// agg-gui's ZOOM_SENSITIVITY per wheel line; a Win32 detent (120) is one line.
		private const double ZoomPerWheelLine = 0.1;

		// A press that moves further than this is a pan, not a click (agg-gui's MAX_CLICK_DIST).
		private const double MaxClickDistance = 6;

		private bool panning;
		private Vector2 panLast;
		private Vector2 panPress;
		private bool panMoved;
		private bool pressWasDoubleClick;
		private double pinchLastDistance;
		private bool userInteracted;
		private SceneTransform transform = SceneTransform.Identity;

		public ScenePanZoom(GuiWidget content)
		{
			this.Content = content ?? throw new ArgumentNullException(nameof(content));
			// The container places the content itself (through its transform), so no layout engine may move it.
			content.HAnchor = HAnchor.Absolute;
			content.VAnchor = VAnchor.Absolute;
			this.AddChild(content);
			this.ApplyTransform();
		}

		/// <summary>Raised whenever the pan/zoom changes; read <see cref="SceneRect"/> for the visible region.</summary>
		public event EventHandler SceneRectChanged;

		/// <summary>The hosted widget, drawn in scene space.</summary>
		public GuiWidget Content { get; }

		public double MinZoom { get; set; } = DefaultMinZoom;

		public double MaxZoom { get; set; } = DefaultMaxZoom;

		/// <summary>
		/// The scene rectangle the view fits at start and on reset. Null (the default) fits the content's bounds.
		/// </summary>
		public RectangleDouble? DefaultSceneRect { get; set; }

		/// <summary>True while a background drag is panning the view.</summary>
		public bool Panning => this.panning;

		public SceneTransform Transform => this.transform;

		/// <summary>The part of scene space currently visible (agg-gui's Scene::scene_rect).</summary>
		public RectangleDouble SceneRect => this.transform.VisibleSceneRect(this.Width, this.Height);

		/// <summary>Forgets the user's pan/zoom and fits the default scene rect again.</summary>
		public void ResetView()
		{
			this.userInteracted = false;
			this.FitToDefault();
		}

		/// <summary>Pans by <paramref name="screenDelta"/> local pixels, as a background drag does.</summary>
		public void Pan(Vector2 screenDelta)
		{
			this.userInteracted = true;
			this.SetTransform(this.transform.Pan(screenDelta));
		}

		/// <summary>Zooms to <paramref name="newZoom"/> (clamped) about the local point <paramref name="screenAnchor"/>.</summary>
		public void ZoomAt(Vector2 screenAnchor, double newZoom)
		{
			this.userInteracted = true;
			this.SetTransform(this.transform.ZoomAt(screenAnchor, newZoom, this.MinZoom, this.MaxZoom));
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			// Until the user moves the view it tracks the container's size, as agg-gui re-fits on every layout.
			if (!this.userInteracted)
			{
				this.FitToDefault();
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			// A press that no interactive descendant took - it landed on this container or on the content's own
			// background - is a background gesture (agg-gui's "the content declined it, so it bubbled to the Scene").
			bool onBackground = this.MouseCaptured || this.Content.MouseCaptured;
			if (onBackground && (mouseEvent.Button == MouseButtons.Left || mouseEvent.Button == MouseButtons.Middle))
			{
				this.panning = true;
				this.panLast = mouseEvent.Position;
				this.panPress = mouseEvent.Position;
				this.panMoved = false;
				this.pressWasDoubleClick = mouseEvent.Button == MouseButtons.Left && mouseEvent.Clicks >= 2;
				this.pinchLastDistance = 0;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (mouseEvent.NumPositions >= 2)
			{
				this.Pinch(mouseEvent);
				return;
			}

			this.pinchLastDistance = 0;
			if (this.panning)
			{
				Vector2 position = mouseEvent.Position;
				if ((position - this.panPress).LengthSquared > MaxClickDistance * MaxClickDistance)
				{
					this.panMoved = true;
				}

				Vector2 delta = position - this.panLast;
				this.panLast = position;
				this.Pan(delta);
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (this.panning)
			{
				this.panning = false;
				// Only a second click that stayed put resets: a drag, even one that starts with a double-click,
				// keeps its pan (agg-gui's pan_then_press_does_not_reset_view).
				if (this.pressWasDoubleClick && !this.panMoved)
				{
					this.ResetView();
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			// Children first: a scrollable hosted widget zeroes the delta it used, and then the scene must not
			// zoom on the same turn (agg-gui delivers the wheel deepest-first too).
			base.OnMouseWheel(mouseEvent);

			if (mouseEvent.FromTrackpadPinch && this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				// The same pinch also arrives as two-finger moves, which Pinch zooms from; used up here so no
				// ancestor scrolls on it either.
				mouseEvent.WheelDelta = 0;
			}

			if (mouseEvent.WheelDelta != 0 && this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				// Exponential, so a notch in and a notch out cancel exactly.
				double factor = Math.Exp(mouseEvent.WheelDelta / 120.0 * ZoomPerWheelLine);
				this.ZoomAt(mouseEvent.Position, this.transform.Zoom * factor);
				mouseEvent.WheelDelta = 0;
			}
		}

		private void Pinch(MouseEventArgs mouseEvent)
		{
			Vector2 first = mouseEvent.GetPosition(0);
			Vector2 second = mouseEvent.GetPosition(1);
			double distance = (second - first).Length;
			Vector2 middle = (first + second) / 2;
			if (this.pinchLastDistance > 0 && distance > 0)
			{
				// Pan by the midpoint's travel, then zoom about where the midpoint now is.
				this.Pan(middle - this.panLast);
				this.ZoomAt(middle, this.transform.Zoom * distance / this.pinchLastDistance);
			}

			this.pinchLastDistance = distance;
			this.panLast = middle;
			// Two fingers are never a click.
			this.panMoved = true;
		}

		private void FitToDefault()
		{
			RectangleDouble target = this.DefaultSceneRect ?? this.Content.LocalBounds;
			if (this.Width > 0 && this.Height > 0 && target.Width > 0 && target.Height > 0)
			{
				this.SetTransform(SceneTransform.Fit(target, this.Width, this.Height, this.MinZoom, this.MaxZoom));
			}
		}

		private void SetTransform(SceneTransform value)
		{
			this.transform = value;
			this.ApplyTransform();
			this.SceneRectChanged?.Invoke(this, EventArgs.Empty);
			this.Invalidate();
		}

		private void ApplyTransform()
		{
			this.Content.ParentToChildTransform = this.transform.ToAffine();
		}
	}
}
