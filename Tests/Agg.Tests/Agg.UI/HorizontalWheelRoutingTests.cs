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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A two finger trackpad scroll carries a horizontal component as well as a vertical one, and agg
	/// delivers it as <see cref="MouseEventArgs.WheelDeltaX"/> alongside the wheel. These cover the two
	/// things that have to be true for a widget deep in the tree to ever see it: the per-child clone has
	/// to carry it down, and consuming it has to carry back up.
	/// </summary>
	public class HorizontalWheelRoutingTests
	{
		[Test]
		public async Task TheClonedEventKeepsBothWheelAxes()
		{
			var original = new MouseEventArgs(MouseButtons.None, 0, 10, 20, 120)
			{
				WheelDeltaX = -45,
			};

			// This is the constructor GuiWidget uses to re-base an event into a child's coordinates; a field
			// it forgets is a field no child can ever act on.
			var moved = new MouseEventArgs(original, 3, 4);

			await Assert.That(moved.WheelDelta).IsEqualTo(120);
			await Assert.That(moved.WheelDeltaX).IsEqualTo(-45);
		}

		[Test]
		public async Task AChildSeesTheHorizontalWheelAndCanConsumeIt()
		{
			var parent = new GuiWidget(200, 200);
			var child = new HorizontalWheelEater()
			{
				LocalBounds = new RectangleDouble(0, 0, 100, 100),
			};
			child.OriginRelativeParent = new VectorMath.Vector2(50, 50);
			parent.AddChild(child);

			var wheelEvent = new MouseEventArgs(MouseButtons.None, 0, 75, 75, 120)
			{
				WheelDeltaX = -30,
			};

			parent.OnMouseWheel(wheelEvent);

			await Assert.That(child.SeenWheelDeltaX).IsEqualTo(-30);

			// The child ate the horizontal component, so the parent (a scroll panel, in the real case) has to
			// be told - otherwise the same gesture would be acted on twice.
			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(0);
			await Assert.That(wheelEvent.WheelDelta).IsEqualTo(120);
		}

		[Test]
		public async Task AScrollPanelLeavesTheHorizontalWheelAlone()
		{
			// ScrollableWidget only scrolls vertically, so a sideways gesture has to pass through it untouched
			// for anything inside it to be able to use it.
			var scrollable = new ScrollableWidget(200, 200, autoScroll: true);
			scrollable.AddChild(new GuiWidget(400, 400));
			scrollable.PerformLayout();

			var wheelEvent = new MouseEventArgs(MouseButtons.None, 0, 100, 100, 0)
			{
				WheelDeltaX = -30,
			};

			scrollable.OnMouseWheel(wheelEvent);

			await Assert.That(wheelEvent.WheelDeltaX).IsEqualTo(-30);
		}

		private class HorizontalWheelEater : GuiWidget
		{
			public int SeenWheelDeltaX { get; private set; }

			public override void OnMouseWheel(MouseEventArgs mouseEvent)
			{
				this.SeenWheelDeltaX = mouseEvent.WheelDeltaX;
				mouseEvent.WheelDeltaX = 0;

				base.OnMouseWheel(mouseEvent);
			}
		}
	}
}
