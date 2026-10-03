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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	// Keyless: the test changes GuiWidget.DeviceScale, which nearly every UI test reads.
	[NotInParallel]
	public class RichTableGridPixelTests
	{
		/// <summary>
		/// The rich editor's table grid, read from the drawn pixels, must look like the Help viewer's: every line a
		/// whole number of device pixels thick with crisp edges, one tone everywhere (a crossing is not painted
		/// twice, a stripe does not darken the line over it) and every line spanning the outer borders. Tones are
		/// compared with each other rather than with fixed values, so the test does not depend on how the back
		/// buffer blends translucent colours.
		/// </summary>
		[Test]
		[Arguments(1.0)]
		[Arguments(1.25)]
		[Arguments(1.5)]
		[Arguments(2.0)]
		public async Task TableGridLinesAreCrispWholePixelsOfOneTone(double deviceScale)
		{
			double savedScale = GuiWidget.DeviceScale;
			GuiWidget.DeviceScale = deviceScale;
			try
			{
				var container = new GuiWidget(500 * deviceScale, 200 * deviceScale, SizeLimitsToSet.None)
				{
					DoubleBuffer = true,
				};
				var editor = new RichMarkdownEditWidget(new ThemeConfig())
				{
					// Header, two body rows: the second body row is striped, so a vertical line crosses a stripe.
					Markdown = "| Feature | Status | Notes |\n| --- | --- | --- |\n| Headings | Ready | 6 levels |\n| Tables | Ready | Pipe and grid |\n| Lists | Done | Nested |\n",
				};
				container.AddChild(editor);
				container.PerformLayout();
				container.BackBuffer.NewGraphics2D().Clear(Color.White);
				container.OnDraw(container.BackBuffer.NewGraphics2D());

				var image = container.BackBuffer;
				var layout = (RichTableLayout)editor.BlockLayout(0);
				double origin = editor.BlockOrigin(0);
				var view = editor.DocumentView;
				var tableBounds = view.TransformToParentSpace(container, new RectangleDouble(0, origin, layout.Width, origin + layout.Height));
				int minX = Math.Max(0, (int)Math.Floor(tableBounds.Left) - 2);
				int maxX = Math.Min(image.Width - 3, (int)Math.Ceiling(tableBounds.Right) + 2);
				int minY = Math.Max(0, (int)Math.Floor(tableBounds.Bottom) - 2);
				int maxY = Math.Min(image.Height - 3, (int)Math.Ceiling(tableBounds.Top) + 2);

				int Tone(int x, int y) => image.GetPixel(x, y).red;

				// Grid lines are the text colour at alpha 150; stripes and the white page are far lighter.
				bool Ink(int x, int y) => Tone(x, y) < 200;

				// The grid can start on the image's first column; nothing is drawn outside the image.
				bool InkAt(int x, int y) => x >= 0 && y >= 0 && x < image.Width && y < image.Height && Ink(x, y);

				var vertical = Groups(Enumerable.Range(minX, maxX - minX + 1)
					.Where(x => Enumerable.Range(minY, maxY - minY + 1).Count(y => Ink(x, y)) > (maxY - minY) * 0.6));
				await Assert.That(vertical.Count).IsEqualTo(4);
				int spanLeft = vertical.First().Min();
				int spanRight = vertical.Last().Max();
				var horizontal = Groups(Enumerable.Range(minY, maxY - minY + 1)
					.Where(y => Enumerable.Range(spanLeft, spanRight - spanLeft + 1).Count(x => Ink(x, y)) > (spanRight - spanLeft) * 0.6));

				// 4 rows -> 5 rules; 3 columns -> 4 verticals; each exactly round(DeviceScale) pixels thick.
				await Assert.That(horizontal.Count).IsEqualTo(5);
				int thickness = Math.Max(1, (int)Math.Round(deviceScale));
				await Assert.That(horizontal.All(group => group.Count == thickness)).IsTrue();
				await Assert.That(vertical.All(group => group.Count == thickness)).IsTrue();

				int gridLeft = spanLeft;
				int gridRight = spanRight;
				int gridBottom = horizontal.First().Min();
				int gridTop = horizontal.Last().Max();

				// The reference: the middle of the first vertical, half way between the top two rules.
				int lineTone = Tone(vertical[0].Min(), (horizontal[^1].Min() + horizontal[^2].Max()) / 2);

				// Every pixel of every line, crossings and the stretch over the stripe included, is that one tone, and
				// each line spans the outer borders with no gaps.
				foreach (int y in horizontal.SelectMany(group => group))
				{
					for (int x = gridLeft; x <= gridRight; x++)
					{
						await Assert.That(Math.Abs(Tone(x, y) - lineTone)).IsLessThanOrEqualTo(2);
					}

					await Assert.That(InkAt(gridLeft - 1, y)).IsFalse();
					await Assert.That(InkAt(gridRight + 1, y)).IsFalse();
				}

				foreach (int x in vertical.SelectMany(group => group))
				{
					for (int y = gridBottom; y <= gridTop; y++)
					{
						await Assert.That(Math.Abs(Tone(x, y) - lineTone)).IsLessThanOrEqualTo(2);
					}

					await Assert.That(InkAt(x, gridBottom - 1)).IsFalse();
					await Assert.That(InkAt(x, gridTop + 1)).IsFalse();
				}

				// Crisp edges: the pixel beside a line is the same tone as the one beyond it (no partial coverage).
				foreach (var group in vertical)
				{
					for (int r = 0; r + 1 < horizontal.Count; r++)
					{
						int y = (horizontal[r].Max() + horizontal[r + 1].Min()) / 2;
						if (group != vertical[0])
						{
							await Assert.That(Math.Abs(Tone(group.Min() - 1, y) - Tone(group.Min() - 2, y))).IsLessThanOrEqualTo(2);
						}

						if (group != vertical[^1])
						{
							await Assert.That(Math.Abs(Tone(group.Max() + 1, y) - Tone(group.Max() + 2, y))).IsLessThanOrEqualTo(2);
						}
					}
				}

				foreach (var group in horizontal)
				{
					int x = (vertical[0].Max() + vertical[1].Min()) / 2;
					if (group != horizontal[0])
					{
						await Assert.That(Math.Abs(Tone(x, group.Min() - 1) - Tone(x, group.Min() - 2))).IsLessThanOrEqualTo(2);
					}

					if (group != horizontal[^1])
					{
						await Assert.That(Math.Abs(Tone(x, group.Max() + 1) - Tone(x, group.Max() + 2))).IsLessThanOrEqualTo(2);
					}
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
			}
		}

		private static List<List<int>> Groups(IEnumerable<int> sortedValues)
		{
			var groups = new List<List<int>>();
			foreach (int value in sortedValues)
			{
				if (groups.Count == 0 || groups[^1][^1] != value - 1)
				{
					groups.Add(new List<int>());
				}

				groups[^1].Add(value);
			}

			return groups;
		}
	}
}
