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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A clickable piece of text drawn as a link - the agg-gui Hyperlink: accent-coloured and underlined,
	/// brighter under the mouse, with a hand cursor. A click, or Enter/Space while focused, raises
	/// <see cref="Activated"/> and, when <see cref="Url"/> is set, opens it through <see cref="UrlOpener"/>, or
	/// <see cref="UrlLauncher"/> (the user's browser) when there is none.
	/// </summary>
	/// <remarks>The widget is named "<c>text</c> Link" so automation can click it by name.</remarks>
	public class Hyperlink : GuiWidget
	{
		private readonly ThemeConfig theme;
		private readonly TextWidget label;

		/// <summary>Creates a link showing <paramref name="text"/> that opens <paramref name="url"/> (none by default).</summary>
		public Hyperlink(string text, ThemeConfig theme, string url = null)
		{
			this.theme = theme;
			Url = url;
			Name = text + " Link";
			HAnchor = HAnchor.Absolute;
			VAnchor = VAnchor.Absolute;
			TabStop = true;
			Cursor = Cursors.Hand;

			label = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: LinkColor(false))
			{
				Selectable = false,
			};
			AddChild(label);

			// One design unit under the text for the underline, one more to keep a focus ring off the glyphs.
			var scale = DeviceScale;
			label.Position = new Vector2(scale, 2 * scale);
			LocalBounds = new RectangleDouble(0, 0, Math.Ceiling(label.Width + 2 * scale), Math.Ceiling(label.Height + 2 * scale));
		}

		/// <summary>Raised when the link is clicked or activated from the keyboard.</summary>
		public event EventHandler Activated;

		/// <summary>The address handed to <see cref="UrlOpener"/> on activation, or null for none.</summary>
		public string Url { get; set; }

		/// <summary>
		/// Opens <see cref="Url"/> on activation instead of the default. Null (the default) hands the url to
		/// <see cref="UrlLauncher.Open"/>, which opens only http, https and mailto addresses and does nothing
		/// when the platform installed no launcher.
		/// </summary>
		public Action<string> UrlOpener { get; set; }

		/// <summary>Raises <see cref="Activated"/>, then hands <see cref="Url"/>, when set, to <see cref="UrlOpener"/>
		/// or, without one, to <see cref="UrlLauncher"/>.</summary>
		public void Activate()
		{
			Activated?.Invoke(this, EventArgs.Empty);
			if (!string.IsNullOrEmpty(Url))
			{
				if (UrlOpener != null)
				{
					UrlOpener(Url);
				}
				else
				{
					UrlLauncher.Open(Url);
				}
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Enter || keyEvent.KeyCode == Keys.Space)
			{
				Activate();
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

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var hovered = UnderMouseState != UnderMouseState.NotUnderMouse && Enabled;

			// Read every frame so a live theme switch reaches the link.
			var color = LinkColor(hovered);
			label.TextColor = color;

			base.OnDraw(graphics2D);

			// Underline one design unit below the text box, full text width.
			var y = Math.Round(label.Position.Y - scale) + .5;
			graphics2D.Line(new Vector2(label.Position.X, y), new Vector2(label.Position.X + label.Width, y), color, scale);

			if (Focused)
			{
				graphics2D.Rectangle(LocalBounds, theme.EditFieldColors.Focused.BorderColor, scale);
			}
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				Activate();
			}

			base.OnClick(mouseEvent);
		}

		/// <summary>The accent pushed to readable contrast on the background, lighter or darker under the mouse.</summary>
		private Color LinkColor(bool hovered)
		{
			var color = theme.PrimaryAccentColor.WithContrast(theme.BackgroundColor, 3).ToColor();
			return hovered ? color.WithContrast(theme.BackgroundColor, 4.5).ToColor() : color;
		}
	}
}
