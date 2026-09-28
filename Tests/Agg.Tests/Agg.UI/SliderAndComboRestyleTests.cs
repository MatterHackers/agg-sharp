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

using System;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.GuiAutomation;
using System.Linq;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins the agg-gui look of <see cref="Slider"/> and <see cref="DropDownList"/> - the accent filled track
	/// and round thumb, the chevron - and, just as much, that the restyle left every size alone: MatterCAD
	/// lays out against these widgets' bounds.
	/// </summary>
	[NotInParallel(new[] { nameof(ThemeConfig.Current), nameof(AutomationRunner.ShowWindowAndExecuteTests) })]
	public class SliderAndComboRestyleTests
	{
		private static readonly Color Accent = new Color(30, 120, 230);

		[After(Test)]
		public void ResetTheme()
		{
			ThemeConfig.Current = null;
		}

		/// <summary>
		/// Captured from main before the restyle, at device scale 1.
		/// </summary>
		[Test]
		public async Task SliderBoundsAreWhatTheyWereBeforeTheRestyle()
		{
			var slider = new Slider(Vector2.Zero, 160);
			await Assert.That(slider.LocalBounds).IsEqualTo(new RectangleDouble(0, -10, 160, 10));

			var vertical = new Slider(Vector2.Zero, 160, orientation: Orientation.Vertical);
			await Assert.That(vertical.LocalBounds).IsEqualTo(new RectangleDouble(-10, 0, 10, 160));
		}

		/// <summary>
		/// Captured from main before the restyle, at device scale 1.
		/// </summary>
		[Test]
		public async Task DropDownSizeIsWhatItWasBeforeTheRestyle()
		{
			var dropDown = new DropDownList("none", Color.Black);
			dropDown.AddItem("A much longer item");
			dropDown.AddItem("B");

			await Assert.That(dropDown.MinimumSize).IsEqualTo(DropDownBaselineMinimumSize);
			await Assert.That(dropDown.Border).IsEqualTo(new BorderDouble(1));
		}

		private static readonly Vector2 DropDownBaselineMinimumSize = new Vector2(177.8359375, 22);

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task SliderFillsTheTrackLeftOfTheThumbInTheThemeAccent(bool dark)
		{
			ThemeConfig.Current = Theme(dark);
			var (root, slider) = NewSlider();

			var image = Render(root);

			// Left of the thumb is the accent, right of it the track - in either theme.
			var left = image.GetPixel(40, 50);
			var right = image.GetPixel(160, 50);
			await Assert.That(IsNear(left, Accent)).IsTrue().Because($"filled track was {left}");
			await Assert.That(IsNear(right, Accent)).IsFalse().Because($"unfilled track was {right}");

			// The thumb is a ring: accent at its edge, the theme background at its middle.
			var center = slider.TransformToScreenSpace(slider.GetThumbHitBounds().Center);
			var middle = image.GetPixel((int)center.X, (int)center.Y);
			await Assert.That(IsNear(middle, ThemeConfig.Current.BackgroundColor)).IsTrue().Because($"thumb middle was {middle}");
			var edge = image.GetPixel((int)center.X - 4, (int)center.Y);
			await Assert.That(IsNear(edge, Accent)).IsTrue().Because($"thumb ring was {edge}");

			Save(image, $"slider-{(dark ? "dark" : "light")}");
		}

		[Test]
		public async Task SliderThumbShadesForHoverAndDragAndGoesGreyDisabled()
		{
			ThemeConfig.Current = Theme(dark: true);
			var (root, slider) = NewSlider();
			var center = slider.TransformToScreenSpace(slider.GetThumbHitBounds().Center);
			int ringX = (int)center.X - 4, ringY = (int)center.Y;

			var normal = Render(root).GetPixel(ringX, ringY);

			root.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, center.X, center.Y, 0));
			var hovered = Render(root).GetPixel(ringX, ringY);

			root.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
			var dragged = Render(root).GetPixel(ringX, ringY);
			root.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));

			slider.Enabled = false;
			var disabledImage = Render(root);
			var disabledFill = disabledImage.GetPixel(40, 50);

			await Assert.That(Luma(hovered)).IsGreaterThan(Luma(normal)).Because("hover lightens the thumb");
			await Assert.That(Luma(dragged)).IsLessThan(Luma(normal)).Because("dragging darkens the thumb");
			await Assert.That(IsNear(disabledFill, Accent)).IsFalse().Because("a disabled slider drops the accent");
			Save(disabledImage, "slider-disabled-dark");
		}

		[Test]
		public async Task ExplicitSliderColorsWinOverTheTheme()
		{
			ThemeConfig.Current = Theme(dark: false);
			var (root, slider) = NewSlider();
			slider.View.ThumbColor = new Color(200, 30, 30);

			var fill = Render(root).GetPixel(40, 50);
			await Assert.That(IsNear(fill, new Color(200, 30, 30))).IsTrue().Because($"filled track was {fill}");
		}

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task DropDownDrawsAChevronInItsTextColor(bool dark)
		{
			ThemeConfig.Current = Theme(dark);
			var theme = ThemeConfig.Current;
			var root = new GuiWidget(200, 60) { BackgroundColor = theme.BackgroundColor };
			var dropDown = new DropDownList("none", theme.TextColor);
			dropDown.AddItem("Item");
			root.AddChild(dropDown);
			dropDown.Position = new Vector2(10, 20);

			var image = Render(root);
			Save(image, $"combo-{(dark ? "dark" : "light")}");

			// A chevron is two strokes meeting at a point: the column through its tip has ink near the
			// bottom of the arrow box but not at the top, where a filled triangle would be solid.
			var arrowBox = new RectangleDouble(dropDown.LocalBounds.Right - DropArrow.ArrowHeight * 4, 0, dropDown.LocalBounds.Right, dropDown.Height);
			var center = dropDown.TransformToScreenSpace(arrowBox.Center);
			int x = (int)Math.Round(center.X);
			int tipY = (int)Math.Round(center.Y + 1 - DropArrow.ArrowHeight / 2);
			int topY = (int)Math.Round(center.Y + 1 + DropArrow.ArrowHeight / 2) - 1;

			await Assert.That(IsNear(image.GetPixel(x, tipY), theme.TextColor, 120)).IsTrue()
				.Because($"the chevron's tip was {image.GetPixel(x, tipY)}");
			await Assert.That(IsNear(image.GetPixel(x, topY), theme.ButtonBackgroundColor, 30)).IsTrue()
				.Because($"above the tip a chevron is open, found {image.GetPixel(x, topY)}");
		}

		/// <summary>
		/// The field is rounded: its outline runs through the Border band the square ring used, so at the very
		/// corner of that band there is background, while the middle of each edge is outline.
		/// </summary>
		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task DropDownFieldIsRoundedInsideTheOldBorderRing(bool dark)
		{
			ThemeConfig.Current = Theme(dark);
			var theme = ThemeConfig.Current;
			var root = new GuiWidget(200, 60) { BackgroundColor = theme.BackgroundColor };
			var dropDown = new DropDownList("none", theme.TextColor) { BorderColor = new Color(255, 0, 0) };
			dropDown.AddItem("Item");
			root.AddChild(dropDown);
			dropDown.Position = new Vector2(10, 20);

			var image = Render(root);
			var inRoot = dropDown.TransformToParentSpace(root, dropDown.LocalBounds);

			// Outer corner pixel of the ring (left-bottom), and the middle of the bottom edge of the ring.
			var corner = image.GetPixel((int)inRoot.Left - 1, (int)inRoot.Bottom - 1);
			var edge = image.GetPixel((int)inRoot.Center.X, (int)inRoot.Bottom - 1);
			await Assert.That(IsNear(corner, theme.BackgroundColor, 30)).IsTrue().Because($"the rounded corner leaves background, found {corner}");
			await Assert.That(edge.red > 150 && edge.green < 100).IsTrue().Because($"the outline runs through the old ring, found {edge}");

			// And the field itself is filled with the theme's button fill.
			var field = image.GetPixel((int)inRoot.Left + 4, (int)inRoot.Center.Y);
			await Assert.That(IsNear(field, theme.ButtonBackgroundColor, 30)).IsTrue().Because($"field fill was {field}");
		}

		/// <summary>
		/// agg-gui paints the current choice in the accent with contrasting text, and a hovered row in a shade.
		/// </summary>
		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task PopupMarksTheSelectedRowApartFromTheHoveredRow(bool dark)
		{
			ThemeConfig.Current = Theme(dark);
			var theme = ThemeConfig.Current;
			var systemWindow = new SystemWindow(300, 200) { BackgroundColor = theme.BackgroundColor };
			var dropDown = new DropDownList("none", theme.TextColor);
			dropDown.AddItem("Item 0");
			dropDown.AddItem("Item 1");
			dropDown.AddItem("Item 2");
			dropDown.SelectedIndex = 1;
			systemWindow.AddChild(dropDown);
			dropDown.Position = new Vector2(10, 150);

			try
			{
				dropDown.InvokeClick();
				var rows = systemWindow.Descendants<MenuItemColorStatesView>().ToList();
				await Assert.That(rows.Count).IsEqualTo(3);

				rows[2].Highlighted = true;

				await Assert.That(rows[1].BackgroundColor).IsEqualTo(Accent).Because("the current choice is an accent row");
				await Assert.That(rows[2].BackgroundColor).IsEqualTo(theme.SlightShade).Because("a hovered row is shaded");
				await Assert.That(rows[0].BackgroundColor).IsNotEqualTo(Accent);

				// Hovering the selected row keeps it marked.
				rows[1].Highlighted = true;
				await Assert.That(rows[1].BackgroundColor).IsEqualTo(Accent);
				rows[1].Highlighted = false;

				Save(Render(systemWindow), $"combo-popup-{(dark ? "dark" : "light")}");
			}
			finally
			{
				for (int i = 0; i < 4; i++)
				{
					UiThread.InvokePendingActions();
				}
			}
		}

		private static ThemeConfig Theme(bool dark)
		{
			var theme = ThemeConfig.DefaultTheme();
			theme.PrimaryAccentColor = Accent;
			if (dark)
			{
				theme.IsDarkTheme = true;
				theme.BackgroundColor = new Color(27, 27, 27);
				theme.TextColor = new Color(230, 230, 235);
				theme.ButtonBackgroundColor = new Color(56, 56, 66);
			}

			theme.SlightShade = theme.PrimaryAccentColor.WithAlpha(80);
			return theme;
		}

		private static (GuiWidget root, Slider slider) NewSlider()
		{
			var root = new GuiWidget(200, 100) { BackgroundColor = ThemeConfig.Current.BackgroundColor };
			var slider = new Slider(new Vector2(20, 50), 160);
			slider.Value = .5;
			root.AddChild(slider);
			return (root, slider);
		}

		private static ImageBuffer Render(GuiWidget widget)
		{
			var image = new ImageBuffer((int)widget.Width, (int)widget.Height);
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(widget.BackgroundColor);
			widget.OnDraw(graphics2D);
			return image;
		}

		private static bool IsNear(Color a, Color b, int tolerance = 40)
		{
			return Math.Abs(a.red - b.red) + Math.Abs(a.green - b.green) + Math.Abs(a.blue - b.blue) <= tolerance;
		}

		private static int Luma(Color c) => c.red * 3 + c.green * 6 + c.blue;

		/// <summary>
		/// Leaves the render where a reviewer can look at it when AGG_RESTYLE_PNG_DIR is set.
		/// </summary>
		private static void Save(ImageBuffer image, string name)
		{
			string dir = Environment.GetEnvironmentVariable("AGG_RESTYLE_PNG_DIR");
			if (!string.IsNullOrEmpty(dir))
			{
				// SaveImageData leaves an existing file alone, so a rerun would keep showing the old render.
				string path = Path.Combine(dir, name + ".png");
				File.Delete(path);
				ImageIO.SaveImageData(path, image);
			}
		}
	}
}
