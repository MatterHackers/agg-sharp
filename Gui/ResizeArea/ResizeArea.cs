/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

using System;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A region the user resizes by dragging a grip in its bottom-right corner - agg-gui's (and egui's)
	/// Resize. Add the content as children; the drag moves the right and bottom edges (a top-to-bottom
	/// parent keeps the top where it was), clamped to [<see cref="GuiWidget.MinimumSize"/>,
	/// <see cref="GuiWidget.MaximumSize"/>]. It draws a faint frame and the grip's three diagonal lines.
	/// </summary>
	public class ResizeArea : GuiWidget
	{
		/// <summary>The grip's side, in design units.</summary>
		public const double DesignGripSize = 12;

		private readonly ThemeConfig theme;

		/// <summary>Creates a region <paramref name="width"/> by <paramref name="height"/> device pixels.</summary>
		public ResizeArea(double width, double height, ThemeConfig theme)
			: base(width, height)
		{
			this.theme = theme;
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;

			Grip = new ResizeGrip(this)
			{
				Name = "Resize Grip",
				HAnchor = HAnchor.Right,
				VAnchor = VAnchor.Bottom,
				Width = DesignGripSize * DeviceScale,
				Height = DesignGripSize * DeviceScale,
				Cursor = Cursors.SizeNWSE,
			};
			AddChild(Grip);
		}

		/// <summary>The bottom-right grip the user drags.</summary>
		public GuiWidget Grip { get; }

		/// <summary>
		/// The size a drag asks for: the start size widened by the mouse's rightward travel and made taller by
		/// its downward travel (y is up), clamped to [<paramref name="minimum"/>, <paramref name="maximum"/>].
		/// </summary>
		public static Vector2 SizeForDrag(Vector2 startSize, Vector2 startMouse, Vector2 mouse, Vector2 minimum, Vector2 maximum)
		{
			double width = startSize.X + (mouse.X - startMouse.X);
			double height = startSize.Y - (mouse.Y - startMouse.Y);
			return new Vector2(
				Math.Max(minimum.X, Math.Min(maximum.X, width)),
				Math.Max(minimum.Y, Math.Min(maximum.Y, height)));
		}

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			// The grip stays last (on top), so content added after it cannot cover it.
			if (Grip != null && indexInChildrenList == -1)
			{
				indexInChildrenList = Math.Max(0, Children.IndexOf(Grip));
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var scale = DeviceScale;
			var frame = LocalBounds;
			frame.Inflate(-.5 * scale);
			graphics2D.Render(new Stroke(new RoundedRect(frame, 2 * scale), scale), theme.TextColor.WithAlpha(40));

			// Three diagonal strokes toward the corner, egui's resize grip.
			var grip = Grip.BoundsRelativeToParent;
			var color = theme.TextColor.WithAlpha(Grip.UnderMouseState != UnderMouseState.NotUnderMouse ? 200 : 120);
			for (int i = 1; i <= 3; i++)
			{
				double offset = grip.Width * i / 4;
				var line = new VertexStorage();
				line.MoveTo(grip.Right - offset, grip.Bottom + 2 * scale);
				line.LineTo(grip.Right - 2 * scale, grip.Bottom + offset);
				graphics2D.Render(new Stroke(line, scale), color);
			}
		}

		/// <summary>The corner grip; it captures the mouse on press and resizes its owner while dragged.</summary>
		private class ResizeGrip : GuiWidget
		{
			private readonly ResizeArea owner;
			private Vector2 startSize;
			private Vector2 startMouse;
			private bool dragging;

			public ResizeGrip(ResizeArea owner)
			{
				this.owner = owner;
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.Button == MouseButtons.Left)
				{
					dragging = true;
					startSize = owner.Size;

					// Screen space, because the grip moves with the edges it drags.
					startMouse = TransformToScreenSpace(mouseEvent.Position);
				}

				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (dragging)
				{
					owner.Size = SizeForDrag(startSize, startMouse, TransformToScreenSpace(mouseEvent.Position), owner.MinimumSize, owner.MaximumSize);
				}

				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				dragging = false;
				base.OnMouseUp(mouseEvent);
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				owner.Invalidate();
				base.OnMouseEnterBounds(mouseEvent);
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				owner.Invalidate();
				base.OnMouseLeaveBounds(mouseEvent);
			}
		}
	}
}
