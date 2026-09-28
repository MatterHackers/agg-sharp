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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// The in-window modal stack: a backdrop over its parent, dialogs centred on it, the top one closing on Escape
	// or a press outside it (agg-gui's demo ModalOverlay).
	public class ModalOverlayTests
	{
		private static (GuiWidget host, ModalOverlay overlay, GuiWidget lower, GuiWidget upper) Build()
		{
			var host = new GuiWidget(400, 300);
			var overlay = new ModalOverlay();
			host.AddChild(overlay);
			var lower = new GuiWidget(250, 150) { Name = "Lower" };
			var upper = new GuiWidget(100, 60) { Name = "Upper" };
			overlay.Push(lower);
			overlay.Push(upper);
			host.PerformLayout();
			return (host, overlay, lower, upper);
		}

		[Test]
		public async Task PushedLayersStackCentredOverTheHost()
		{
			var (_, overlay, lower, upper) = Build();

			await Assert.That(overlay.Width).IsEqualTo(400);
			await Assert.That(overlay.Layers.Count).IsEqualTo(2);
			await Assert.That(overlay.TopLayer).IsEqualTo(upper);
			await Assert.That(upper.BoundsRelativeToParent.Center.X).IsEqualTo(200);
			await Assert.That(upper.BoundsRelativeToParent.Center.Y).IsEqualTo(150);
			await Assert.That(lower.BoundsRelativeToParent.Center.X).IsEqualTo(200);
		}

		[Test]
		public async Task PressOutsideTheTopLayerClosesOnlyIt()
		{
			var (_, overlay, lower, upper) = Build();
			int changes = 0;
			overlay.LayersChanged += (s, e) => changes++;

			// Inside the top layer: nothing closes.
			overlay.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 200, 150, 0));
			overlay.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 200, 150, 0));
			await Assert.That(overlay.Layers.Count).IsEqualTo(2);

			// On the lower layer but outside the top one: the top closes, the lower stays.
			overlay.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 200, 210, 0));
			overlay.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 200, 210, 0));
			await Assert.That(overlay.TopLayer).IsEqualTo(lower);
			await Assert.That(upper.Parent).IsNull();
			await Assert.That(changes).IsEqualTo(1);
		}

		[Test]
		public async Task EscapeClosesTheTopLayer()
		{
			var (_, overlay, lower, _) = Build();

			overlay.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(overlay.TopLayer).IsEqualTo(lower);

			overlay.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(overlay.Layers.Count).IsEqualTo(0);
			await Assert.That(overlay.TopLayer).IsNull();
		}
	}
}
