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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

// The window side of agg-gui's snapping: snap/registry.rs (the targets and the guide buffer), snap/mod.rs's
// enabled flag and widgets/window/snap_glue.rs (the move and resize passes). agg-gui keeps the targets and
// guides thread-local and every window registers itself from layout; here one coordinator per container
// holds the windows it was given, so two containers never snap against each other. As in agg-gui, windows
// snap to each other only - not to the container's edges.

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Snaps the <see cref="WindowWidget"/>s floating in one container to each other while they are dragged by
	/// the title bar or resized by an edge, and shows the <see cref="SnapGuide"/>s of the snap on a
	/// <see cref="SnapGuideOverlay"/> until the drag ends.
	/// </summary>
	/// <remarks>
	/// Works from outside the window: it listens to the title bar's and grab handles'
	/// <see cref="TitleBarWidget.DragMoved"/> / <see cref="GrabControl.DragMoved"/>, which fire after the
	/// window has followed the mouse, and moves the window the last few pixels onto the snap. That never
	/// accumulates, because both drags place the window from where the press was, not from where the previous
	/// move left it. Rectangles are the visible window panels (inside the resize grab border), in the
	/// container's coordinates, so snapped windows sit flush rather than a grab border apart. Only attached
	/// windows that are in the container and visible are targets - a closed window pulls nothing.
	/// </remarks>
	public class SnapCoordinator
	{
		private readonly GuiWidget container;

		private readonly Dictionary<WindowWidget, SnapId> windows = new Dictionary<WindowWidget, SnapId>();

		private bool enabled = true;

		/// <param name="container">The widget the windows float in. The guide overlay is added to it.</param>
		public SnapCoordinator(GuiWidget container)
		{
			this.container = container ?? throw new ArgumentNullException(nameof(container));
			Overlay = new SnapGuideOverlay();
			container.AddChild(Overlay);
		}

		/// <summary>
		/// Whether drags snap - agg-gui's global snap flag, on by default. Turning it off mid-drag clears the
		/// guides; the window then just follows the mouse.
		/// </summary>
		public bool Enabled
		{
			get => enabled;
			set
			{
				enabled = value;
				if (!enabled)
				{
					Overlay.Guides = null;
				}
			}
		}

		/// <summary>How close, in design units, an edge has to come to snap. agg-gui's DEFAULT_THRESHOLD.</summary>
		public double Threshold { get; set; } = SnapEngine.DefaultThreshold;

		/// <summary>The widget in the container that draws the current drag's guides.</summary>
		public SnapGuideOverlay Overlay { get; }

		/// <summary>The guides of the drag in progress; empty when nothing is being dragged or nothing snapped.</summary>
		public IReadOnlyList<SnapGuide> Guides => Overlay.Guides;

		/// <summary>
		/// Makes <paramref name="window"/> snap while it is dragged and a target for the others. The window is
		/// expected to float in the container, though it may come and go from it. Attaching twice does nothing;
		/// closing the window detaches it.
		/// </summary>
		public void Attach(WindowWidget window)
		{
			if (window == null
				|| windows.ContainsKey(window))
			{
				return;
			}

			windows.Add(window, SnapId.Next());
			window.Closed += Window_Closed;
			window.TitleBar.DragMoved += TitleBar_DragMoved;
			window.TitleBar.DragEnded += Drag_Ended;
			foreach (GrabControl grab in window.Children.OfType<GrabControl>())
			{
				grab.DragMoved += Grab_DragMoved;
				grab.DragEnded += Drag_Ended;
			}
		}

		// A closed window is gone for good, so let go of it - hosts with short-lived windows need not Detach.
		private void Window_Closed(object sender, EventArgs e) => Detach((WindowWidget)sender);

		/// <summary>Stops <paramref name="window"/> snapping and being snapped to.</summary>
		public void Detach(WindowWidget window)
		{
			if (window == null
				|| !windows.Remove(window))
			{
				return;
			}

			window.Closed -= Window_Closed;
			window.TitleBar.DragMoved -= TitleBar_DragMoved;
			window.TitleBar.DragEnded -= Drag_Ended;
			foreach (GrabControl grab in window.Children.OfType<GrabControl>())
			{
				grab.DragMoved -= Grab_DragMoved;
				grab.DragEnded -= Drag_Ended;
			}
		}

		/// <summary>
		/// The visible panel of <paramref name="window"/> in the container's coordinates: the window less the
		/// resize grab border around it.
		/// </summary>
		public static RectangleDouble PanelBounds(WindowWidget window)
		{
			BorderDouble inset = GrabBorder(window);
			return new RectangleDouble(window.Position.X + inset.Left,
				window.Position.Y + inset.Bottom,
				window.Position.X + window.Width - inset.Right,
				window.Position.Y + window.Height - inset.Top);
		}

		// The title bar sits in the window's visible panel, which is inset from the window by its margin.
		private static BorderDouble GrabBorder(WindowWidget window) => window.TitleBar.Parent?.DeviceMargin ?? default;

		private void TitleBar_DragMoved(object sender, EventArgs e)
		{
			WindowWidget window = windows.Keys.FirstOrDefault(w => w.TitleBar == sender);
			if (window != null)
			{
				Snap(window, SnapMode.Move);
			}
		}

		private void Grab_DragMoved(object sender, EventArgs e)
		{
			var grab = (GrabControl)sender;
			if (grab.Parent is WindowWidget window
				&& windows.ContainsKey(window))
			{
				Snap(window, SnapMode.Resize(grab.Edge));
			}
		}

		// agg-gui clears the guides on mouse up so the overlay empties when the drag ends.
		private void Drag_Ended(object sender, EventArgs e) => Overlay.Guides = null;

		/// <summary>agg-gui's apply_move_snap / apply_resize_snap: snap the window's panel against the other
		/// windows' panels, put the window where the snapped panel says, and publish the guides.</summary>
		private void Snap(WindowWidget window, SnapMode mode)
		{
			if (!enabled
				|| window.Parent != container)
			{
				Overlay.Guides = null;
				return;
			}

			var targets = windows
				.Where(pair => pair.Key != window && pair.Key.Parent == container && pair.Key.Visible)
				.Select(pair => (pair.Value, PanelBounds(pair.Key)))
				.ToList();

			SnapResult result = SnapEngine.ComputeSnap(PanelBounds(window), windows[window], targets, Threshold * GuiWidget.DeviceScale, mode);
			Place(window, result.Bounds, mode);

			// The minimum size can stop a resize short of the snapped edge; a guide would then mark an
			// alignment that is not there.
			RectangleDouble placed = PanelBounds(window);
			bool reachedSnap = Math.Abs(placed.Left - result.Bounds.Left) < 0.001
				&& Math.Abs(placed.Right - result.Bounds.Right) < 0.001
				&& Math.Abs(placed.Bottom - result.Bounds.Bottom) < 0.001
				&& Math.Abs(placed.Top - result.Bounds.Top) < 0.001;
			Overlay.Guides = reachedSnap ? result.Guides : null;
		}

		private static void Place(WindowWidget window, RectangleDouble panel, SnapMode mode)
		{
			BorderDouble inset = GrabBorder(window);
			var left = panel.Left - inset.Left;
			var bottom = panel.Bottom - inset.Bottom;

			if (mode.IsResize)
			{
				RectangleDouble before = window.BoundsRelativeToParent;
				window.Size = new Vector2(panel.Width + inset.Width, panel.Height + inset.Height);

				// From the size the window actually took, as WindowWidget's handles do: if the minimum size
				// stopped it, the moving edge stops rather than the opposite edge being pushed.
				if (mode.Edge.AffectsLeft())
				{
					left = window.Position.X + before.Width - window.Width;
				}

				if (mode.Edge.AffectsBottom())
				{
					bottom = window.Position.Y + before.Height - window.Height;
				}
			}

			window.Position = new Vector2(left, bottom);
		}
	}
}
