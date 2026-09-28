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

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// SliderMath (agg-gui's slider_math.rs cases) and the Slider value options built on it.
	public class SliderMathTests
	{
		private const double Inf = double.PositiveInfinity;

		[Test]
		public async Task LinearMappingRoundTripsAndPinsOutsideTheRange()
		{
			foreach ((double v, double n) in new[] { (0.0, 0.0), (5.0, .5), (10.0, 1.0), (2.5, .25) })
			{
				await Assert.That(SliderMath.NormalizedFromValue(v, 0, 10, false)).IsEqualTo(n).Within(1e-9);
				await Assert.That(SliderMath.ValueFromNormalized(n, 0, 10, false)).IsEqualTo(v).Within(1e-9);
			}

			await Assert.That(SliderMath.NormalizedFromValue(-5, 0, 10, false)).IsEqualTo(0);
			await Assert.That(SliderMath.NormalizedFromValue(50, 0, 10, false)).IsEqualTo(1);
		}

		[Test]
		public async Task LogarithmicMapsGeometricallyIncludingZeroAndInfinity()
		{
			// The middle of 1..100 is 10, both ways; a reversed range runs high to low.
			await Assert.That(SliderMath.ValueFromNormalized(.5, 1, 100, true)).IsEqualTo(10).Within(1e-6);
			await Assert.That(SliderMath.NormalizedFromValue(10, 1, 100, true)).IsEqualTo(.5).Within(1e-9);
			await Assert.That(SliderMath.ValueFromNormalized(0, 100, 1, true)).IsEqualTo(100).Within(1e-9);

			// A range symmetric about zero puts zero in the middle.
			await Assert.That(System.Math.Abs(SliderMath.ValueFromNormalized(.5, -1000, 1000, true))).IsLessThan(1);

			// 0..infinity reaches both ends and stays finite between.
			await Assert.That(SliderMath.ValueFromNormalized(1, 0, Inf, true)).IsEqualTo(Inf);
			await Assert.That(SliderMath.ValueFromNormalized(0, 0, Inf, true)).IsEqualTo(0);
			double middle = SliderMath.ValueFromNormalized(.5, 0, Inf, true);
			await Assert.That(double.IsFinite(middle) && middle > 0).IsTrue();

			// The Sliders demo's range sliders span -inf..inf; its min and max must sit at finite positions.
			foreach (double v in new[] { 0.0, 10000.0 })
			{
				double n = SliderMath.NormalizedFromValue(v, -Inf, Inf, true);
				await Assert.That(n >= 0 && n <= 1).IsTrue();
			}

			await Assert.That(double.IsFinite(SliderMath.ValueFromNormalized(.5, -Inf, Inf, true))).IsTrue();
		}

		[Test]
		public async Task CommitClampsStepsAndRounds()
		{
			await Assert.That(SliderMath.ClampToRange(-1, 10, 0)).IsEqualTo(0);
			await Assert.That(SliderMath.ClampToRange(11, 10, 0)).IsEqualTo(10);
			await Assert.That(SliderMath.Commit(123, 0, 100, clamp: true, 0, false)).IsEqualTo(100);
			await Assert.That(SliderMath.Commit(123, 0, 100, clamp: false, 0, false)).IsEqualTo(123);

			// Steps count from the minimum; an infinite minimum has no multiples, so no snapping.
			await Assert.That(SliderMath.Commit(17, 2, 100, true, 10, false)).IsEqualTo(22);
			await Assert.That(SliderMath.Commit(17, -Inf, Inf, true, 10, false)).IsEqualTo(17);
			await Assert.That(SliderMath.Commit(2.5, 0, 10, true, 0, integer: true)).IsEqualTo(3);
		}

		[Test]
		public async Task SmartAimPicksTheRoundestValue()
		{
			(double, double, double)[] cases =
			{
				(-.2, 0, 0), (-10004.23, 3.14, 0), (7.8, 17.8, 10), (99, 300, 100), (-99, -300, -100),
				(.4, .9, .5), (14.1, 19.99, 15), (12.3, 65.9, 50), (493, 879, 500), (.37, .48, .4),
				(7.5, 16.3, 10), (7.5, 763.3, 100), (7.5, 123456, 1000), (9.9999, 99.999, 10), (10.001, 99.999, 50),
				(4, 9, 5), (37, 48, 40), (12345, 12780, 12500), (1.2, Inf, 1.2), (-Inf, 1.2, 0), (-Inf, -2.7, -2.7),
			};
			foreach ((double min, double max, double best) in cases)
			{
				await Assert.That(SliderMath.BestInRange(min, max)).IsEqualTo(best).Within(1e-12);
			}

			await Assert.That(SliderMath.BestInRange(double.NaN, 1.2)).IsEqualTo(1.2);
		}

		[Test]
		public async Task AutoDecimalsFollowStepThenMagnitude()
		{
			await Assert.That(SliderMath.AutoDecimals(3.14159, 0, integer: true)).IsEqualTo(0);
			await Assert.That(SliderMath.AutoDecimals(3.14159, .1, false)).IsEqualTo(1);
			await Assert.That(SliderMath.AutoDecimals(10000, 0, false)).IsEqualTo(0);
			await Assert.That(SliderMath.AutoDecimals(10, 0, false)).IsEqualTo(2);
			await Assert.That(SliderMath.AutoDecimals(.001, 0, false)).IsEqualTo(6);
		}

		[Test]
		public async Task SliderValueOptions()
		{
			// Logarithmic: the value survives exactly and sits at its geometric position.
			var slider = new Slider(Vector2.Zero, 200, 1, 100) { Logarithmic = true };
			slider.Value = 10;
			await Assert.That(slider.Value).IsEqualTo(10);
			await Assert.That(slider.Position0To1).IsEqualTo(.5).Within(1e-9);

			// Clamping: Always clamps a value set in code, Never and Edits keep it (the thumb pins to the end).
			slider = new Slider(Vector2.Zero, 200, 0, 10) { Clamping = SliderClamping.Never };
			slider.Value = 20;
			await Assert.That(slider.Value).IsEqualTo(20);
			await Assert.That(slider.Position0To1).IsEqualTo(1);
			slider.Clamping = SliderClamping.Always;
			slider.Value = 20;
			await Assert.That(slider.Value).IsEqualTo(10);

			// Steps and the arrow keys: a step per press, clamped as an edit.
			slider = new Slider(Vector2.Zero, 200, 0, 10) { Step = 2, KeyboardStepping = true, Clamping = SliderClamping.Edits };
			slider.Value = 3.1;
			await Assert.That(slider.Value).IsEqualTo(4);
			slider.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(slider.Value).IsEqualTo(6);
			slider.Value = 10;
			slider.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(slider.Value).IsEqualTo(10);
			slider.OnKeyDown(new KeyEventArgs(Keys.Left));
			await Assert.That(slider.Value).IsEqualTo(8);

			// Without KeyboardStepping the arrows are left to the app, as before.
			var plain = new Slider(Vector2.Zero, 200, 0, 10) { Step = 2 };
			plain.Value = 4;
			var key = new KeyEventArgs(Keys.Right);
			plain.OnKeyDown(key);
			await Assert.That(plain.Value).IsEqualTo(4);
			await Assert.That(key.Handled).IsFalse();
		}

		[Test]
		public async Task DefaultSliderIsUnchanged()
		{
			// No option on: the linear, clamped slider every app already has.
			var slider = new Slider(Vector2.Zero, 200, 0, 10);
			int changes = 0;
			slider.ValueChanged += (s, e) => changes++;
			slider.Value = 2.5;
			await Assert.That(changes).IsEqualTo(1);
			slider.Value = 2.5;
			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(slider.Position0To1).IsEqualTo(.25);
			await Assert.That(slider.Value).IsEqualTo(2.5);
			slider.Value = 20;
			await Assert.That(slider.Value).IsEqualTo(10);
			slider.Position0To1 = .5;
			await Assert.That(slider.Value).IsEqualTo(5);

			// A range change keeps the position, so the value moves with it, as it always has.
			slider.Maximum = 20;
			await Assert.That(slider.Value).IsEqualTo(10);
		}

		[Test]
		public async Task ClampToRangeTreatsANaNBoundAsOpen()
		{
			// As Rust's total_cmp orders a positive NaN: above everything, so the value passes that side.
			await Assert.That(SliderMath.ClampToRange(5, 0, double.NaN)).IsEqualTo(5);
			await Assert.That(SliderMath.ClampToRange(-1, 0, double.NaN)).IsEqualTo(0);
			await Assert.That(SliderMath.ClampToRange(5, double.NaN, 0)).IsEqualTo(5);
			await Assert.That(SliderMath.ClampToRange(-1, double.NaN, 0)).IsEqualTo(0);
		}

		[Test]
		public async Task RangeAndOptionChangesRecommitTheValue()
		{
			// With options on, a range change keeps the value and moves the thumb to it.
			var slider = new Slider(Vector2.Zero, 200, 0, 100) { Step = 1 };
			slider.Value = 5;
			slider.Maximum = 10;
			await Assert.That(slider.Value).IsEqualTo(5);
			await Assert.That(slider.Position0To1).IsEqualTo(.5).Within(1e-9);

			// Always clamping pulls a value the new range excludes into it.
			slider.SetRange(20, 30);
			await Assert.That(slider.Value).IsEqualTo(20);
			await Assert.That(slider.Position0To1).IsEqualTo(0);

			// Turning an option on keeps the value rather than reading the old linear position logarithmically.
			var log = new Slider(Vector2.Zero, 200, 1, 100);
			log.Value = 10;
			log.Logarithmic = true;
			await Assert.That(log.Value).IsEqualTo(10).Within(1e-9);
			await Assert.That(log.Position0To1).IsEqualTo(.5).Within(1e-9);
		}

		[Test]
		public async Task KeyStepsAreAGrabAndARelease()
		{
			// Apps that commit on release (SliderReleased) see each key step as a finished edit.
			var slider = new Slider(Vector2.Zero, 200, 0, 10) { Step = 1, KeyboardStepping = true };
			var events = new System.Collections.Generic.List<string>();
			slider.SliderGrabed += (s, e) => events.Add("grab");
			slider.ValueChanged += (s, e) => events.Add("change");
			slider.SliderReleased += (s, e) => events.Add("release");
			slider.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(string.Join(",", events)).IsEqualTo("grab,change,release");
		}

		[Test]
		public async Task VerticalSliderStepsWithUpAndDown()
		{
			var slider = new Slider(Vector2.Zero, 200, 0, 10, Orientation.Vertical) { Step = 1, KeyboardStepping = true };
			slider.Value = 5;
			slider.OnKeyDown(new KeyEventArgs(Keys.Up));
			await Assert.That(slider.Value).IsEqualTo(6);
			slider.OnKeyDown(new KeyEventArgs(Keys.Down));
			slider.OnKeyDown(new KeyEventArgs(Keys.Down));
			await Assert.That(slider.Value).IsEqualTo(4);

			// Left and right are not along a vertical slider's axis.
			slider.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(slider.Value).IsEqualTo(4);
		}

		[Test]
		public async Task MouseTrackClickAndDragSnapWithStepAndSmartAim()
		{
			var slider = new Slider(Vector2.Zero, 200, 0, 100) { Step = 10, SmartAim = true };

			// A click on the track at 47 snaps to the step.
			double at47 = slider.GetPositionPixelsFromValue(47);
			slider.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, at47, 0, 0));
			slider.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, at47, 0, 0));
			await Assert.That(slider.Value).IsEqualTo(50);

			// Grabbing the thumb and dragging to 63 lands on 60.
			double thumb = slider.PositionPixelsFromFirstValue;
			slider.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, thumb, 0, 0));
			await Assert.That(slider.IsDraggingThumb).IsTrue();
			double at63 = slider.GetPositionPixelsFromValue(63);
			slider.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, at63, 0, 0));
			slider.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, at63, 0, 0));
			await Assert.That(slider.Value).IsEqualTo(60);
		}
	}
}
