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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets
{
	/// <summary>
	/// The Misc Demos sections that exercise small widget features - agg-gui's misc_sections.rs: the angle
	/// DragValue and password reveal, the colour picker and named swatches, the check box and radio walls with
	/// the tri-state "check all", the custom collapsing header and the painted icon.
	/// </summary>
	internal static class MiscDemoSections
	{
		/// <summary>The reveal button's glyph while the password is hidden: Font Awesome's eye, as agg-gui's.</summary>
		public const string ShowPassword = IconFont.Eye;

		/// <summary>The reveal button's glyph while the password shows: eye-slash.</summary>
		public const string HidePassword = IconFont.EyeSlash;

		private static readonly (string Name, Color Color)[] NamedColors =
		{
			("Red", new ColorF(0.88, 0.25, 0.18).ToColor()),
			("Orange", new ColorF(0.92, 0.55, 0.15).ToColor()),
			("Yellow", new ColorF(0.92, 0.85, 0.15).ToColor()),
			("Green", new ColorF(0.25, 0.78, 0.30).ToColor()),
			("Cyan", new ColorF(0.22, 0.65, 0.88).ToColor()),
			("Blue", new ColorF(0.22, 0.45, 0.88).ToColor()),
			("Purple", new ColorF(0.60, 0.25, 0.88).ToColor()),
			("Pink", new ColorF(0.88, 0.25, 0.65).ToColor()),
		};

		/// <summary>egui's angle drag (degrees with a ° suffix and a live ≈ Nτ readout) and a password field with a reveal toggle.</summary>
		public static GuiWidget MiscWidgets(MiscDemoKit kit)
		{
			FlowLayoutWidget column = kit.Column();

			FlowLayoutWidget angleRow = kit.Row(8);
			angleRow.AddChild(kit.Label("An angle:", 12.5));
			var angle = new DragValue(120, -360, 360, kit.Theme)
			{
				Name = "Misc Angle",
				Speed = 1,
				Decimals = 0,
				Suffix = "°",
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(8, 0),
			};
			angleRow.AddChild(angle);
			TextWidget turns = kit.Label(TurnsText(angle.Value), 12.5);
			turns.Name = "Misc Angle Turns";
			angle.ValueChanged += (s, e) => turns.Text = TurnsText(angle.Value);
			angleRow.AddChild(turns);
			column.AddChild(angleRow);

			FlowLayoutWidget passwordRow = kit.Row(8);
			passwordRow.AddChild(kit.Label("Password:", 12.5));
			var password = new ThemedPasswordTextEditWidget("", kit.Theme, pixelWidth: 140 * GuiWidget.DeviceScale, messageWhenEmptyAndNotSelected: "hunter2")
			{
				Name = "Misc Password",
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(8, 0),
			};
			passwordRow.AddChild(password);
			ThemedIconButton reveal = kit.GlyphButton("Misc Password Reveal", ShowPassword, 13);
			reveal.Click += (s, e) =>
			{
				password.Hidden = !password.Hidden;
				kit.SetGlyph(reveal, password.Hidden ? ShowPassword : HidePassword);
			};
			passwordRow.AddChild(reveal);
			column.AddChild(passwordRow);

			return column;
		}

		/// <summary>The angle in whole turns (τ = 360°), as egui's readout shows it.</summary>
		public static string TurnsText(double degrees) => $"≈ {degrees / 360:0.000}τ";

		/// <summary>An editable colour (an always-open colour wheel) above the eight named swatches.</summary>
		public static GuiWidget Colors(MiscDemoKit kit)
		{
			FlowLayoutWidget column = kit.Column();
			column.AddChild(kit.Label("Click the swatch to edit this sRGBA color:", 11.5));
			// misc_sections.rs shows the colour wheel inline, always open.
			var picker = new ColorWheelPicker(NamedColors[5].Color, kit.Theme)
			{
				Name = "Misc Color Picker",
				Margin = new BorderDouble(0, 4),
			};
			column.AddChild(picker);
			column.AddChild(kit.Label("(Premultiplied and unmultiplied u8 & f32 variants also exist.)", 10.5));

			foreach ((string name, Color color) in NamedColors)
			{
				column.AddChild(new MiscSwatchRow(name, color, kit.Label(name, 11.5)));
			}

			return column;
		}

		/// <summary>
		/// 64 unlabelled check boxes sharing one bool, 64 unlabelled radios plus "radio_value" in one group,
		/// and a tri-state "Check/uncheck all" over three items.
		/// </summary>
		public static GuiWidget Checkboxes(MiscDemoKit kit)
		{
			FlowLayoutWidget column = kit.Column();
			column.AddChild(kit.Label("Checkboxes with empty labels take up very little space:", 11.5));

			// egui binds them all to one dummy bool, so toggling any toggles all.
			var shared = new List<CheckBox>();
			void Share(CheckBox source)
			{
				foreach (CheckBox other in shared)
				{
					other.Checked = source.Checked;
				}
			}

			for (int row = 0; row < 4; row++)
			{
				FlowLayoutWidget boxRow = kit.Row(2);
				for (int i = 0; i < 16; i++)
				{
					CheckBox box = kit.CheckBox($"Misc Checkbox {row * 16 + i}", "", false, 11);
					box.Margin = new BorderDouble(0, 0, 2, 0);
					boxRow.AddChild(box);
					shared.Add(box);
				}

				column.AddChild(boxRow);
			}

			CheckBox labelled = kit.CheckBox("Misc Checkbox Labelled", "checkbox", false);
			labelled.HAnchor = HAnchor.Left;
			labelled.Margin = new BorderDouble(0, 4);
			column.AddChild(labelled);
			shared.Add(labelled);
			foreach (CheckBox box in shared)
			{
				box.CheckedStateChanged += (s, e) => Share((CheckBox)s);
			}

			column.AddChild(kit.Label("Radiobuttons are similar:", 11.5));
			var radioWrap = new FlowLeftRightWithWrapping { Name = "Misc Radios", RowMargin = new BorderDouble(0), RowPadding = new BorderDouble(0) };
			var group = new List<GuiWidget>();
			for (int i = 0; i <= 64; i++)
			{
				RadioButton radio = kit.Radio($"Misc Radio {i}", i == 64 ? "radio_value" : "");

				// The wrap parents each row separately, so the group is named rather than taken from the parent.
				radio.SiblingRadioButtonList = group;
				radio.Checked = i == 0;
				group.Add(radio);
				radioWrap.AddChild(radio);
			}

			column.AddChild(radioWrap);

			column.AddChild(kit.Label("Checkboxes can be in an indeterminate state:", 11.5));
			CheckBox all = kit.CheckBox("Misc Check All", "Check/uncheck all", false);
			all.HAnchor = HAnchor.Left;
			column.AddChild(all);
			var items = new List<CheckBox>();
			for (int i = 0; i < 3; i++)
			{
				CheckBox item = kit.CheckBox($"Misc Item {i + 1}", $"Item {i + 1}", i == 0);
				item.HAnchor = HAnchor.Left;
				items.Add(item);
				column.AddChild(item);
			}

			bool syncing = false;
			void ShowAll()
			{
				syncing = true;
				all.Checked = items.All(c => c.Checked);
				all.Indeterminate = items.Any(c => c.Checked) && !all.Checked;
				syncing = false;
			}

			all.CheckedStateChanged += (s, e) =>
			{
				if (!syncing)
				{
					syncing = true;
					foreach (CheckBox item in items)
					{
						item.Checked = all.Checked;
					}

					syncing = false;
					ShowAll();
				}
			};
			foreach (CheckBox item in items)
			{
				item.CheckedStateChanged += (s, e) =>
				{
					if (!syncing)
					{
						ShowAll();
					}
				};
			}

			ShowAll();
			return column;
		}

		/// <summary>
		/// agg-gui's stand-in for egui's custom collapsing header: a check box in the header row that shows or
		/// hides the body below it.
		/// </summary>
		public static GuiWidget CustomCollapsing(MiscDemoKit kit)
		{
			FlowLayoutWidget column = kit.Column();
			CheckBox toggle = kit.CheckBox("Misc Custom Header Toggle", "Show body (toggle in header row)", true);
			toggle.HAnchor = HAnchor.Left;
			column.AddChild(toggle);

			FlowLayoutWidget body = kit.Column();
			body.Name = "Misc Custom Header Body";
			body.Padding = new BorderDouble(8);
			body.AddChild(kit.Wrapped("The checkbox above is embedded in the header row and toggles this body.", 11.5));
			column.AddChild(body);
			toggle.CheckedStateChanged += (s, e) => body.Visible = toggle.Checked;
			return column;
		}

		/// <summary>egui's "Misc" section: a label beside an icon painted from path commands.</summary>
		public static GuiWidget PaintIcon(MiscDemoKit kit)
		{
			FlowLayoutWidget row = kit.Row(8);
			TextWidget label = kit.Label("You can pretty easily paint your own small icons:");
			label.Margin = new BorderDouble(0, 0, 8, 0);
			row.AddChild(label);
			row.AddChild(new MiscPaintedIcon());
			return row;
		}
	}
}
