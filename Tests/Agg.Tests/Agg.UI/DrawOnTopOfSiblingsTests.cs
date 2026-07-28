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
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	public class DrawOnTopOfSiblingsTests
	{
		[Test]
		public async Task FlaggedChildDrawsOverSiblingsWithoutMovingInChildList()
		{
			var parent = new GuiWidget(100, 100);

			// two children that overlap between x 40 and x 60
			var childA = new GuiWidget(60, 100)
			{
				BackgroundColor = Color.Red,
				OriginRelativeParent = new Vector2(0, 0),
			};

			var childB = new GuiWidget(60, 100)
			{
				BackgroundColor = Color.Blue,
				OriginRelativeParent = new Vector2(40, 0),
			};

			parent.AddChild(childA);
			parent.AddChild(childB);

			// baseline, without the flag the later child in the list paints over the earlier one
			await Assert.That(RenderOverlapPixel(parent)).IsEqualTo(Color.Blue);

			childA.DrawOnTopOfSiblings = true;

			await Assert.That(RenderOverlapPixel(parent)).IsEqualTo(Color.Red);

			// the flag must not reorder the children, layout (flow slots) depends on list order
			await Assert.That(parent.Children.IndexOf(childA)).IsEqualTo(0);
			await Assert.That(parent.Children.IndexOf(childB)).IsEqualTo(1);
		}

		private static Color RenderOverlapPixel(GuiWidget parent)
		{
			var image = new ImageBuffer(100, 100);

			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);

			parent.OnDraw(graphics2D);

			return image.GetPixel(50, 50);
		}
	}
}
