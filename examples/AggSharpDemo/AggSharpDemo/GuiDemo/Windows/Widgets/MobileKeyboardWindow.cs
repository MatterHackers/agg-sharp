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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The "Mobile Keyboard" window, agg-gui's demo-ui/src/windows/mobile_keyboard.rs: pick the process-wide
	/// <see cref="InputProfile"/> (Desktop, iPhone or Android) and focus a field to bring up the
	/// <see cref="SoftwareKeyboard"/>. The first field's keyboard layer follows a Text / Numeric radio; the second
	/// always opens on the numbers. The keyboard itself is the shell's <see cref="SoftwareKeyboardController"/>.
	/// </summary>
	public class MobileKeyboardWindow : ScrollableWidget, IScrollFittedDemoContent
	{
		public static readonly InputProfile[] Profiles = { InputProfile.Desktop, InputProfile.MobileIOS, InputProfile.MobileAndroid };

		private const double Gap = 8;

		private readonly MiscDemoKit kit;
		private readonly DemoTheme demoTheme;
		private readonly List<WrappedTextWidget> dimNotes = new List<WrappedTextWidget>();
		private readonly List<ThemedTextEditWidget> fields = new List<ThemedTextEditWidget>();

		public MobileKeyboardWindow(DemoTheme demoTheme)
			: base(autoScroll: true)
		{
			this.demoTheme = demoTheme;
			this.kit = new MiscDemoKit(demoTheme);
			this.Name = "Mobile Keyboard Content";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.ScrollArea.HAnchor = HAnchor.Stretch;

			var column = this.FittedContent = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(12),
			};
			this.AddChild(column);

			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				column.AddChild(widget);
				return widget;
			}

			Spaced(this.kit.Label("Input profile", 13));
			var profileRadios = this.kit.Column();
			string[] profileNames = { "Desktop (no keyboard)", "iPhone", "Android" };
			this.ProfileRadios = new RadioButton[profileNames.Length];
			for (int i = 0; i < profileNames.Length; i++)
			{
				InputProfile profile = Profiles[i];
				RadioButton radio = this.kit.Radio("Mobile Keyboard Profile " + profileNames[i], profileNames[i], 13);
				radio.Checked = InputProfiles.Current == profile;
				radio.CheckedStateChanged += (s, e) =>
				{
					if (radio.Checked)
					{
						InputProfiles.Current = profile;
					}
				};
				this.ProfileRadios[i] = radio;
				profileRadios.AddChild(radio);
			}

			Spaced(profileRadios);
			this.DimNote(Spaced, "Picking iPhone or Android flips the global agg-sharp input profile and enables the on-screen keyboard. Tap the field below - the keyboard slides up and types into it.", 11);

			Spaced(this.kit.Label("First field input mode", 13));
			this.TextModeRadio = this.kit.Radio("Mobile Keyboard Mode Text", "Text (letters)", 13);
			this.NumericModeRadio = this.kit.Radio("Mobile Keyboard Mode Numeric", "Numeric (digit pad)", 13);
			this.TextModeRadio.Checked = true;
			var modeRadios = this.kit.Column();
			modeRadios.AddChild(this.TextModeRadio);
			modeRadios.AddChild(this.NumericModeRadio);
			Spaced(modeRadios);
			this.DimNote(Spaced, "Picking Numeric routes the first field below to the numbers layer the next time it gains focus. Tap the field again after switching to see the new layer.", 11);

			Spaced(this.kit.Label("Type here (mode follows radio)", 12));
			this.PrimaryField = this.Field(Spaced, "Mobile Keyboard Primary Field", "Tap to focus, then type / tap keys...");
			this.NumericModeRadio.CheckedStateChanged += (s, e) =>
				this.PrimaryField.ActualTextEditWidget.KeyboardInputMode = this.NumericModeRadio.Checked ? KeyboardInputMode.Numeric : KeyboardInputMode.Text;

			Spaced(this.kit.Label("Numeric-only field (digit pad)", 12));
			this.NumericField = this.Field(Spaced, "Mobile Keyboard Numeric Field", "Always opens on the numbers layer");
			this.NumericField.ActualTextEditWidget.KeyboardInputMode = KeyboardInputMode.Numeric;

			this.DimNote(Spaced, "Tip: try shift, the 123 / ABC mode switch, and the hide key in the bottom-left of the keyboard to dismiss without changing focus.", 10);

			// Another window or the shell may change the profile; keep the radios showing the real one.
			InputProfiles.Changed += this.InputProfiles_Changed;
			this.Recolor(null, null);
			demoTheme.ThemeChanged += this.Recolor;
		}

		/// <summary>The column inside the scroll area: the window's height is held to it (agg-gui's tight_fit).</summary>
		public GuiWidget FittedContent { get; }

		public RadioButton[] ProfileRadios { get; }

		public RadioButton TextModeRadio { get; }

		public RadioButton NumericModeRadio { get; }

		public ThemedTextEditWidget PrimaryField { get; }

		public ThemedTextEditWidget NumericField { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme and the profile outlive the window.
			this.demoTheme.ThemeChanged -= this.Recolor;
			InputProfiles.Changed -= this.InputProfiles_Changed;
			base.OnClosed(e);
		}

		private void DimNote(Func<GuiWidget, GuiWidget> add, string text, double size)
		{
			WrappedTextWidget note = this.kit.Wrapped(text, size);
			note.HAnchor = HAnchor.Stretch;
			this.dimNotes.Add(note);
			add(note);
		}

		private ThemedTextEditWidget Field(Func<GuiWidget, GuiWidget> add, string name, string placeholder)
		{
			var field = new ThemedTextEditWidget(string.Empty, this.kit.Theme, messageWhenEmptyAndNotSelected: placeholder)
			{
				Name = name,
				HAnchor = HAnchor.Stretch,
			};
			this.fields.Add(field);
			add(field);
			return field;
		}

		private void InputProfiles_Changed(object sender, EventArgs e)
		{
			int index = Array.IndexOf(Profiles, InputProfiles.Current);
			if (index >= 0 && !this.ProfileRadios[index].Checked)
			{
				this.ProfileRadios[index].Checked = true;
			}
		}

		private void Recolor(object sender, EventArgs e)
		{
			DemoPalette palette = this.demoTheme.Palette;
			this.BackgroundColor = palette.PanelFill;
			this.kit.Recolor();
			foreach (WrappedTextWidget note in this.dimNotes)
			{
				note.TextColor = palette.TextDim;
			}

			foreach (ThemedTextEditWidget field in this.fields)
			{
				field.ActualTextEditWidget.TextColor = palette.TextColor;
				field.BackgroundColor = palette.WidgetBackground;
			}
		}
	}
}
