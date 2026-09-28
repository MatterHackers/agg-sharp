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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>Spinner's animation step, spoke fading, and what one frame paints.</summary>
	public class SpinnerTests
	{
		[Test]
		public async Task HeadAdvancesOneSpokePerStepAndWraps()
		{
			await Assert.That(Spinner.HeadSpokeAt(0)).IsEqualTo(0);
			await Assert.That(Spinner.HeadSpokeAt(Spinner.StepMilliseconds - 1)).IsEqualTo(0);
			await Assert.That(Spinner.HeadSpokeAt(Spinner.StepMilliseconds)).IsEqualTo(1);
			await Assert.That(Spinner.HeadSpokeAt(Spinner.StepMilliseconds * Spinner.SpokeCount)).IsEqualTo(0);
			await Assert.That(Spinner.HeadSpokeAt(Spinner.StepMilliseconds * (Spinner.SpokeCount + 3))).IsEqualTo(3);
		}

		[Test]
		public async Task SpokesFadeBehindTheHead()
		{
			const int head = 4;
			await Assert.That(Spinner.SpokeAlpha(head, head)).IsEqualTo(1);
			await Assert.That(Spinner.SpokeAlpha(3, head)).IsLessThan(1);
			await Assert.That(Spinner.SpokeAlpha(2, head)).IsLessThan(Spinner.SpokeAlpha(3, head));
			await Assert.That(Math.Abs(Spinner.SpokeAlpha(5, head) - Spinner.TailAlpha)).IsLessThan(1e-9)
				.Because("the spoke just ahead of the head is the oldest");
		}

		/// <summary>
		/// Pins DeviceScale (process-wide) so the ring is 32 pixels; keyless [NotInParallel] as in ProgressBarTests.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AFrameDrawsTheHeadSpokeDarkest()
		{
			var savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var spinner = new Spinner(new ThemeConfig()) { SpokeColor = Color.Black };
				await Assert.That(spinner.Width).IsEqualTo(32);

				var image = new ImageBuffer(32, 32);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);
				int head = spinner.HeadSpoke;
				spinner.OnDraw(graphics2D);
				bool clockSteppedDuringDraw = spinner.HeadSpoke != head;

				int RedAlong(int spoke)
				{
					var point = new Vector2(16, 16) + Spinner.SpokeDirection(spoke) * 11;
					return image.GetPixel((int)point.X, (int)point.Y).red;
				}

				int tail = (head + 1) % Spinner.SpokeCount;
				await Assert.That(RedAlong(tail)).IsLessThan(255).Because("even the faded tail is painted");
				if (!clockSteppedDuringDraw)
				{
					await Assert.That(RedAlong(head)).IsLessThan(RedAlong(tail));
				}

				// With no SpokeColor the theme's text colour is read at draw time, so a live theme change shows.
				var theme = new ThemeConfig();
				var themed = new Spinner(theme);
				theme.TextColor = Color.Blue;
				graphics2D.Clear(Color.White);
				themed.OnDraw(graphics2D);
				var headPoint = new Vector2(16, 16) + Spinner.SpokeDirection(themed.HeadSpoke) * 11;
				var headPixel = image.GetPixel((int)headPoint.X, (int)headPoint.Y);
				await Assert.That(headPixel.blue).IsGreaterThan(headPixel.red);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}
	}
}
