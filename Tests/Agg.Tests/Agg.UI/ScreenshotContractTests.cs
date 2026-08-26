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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="IPlatformWindow.CaptureScreenshotAsync"/> exists for hosts that cannot block while waiting
	/// for a frame (the browser). Every other host is meant to keep working untouched through the interface's
	/// default, which runs the synchronous capture - so what is worth pinning is the dispatch: a host that
	/// implements only the synchronous capture still answers the async call, and a host that implements the
	/// async one is not quietly bypassed in favour of its synchronous sibling.
	/// </summary>
	public class ScreenshotContractTests
	{
		[Test]
		public async Task DefaultAsyncCaptureRunsTheSynchronousCapture()
		{
			var platformWindow = new SyncOnlyPlatformWindow();
			var systemWindow = new SystemWindow(100, 100) { PlatformWindow = platformWindow };

			await systemWindow.CaptureScreenshotAsync("shot.png");

			await Assert.That(platformWindow.SyncCapturePath).IsEqualTo("shot.png");
		}

		[Test]
		public async Task AHostThatImplementsAsyncCaptureGetsUsed()
		{
			var platformWindow = new AsyncCapablePlatformWindow();
			var systemWindow = new SystemWindow(100, 100) { PlatformWindow = platformWindow };

			await systemWindow.CaptureScreenshotAsync("shot.png");

			await Assert.That(platformWindow.AsyncCapturePath).IsEqualTo("shot.png");
			await Assert.That(platformWindow.SyncCapturePath).IsNull();
		}

		[Test]
		public async Task AWindowWithNoPlatformWindowCompletesWithoutThrowing()
		{
			var systemWindow = new SystemWindow(100, 100);

			await systemWindow.CaptureScreenshotAsync("shot.png");
		}

		/// <summary>A host from before the async contract existed: only the synchronous member.</summary>
		private class SyncOnlyPlatformWindow : StubPlatformWindow, IPlatformWindow
		{
		}

		/// <summary>A host that captures asynchronously, the way the browser host will.</summary>
		private class AsyncCapablePlatformWindow : StubPlatformWindow, IPlatformWindow
		{
			public string AsyncCapturePath { get; private set; }

			public Task CaptureScreenshotAsync(string path)
			{
				this.AsyncCapturePath = path;
				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// Deliberately does not implement <see cref="IPlatformWindow"/> itself: interface mapping is fixed
		/// at the class that declares the interface, so an async capture introduced by a subclass of an
		/// implementing base would never be reached through the interface. Each stub below declares the
		/// interface itself, which is the same reason WinformsSystemWindow declares the async member instead
		/// of leaving WebGpuSystemWindow to introduce it.
		/// </summary>
		private class StubPlatformWindow
		{
			public string SyncCapturePath { get; private set; }

			public string Caption { get; set; }

			public int TitleBarHeight => 0;

			public Point2D DesktopPosition { get; set; }

			public Vector2 MinimumSize { get; set; }

			public Keys ModifierKeys => Keys.None;

			public void Activate()
			{
			}

			public void BringToFront()
			{
			}

			public void Close()
			{
			}

			public void CloseSystemWindow(SystemWindow systemWindow)
			{
			}

			public void Invalidate(RectangleDouble rectToInvalidate)
			{
			}

			public Graphics2D NewGraphics2D() => null;

			public void SetCursor(Cursors cursorToSet)
			{
			}

			public void ShowSystemWindow(SystemWindow systemWindow)
			{
			}

			public void CaptureScreenshot(string path)
			{
				this.SyncCapturePath = path;
			}
		}
	}
}
