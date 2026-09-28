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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Brings a <see cref="SoftwareKeyboard"/> up at the bottom of the window when a text field takes the focus and
	/// takes it down when the focus leaves - but only while <see cref="InputProfiles.Current"/> is a mobile profile.
	/// On the Desktop default it does nothing, and nothing uses it unless an application creates one, so apps that
	/// never opt in see no keyboard. Dispose it to stop listening.
	/// </summary>
	public sealed class SoftwareKeyboardController : IDisposable
	{
		public SoftwareKeyboardController()
		{
			TextEditWidget.ShowSoftwareKeyboard += this.TextEdit_Show;
			TextEditWidget.HideSoftwareKeyboard += this.TextEdit_Hide;
			InputProfiles.Changed += this.InputProfiles_Changed;
		}

		public SoftwareKeyboard Keyboard { get; } = new SoftwareKeyboard();

		/// <summary>The field the keyboard is typing into while it is up, else null.</summary>
		public TextEditWidget Target { get; private set; }

		public bool IsShown => this.Keyboard.Parent != null;

		public void Dispose()
		{
			TextEditWidget.ShowSoftwareKeyboard -= this.TextEdit_Show;
			TextEditWidget.HideSoftwareKeyboard -= this.TextEdit_Hide;
			InputProfiles.Changed -= this.InputProfiles_Changed;
			this.Hide();
		}

		/// <summary>
		/// Bring the keyboard up for a field, on the field's window. It goes on the topmost widget (the window itself)
		/// because every other ancestor takes the focus when a press inside it lands on nothing focusable - the
		/// window has no parent, so it cannot, and a tap on a key leaves the focus on the field.
		/// </summary>
		public void Show(TextEditWidget field)
		{
			if (!InputProfiles.Current.IsMobileTouch() || field.Parent == null)
			{
				return;
			}

			GuiWidget window = field.TopmostParent();
			if (this.Target == field && this.Keyboard.Parent == window)
			{
				// Already typing into this field; keep the layer and shift the user has chosen.
				return;
			}

			this.Hide();
			this.Target = field;
			this.Keyboard.Target = field;
			this.Keyboard.Style = InputProfiles.Current;
			this.Keyboard.State.Open(field.KeyboardInputMode, field.Text);
			this.Keyboard.DismissRequested += this.Keyboard_DismissRequested;

			// The one keyboard comes and goes with the focus; RemoveChild marks it removed, so clear that to re-add it.
			this.Keyboard.ClearRemovedFlag();
			window.AddChild(this.Keyboard);
		}

		public void Hide()
		{
			this.Keyboard.DismissRequested -= this.Keyboard_DismissRequested;
			this.Keyboard.Parent?.RemoveChild(this.Keyboard);
			this.Keyboard.Target = null;
			this.Target = null;
		}

		private void Keyboard_DismissRequested(object sender, EventArgs e) => this.Hide();

		private void TextEdit_Show(object sender, EventArgs e)
		{
			if (sender is TextEditWidget field)
			{
				this.Show(field);
			}
		}

		private void TextEdit_Hide(object sender, EventArgs e)
		{
			// The notice runs on idle, after any refocus the same press caused, so only hide if the field really
			// has let the focus go.
			if (sender == this.Target && !this.Target.ContainsFocus)
			{
				this.Hide();
			}
		}

		private void InputProfiles_Changed(object sender, EventArgs e)
		{
			if (!InputProfiles.Current.IsMobileTouch())
			{
				this.Hide();
			}
			else
			{
				this.Keyboard.Style = InputProfiles.Current;
				this.Keyboard.Invalidate();
			}
		}
	}
}
