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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <c>AggContext</c> resolves its providers from <c>"Type, Assembly"</c> strings, which means a renamed
	/// type or a mistyped assembly is a null provider at run time and a compiler that never complained. Every
	/// other platform's strings are proved by the suite simply running there; the browser's cannot be, because
	/// no desktop test process reports <c>IsBrowser</c>. What makes them testable anyway is that
	/// <c>agg_platform_browser</c> is a plain net10.0 assembly and loads on any OS - so the strings can be
	/// resolved here against the very types they name.
	/// </summary>
	public class BrowserProviderResolutionTests
	{
		/// <summary>Each string names the type it is supposed to.</summary>
		[Test]
		public async Task TheBrowserProviderStringsResolveToTheBrowserProviders()
		{
			await Assert.That(Type.GetType(AggContext.ProviderSettings.BrowserOsInformationProvider))
				.IsEqualTo(typeof(BrowserInformationProvider));

			await Assert.That(Type.GetType(AggContext.ProviderSettings.BrowserDialogProvider))
				.IsEqualTo(typeof(BrowserFileDialogProvider));

			await Assert.That(Type.GetType(AggContext.ProviderSettings.BrowserSystemWindowProvider))
				.IsEqualTo(typeof(WebGpuBrowserWindowProvider));
		}

		/// <summary>
		/// And each one is constructible the way <c>AggContext</c> constructs it - by
		/// <see cref="Activator"/>, with no arguments - and is the interface the caller will cast it to. A
		/// provider whose only constructor took a seam would resolve to a type and then yield null here.
		/// </summary>
		[Test]
		public async Task EachBrowserProviderIsConstructibleAsItsInterface()
		{
			await Assert.That(AggContext.CreateInstanceFrom<IOsInformationProvider>(
				AggContext.ProviderSettings.BrowserOsInformationProvider)).IsNotNull();

			await Assert.That(AggContext.CreateInstanceFrom<IFileDialogProvider>(
				AggContext.ProviderSettings.BrowserDialogProvider)).IsNotNull();

			await Assert.That(AggContext.CreateInstanceFrom<ISystemWindowProvider>(
				AggContext.ProviderSettings.BrowserSystemWindowProvider)).IsNotNull();
		}

		/// <summary>
		/// The browser arms sit ahead of the desktop ones in every ternary, so the guard that keeps them from
		/// swallowing every platform is <c>IsBrowser</c> being false everywhere else. This is what would catch
		/// that going wrong - a desktop process must not default to the browser providers.
		/// </summary>
		[Test]
		public async Task ADesktopProcessDoesNotDefaultToTheBrowserProviders()
		{
			var defaults = new AggContext.ProviderSettings();

			await Assert.That(defaults.OsInformationProvider)
				.IsNotEqualTo(AggContext.ProviderSettings.BrowserOsInformationProvider);
			await Assert.That(defaults.DialogProvider)
				.IsNotEqualTo(AggContext.ProviderSettings.BrowserDialogProvider);
			await Assert.That(defaults.SystemWindowProvider)
				.IsNotEqualTo(AggContext.ProviderSettings.BrowserSystemWindowProvider);
		}

		/// <summary>
		/// Off a browser the information provider answers with something a caller can size a window from
		/// rather than a zero desktop, which is the same degradation <c>LinuxInformationProvider</c> makes
		/// with no X display. It still says it is a browser, because the type is the whole of that answer.
		/// </summary>
		[Test]
		public async Task TheInformationProviderDegradesRatherThanFailingOffABrowser()
		{
			var information = new BrowserInformationProvider(screen: null);

			await Assert.That(information.OperatingSystem).IsEqualTo(OSType.Browser);
			await Assert.That(information.DesktopSize.x).IsGreaterThan(0);
			await Assert.That(information.DesktopSize.y).IsGreaterThan(0);
			await Assert.That(information.DisplayScale).IsEqualTo(1.0);
			await Assert.That(information.PhysicalMemory).IsEqualTo(0);
		}

		/// <summary>
		/// The screen is reported in CSS pixels and agg's sizes are device pixels everywhere, so the ratio has
		/// to be applied - the same step the mac provider makes with <c>backingScaleFactor</c>. Missing it
		/// halves the desktop on every Retina display.
		/// </summary>
		[Test]
		public async Task TheDesktopSizeIsInDevicePixels()
		{
			var information = new BrowserInformationProvider(
				new FixedScreen(cssWidth: 1512, cssHeight: 982, devicePixelRatio: 2, memoryGigabytes: 8));

			await Assert.That(information.DesktopSize.x).IsEqualTo(3024);
			await Assert.That(information.DesktopSize.y).IsEqualTo(1964);
			await Assert.That(information.DisplayScale).IsEqualTo(2.0);

			// deviceMemory is in gigabytes; PhysicalMemory is in bytes like every other provider's.
			await Assert.That(information.PhysicalMemory).IsEqualTo(8L * 1024 * 1024 * 1024);
		}

		/// <summary>
		/// Only Chromium implements <c>navigator.deviceMemory</c>. Everywhere else it is absent, which reaches
		/// here as zero - the same "would not say" the mac provider reports when <c>sysctl</c> fails.
		/// </summary>
		[Test]
		public async Task AnUnreportedMemorySizeIsZeroRatherThanNonsense()
		{
			var information = new BrowserInformationProvider(
				new FixedScreen(cssWidth: 1920, cssHeight: 1080, devicePixelRatio: 1, memoryGigabytes: 0));

			await Assert.That(information.PhysicalMemory).IsEqualTo(0);
		}

		/// <summary>window.screen, replaced by four numbers.</summary>
		private sealed class FixedScreen : MatterHackers.Agg.Platform.Browser.IBrowserScreenInterop
		{
			private readonly double[] metrics;

			public FixedScreen(double cssWidth, double cssHeight, double devicePixelRatio, double memoryGigabytes)
			{
				this.metrics = new[] { cssWidth, cssHeight, devicePixelRatio, memoryGigabytes };
			}

			public double[] ReadScreenMetrics() => this.metrics;
		}
	}
}
