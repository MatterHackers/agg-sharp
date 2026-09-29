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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The theme-token looks: the Strip segmented control and its equal-share stretch, SelectableCard, InfoBox,
	/// the section header and primary button factories, and the opt-in button outline, hover shading and
	/// rounded fields. Pixels are read in agg's bottom-up coordinates, offset by <see cref="HostPad"/>.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: every test pins the process-wide <see cref="GuiWidget.DeviceScale"/> to 1
	/// and points <see cref="ThemeConfig.Current"/> at the theme under test.
	/// </remarks>
	[NotInParallel]
	public class ThemeWidgetTests
	{
		private const int HostPad = 4;

		private static readonly Color HostBackground = new Color(128, 128, 128);
		private static readonly Color Accent = new Color("#1f5f7a");
		private static readonly Color Border = new Color("#c9c6bd");
		private static readonly Color Tint = new Color("#eef4f6");
		private static readonly Color TintBorder = new Color("#c8dbe2");

		/// <summary>The approved design's values, set explicitly so these tests do not depend on DefaultTheme.</summary>
		private static ThemeConfig DesignTheme() => new ThemeConfig
		{
			TextColor = new Color("#1d2226"),
			PrimaryAccentColor = Accent,
			ButtonBackgroundColor = Color.White,
			ControlFillColor = Color.White,
			ControlBorderColor = Border,
			AccentTintColor = Tint,
			AccentTintBorderColor = TintBorder,
			ButtonRadius = 8,
		};

		private static async Task WithScaleOne(ThemeConfig theme, Func<Task> test)
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				ThemeConfig.Current = theme;
				await test();
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		private static ImageBuffer Render(GuiWidget widget, Vector2? mouse = null)
		{
			var host = new GuiWidget(Math.Ceiling(widget.Width) + HostPad * 2, Math.Ceiling(widget.Height) + HostPad * 2)
			{
				BackgroundColor = HostBackground,
			};
			widget.Position = new Vector2(HostPad, HostPad);
			host.AddChild(widget);
			if (mouse is Vector2 at)
			{
				host.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, HostPad + at.X, HostPad + at.Y, 0));
			}

			var image = new ImageBuffer((int)host.Width, (int)host.Height);
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(HostBackground);
			host.OnDraw(graphics2D);
			return image;
		}

		/// <summary>The pixel at widget-local (x, y).</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(HostPad + x, HostPad + y);

		private static bool Near(Color a, Color b, int tolerance = 6)
			=> Math.Abs(a.red - b.red) <= tolerance && Math.Abs(a.green - b.green) <= tolerance && Math.Abs(a.blue - b.blue) <= tolerance;

		[Test]
		public async Task StripDrawsWhiteTrackSquareDividersAndEdgeToEdgeSelection()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var control = new SegmentedControl(new[] { "Fast", "Medium", "Best" }, theme, 1, SegmentedStyle.Strip);
				control.Width = 300;
				var image = Render(control);
				int top = (int)control.Height - 1;

				await Assert.That(Near(At(image, 150, 2), Accent)).IsTrue().Because("the selected segment fills with the accent");
				await Assert.That(Near(At(image, 101, 1), Accent)).IsTrue().Because("edge to edge: no inset gap beside the divider or the outline");
				await Assert.That(Near(At(image, 50, 3), Color.White)).IsTrue().Because("the track is the control fill");
				await Assert.That(Near(At(image, 99, top / 2), Border)).IsTrue().Because("the divider is a crisp 1 px column ending on the boundary");
				await Assert.That(Near(At(image, 99, 1), Border)).IsTrue().Because("dividers run the full height, square at the ends");
				await Assert.That(Near(At(image, 50, 0), Border)).IsTrue().Because("a 1 px outline runs round the strip");
				await Assert.That(Near(At(image, 0, 0), HostBackground, 40)).IsTrue().Because("the outer corners are rounded");
				await Assert.That(Near(At(image, 199, top / 2), Border)).IsTrue().Because("the divider beside the selection is drawn too");

				var selectedLabel = control.Segments[1].Descendants<TextWidget>().Single();
				await Assert.That(selectedLabel.Bold).IsTrue();
				await Assert.That(selectedLabel.TextColor).IsEqualTo(theme.OnAccentTextColor);
				await Assert.That(control.Segments[0].Descendants<TextWidget>().Single().Bold).IsFalse();
			});
		}

		[Test]
		public async Task StripSelectionOnAnEndIsClippedToTheRounding()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var control = new SegmentedControl(new[] { "Fast", "Medium", "Best" }, theme, 0, SegmentedStyle.Strip);
				var image = Render(control);

				await Assert.That(Near(At(image, 0, 0), HostBackground, 40)).IsTrue().Because("the accent must not poke a square corner past the outline");
				await Assert.That(Near(At(image, 10, 2), Accent)).IsTrue();
			});
		}

		[Test]
		public async Task StretchedControlSharesItsWidthEqually()
		{
			var theme = new ThemeConfig();
			await WithScaleOne(theme, async () =>
			{
				var control = new SegmentedControl(new[] { "A", "Much longer", "C" }, theme) { HAnchor = HAnchor.Stretch };
				var natural = control.Width;
				var parent = new GuiWidget(natural * 2, 50);
				parent.AddChild(control);
				parent.PerformLayout();

				await Assert.That(control.Width).IsEqualTo(natural * 2);
				foreach (var (segment, i) in control.Segments.Select((s, i) => (s, i)))
				{
					await Assert.That(segment.Width).IsEqualTo(natural * 2 / 3);
					await Assert.That(segment.Position.X).IsEqualTo(i * natural * 2 / 3);
				}
			});
		}

		[Test]
		public async Task StretchedControlInANarrowParentClipsAtItsNaturalSegmentWidth()
		{
			var theme = new ThemeConfig();
			await WithScaleOne(theme, async () =>
			{
				var control = new SegmentedControl(new[] { "A", "Much longer", "C" }, theme) { HAnchor = HAnchor.Stretch };
				var naturalSegment = control.Segments[0].Width;
				var parent = new GuiWidget(control.Width / 2, 50);
				parent.AddChild(control);
				parent.PerformLayout();

				await Assert.That(control.Width).IsEqualTo(parent.Width).Because("a stretched control follows a narrow parent, as it always did");
				await Assert.That(control.Segments[1].Width).IsEqualTo(naturalSegment).Because("below its natural width it clips rather than squeezing labels");
				await Assert.That(control.Segments[1].Position.X).IsEqualTo(naturalSegment);
			});
		}

		[Test]
		public async Task StripStaysCrispAtAFractionalScale()
		{
			var theme = DesignTheme();
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1.5;
				ThemeConfig.Current = theme;
				var control = new SegmentedControl(new[] { "Fast", "Medium", "Best" }, theme, 1, SegmentedStyle.Strip);
				control.Width = 3 * Math.Ceiling(control.Segments[0].Width) + 1.5;
				var image = Render(control);
				int y = 4;

				foreach (var segment in control.Segments)
				{
					await Assert.That(segment.Position.X).IsEqualTo(Math.Round(segment.Position.X)).Because("segment boundaries land on whole pixels");
					await Assert.That(segment.Width).IsEqualTo(Math.Round(segment.Width));
				}

				int left = (int)control.Segments[1].Position.X;
				int right = (int)(control.Segments[1].Position.X + control.Segments[1].Width);
				await Assert.That(At(image, left - 1, y)).IsEqualTo(Border).Because("the divider is one whole, unblended pixel");
				await Assert.That(At(image, left, y)).IsEqualTo(Accent).Because("the selection meets the divider with no sliver");
				await Assert.That(At(image, right - 1, y)).IsEqualTo(Border);
				await Assert.That(At(image, right, y)).IsEqualTo(Color.White);
				await Assert.That(At(image, (int)control.Width / 2 + 20, 0)).IsEqualTo(Border).Because("the outline is a whole pixel too");
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		[Test]
		public async Task CardGroupFollowsCodeAndAlreadySelectedCards()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var group = new SelectableCardGroup();
				int changes = 0;
				group.SelectionChanged += (s, e) => changes++;
				var first = group.Add(new SelectableCard("First", "", theme) { Selected = true });
				await Assert.That(group.SelectedCard).IsSameReferenceAs(first);
				await Assert.That(changes).IsEqualTo(1).Because("adding a selected card selects it in the group");

				var second = group.Add(new SelectableCard("Second", "", theme) { Selected = true });
				await Assert.That(first.Selected).IsFalse();
				await Assert.That(group.SelectedCard).IsSameReferenceAs(second);
				await Assert.That(changes).IsEqualTo(2);

				second.Selected = false;
				await Assert.That(group.SelectedCard).IsNull().Because("deselecting from code leaves nothing selected");
				await Assert.That(changes).IsEqualTo(3);
			});
		}

		[Test]
		public async Task ArrowKeysMoveBetweenCardsInAGroup()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var group = new SelectableCardGroup();
				var cards = Enumerable.Range(0, 3).Select(i => group.Add(new SelectableCard("Card" + i, "", theme))).ToList();
				cards[0].Selected = true;

				var right = new KeyEventArgs(Keys.Right);
				cards[0].OnKeyDown(right);
				await Assert.That(group.SelectedCard).IsSameReferenceAs(cards[1]);
				await Assert.That(right.Handled).IsTrue();

				cards[1].OnKeyDown(new KeyEventArgs(Keys.Down));
				cards[2].OnKeyDown(new KeyEventArgs(Keys.Right));
				await Assert.That(group.SelectedCard).IsSameReferenceAs(cards[2]).Because("the last card stops, like a radio group");

				cards[2].OnKeyDown(new KeyEventArgs(Keys.Up));
				cards[1].OnKeyDown(new KeyEventArgs(Keys.Left));
				await Assert.That(group.SelectedCard).IsSameReferenceAs(cards[0]);
			});
		}

		private static SelectableCard Card(ThemeConfig theme, bool selected)
		{
			var card = new SelectableCard("One object", "On a plain background.", theme)
			{
				HAnchor = HAnchor.Absolute,
				Width = 200,
				Selected = selected,
			};
			card.PerformLayout();
			return card;
		}

		[Test]
		public async Task CardSelectedAndUnselectedKeepTheirSizeAndDrawTheirEdges()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var off = Card(theme, false);
				var on = Card(theme, true);
				await Assert.That(on.LocalBounds).IsEqualTo(off.LocalBounds).Because("selecting a card must not move anything");

				var offImage = Render(off);
				var onImage = Render(on);
				int mid = (int)(off.Height / 2);

				await Assert.That(Near(At(offImage, 0, mid), HostBackground)).IsTrue().Because("unselected keeps a 1 px margin");
				await Assert.That(Near(At(offImage, 1, mid), Border)).IsTrue().Because("unselected draws a 1 px control border");
				await Assert.That(Near(At(offImage, 2, mid), Color.White)).IsTrue();
				await Assert.That(Near(At(onImage, 0, mid), Accent)).IsTrue().Because("selected draws a 2 px accent edge");
				await Assert.That(Near(At(onImage, 1, mid), Accent)).IsTrue();
				await Assert.That(Near(At(onImage, 2, mid), Tint)).IsTrue().Because("selected fills with the accent tint");
				await Assert.That(Near(At(onImage, 0, 0), HostBackground, 40)).IsTrue().Because("the card's corners are rounded");
			});
		}

		[Test]
		public async Task CardClickRaisesOnceAndKeysSelect()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var card = Card(theme, false);
				int changes = 0;
				card.SelectedChanged += (s, e) => changes++;
				card.InvokeClick();
				card.InvokeClick();
				await Assert.That(card.Selected).IsTrue();
				await Assert.That(changes).IsEqualTo(1).Because("clicking a selected card is not a change");
				await Assert.That(card.Name).IsEqualTo("One object Card");
				await Assert.That(card.TabStop).IsTrue();

				foreach (var key in new[] { Keys.Space, Keys.Enter })
				{
					var keyed = Card(theme, false);
					var args = new KeyEventArgs(key);
					keyed.OnKeyDown(args);
					await Assert.That(keyed.Selected).IsTrue();
					await Assert.That(args.Handled).IsTrue();
				}
			});
		}

		[Test]
		public async Task CardGroupKeepsOneSelected()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var group = new SelectableCardGroup();
				var first = group.Add(new SelectableCard("First", "", theme) { Selected = true });
				var second = group.Add(new SelectableCard("Second", "", theme));
				int changes = 0;
				group.SelectionChanged += (s, e) => changes++;

				second.InvokeClick();
				await Assert.That(first.Selected).IsFalse();
				await Assert.That(group.SelectedCard).IsSameReferenceAs(second);
				await Assert.That(changes).IsEqualTo(1);
			});
		}

		[Test]
		public async Task InfoBoxDrawsTintInARoundedOutline()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var box = new InfoBox(theme) { HAnchor = HAnchor.Absolute, Width = 200 };
				box.AddChild(new TextWidget("Note"));
				box.PerformLayout();
				var image = Render(box);
				int mid = (int)(box.Height / 2);

				await Assert.That(box.Height).IsGreaterThanOrEqualTo(28).Because("14 units of padding above and below");
				await Assert.That(Near(At(image, 0, mid), TintBorder)).IsTrue().Because("a 1 px tint border");
				await Assert.That(Near(At(image, 1, mid), Tint)).IsTrue();
				await Assert.That(Near(At(image, 190, mid), Tint)).IsTrue();
				await Assert.That(Near(At(image, 0, 0), HostBackground, 40)).IsTrue().Because("ContainerRadius rounds the corners");
			});
		}

		[Test]
		public async Task SectionHeaderAndPrimaryButtonFollowTheTheme()
		{
			var theme = DesignTheme();
			await WithScaleOne(theme, async () =>
			{
				var header = theme.CreateSectionHeader("Quality");
				await Assert.That(header.Name).IsEqualTo("Quality Header");
				await Assert.That(header.Text).IsEqualTo("QUALITY");
				await Assert.That(header.Bold).IsTrue();
				await Assert.That(header.TextColor).IsEqualTo(theme.MutedTextColor);

				var button = theme.CreatePrimaryButton("Build");
				await Assert.That(button.BackgroundColor).IsEqualTo(Accent);
				await Assert.That(button.TextColor).IsEqualTo(theme.OnAccentTextColor);
				await Assert.That(button.Bold).IsTrue();

				var hovered = Render(button, new Vector2(4, button.Height / 2));
				var pixel = At(hovered, 4, (int)(button.Height / 2));
				await Assert.That(Near(pixel, Accent)).IsFalse().Because("hover shades the accent");
				await Assert.That(pixel.red <= Accent.red && pixel.green <= Accent.green && pixel.blue <= Accent.blue).IsTrue()
					.Because("the shade is a darker accent, not a translucent wash");
			});
		}

		[Test]
		public async Task ButtonOutlineAndOpaqueHoverShadeAreOptIn()
		{
			var theme = DesignTheme();
			theme.ButtonBorderWidth = 1;
			theme.ButtonBorderColor = Border;
			theme.ButtonHoverShadesFill = true;
			theme.SlightShade = new Color(0, 0, 0, 20);
			await WithScaleOne(theme, async () =>
			{
				var button = new ThemedTextButton("Go", theme);
				var mid = (int)(button.Height / 2);
				var plain = Render(button);
				await Assert.That(Near(At(plain, 20, 0), Border)).IsTrue().Because("the theme's 1 px outline");
				await Assert.That(Near(At(plain, 3, mid), Color.White)).IsTrue();

				var hovered = Render(new ThemedTextButton("Go", theme), new Vector2(3, mid));
				var shaded = At(hovered, 3, mid);
				await Assert.That(Near(shaded, new BlenderBGRA().Blend(Color.White, theme.SlightShade), 2)).IsTrue()
					.Because("an opaque fill is shaded, not replaced by the translucent hover colour");
			});
		}

		[Test]
		public async Task FieldRadiusRoundsTextFields()
		{
			var theme = DesignTheme();
			theme.FieldRadius = 8;
			theme.EditFieldColors.Inactive.BackgroundColor = Color.White;
			await WithScaleOne(theme, async () =>
			{
				var field = new ThemedTextEditWidget("", theme, pixelWidth: 80);
				var image = Render(field);
				int mid = (int)(field.Height / 2);

				// The border ring sits just outside the widget's bounds, in its Border band.
				await Assert.That(Near(At(image, -1, mid), Border)).IsTrue().Because("ControlBorderColor outlines the field");
				await Assert.That(Near(At(image, 2, mid), Color.White)).IsTrue();
				await Assert.That(Near(At(image, -1, -1), HostBackground, 40)).IsTrue().Because("the field's corners are rounded");
			});
		}
	}
}
