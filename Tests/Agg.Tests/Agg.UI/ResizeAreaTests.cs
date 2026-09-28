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

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>ResizeArea's drag maths and its grip resizing the region.</summary>
	public class ResizeAreaTests
	{
		[Test]
		public async Task DragRightAndDownGrowsAndClamps()
		{
			var start = new Vector2(250, 100);
			var min = new Vector2(80, 40);
			var max = new Vector2(400, 300);

			// y is up, so a mouse moving down (y falling) makes the region taller
			var grown = ResizeArea.SizeForDrag(start, new Vector2(10, 10), new Vector2(40, -20), min, max);
			await Assert.That(grown).IsEqualTo(new Vector2(280, 130));

			var tooSmall = ResizeArea.SizeForDrag(start, Vector2.Zero, new Vector2(-1000, 1000), min, max);
			await Assert.That(tooSmall).IsEqualTo(min);

			var tooBig = ResizeArea.SizeForDrag(start, Vector2.Zero, new Vector2(1000, -1000), min, max);
			await Assert.That(tooBig).IsEqualTo(max);
		}

		[Test]
		public async Task DraggingTheGripResizesTheRegion()
		{
			var area = new ResizeArea(100, 50, new ThemeConfig());
			var content = new GuiWidget(10, 10);
			area.AddChild(content);
			await Assert.That(area.Children[area.Children.Count - 1]).IsEqualTo(area.Grip).Because("the grip stays on top of content");

			area.Grip.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			area.Grip.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 1, 25, -5, 0));
			area.Grip.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 25, -5, 0));

			await Assert.That(area.Size).IsEqualTo(new Vector2(120, 60));
		}
	}
}
