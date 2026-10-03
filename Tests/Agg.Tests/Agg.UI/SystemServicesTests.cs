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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The platform-neutral halves of <see cref="UrlLauncher"/> and <see cref="SystemAppearance"/>, driven with
	/// fake providers. The real mac/browser/Windows providers are smoke tested by running a demo.
	/// </summary>
	[NotInParallel(SystemServicesTests.NotInParallelKey)]
	public class SystemServicesTests
	{
		/// <summary>Every test that swaps <see cref="UrlLauncher.Provider"/> or <see cref="SystemAppearance.Provider"/>.</summary>
		public const string NotInParallelKey = MatterHackers.Agg.Tests.SharedStateKeys.SystemServices;

		/// <summary>
		/// A test run must never open the developer's browser. The platform window providers install the real
		/// launcher only while <see cref="UrlLauncher.Provider"/> is null, so a no-op installed before any test
		/// shows a window keeps every test (automation included) on the fake.
		/// </summary>
		/// <remarks>The appearance gets a fixed (dark) fake for the same reason: no test may pass or fail with
		/// the developer's OS appearance.</remarks>
		[Before(HookType.Assembly)]
		public static void NeverLaunchARealBrowser()
		{
			UrlLauncher.Provider = _ => { };
			SystemAppearance.Provider = () => true;
		}

		// Clearing a provider leaves a window shown meanwhile free to install the real one, so a test that
		// does also excludes the tests that show windows.
		[Test]
		[NotInParallel(new[] { NotInParallelKey, nameof(AutomationRunner.ShowWindowAndExecuteTests) })]
		public async Task OpenGoesToTheProviderAndReportsWhetherThereWasOne()
		{
			var saved = UrlLauncher.Provider;
			try
			{
				UrlLauncher.Provider = null;
				await Assert.That(UrlLauncher.Open("https://example.com")).IsFalse();

				var opened = new List<string>();
				UrlLauncher.Provider = opened.Add;
				await Assert.That(UrlLauncher.Open("https://example.com")).IsTrue();
				await Assert.That(UrlLauncher.Open(string.Empty)).IsFalse();
				await Assert.That(string.Join("|", opened)).IsEqualTo("https://example.com/");
			}
			finally
			{
				UrlLauncher.Provider = saved;
			}
		}

		[Test]
		[Arguments("http://example.com", "http://example.com/")]
		[Arguments("https://example.com/a?b=c", "https://example.com/a?b=c")]
		[Arguments("  https://example.com/padded \n", "https://example.com/padded")]
		[Arguments("https://example.com/already%20escaped", "https://example.com/already%20escaped")]
		[Arguments("mailto:someone@example.com", "mailto:someone@example.com")]
		// ShellExecute puts the url on the browser's command line: a raw quote or space would end it there and
		// let the rest of the link add browser switches, so the provider only ever sees the escaped form.
		[Arguments("http://x/\" --gpu-launcher=calc \"", "http://x/%22%20--gpu-launcher=calc%20%22")]
		[Arguments("file:///etc/passwd", null)]
		[Arguments("/Applications/Calculator.app", null)]
		[Arguments("C:\\Windows\\System32\\calc.exe", null)]
		[Arguments("calc.exe", null)]
		[Arguments("vscode://open?file=x", null)]
		[Arguments("javascript:alert(1)", null)]
		[Arguments("", null)]
		[Arguments(null, null)]
		public async Task OnlyWebAndMailAddressesReachTheProvider(string url, string expectedOpened)
		{
			var saved = UrlLauncher.Provider;
			try
			{
				string opened = null;
				UrlLauncher.Provider = u => opened = u;

				await Assert.That(UrlLauncher.Open(url)).IsEqualTo(expectedOpened != null);
				// The provider gets the escaped address, never raw link text
				await Assert.That(opened).IsEqualTo(expectedOpened);
			}
			finally
			{
				UrlLauncher.Provider = saved;
			}
		}

		[Test]
		public async Task PrefersDarkAsksTheProviderEveryTime()
		{
			var saved = SystemAppearance.Provider;
			try
			{
				SystemAppearance.Provider = null;
				await Assert.That(SystemAppearance.PrefersDark).IsNull();

				bool dark = true;
				SystemAppearance.Provider = () => dark;
				await Assert.That(SystemAppearance.PrefersDark).IsEqualTo(true);

				// The OS setting changed: the next read sees it
				dark = false;
				await Assert.That(SystemAppearance.PrefersDark).IsEqualTo(false);
			}
			finally
			{
				SystemAppearance.Provider = saved;
			}
		}
	}
}
