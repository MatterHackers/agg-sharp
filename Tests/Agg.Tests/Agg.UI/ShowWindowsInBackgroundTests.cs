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
	/// A test run must not make the machine it runs on unusable: every window it opened used to activate,
	/// pop over the user's other applications and take the keyboard. The platform-neutral half of
	/// <see cref="IPlatformWindow.ShowWindowsInBackground"/>; the WinForms host's half is in
	/// WinformsShowInBackgroundTests.
	/// </summary>
	public class ShowWindowsInBackgroundTests
	{
		[Test]
		public async Task TheTestProcessShowsWindowsInBackground()
		{
			// Program.Main turns it on before any test runs, so no window this suite opens takes the foreground.
			await Assert.That(IPlatformWindow.ShowWindowsInBackground).IsTrue();
		}

		[Test]
		public async Task SystemWindowBringToFrontDoesNotActivateInBackground()
		{
			var platformWindow = new RecordingPlatformWindow();
			var systemWindow = new SystemWindow(100, 100) { PlatformWindow = platformWindow };

			systemWindow.BringToFront();

			await Assert.That(platformWindow.ActivateCalls).IsEqualTo(0);
		}

		private class RecordingPlatformWindow : IPlatformWindow
		{
			public int ActivateCalls { get; private set; }

			public string Caption { get; set; }

			public int TitleBarHeight => 0;

			public Point2D DesktopPosition { get; set; }

			public Vector2 MinimumSize { get; set; }

			public Keys ModifierKeys => Keys.None;

			public void Activate() => this.ActivateCalls++;

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
			}
		}
	}
}
