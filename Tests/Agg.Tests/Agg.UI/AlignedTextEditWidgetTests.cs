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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class AlignedTextEditWidgetTests
	{
		private static readonly RectangleDouble Frame = new RectangleDouble(10, 20, 210, 120);

		[Test]
		public async Task PlaceAnchorsTheBlockInTheFrame()
		{
			var size = new Vector2(50, 20);
			await Assert.That(AlignedTextEditWidget.Place(size, Frame, HAnchor.Left, VAnchor.Top)).IsEqualTo(new RectangleDouble(10, 100, 60, 120));
			await Assert.That(AlignedTextEditWidget.Place(size, Frame, HAnchor.Center, VAnchor.Center)).IsEqualTo(new RectangleDouble(85, 60, 135, 80));
			await Assert.That(AlignedTextEditWidget.Place(size, Frame, HAnchor.Right, VAnchor.Bottom)).IsEqualTo(new RectangleDouble(160, 20, 210, 40));
		}

		[Test]
		public async Task PlaceClipsABlockBiggerThanTheFrame()
		{
			// Too big: the editor gets the whole frame and scrolls inside it.
			var placed = AlignedTextEditWidget.Place(new Vector2(500, 300), Frame, HAnchor.Right, VAnchor.Center);
			await Assert.That(placed).IsEqualTo(Frame);
		}

		[Test]
		public async Task EditorFollowsItsTextAndTheAlignment()
		{
			var field = new AlignedTextEditWidget("abc", 12, hintText: "hint") { Padding = 0 };
			field.LocalBounds = new RectangleDouble(0, 0, 300, 100);

			// Right, bottom: the editor's right edge is the frame's, its bottom the frame's.
			field.TextHAnchor = HAnchor.Right;
			field.TextVAnchor = VAnchor.Bottom;
			RectangleDouble placed = field.Editor.BoundsRelativeToParent;
			await Assert.That(placed.Right).IsEqualTo(300);
			await Assert.That(placed.Bottom).IsEqualTo(0);

			// Longer text makes a wider editor, still against the right edge.
			double width = placed.Width;
			field.Text = "abcdefghijkl";
			await Assert.That(field.Editor.Width).IsGreaterThan(width);
			await Assert.That(field.Editor.BoundsRelativeToParent.Right).IsEqualTo(300);
		}
	}
}
