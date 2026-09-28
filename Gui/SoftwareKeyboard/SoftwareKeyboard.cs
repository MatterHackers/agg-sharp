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
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// agg-gui's on-screen keyboard (agg-gui/src/widgets/on_screen_keyboard): a panel of keys that types into a
	/// <see cref="Target"/> widget with the same KeyDown and KeyPress events a physical keyboard sends, so every text
	/// field works with it unchanged. One widget draws and hit-tests all the keys, and it never takes the focus, so a
	/// tap leaves the focus on the field being typed into. <see cref="SoftwareKeyboardController"/> shows it when a text
	/// field takes the focus on a mobile <see cref="InputProfile"/>.
	/// </summary>
	public class SoftwareKeyboard : GuiWidget
	{
		private const double KeyHeight = 42;
		private const double Gap = 6;

		private SoftwareKey pressedKey;

		public SoftwareKeyboard()
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.Height = this.PanelHeight;
			this.Name = "Software Keyboard";
		}

		/// <summary>Raised when the user taps the hide key.</summary>
		public event EventHandler DismissRequested;

		public SoftwareKeyboardState State { get; } = new SoftwareKeyboardState();

		/// <summary>The widget the keys type into; normally the text field that has the focus.</summary>
		public GuiWidget Target { get; set; }

		/// <summary>Chrome follows the profile: iOS light gray with white keys, Android dark with raised gray keys.</summary>
		public InputProfile Style { get; set; } = InputProfile.MobileIOS;

		public double PanelHeight => (4 * KeyHeight + 5 * Gap) * DeviceScale;

		// Taps must leave the focus where it is - on the field being typed into.
		public override bool CanFocus => false;

		/// <summary>Type one key into <see cref="Target"/>: exactly what a tap on it does.</summary>
		public void Press(SoftwareKey key)
		{
			if (key.Kind == SoftwareKeyKind.Dismiss)
			{
				this.DismissRequested?.Invoke(this, EventArgs.Empty);
				return;
			}

			char? typed = this.State.Press(key);
			if (key.Kind == SoftwareKeyKind.Backspace)
			{
				this.SendKey(Keys.Back, null);
			}
			else if (key.Kind == SoftwareKeyKind.Return)
			{
				this.SendKey(Keys.Enter, '\r');
			}
			else if (typed != null)
			{
				this.SendKey(Keys.None, typed.Value);
			}

			this.Invalidate();
		}

		/// <summary>The key under a point in this widget's coordinates, or null between keys.</summary>
		public SoftwareKey KeyAt(Vector2 position)
		{
			foreach (var (key, bounds) in this.KeyBounds())
			{
				if (bounds.Contains(position))
				{
					return key;
				}
			}

			return null;
		}

		/// <summary>Where a key sits in this widget, or an empty rectangle when the current layer does not have it.</summary>
		public RectangleDouble BoundsOf(SoftwareKey key) => this.KeyBounds().FirstOrDefault(k => k.Key == key).Bounds;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			this.pressedKey = this.KeyAt(mouseEvent.Position);
			this.Invalidate();
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			SoftwareKey key = this.pressedKey;
			this.pressedKey = null;
			base.OnMouseUp(mouseEvent);
			if (key != null && this.KeyAt(mouseEvent.Position) == key)
			{
				// The press reached this panel through its ancestors, and one of them took the focus when no child
				// did. Give it back to the field before typing so the keys land there.
				if (this.Target != null && !this.Target.ContainsFocus && this.Target.Parent != null)
				{
					this.Target.Focus();
				}

				this.Press(key);
			}

			this.Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			bool ios = this.Style != InputProfile.MobileAndroid;
			Color panel = ios ? new Color(209, 212, 217) : new Color(33, 33, 36);
			Color keyFill = ios ? Color.White : new Color(59, 59, 64);
			Color specialFill = ios ? new Color(171, 177, 186) : new Color(46, 46, 51);
			Color pressedFill = ios ? new Color(0, 122, 255) : new Color(138, 180, 248);
			Color text = ios ? Color.Black : new Color(232, 234, 237);

			graphics2D.FillRectangle(this.LocalBounds, panel);
			double radius = (ios ? 5 : 3) * DeviceScale;
			foreach (var (key, bounds) in this.KeyBounds())
			{
				bool special = key.Kind != SoftwareKeyKind.Character || key.Character == ' ';
				bool shiftOn = key.Kind == SoftwareKeyKind.Shift && this.State.Shifted;
				Color fill = key == this.pressedKey || shiftOn ? pressedFill : special ? specialFill : keyFill;
				graphics2D.Render(new RoundedRect(bounds, radius), fill);
				string label = key.Label(this.State);
				double pointSize = (label.Length > 1 ? 11 : 15) * GuiWidget.DeviceScale;
				graphics2D.DrawString(label, bounds.Center.X, bounds.Center.Y, pointSize, Justification.Center, Baseline.BoundsCenter, key == this.pressedKey || shiftOn ? Color.White : text);
			}

			base.OnDraw(graphics2D);
		}

		private System.Collections.Generic.IEnumerable<(SoftwareKey Key, RectangleDouble Bounds)> KeyBounds()
		{
			double gap = Gap * DeviceScale;
			double keyHeight = KeyHeight * DeviceScale;
			var rows = this.State.Rows;

			// Every row is laid out on the widest row's key unit, centred, as a phone keyboard indents its middle row.
			double widestUnits = rows.Max(r => r.Sum(k => k.Width));
			int widestCount = rows.First(r => r.Sum(k => k.Width) == widestUnits).Count;
			double unit = Math.Max(1, (this.Width - gap * (widestCount + 1)) / widestUnits);
			for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var row = rows[rowIndex];
				double rowWidth = row.Sum(k => k.Width) * unit + gap * (row.Count - 1);
				double x = (this.Width - rowWidth) / 2;
				double top = this.Height - gap - rowIndex * (keyHeight + gap);
				foreach (SoftwareKey key in row)
				{
					double width = key.Width * unit;
					yield return (key, new RectangleDouble(x, top - keyHeight, x + width, top));
					x += width + gap;
				}
			}
		}

		// A physical key arrives as KeyDown and then, unless the down was suppressed, KeyPress. The text edit widgets
		// act on Back and Enter in KeyDown and insert characters in KeyPress, so the keys follow the same order.
		private void SendKey(Keys keyCode, char? character)
		{
			GuiWidget target = this.Target;
			if (target == null)
			{
				return;
			}

			var keyDown = new KeyEventArgs(keyCode);
			if (keyCode != Keys.None)
			{
				target.OnKeyDown(keyDown);
			}

			if (character != null && !keyDown.SuppressKeyPress)
			{
				target.OnKeyPress(new KeyPressEventArgs(character.Value));
			}
		}
	}
}
