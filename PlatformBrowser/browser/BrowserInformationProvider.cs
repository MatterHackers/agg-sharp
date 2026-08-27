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
using MatterHackers.Agg.Platform.Browser;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The browser <see cref="IOsInformationProvider"/>. Exists for the same reason
	/// <c>MacInformationProvider</c> and <c>LinuxInformationProvider</c> do: the Windows one is built on
	/// <c>System.Windows.Forms.Screen</c> and <c>Microsoft.VisualBasic.Devices.ComputerInfo</c>, neither of
	/// which will even load here.
	/// </summary>
	/// <remarks>
	/// Read once at construction, like both of the others - a display change mid-run is not something the
	/// toolkit reacts to today. (The <em>canvas</em> resizing is a different thing entirely, and
	/// <c>BrowserSystemWindow</c> does react to that.)
	/// </remarks>
	public class BrowserInformationProvider : IOsInformationProvider
	{
		/// <summary>
		/// What <see cref="DesktopSize"/> reports when there is no page to ask - a desktop test process, or
		/// this provider named by mistake outside a browser. The same value and the same argument as
		/// <c>LinuxInformationProvider</c>'s headless default: every caller of DesktopSize is sizing or
		/// centring something, and a 0x0 desktop turns into a zero-size window or a divide by zero a long
		/// way from here.
		/// </summary>
		private static readonly Point2D FallbackDesktopSize = new Point2D(1920, 1080);

		public BrowserInformationProvider()
			: this(CreatePageInterop())
		{
		}

		/// <param name="screen">The page seam, or null to answer with the fallbacks.</param>
		public BrowserInformationProvider(IBrowserScreenInterop screen)
		{
			if (screen == null)
			{
				this.DesktopSize = FallbackDesktopSize;
				this.DisplayScale = 1;
				this.PhysicalMemory = 0;
				return;
			}

			double[] metrics = screen.ReadScreenMetrics();

			// screen.width/height are CSS pixels; agg's sizes are device pixels everywhere, so they are
			// multiplied by devicePixelRatio exactly as the mac provider multiplies visibleFrame by
			// backingScaleFactor. This is the whole screen and not a work area - a page has no idea where
			// the user's taskbar or dock is, and window.screen.availWidth reports the browser's guess rather
			// than a measurement.
			double devicePixelRatio = BrowserBacking.ClampDevicePixelRatio(metrics[2]);

			this.DesktopSize = new Point2D(
				(int)BrowserBacking.ClampPixelExtent(metrics[0] * devicePixelRatio),
				(int)BrowserBacking.ClampPixelExtent(metrics[1] * devicePixelRatio));

			this.DisplayScale = devicePixelRatio;
			this.PhysicalMemory = ToPhysicalMemoryBytes(metrics[3]);
		}

		public OSType OperatingSystem => OSType.Browser;

		/// <summary>The whole screen in device pixels, matching the space every other size in agg is in.</summary>
		public Point2D DesktopSize { get; }

		/// <summary>
		/// <c>window.devicePixelRatio</c> - 2 on a Retina display, 1.25/1.5/2 for Windows' display scaling,
		/// and also whatever the user's page zoom is, because a browser folds the two together and gives a
		/// page no way to separate them. That is the right answer for this property's purpose: a user who
		/// zoomed the page wants the UI bigger, which is exactly what <c>GuiWidget.DeviceScale</c> does with
		/// it.
		/// </summary>
		public double DisplayScale { get; }

		/// <summary>
		/// Total memory, in bytes, from <c>navigator.deviceMemory</c> - or zero, which is what the mac
		/// provider reports when its own query fails and what every browser but Chromium reports here.
		/// </summary>
		/// <remarks>
		/// Deliberately coarse at the source: the browser rounds to a power of two and caps at 8 GB
		/// specifically so it cannot be used to fingerprint a machine. Treat it as an order of magnitude,
		/// which is all any caller of <see cref="AggContext.PhysicalMemory"/> uses it for. Note it describes
		/// the <em>device</em>, not what this tab may allocate - wasm has its own, much lower ceiling.
		/// </remarks>
		public long PhysicalMemory { get; }

		/// <summary>Turns <c>navigator.deviceMemory</c>'s gigabytes into bytes, with zero for "would not say".</summary>
		private static long ToPhysicalMemoryBytes(double gigabytes)
		{
			if (double.IsNaN(gigabytes) || gigabytes <= 0)
			{
				return 0;
			}

			return (long)(gigabytes * 1024 * 1024 * 1024);
		}

		/// <summary>
		/// The page seam in a browser, and nothing on a desktop. See <c>BrowserClipboard.CreatePageInterop</c>
		/// for why this is a method rather than a ternary.
		/// </summary>
		private static IBrowserScreenInterop CreatePageInterop()
		{
			if (System.OperatingSystem.IsBrowser())
			{
				return new BrowserPeripherals();
			}

			return null;
		}
	}
}
