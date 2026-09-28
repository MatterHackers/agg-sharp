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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>Hyperlink activation by click, keyboard and code.</summary>
	public class HyperlinkTests
	{
		[Test]
		public async Task ClickAndKeysActivateAndOpenTheUrl()
		{
			var opened = new List<string>();
			var link = new Hyperlink("Docs", new ThemeConfig(), "https://example.com")
			{
				UrlOpener = opened.Add,
			};
			int activations = 0;
			link.Activated += (s, e) => activations++;

			link.InvokeClick();
			var enter = new KeyEventArgs(Keys.Enter);
			link.OnKeyDown(enter);
			link.OnKeyDown(new KeyEventArgs(Keys.Space));
			link.OnKeyDown(new KeyEventArgs(Keys.A));

			await Assert.That(activations).IsEqualTo(3);
			await Assert.That(opened.Count).IsEqualTo(3);
			await Assert.That(opened[0]).IsEqualTo("https://example.com");
			await Assert.That(enter.Handled).IsTrue();
			await Assert.That(link.Name).IsEqualTo("Docs Link");
			await Assert.That(link.TabStop).IsTrue();
		}

		[Test]
		public async Task ALinkWithoutAUrlOnlyRaisesActivated()
		{
			var opened = new List<string>();
			var link = new Hyperlink("Click me!", new ThemeConfig()) { UrlOpener = opened.Add };
			int activations = 0;
			link.Activated += (s, e) => activations++;

			link.Activate();

			await Assert.That(activations).IsEqualTo(1);
			await Assert.That(opened.Count).IsEqualTo(0);
		}

		[Test]
		[NotInParallel(new[] { SystemServicesTests.NotInParallelKey, nameof(AutomationRunner.ShowWindowAndExecuteTests) })]
		public async Task ALinkWithAUrlButNoOpenerGoesToTheUrlLauncher()
		{
			var saved = UrlLauncher.Provider;
			try
			{
				var launched = new List<string>();
				UrlLauncher.Provider = launched.Add;
				var link = new Hyperlink("Docs", new ThemeConfig(), "https://example.com");
				int activations = 0;
				link.Activated += (s, e) => activations++;

				link.Activate();

				await Assert.That(activations).IsEqualTo(1);
				await Assert.That(string.Join("|", launched)).IsEqualTo("https://example.com");

				// With no platform launcher either (headless), activation still just raises Activated
				UrlLauncher.Provider = null;
				link.Activate();
				await Assert.That(activations).IsEqualTo(2);
			}
			finally
			{
				UrlLauncher.Provider = saved;
			}
		}
	}
}
