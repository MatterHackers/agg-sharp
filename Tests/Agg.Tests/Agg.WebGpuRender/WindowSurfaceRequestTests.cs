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
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Which half of an X11 drawable lands in which field of a <see cref="WindowSurfaceRequest"/>. No
	/// window and no GPU are needed for this, and none should be: the point is that a host that gets the
	/// two values backwards is told so here rather than crashing inside Vulkan on a machine with a
	/// display attached.
	/// <para>
	/// The <c>Display*</c> going in <see cref="WindowSurfaceRequest.NativeSurfaceHandle"/> is not an
	/// implementation detail the surface path is free to change: <c>CreateRawSurface</c> reads that one
	/// field to decide there is no drawable at all, so if the XID were put there instead, an X11 request
	/// with a null display would sail past the guard.
	/// </para>
	/// <para>
	/// The browser canvas factory is here for the same reason and runs on every desktop OS: its request
	/// shape - a selector and deliberately no handle - is what the surface path branches on, and getting it
	/// wrong is only visible in a browser, where nothing in this suite can look.
	/// </para>
	/// </summary>
	public class WindowSurfaceRequestTests
	{
		[Test]
		public async Task AnXlibRequestPutsTheDisplayInTheNativeHandleAndTheWindowInItsOwnField()
		{
			var display = new IntPtr(0x7f00_1234);

			// Deliberately larger than uint.MaxValue: an XID is an unsigned long, and a 64-bit X server
			// hands out resource ids above the 32-bit range once a client has been running a while.
			const ulong window = 0x1_0000_0042ul;

			var request = WindowSurfaceRequest.ForXlibWindow(display, window, 800, 600, "x11Window");

			await Assert.That(request.NativeSurfaceHandle).IsEqualTo(display);
			await Assert.That(request.XlibWindow).IsEqualTo(window);
			await Assert.That(request.Width).IsEqualTo(800u);
			await Assert.That(request.Height).IsEqualTo(600u);
			await Assert.That(request.Label).IsEqualTo("x11Window");

			// HINSTANCE is a Windows-only hint; an X11 request must not invent one.
			await Assert.That(request.ModuleHandle).IsEqualTo(IntPtr.Zero);
		}

		[Test]
		public async Task AnXlibRequestRejectsANullDisplay()
		{
			await Assert.That(() => WindowSurfaceRequest.ForXlibWindow(IntPtr.Zero, 42, 800, 600))
				.Throws<ArgumentException>();
		}

		[Test]
		public async Task AnXlibRequestRejectsTheNoneWindow()
		{
			await Assert.That(() => WindowSurfaceRequest.ForXlibWindow(new IntPtr(0x7f00_1234), 0, 800, 600))
				.Throws<ArgumentException>();
		}

		[Test]
		public async Task TheOtherPlatformsFactoriesLeaveTheXlibWindowUnset()
		{
			var hwnd = WindowSurfaceRequest.ForWindowsHwnd(new IntPtr(0x1234), new IntPtr(0x5678), 320, 240);
			await Assert.That(hwnd.XlibWindow).IsEqualTo(0ul);

			var metalLayer = WindowSurfaceRequest.ForMetalLayer(new IntPtr(0x1234), 320, 240);
			await Assert.That(metalLayer.XlibWindow).IsEqualTo(0ul);
		}

		[Test]
		public async Task ABrowserCanvasRequestCarriesItsSelectorAndNoHandleAtAll()
		{
			var request = WindowSurfaceRequest.ForBrowserCanvas("#agg-canvas", 800, 600, "browserCanvas");

			await Assert.That(request.CanvasSelector).IsEqualTo("#agg-canvas");
			await Assert.That(request.Width).IsEqualTo(800u);
			await Assert.That(request.Height).IsEqualTo(600u);
			await Assert.That(request.Label).IsEqualTo("browserCanvas");

			// The zero handle is the point, not an accident: a canvas is named rather than handed over, and
			// the surface path has to answer the browser branch before it reaches the "no handle means no
			// drawable" guard the three native sources share. If this ever became non-zero, that ordering
			// would stop being load bearing and the next reader would reasonably re-order it.
			await Assert.That(request.NativeSurfaceHandle).IsEqualTo(IntPtr.Zero);
			await Assert.That(request.ModuleHandle).IsEqualTo(IntPtr.Zero);
			await Assert.That(request.XlibWindow).IsEqualTo(0ul);
		}

		[Test]
		public async Task ABrowserCanvasRequestRejectsASelectorThatNamesNothing()
		{
			// An empty selector matches no element, and emdawnwebgpu reports that as a null surface with no
			// hint that the name was the problem.
			await Assert.That(() => WindowSurfaceRequest.ForBrowserCanvas(null, 800, 600)).Throws<ArgumentException>();
			await Assert.That(() => WindowSurfaceRequest.ForBrowserCanvas("   ", 800, 600)).Throws<ArgumentException>();
		}

		[Test]
		public async Task TheNativeFactoriesLeaveTheCanvasSelectorUnset()
		{
			// The mirror of the assertion above: the surface path decides it is looking at a canvas request
			// by the selector alone, so a native request must never carry one.
			await Assert.That(WindowSurfaceRequest.ForWindowsHwnd(new IntPtr(0x1234), IntPtr.Zero, 320, 240).CanvasSelector).IsNull();
			await Assert.That(WindowSurfaceRequest.ForMetalLayer(new IntPtr(0x1234), 320, 240).CanvasSelector).IsNull();
			await Assert.That(WindowSurfaceRequest.ForXlibWindow(new IntPtr(0x1234), 42, 320, 240).CanvasSelector).IsNull();
		}
	}
}
