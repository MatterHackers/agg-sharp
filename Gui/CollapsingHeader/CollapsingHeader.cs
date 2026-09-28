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
	/// A section with a clickable header row that shows or hides its body - the agg-gui CollapsingHeader.
	/// The header has a faint tint (deeper under the mouse), a hairline divider along its top and a
	/// triangle that points right when collapsed and down when expanded. Enter or Space toggles it when
	/// it has focus. Starts expanded, like agg-gui's.
	/// </summary>
	/// <remarks>
	/// The header row is named "<c>text</c> Header" so automation can click it by name. A collapsed body
	/// is hidden rather than removed, so it keeps its state and does not take part in layout.
	/// agg-gui swaps the state instantly, with no animation, and so does this.
	/// </remarks>
	public class CollapsingHeader : FlowLayoutWidget
	{
		private const double DesignHeaderHeight = 22;
		private const double DesignTriangleSize = 6;
		private const double DesignIndent = 12;

		private readonly ThemeConfig theme;
		private readonly TextWidget label;
		private bool expanded;

		/// <summary>Creates a section titled <paramref name="text"/> with an empty body.</summary>
		public CollapsingHeader(string text, ThemeConfig theme, bool expanded = true)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.expanded = expanded;
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;
			TabStop = true;

			var scale = DeviceScale;
			Header = new GuiWidget
			{
				Name = text + " Header",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = DesignHeaderHeight * scale,
				Cursor = Cursors.Hand,
			};
			label = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(DesignIndent + DesignTriangleSize * 2 + 4, 0, 0, 0),
				Selectable = false,
			};
			Header.AddChild(label);
			Header.Click += (s, e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					Expanded = !Expanded;
				}
			};
			Header.MouseEnterBounds += (s, e) => Invalidate();
			Header.MouseLeaveBounds += (s, e) => Invalidate();
			AddChild(Header);

			Body = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Name = text + " Body",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(DesignIndent / 2, 0, 0, 0),
				Visible = expanded,
			};
			AddChild(Body);
		}

		/// <summary>Raised when <see cref="Expanded"/> changes, by click, keyboard or code.</summary>
		public event EventHandler ExpandedChanged;

		/// <summary>The clickable header row.</summary>
		public GuiWidget Header { get; }

		/// <summary>The collapsible body: add the section's content here.</summary>
		public GuiWidget Body { get; }

		/// <summary>Whether the body is shown. Setting a different value raises <see cref="ExpandedChanged"/>.</summary>
		public bool Expanded
		{
			get => expanded;
			set
			{
				if (value == expanded)
				{
					return;
				}

				expanded = value;
				Body.Visible = value;
				Invalidate();
				ExpandedChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Enter || keyEvent.KeyCode == Keys.Space)
			{
				Expanded = !Expanded;
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnFocusChanged(EventArgs e)
		{
			Invalidate();
			base.OnFocusChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var row = Header.BoundsRelativeToParent;
			var hovered = Header.UnderMouseState != UnderMouseState.NotUnderMouse && Enabled;

			// Read every frame so a live theme switch reaches the section.
			label.TextColor = theme.TextColor;

			// The tint is always there so a stack of headers reads as section boundaries; hover deepens it.
			var tint = theme.TextColor.WithAlpha(hovered ? 26 : 15);
			graphics2D.FillRectangle(new RectangleDouble(row.Left, row.Bottom, row.Right, row.Top - scale), tint);
			graphics2D.FillRectangle(new RectangleDouble(row.Left, row.Top - scale, row.Right, row.Top), theme.TextColor.WithAlpha(50));

			// Right-pointing when collapsed, down-pointing when expanded (agg-gui's proportions).
			var centerY = row.Center.Y;
			var x = row.Left + DesignIndent * scale;
			var half = DesignTriangleSize * .5 * scale;
			var triangle = new VertexStorage();
			if (expanded)
			{
				triangle.MoveTo(x, centerY + half * .5);
				triangle.LineTo(x + half * 2, centerY + half * .5);
				triangle.LineTo(x + half, centerY - half * .8);
			}
			else
			{
				triangle.MoveTo(x, centerY + half);
				triangle.LineTo(x, centerY - half);
				triangle.LineTo(x + half * 1.6, centerY);
			}

			triangle.ClosePolygon();
			graphics2D.Render(triangle, theme.TextColor.WithAlpha(170));

			if (Focused)
			{
				var inset = .75 * scale;
				var ring = new RoundedRect(row.Left + inset, row.Bottom + inset, row.Right - inset, row.Top - inset, 3 * scale);
				graphics2D.Render(new Stroke(ring, 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
			}

			base.OnDraw(graphics2D);
		}
	}
}
