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

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The WinForms host under <see cref="IPlatformWindow.ShowWindowsInBackground"/> (on for this whole test
	/// process): a shown window is visible and normal-sized - so it still renders - but is not activated and
	/// opens directly beneath the window the user is working in rather than over it. The user's foreground
	/// window is stood in for by a window of the test's own (through BackgroundWindowPlacement.ForegroundWindow),
	/// so the z-order checks do not depend on what the machine running them has in front.
	/// </summary>
	[NotInParallel(nameof(WinformsShowInBackgroundTests))]
	public class WinformsShowInBackgroundTests
	{
		private const uint GW_HWNDNEXT = 2;

		private class TestWinformsWindow : WinformsSystemWindow
		{
			private readonly ImageBuffer backBuffer = new ImageBuffer(200, 100);

			public bool ShowsWithoutActivation => this.ShowWithoutActivation;

			public override Graphics2D NewGraphics2D() => backBuffer.NewGraphics2D();

			public override void CopyBackBufferToScreen(Graphics displayGraphics)
			{
			}
		}

		/// <summary>Applies the agg window's requested state on load, as WebGpuSystemWindow.OnLoad does.</summary>
		private class LoadingTestWindow : TestWinformsWindow
		{
			protected override void OnLoad(EventArgs e)
			{
				base.OnLoad(e);
				this.ApplyRequestedWindowState(this.AggSystemWindow.Maximized);
				this.IsInitialized = true;
			}
		}

		/// <summary>A plain window standing in for the user's application; it must not take the foreground either.</summary>
		private class StandInWindow : Form
		{
			protected override bool ShowWithoutActivation => true;
		}

		[Test]
		public async Task OnlyABackgroundWindowLosesTopmost()
		{
			const int otherStyle = 0x00000100;
			int requested = BackgroundWindowPlacement.WS_EX_TOPMOST | otherStyle;

			// Switch off is what applications run with: the style is exactly what WinForms asked for.
			await Assert.That(BackgroundWindowPlacement.ExtendedStyle(requested, false)).IsEqualTo(requested);
			await Assert.That(BackgroundWindowPlacement.ExtendedStyle(requested, true)).IsEqualTo(otherStyle);
		}

		[Test]
		public async Task AShownWindowOpensDirectlyBelowTheForegroundWithoutActivating()
		{
			await WithWindows(async (standIn, form) =>
			{
				BackgroundWindowPlacement.ForegroundWindow = () => standIn.Handle;

				form.Show();

				// Read before the first await: the active window is per thread, and the assertions below may
				// resume on another one.
				IntPtr belowStandIn = GetWindow(standIn.Handle, GW_HWNDNEXT);
				IntPtr active = GetActiveWindow();

				await Assert.That(active).IsNotEqualTo(form.Handle);
				await Assert.That(belowStandIn).IsEqualTo(form.Handle);
				await Assert.That(form.ShowsWithoutActivation).IsTrue();
				await Assert.That(form.Visible).IsTrue();
				await Assert.That(form.WindowState).IsEqualTo(FormWindowState.Normal);
				await Assert.That(form.TopMost).IsFalse();
			});
		}

		[Test]
		public async Task AnOwnedForegroundPlacesTheWindowBelowItsOwnerNotBetweenThem()
		{
			await WithWindows(async (standIn, form) =>
			{
				using var dialog = new StandInWindow { Owner = standIn, Size = new Size(120, 80) };
				dialog.Show();
				BackgroundWindowPlacement.ForegroundWindow = () => dialog.Handle;

				form.Show();

				await Assert.That(GetWindow(standIn.Handle, GW_HWNDNEXT)).IsEqualTo(form.Handle);
			});
		}

		[Test]
		public async Task TheDesktopInTheForegroundDoesNotBuryTheWindowUnderTheWallpaper()
		{
			IntPtr shell = GetShellWindow();
			if (shell == IntPtr.Zero)
			{
				Skip.Test("No shell window on this desktop.");
			}

			await WithWindows(async (standIn, form) =>
			{
				BackgroundWindowPlacement.ForegroundWindow = () => shell;

				form.Show();

				// Above the desktop means the desktop is somewhere below it in the z-order.
				await Assert.That(IsBelow(shell, form.Handle)).IsTrue();
			});
		}

		[Test]
		public async Task AMaximizedWindowFillsTheWorkingAreaWithoutActivating()
		{
			await WithWindows(async (standIn, mainForm) =>
			{
				BackgroundWindowPlacement.ForegroundWindow = () => standIn.Handle;

				using var form = new LoadingTestWindow { AggSystemWindow = new SystemWindow(200, 100) { Maximized = true } };
				form.Show();

				// Everything read before the first await, which may resume on another thread.
				IntPtr active = GetActiveWindow();
				IntPtr handle = form.Handle;
				Rectangle workingArea = Screen.FromControl(form).WorkingArea;
				FormWindowState state = form.WindowState;
				Rectangle shownBounds = form.Bounds;
				bool maximizedWhenShown = form.AggSystemWindow.Maximized;

				// Still maximized as far as the application is concerned, until the window leaves those bounds.
				form.Width -= 50;
				bool maximizedAfterResize = form.AggSystemWindow.Maximized;
				form.Bounds = workingArea;
				bool maximizedBack = form.AggSystemWindow.Maximized;

				form.AggSystemWindow = null;
				form.Close();

				await Assert.That(active).IsNotEqualTo(handle);
				await Assert.That(state).IsEqualTo(FormWindowState.Normal);
				await Assert.That(shownBounds).IsEqualTo(workingArea);
				await Assert.That(maximizedWhenShown).IsTrue();
				await Assert.That(maximizedAfterResize).IsFalse();
				await Assert.That(maximizedBack).IsTrue();
			});
		}

		[Test]
		public async Task AModalDialogOpensAboveItsTestWindowWithoutActivating()
		{
			await WithWindows(async (standIn, mainForm) =>
			{
				BackgroundWindowPlacement.ForegroundWindow = () => standIn.Handle;
				mainForm.Show();

				using var dialog = new TestWinformsWindow { AggSystemWindow = new SystemWindow(120, 80) };
				Form owner = null;
				bool dialogAboveMain = false;
				IntPtr activeWhileShown = IntPtr.Zero;
				dialog.Shown += (s, e) =>
				{
					owner = dialog.Owner;
					dialogAboveMain = IsBelow(mainForm.Handle, dialog.Handle);
					activeWhileShown = GetActiveWindow();
					dialog.AggSystemWindow = null;
					dialog.Close();
				};

				dialog.ShowModal();

				await Assert.That(owner).IsSameReferenceAs(mainForm);
				await Assert.That(dialogAboveMain).IsTrue();
				await Assert.That(activeWhileShown).IsNotEqualTo(dialog.Handle);
			});
		}

		/// <summary>
		/// Runs a test with a shown stand-in foreground window and an unshown <see cref="TestWinformsWindow"/> -
		/// the first constructed, so it is MainWindowsFormsWindow - and puts back everything they touch.
		/// </summary>
		private static async Task WithWindows(Func<StandInWindow, TestWinformsWindow, Task> test)
		{
			// Drag/drop registration requires an STA thread; see WinformsShowCenteringTests.
			bool savedEnableAllowDrop = SystemWindow.EnableAllowDrop;
			SystemWindow.EnableAllowDrop = false;
			Func<IntPtr> savedForeground = BackgroundWindowPlacement.ForegroundWindow;

			var standIn = new StandInWindow { Size = new Size(160, 120) };
			TestWinformsWindow form = null;
			try
			{
				standIn.Show();
				form = new TestWinformsWindow { AggSystemWindow = new SystemWindow(200, 100) };
				await test(standIn, form);
			}
			finally
			{
				BackgroundWindowPlacement.ForegroundWindow = savedForeground;
				SystemWindow.EnableAllowDrop = savedEnableAllowDrop;

				if (form != null)
				{
					// Detach the agg window first so Close() skips the OnShouldClose cascade.
					form.AggSystemWindow = null;
					form.Close();
					form.Dispose();
				}

				standIn.Close();
				standIn.Dispose();

				// OnClosed clears the MainWindowsFormsWindow latch only for windows shown through
				// ShowSystemWindow, so clear it here for later tests.
				typeof(WinformsSystemWindow)
					.GetProperty(nameof(WinformsSystemWindow.MainWindowsFormsWindow), BindingFlags.Public | BindingFlags.Static)
					.SetValue(null, null);
			}
		}

		/// <summary>Whether <paramref name="lower"/> is somewhere beneath <paramref name="upper"/> in the z-order.</summary>
		private static bool IsBelow(IntPtr lower, IntPtr upper)
		{
			for (IntPtr window = GetWindow(upper, GW_HWNDNEXT); window != IntPtr.Zero; window = GetWindow(window, GW_HWNDNEXT))
			{
				if (window == lower)
				{
					return true;
				}
			}

			return false;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetActiveWindow();

		[DllImport("user32.dll")]
		private static extern IntPtr GetShellWindow();

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindow(IntPtr hWnd, uint command);
	}
}
