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

using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Since TitleBarHeight moved from the constructor to OnHandleCreated, Show() must force
	/// Win32 handle creation before running the CenterInParent math. Without that, the first
	/// Show() of a centered child window reads the pre-handle TitleBarHeight default of 0 and
	/// positions the window ~TitleBarHeight/2 too low (and the Left/Top set before handle
	/// creation can be discarded by the default WindowsDefaultLocation start position).
	/// </summary>
	public class WinformsShowCenteringTests
	{
		private class TestWinformsWindow : WinformsSystemWindow
		{
			// A real in-memory buffer so any paint triggered while the window is briefly
			// visible does not hit a null Graphics2D.
			private readonly ImageBuffer backBuffer = new ImageBuffer(400, 400);

			public override Graphics2D NewGraphics2D() => backBuffer.NewGraphics2D();

			public override void CopyBackBufferToScreen(Graphics displayGraphics)
			{
			}
		}

		[Test]
		[NotInParallel]
		public async Task FirstShowCentersChildUsingRealTitleBarHeight()
		{
			// Drag/drop registration requires an STA thread; disable it so handle creation
			// works regardless of the test runner's apartment state.
			bool savedEnableAllowDrop = SystemWindow.EnableAllowDrop;
			SystemWindow.EnableAllowDrop = false;

			TestWinformsWindow mainForm = null;
			TestWinformsWindow childForm = null;

			try
			{
				// The first WinformsSystemWindow constructed latches as MainWindowsFormsWindow;
				// it does not need to be shown for a child to center against its DesktopBounds.
				mainForm = new TestWinformsWindow
				{
					AggSystemWindow = new SystemWindow(300, 300)
				};

				await Assert.That(ReferenceEquals(WinformsSystemWindow.MainWindowsFormsWindow, mainForm)).IsTrue();

				childForm = new TestWinformsWindow
				{
					AggSystemWindow = new SystemWindow(200, 100)
					{
						CenterInParent = true
					}
				};

				// No handle yet - this is the state the old code centered in, reading 0.
				await Assert.That(childForm.IsHandleCreated).IsFalse();

				childForm.Show();

				// Show() must have created the handle before the centering math ran, so
				// TitleBarHeight is the real caption height rather than the pre-handle 0.
				await Assert.That(childForm.TitleBarHeight > 0).IsTrue();

				Rectangle mainBounds = mainForm.DesktopBounds;
				int expectedLeft = mainBounds.X + mainBounds.Width / 2 - 200 / 2;
				int expectedTop = mainBounds.Y + mainBounds.Height / 2 - 100 / 2 - childForm.TitleBarHeight / 2;

				await Assert.That(childForm.Left == expectedLeft).IsTrue();
				await Assert.That(childForm.Top == expectedTop).IsTrue();
			}
			finally
			{
				SystemWindow.EnableAllowDrop = savedEnableAllowDrop;

				if (childForm != null)
				{
					// Detach the agg window first so Close() skips the OnShouldClose cascade.
					childForm.AggSystemWindow = null;
					childForm.Close();
					childForm.Dispose();
				}

				if (mainForm != null)
				{
					mainForm.AggSystemWindow = null;
					mainForm.Dispose();
				}

				// MainWindowsFormsWindow is normally cleared in OnClosed, which only runs for
				// windows that were shown; clear the latch directly so later tests start clean.
				typeof(WinformsSystemWindow)
					.GetProperty(nameof(WinformsSystemWindow.MainWindowsFormsWindow), BindingFlags.Public | BindingFlags.Static)
					.SetValue(null, null);
			}
		}
	}
}
