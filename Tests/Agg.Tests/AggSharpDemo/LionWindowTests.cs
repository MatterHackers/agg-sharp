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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class LionWindowTests
	{
		private static LionWindow Build()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "Lion");
			var window = (LionWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var host = new GuiWidget(spec.DefaultWidth, spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		[Test]
		public async Task BuildsTheAlphaSliderNoteAndView()
		{
			var window = Build();

			await Assert.That(window.FindDescendant("Lion Alpha")).IsSameReferenceAs(window.AlphaSlider);
			await Assert.That(window.FindDescendant("Lion View")).IsSameReferenceAs(window.View);
			await Assert.That(window.FindDescendant("Lion Note")).IsNotNull();
			await Assert.That(window.AlphaSlider.Value).IsEqualTo(1.0);
			await Assert.That(window.View.Width).IsGreaterThan(100);

			window.AlphaSlider.Value = 0.25;
			await Assert.That(window.View.Alpha).IsEqualTo(0.25);
		}

		[Test]
		public async Task LeftDragRotatesAndScalesRelativeToThePress()
		{
			var view = Build().View;
			double cx = view.Width / 2;
			double cy = view.Height / 2;
			Transform.Affine before = view.GetTransform();

			// Press 100 right of centre: the press alone must not move the lion (no snap to the cursor).
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, cx + 100, cy, 0));
			await Assert.That(view.Angle).IsEqualTo(0.0);
			await Assert.That(view.MouseScale).IsEqualTo(1.0);

			// A quarter turn about the centre at twice the radius.
			view.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, cx, cy + 200, 0));
			view.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, cx, cy + 200, 0));
			await Assert.That(Math.Abs(view.Angle - (Math.PI / 2))).IsLessThan(1e-9);
			await Assert.That(Math.Abs(view.MouseScale - 2)).IsLessThan(1e-9);
			await Assert.That(view.GetTransform().is_equal(before, 1e-6)).IsFalse();

			// Right drag skews to the cursor, lion.cpp's units.
			view.OnMouseDown(new MouseEventArgs(MouseButtons.Right, 1, 30, 40, 0));
			await Assert.That(view.SkewX).IsEqualTo(30.0);
			await Assert.That(view.SkewY).IsEqualTo(40.0);
		}

		[Test]
		public async Task ATrackpadPinchZoomsOnceFromItsFingersNotItsWheel()
		{
			var view = Build().View;
			var pointer = new Vector2(view.Width / 2, view.Height / 2);
			view.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, pointer.X, pointer.Y, 120) { FromTrackpadPinch = true });
			await Assert.That(view.MouseScale).IsEqualTo(1.0);

			var fingers = new TrackpadPinchFingers(50);
			foreach (Vector2[] frame in fingers.Magnify(pointer, 1, TrackpadGesturePhase.Began))
			{
				view.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, frame, 0, null));
			}

			await Assert.That(Math.Abs(view.MouseScale - 2)).IsLessThan(1e-9);
		}

		[Test]
		public async Task ATwoPositionMovePinchesTwistsAndPans()
		{
			var view = Build().View;
			view.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, new[] { new Vector2(100, 100), new Vector2(200, 100) }, 0, null));
			view.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, new[] { new Vector2(110, 110), new Vector2(110, 310) }, 0, null));

			await Assert.That(Math.Abs(view.MouseScale - 2)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(view.Angle - (Math.PI / 2))).IsLessThan(1e-9);
			await Assert.That(view.Offset.X).IsEqualTo(-40.0);
			await Assert.That(view.Offset.Y).IsEqualTo(110.0);
		}
	}
}
