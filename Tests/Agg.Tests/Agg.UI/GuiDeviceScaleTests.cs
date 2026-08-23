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
using System.Linq;
using System.Threading.Tasks;
using Gui.Charting;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A sweep over widgets that sized a piece of themselves from raw device pixels. Padding, Margin and
	/// Border scale themselves, but an explicit Width, Height, MinimumSize, BackgroundRadius, stroke width
	/// or point size does not - so each of these stayed put while the text beside it doubled on a Retina
	/// panel.
	/// </summary>
	/// <remarks>
	/// A stroke <see cref="Graphics2D"/> is left to choose does not rescue any of this either: the default
	/// is resolved against <see cref="Graphics2D.DeviceScale"/>, which is a per instance property that
	/// <see cref="ImageBuffer.NewGraphics2D"/> leaves at 1, and the five argument Rectangle overload does
	/// not resolve it at all. A widget that wants a band to grow has to size the band itself and fill it.
	/// </remarks>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> -
	/// exclusive, not merely serialized against each other - and restore the previous value in a finally.
	/// See <c>SliderDeviceScaleTests</c> and <c>SubMenuArrowDeviceScaleTests</c> for the same pattern.
	/// </remarks>
	public class GuiDeviceScaleTests
	{
		/// <remarks>
		/// The container is Fit in both directions, so its own Width and Height are recomputed from the icon
		/// button and the raw 16 it used to carry never reached the screen. This guards the size the tree
		/// actually lays out with, so a future Absolute anchor cannot quietly pin it to device pixels.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task TreeNodeExpanderGrowsWithDeviceScale()
		{
			Vector2 atOne = AtDeviceScale(1, ExpanderSize);
			Vector2 atTwo = AtDeviceScale(2, ExpanderSize);

			await Assert.That(atTwo.X).IsEqualTo(atOne.X * 2).Within(0.001)
				.Because("the expander container has to keep up with the icon button inside it, which already scaled");
			await Assert.That(atTwo.Y).IsEqualTo(atOne.Y * 2).Within(0.001);

			static Vector2 ExpanderSize()
			{
				var node = new TreeNode(new ThemeConfig());
				GuiWidget expander = node.TitleBar.Children.First(child => child.Name == "Expand Widget");

				return new Vector2(expander.Width, expander.Height);
			}
		}

		[Test]
		[NotInParallel]
		public async Task SplitterBarAndStartingDistanceGrowWithDeviceScale()
		{
			(double size, double distance) atOne = AtDeviceScale(1, SplitterGeometry);
			(double size, double distance) atTwo = AtDeviceScale(2, SplitterGeometry);

			await Assert.That(atTwo.size).IsEqualTo(atOne.size * 2).Within(0.001)
				.Because("the grab bar has to stay the same physical width as ThemeConfig.SplitterWidth");
			await Assert.That(atTwo.distance).IsEqualTo(atOne.distance * 2).Within(0.001)
				.Because("the default split is a size for the panel beside it, so it scales with that panel's content");

			static (double size, double distance) SplitterGeometry()
			{
				var splitter = new Splitter();

				return (splitter.SplitterSize, splitter.SplitterDistance);
			}
		}

		[Test]
		[NotInParallel]
		public async Task DropDownListSeparatorGrowsWithDeviceScale()
		{
			(double height, double minimumHeight) atOne = AtDeviceScale(1, SeparatorGeometry);
			(double height, double minimumHeight) atTwo = AtDeviceScale(2, SeparatorGeometry);

			await Assert.That(atTwo.height).IsEqualTo(atOne.height * 2).Within(0.001);
			await Assert.That(atTwo.minimumHeight).IsEqualTo(atOne.minimumHeight * 2).Within(0.001)
				.Because("the minimum is what actually holds the separator open, so it has to scale too");

			static (double height, double minimumHeight) SeparatorGeometry()
			{
				var dropDownList = new DropDownList("none", Color.Black);
				MenuItem separator = dropDownList.CreateSeparator();

				return (separator.Height, separator.MinimumSize.Y);
			}
		}

		[Test]
		[NotInParallel]
		public async Task MenuHorizontalLineGrowsWithDeviceScale()
		{
			double atOne = AtDeviceScale(1, LineHeight);
			double atTwo = AtDeviceScale(2, LineHeight);

			await Assert.That(atTwo).IsEqualTo(atOne * 2).Within(0.001)
				.Because("a one pixel rule disappears on a Retina panel, so it is a one point rule instead");

			static double LineHeight()
			{
				MenuItem menuItem = new Menu().AddHorizontalLine();

				return menuItem.Children.First().Height;
			}
		}

		/// <remarks>
		/// This measures the painted band rather than a property, because the bug it guards was in how the
		/// band was drawn: a stroked rectangle grows its bounds with the scale but stays a hollow outline,
		/// so only a solid run of accent colored rows proves the underline is filled.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task RadioTextButtonUnderlineGrowsWithDeviceScale()
		{
			double atOne = AtDeviceScale(1, UnderlineHeight);
			double atTwo = AtDeviceScale(2, UnderlineHeight);

			await Assert.That(atOne).IsEqualTo(2)
				.Because("the underline is two points tall, which is two solid device pixels at scale one");
			await Assert.That(atTwo).IsEqualTo(4)
				.Because("the checked marker is the button's own geometry, so it doubles when the label does");

			// The height of the solid accent band along the bottom of a checked button. Everything else the
			// button paints is made white, so the band is all that lands on the white page.
			static double UnderlineHeight()
			{
				var theme = new ThemeConfig
				{
					PrimaryAccentColor = Color.Red
				};

				var button = new ThemedRadioTextButton("", theme)
				{
					SelectedBackgroundColor = Color.White,
					UnselectedBackgroundColor = Color.White,
					BorderColor = Color.White,
					Checked = true
				};

				var image = new ImageBuffer((int)Math.Ceiling(button.Width) + 2, (int)Math.Ceiling(button.Height) + 2);
				Graphics2D graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				button.OnDraw(graphics2D);

				// The button's local origin is its center and nothing translates this draw, so the half of the
				// band at positive x is what lands on the image - sample down the middle of that half. An
				// outline would paint the band's first and last row and leave the rows between them white,
				// so a hollow band counts as one row here instead of as its full height.
				int middle = (int)(button.LocalBounds.Right / 2);

				int height = 0;
				while (height < image.Height && image.GetPixel(middle, height) == theme.PrimaryAccentColor)
				{
					height++;
				}

				return height;
			}
		}

		[Test]
		[NotInParallel]
		public async Task ScrollingPopupLeavesRoomForTheScrollBar()
		{
			double atOne = AtDeviceScale(1, ExtraWidth);
			double atTwo = AtDeviceScale(2, ExtraWidth);

			await Assert.That(atOne).IsEqualTo(ScrollBarWidthAtScale(1)).Within(0.001)
				.Because("the room made for the scroll bar has to be the width the scroll bar actually uses");
			await Assert.That(atTwo).IsEqualTo(ScrollBarWidthAtScale(2)).Within(0.001);

			// How much wider than its content a popup makes itself once it has to scroll.
			static double ExtraWidth()
			{
				const double contentWidth = 100;

				var content = new GuiWidget(contentWidth, 200);
				var popup = new PopupWidget(content, new StubPopupLayoutEngine(maxHeight: 50), makeScrollable: true);

				return popup.Width - contentWidth;
			}

			static double ScrollBarWidthAtScale(double deviceScale)
			{
				return AtDeviceScale(deviceScale, () => ScrollBar.ScrollBarWidth);
			}
		}

		[Test]
		[NotInParallel]
		public async Task GroupBoxBorderInsetGrowsWithDeviceScale()
		{
			double atOne = AtDeviceScale(1, LeftmostPaintedColumn);
			double atTwo = AtDeviceScale(2, LeftmostPaintedColumn);

			await Assert.That(atTwo).IsEqualTo(atOne * 2).Within(1)
				.Because("the frame is inset by a fixed distance from the widget's own edge, in points not pixels");

			static double LeftmostPaintedColumn()
			{
				var groupBox = new GroupBox("")
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					LocalBounds = new RectangleDouble(0, 0, 100, 100)
				};

				var image = new ImageBuffer(100, 100);
				Graphics2D graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				groupBox.OnDraw(graphics2D);

				return LeftmostPaintedColumnOf(image);
			}
		}

		[Test]
		[NotInParallel]
		public async Task CheckBoxFrameInsetGrowsWithDeviceScale()
		{
			// One point of inset is a single pixel at scale 1, so the two scales are picked far enough apart
			// that the gap is several pixels wide and cannot be confused with antialiasing.
			double atOne = AtDeviceScale(1, LeftmostPaintedColumn);
			double atFour = AtDeviceScale(4, LeftmostPaintedColumn);

			await Assert.That(atFour).IsEqualTo(atOne * 4).Within(1)
				.Because("the gap between the widget edge and the box is the view's own geometry");

			static double LeftmostPaintedColumn()
			{
				// An empty label leaves the box as the only thing the view paints.
				var checkBox = new CheckBox("");

				var image = new ImageBuffer((int)Math.Ceiling(checkBox.Width) + 2, (int)Math.Ceiling(checkBox.Height) + 2);
				Graphics2D graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				checkBox.OnDraw(graphics2D);

				return LeftmostPaintedColumnOf(image);
			}
		}

		[Test]
		[NotInParallel]
		public async Task SimpleChartTextGrowsWithDeviceScale()
		{
			double atOne = AtDeviceScale(1, GraphLeftEdge);
			double atTwo = AtDeviceScale(2, GraphLeftEdge);

			// The chart pushes its plot area right by the width of the axis labels, so where the plot starts
			// is a readout of how big those labels came out. Glyph advances do not land on exact multiples,
			// so this asks for the doubling rather than an exact pixel.
			await Assert.That(atTwo / atOne).IsGreaterThan(1.7)
				.Because("the axis labels are drawn at a fixed point size, which is device pixels unless it is scaled");
			await Assert.That(atTwo / atOne).IsLessThan(2.3);

			static double GraphLeftEdge()
			{
				var chartData = new ChartData();
				chartData.Datasets.Add(new Dataset
				{
					Data = { 5 },
					BackgroundColor = Color.Blue
				});

				var chart = new SimpleChartWidget(new ThemeConfig(), chartData)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Absolute,
					LocalBounds = new RectangleDouble(0, 0, 200, 100)
				};

				var image = new ImageBuffer(200, 100);
				Graphics2D graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				chart.OnDraw(graphics2D);

				return StartOfLongestPaintedRun(image);
			}
		}

		[Test]
		[NotInParallel]
		public async Task DialogButtonMinimumWidthGrowsWithDeviceScale()
		{
			double atOne = AtDeviceScale(1, MinimumWidth);
			double atTwo = AtDeviceScale(2, MinimumWidth);

			await Assert.That(atTwo).IsEqualTo(atOne * 2).Within(0.001)
				.Because("the minimum is there to hold a word of text, and the text doubled");

			static double MinimumWidth()
			{
				return new ThemeConfig().CreateDialogButton("OK").MinimumSize.X;
			}
		}

		/// <summary>
		/// Runs <paramref name="measure"/> with <see cref="GuiWidget.DeviceScale"/> set, restoring whatever
		/// it was before. The scale is read while widgets are built, so it has to be in place first.
		/// </summary>
		private static T AtDeviceScale<T>(double deviceScale, Func<T> measure)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;

				return measure();
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static double LeftmostPaintedColumnOf(ImageBuffer image)
		{
			for (int x = 0; x < image.Width; x++)
			{
				for (int y = 0; y < image.Height; y++)
				{
					if (image.GetPixel(x, y) != Color.White)
					{
						return x;
					}
				}
			}

			return -1;
		}

		/// <summary>
		/// Finds the longest unbroken horizontal run of painted pixels and returns the column it starts at.
		/// In a bar chart that run is one of the long edges of the plot frame, so its start is the left edge
		/// of the plot area.
		/// </summary>
		private static double StartOfLongestPaintedRun(ImageBuffer image)
		{
			int bestLength = 0;
			int bestStart = -1;

			for (int y = 0; y < image.Height; y++)
			{
				int runStart = -1;

				for (int x = 0; x <= image.Width; x++)
				{
					bool painted = x < image.Width && image.GetPixel(x, y) != Color.White;

					if (painted)
					{
						if (runStart < 0)
						{
							runStart = x;
						}
					}
					else if (runStart >= 0)
					{
						if (x - runStart > bestLength)
						{
							bestLength = x - runStart;
							bestStart = runStart;
						}

						runStart = -1;
					}
				}
			}

			return bestStart;
		}

		/// <summary>
		/// The least a <see cref="PopupWidget"/> needs from a layout engine: a maximum height to push it into
		/// scrolling, and a ShowPopup that does not need a window to put the popup in.
		/// </summary>
		private class StubPopupLayoutEngine : IPopupLayoutEngine
		{
			public StubPopupLayoutEngine(double maxHeight)
			{
				this.MaxHeight = maxHeight;
			}

			public double MaxHeight { get; }

			public GuiWidget Anchor => null;

			public void Closed()
			{
			}

			public void ShowPopup(PopupWidget popupWidget)
			{
			}
		}
	}
}
