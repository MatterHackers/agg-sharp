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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins <see cref="ThemeConfig.DefaultTheme"/> to the approved design (Main.dc.html): its tokens, and one
	/// telling pixel of each themed widget drawn with it. Set AGG_DEFAULT_THEME_PNG_DIR to also save the
	/// composed panel as default-theme-panel.png for a side by side look at the design.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: it pins <see cref="GuiWidget.DeviceScale"/> to 1 and writes
	/// <see cref="ThemeConfig.Current"/>, which the drop down and check box read while drawing.
	/// </remarks>
	[NotInParallel]
	public class DefaultThemeLookTests
	{
		private static readonly Color Accent = new Color("#1f5f7a");
		private static readonly Color Border = new Color("#c9c6bd");
		private static readonly Color Window = new Color("#fbfaf7");
		private static readonly Color Tint = new Color("#eef4f6");

		[Test]
		public async Task DefaultThemeCarriesTheDesignTokens()
		{
			var theme = ThemeConfig.DefaultTheme();
			await Assert.That(theme.PrimaryAccentColor).IsEqualTo(Accent);
			await Assert.That(theme.TextColor).IsEqualTo(new Color("#1d2226"));
			await Assert.That(theme.SecondaryTextColor).IsEqualTo(new Color("#3c4449"));
			await Assert.That(theme.MutedTextColor).IsEqualTo(new Color("#5b6166"));
			await Assert.That(theme.LightTextColor).IsEqualTo(new Color("#5b6166"));
			await Assert.That(theme.BackgroundColor).IsEqualTo(Window);
			await Assert.That(theme.ButtonBackgroundColor).IsEqualTo(Color.White);
			await Assert.That(theme.ControlFillColor).IsEqualTo(Color.White);
			await Assert.That(theme.ControlBorderColor).IsEqualTo(Border);
			await Assert.That(theme.ButtonBorderWidth).IsEqualTo(1);
			await Assert.That(theme.ButtonBorderColor).IsEqualTo(Border);
			await Assert.That(theme.ButtonHoverShadesFill).IsTrue();
			await Assert.That(theme.ButtonRadius).IsEqualTo(8);
			await Assert.That(theme.FieldRadius).IsEqualTo(8);
			await Assert.That(theme.DropDownRadius).IsEqualTo(8);
			await Assert.That(theme.CardRadius).IsEqualTo(10);
			await Assert.That(theme.ContainerRadius).IsEqualTo(10);
			await Assert.That(theme.AccentTintColor).IsEqualTo(Tint);
			await Assert.That(theme.AccentTintBorderColor).IsEqualTo(new Color("#c8dbe2"));
			await Assert.That(theme.WarningTextColor).IsEqualTo(new Color("#8a4a12"));
			await Assert.That(theme.OnAccentMinimumContrast).IsEqualTo(4.5);
			await Assert.That(ThemeConfig.ContrastRatio(theme.OnAccentTextColor, theme.PrimaryAccentColor)).IsGreaterThanOrEqualTo(4.5);
			await Assert.That(theme.SegmentedHeight).IsEqualTo(40);
			await Assert.That(theme.SegmentedStyle).IsEqualTo(SegmentedStyle.Strip);
			await Assert.That(theme.ButtonDesignHeight).IsEqualTo(36);
			await Assert.That(theme.FieldDesignHeight).IsEqualTo(36);
			await Assert.That(theme.EditFieldColors.Inactive.BorderColor).IsEqualTo(Border);
			await Assert.That(theme.EditFieldColors.Hovered.BorderColor).IsEqualTo(Accent);
			await Assert.That(theme.EditFieldColors.Focused.BorderColor).IsEqualTo(Accent);
			await Assert.That(theme.EditFieldColors.Inactive.BackgroundColor).IsEqualTo(Color.White);
		}

		[Test]
		public async Task EachThemedWidgetDrawsTheDesignLook()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				var theme = ThemeConfig.DefaultTheme();
				ThemeConfig.Current = theme;
				var (panel, parts) = BuildPanel(theme);
				var image = new ImageBuffer((int)panel.Width, (int)panel.Height);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(theme.BackgroundColor);
				panel.OnDraw(graphics2D);
				Save(image);

				Color At(GuiWidget widget, double x, double y)
				{
					// Rounded rather than truncated: layout leaves positions like 71.99999, a hair below the row.
					// Offsets from the widget's bottom-left corner, which is not always its origin (a Fit button's
					// LocalBounds can start below zero).
					var p = widget.TransformToParentSpace(panel, new Vector2(widget.LocalBounds.Left + x, widget.LocalBounds.Bottom + y));
					return image.GetPixel((int)Math.Floor(p.X + 1e-6), (int)Math.Floor(p.Y + 1e-6));
				}

				var button = parts.Button;
				// Layout leaves the button on a fractional row, so its 1 px edge straddles two pixel rows; take the
				// darker, fuller one.
				var edgeRows = new[] { At(button, button.Width / 2, 0), At(button, button.Width / 2, 1) };
				var buttonEdge = edgeRows.OrderBy(c => c.red + c.green + c.blue).First();
				await Assert.That(Near(buttonEdge, Border, 24)).IsTrue().Because($"buttons have a 1 px warm grey border, found {buttonEdge}");
				var primaryText = parts.Primary.TextColor;
				await Assert.That(primaryText.red > 200 && primaryText.green > 200 && primaryText.blue > 200).IsTrue().Because("text on the dark accent is near white");
				await Assert.That(Near(At(button, 3, button.Height / 2), Color.White)).IsTrue().Because("buttons are white");
				var primaryFill = At(parts.Primary, 3, parts.Primary.Height / 2);
				await Assert.That(Near(primaryFill, Accent)).IsTrue().Because($"the primary button is accent filled, found {primaryFill}");
				await Assert.That(Near(At(parts.Field, -1, parts.Field.Height / 2), Border)).IsTrue().Because("fields are outlined in the control border");
				await Assert.That(Near(At(parts.Field, -1, -1), Window, 30)).IsTrue().Because("fields are rounded");
				await Assert.That(Near(At(parts.Number, 4, 4), Color.White)).IsTrue().Because("number fields are white");
				await Assert.That(Near(At(parts.DropDown, 4, parts.DropDown.Height / 2), Color.White)).IsTrue().Because("drop downs are white");
				await Assert.That(Near(At(parts.DropDown, parts.DropDown.Width / 2, -0.5), Border, 20)).IsTrue().Because("drop downs share the control border");
				var selected = parts.Segmented.Segments[1];
				await Assert.That(Near(At(parts.Segmented, selected.Position.X + 3, 3), Accent)).IsTrue().Because("the segmented control is a Strip with an accent selection");
				await Assert.That(Near(At(parts.Segmented, 20, 5), Color.White)).IsTrue();
				await Assert.That(parts.Segmented.Height).IsEqualTo(40);
				await Assert.That(Near(At(parts.Card, 0, parts.Card.Height / 2), Accent)).IsTrue().Because("a selected card has an accent edge");
				await Assert.That(Near(At(parts.Card, 4, parts.Card.Height / 2), Tint)).IsTrue();
				await Assert.That(Near(At(parts.Info, 3, parts.Info.Height / 2), Tint)).IsTrue().Because("an info box is tinted");
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		/// <summary>
		/// Text fields, number fields and drop downs are FieldDesignHeight tall outside their 1 px border - the
		/// same 36 as the buttons beside them - with their text centred.
		/// </summary>
		[Test]
		public async Task FieldsAreFieldDesignHeightWithCentredText()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				var theme = ThemeConfig.DefaultTheme();
				ThemeConfig.Current = theme;
				var (panel, parts) = BuildPanel(theme);
				foreach (GuiWidget field in new GuiWidget[] { parts.Field, parts.Number, parts.DropDown })
				{
					await Assert.That(field.Height + field.DeviceBorder.Height).IsEqualTo(36).Because($"{field.GetType().Name} is as tall as a button");
					var text = field.Descendants<TextWidget>().First(t => t.Visible && t.Text.Length > 0 && t.Text != "mm");
					var textBounds = text.TransformToParentSpace(field, text.LocalBounds);
					await Assert.That(Math.Abs(textBounds.Center.Y - field.LocalBounds.Center.Y)).IsLessThanOrEqualTo(1.5)
						.Because($"{field.GetType().Name}'s text is centred");
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		/// <summary>
		/// A disabled primary button must not look clickable: a pale accent wash with muted text instead of the
		/// teal. A disabled outlined button dims its border and its text.
		/// </summary>
		[Test]
		public async Task DisabledButtonsReadAsUnavailable()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				var theme = ThemeConfig.DefaultTheme();
				ThemeConfig.Current = theme;

				(ImageBuffer image, ThemedTextButton button) Draw(ThemedTextButton button)
				{
					var host = new GuiWidget(120, 60) { BackgroundColor = theme.BackgroundColor };
					button.Position = new Vector2(10, 10);
					host.AddChild(button);
					var image = new ImageBuffer(120, 60);
					var graphics2D = image.NewGraphics2D();
					graphics2D.Clear(theme.BackgroundColor);
					host.OnDraw(graphics2D);
					return (image, button);
				}

				var primary = theme.CreatePrimaryButton("Run");
				primary.Enabled = false;
				var (primaryImage, _) = Draw(primary);
				var fill = primaryImage.GetPixel(10 + 4, 10 + 18);
				await Assert.That(Near(fill, Accent, 40)).IsFalse().Because($"a disabled primary is not teal, found {fill}");
				await Assert.That(fill.red + fill.green + fill.blue).IsGreaterThan(3 * 200).Because($"it is a pale wash, found {fill}");
				await Assert.That(primary.TextColor).IsEqualTo(theme.MutedTextColor).Because("its text is muted, not faded white on a pale fill");

				var (enabledImage, _) = Draw(new ThemedTextButton("Cancel", theme));
				var outlined = new ThemedTextButton("Cancel", theme) { Enabled = false };
				var (disabledImage, _) = Draw(outlined);
				var enabledEdge = enabledImage.GetPixel(40, 10);
				var disabledEdge = disabledImage.GetPixel(40, 10);
				await Assert.That(disabledEdge.red + disabledEdge.green + disabledEdge.blue).IsGreaterThan(enabledEdge.red + enabledEdge.green + enabledEdge.blue + 30)
					.Because($"the border dims, {enabledEdge} to {disabledEdge}");
				await Assert.That(outlined.TextColor.Alpha0To255).IsLessThan(255).Because("the label fades");
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		private sealed class Parts
		{
			public ThemedTextButton Button;
			public ThemedTextButton Primary;
			public ThemedTextEditWidget Field;
			public ThemedNumberEdit Number;
			public DropDownList DropDown;
			public SegmentedControl Segmented;
			public SelectableCard Card;
			public InfoBox Info;
		}

		/// <summary>A settings panel in the design's order, laid out at 420 wide.</summary>
		private static (GuiWidget panel, Parts parts) BuildPanel(ThemeConfig theme)
		{
			var parts = new Parts();
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = new BorderDouble(22, 18),
			};

			column.AddChild(theme.CreateSectionHeader("What are you capturing?"));
			var group = new SelectableCardGroup();
			var cards = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, Margin = new BorderDouble(0, 12, 0, 6) };
			cards.AddChild(parts.Card = group.Add(new SelectableCard("One object", "On a plain background. Closed, printable shape.", theme) { Selected = true }));
			cards.AddChild(group.Add(new SelectableCard("A scene or room", "Everything in view. Open surface.", theme) { Margin = new BorderDouble(left: 10) }));
			column.AddChild(cards);

			column.AddChild(theme.CreateSectionHeader("Quality"));
			column.AddChild(parts.Segmented = new SegmentedControl(new[] { "Fast", "Medium", "High", "Best" }, theme, 1)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 12, 0, 6),
			});

			var dropRow = new FlowLayoutWidget { HAnchor = HAnchor.Stretch, Margin = new BorderDouble(bottom: 12) };
			dropRow.AddChild(new TextWidget("Largest photo size", pointSize: theme.DefaultFontSize, textColor: theme.TextColor) { VAnchor = VAnchor.Center });
			dropRow.AddChild(new HorizontalSpacer());
			parts.DropDown = new DropDownList("1000 px", theme.TextColor, pointSize: theme.DefaultFontSize) { MinimumSize = new Vector2(120, 0) };
			parts.DropDown.AddItem("1000 px");
			parts.DropDown.SelectedIndex = 0;
			dropRow.AddChild(parts.DropDown);
			column.AddChild(dropRow);

			column.AddChild(theme.CreateSectionHeader("Camera"));
			parts.Info = new InfoBox(theme) { Margin = new BorderDouble(0, 12, 0, 6) };
			var infoRow = new FlowLayoutWidget();
			infoRow.AddChild(parts.Field = new ThemedTextEditWidget("26", theme, pixelWidth: 80));
			infoRow.AddChild(parts.Number = new ThemedNumberEdit(5, theme, unitsLabel: "mm", pixelWidth: 72) { Margin = new BorderDouble(left: 10) });
			parts.Info.AddChild(infoRow);
			column.AddChild(parts.Info);

			var buttons = new FlowLayoutWidget { HAnchor = HAnchor.Right | HAnchor.Fit };
			buttons.AddChild(parts.Button = new ThemedTextButton("Cancel", theme));
			buttons.AddChild(parts.Primary = theme.CreatePrimaryButton("Build"));
			column.AddChild(buttons);

			var panel = new GuiWidget(420, 440) { BackgroundColor = theme.BackgroundColor };
			panel.AddChild(column);
			panel.PerformLayout();
			return (panel, parts);
		}

		private static bool Near(Color a, Color b, int tolerance = 8)
			=> Math.Abs(a.red - b.red) <= tolerance && Math.Abs(a.green - b.green) <= tolerance && Math.Abs(a.blue - b.blue) <= tolerance;

		private static void Save(ImageBuffer image)
		{
			string dir = Environment.GetEnvironmentVariable("AGG_DEFAULT_THEME_PNG_DIR");
			if (!string.IsNullOrEmpty(dir))
			{
				// SaveImageData leaves an existing file alone, so a rerun would keep showing the old render.
				string path = Path.Combine(dir, "default-theme-panel.png");
				File.Delete(path);
				ImageIO.SaveImageData(path, image);
			}
		}
	}
}
