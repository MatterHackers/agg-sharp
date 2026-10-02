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
	/// <summary>
	/// A finger's tap tolerance is 8 points, so it scales with the window's <see cref="SystemWindow.DisplayScale"/>:
	/// on a DPR 3.5 phone 8 device pixels is about 2 CSS pixels, and ordinary finger jitter turned every tap into a
	/// drag, so buttons outside a scroll area never clicked.
	/// </summary>
	/// <remarks>
	/// Keyless [NotInParallel]: <see cref="SystemWindow.SetDisplayScale"/> honours the static
	/// <see cref="SystemWindow.SimulatedDisplayScale"/> other tests set, and queues its change event on the shared
	/// idle queue, which these drain.
	/// </remarks>
	[NotInParallel]
	public class TouchTapToleranceTests
	{
		private sealed class View
		{
			public SystemWindow Root;
			public ScrollableWidget Scroll;
			public int Clicks;
		}

		/// <summary>
		/// A 400 x 400 window: a 200 x 80 button at (20..220, 300..380) outside any scroll area, and below it a
		/// 400 x 200 scroll area over 1000 of content.
		/// </summary>
		private static View Make(double displayScale)
		{
			var view = new View { Root = new SystemWindow(400, 400) };
			view.Root.SetDisplayScale(displayScale);
			UiThread.InvokePendingActions();

			var button = new GuiWidget(200, 80, SizeLimitsToSet.None) { OriginRelativeParent = new Vector2(20, 300) };
			button.Click += (s, e) => view.Clicks++;
			view.Root.AddChild(button);

			view.Scroll = new ScrollableWidget(400, 200, autoScroll: true) { MinimumSize = Vector2.Zero };
			view.Root.AddChild(view.Scroll);
			view.Scroll.AddChild(new GuiWidget(400, 1000, SizeLimitsToSet.None));
			return view;
		}

		private static MouseEventArgs Touch(double x, double y)
			=> new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) { PointerType = PointerType.Touch };

		private static void Drag(View view, Vector2 from, Vector2 to)
		{
			view.Root.OnMouseDown(Touch(from.X, from.Y));
			view.Root.OnMouseMove(Touch(to.X, to.Y));
			view.Root.OnMouseUp(Touch(to.X, to.Y));
		}

		[Test]
		public async Task OnADensePhoneATapWithFingerJitterClicksAButtonOutsideAnyScrollArea()
		{
			// 20 device px at DPR 3.5 is under 6 points - a still finger, not a drag
			View view = Make(3.5);
			Drag(view, new Vector2(100, 320), new Vector2(100, 340));
			await Assert.That(view.Clicks).IsEqualTo(1);
		}

		[Test]
		public async Task OnADensePhoneADragPastTheToleranceStillPans()
		{
			View view = Make(3.5);
			Drag(view, new Vector2(100, 50), new Vector2(100, 90));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(40).Within(1e-9);
		}

		[Test]
		public async Task AtOneDevicePixelPerPointTenPixelsIsADrag()
		{
			View view = Make(1);
			Drag(view, new Vector2(100, 320), new Vector2(100, 330));
			await Assert.That(view.Clicks).IsEqualTo(0);

			Drag(view, new Vector2(100, 50), new Vector2(100, 60));
			await Assert.That(view.Scroll.ScrollOffsetFromTop()).IsEqualTo(10).Within(1e-9);
		}
	}
}
