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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Clipboard Test window (agg-gui's demo-ui/src/windows/tests/basic/controls.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class ClipboardTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Clipboard Test");

		private static ClipboardTestWindow Build()
		{
			var page = new GuiWidget(360, 290);
			GuiWidget content = GuiDemoSpecs.CreateContent(Spec);
			page.AddChild(content);
			page.PerformLayout();
			return (ClipboardTestWindow)content;
		}

		[Test]
		public async Task BuildsEveryControlNamed()
		{
			ClipboardTestWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Clipboard Test Content");
			foreach (string name in new[] { "Clipboard Text", "Clipboard Copy Text", "Clipboard Image", "Clipboard Copy Image" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			await Assert.That(window.TextField.Text).IsEqualTo(ClipboardTestWindow.InitialText);

			// agg-gui's gradient: red rises left to right, green top to bottom (ImageBuffer rows run bottom up).
			Color topRight = window.Image.GetPixel(ClipboardTestWindow.ImageSize - 1, ClipboardTestWindow.ImageSize - 1);
			Color bottomLeft = window.Image.GetPixel(0, 0);
			await Assert.That((int)topRight.red).IsEqualTo(255 * 47 / 48);
			await Assert.That((int)topRight.green).IsEqualTo(0);
			await Assert.That((int)bottomLeft.red).IsEqualTo(0);
			await Assert.That((int)bottomLeft.green).IsEqualTo(255 * 47 / 48);
			await Assert.That((int)bottomLeft.blue).IsEqualTo(160);
		}

		[Test]
		public async Task TheCopyButtonsPutTheLiveTextAndTheImageOnTheClipboard()
		{
			ClipboardTestWindow window = Build();
			var clipboard = new SimulatedClipboard();
			window.ClipboardTarget = clipboard;

			window.TextField.Text = "edited";
			window.CopyTextButton.InvokeClick();
			await Assert.That(clipboard.GetText()).IsEqualTo("edited");

			window.CopyImageButton.InvokeClick();
			await Assert.That(clipboard.GetImage()).IsSameReferenceAs(window.Image);
		}
	}
}
