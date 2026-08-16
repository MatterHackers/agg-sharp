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
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The shared idle timer runs on a thread pool thread and can tick while the window it drives is
	/// going down. WinForms' Control.Invoke reports every such failure as ObjectDisposedException naming
	/// the control, and the window's own state does not reliably say so: Control.IsDisposed does not go
	/// true until Dispose has finished, and IsHandleCreated keeps answering true throughout. A tick
	/// landing in that gap escaped the benign-teardown-race filter and was reported as an unhandled
	/// exception, so every clean shutdown printed "Cannot access a disposed object."
	/// <para>
	/// The filter now keys off whether the marshaled work ever reached the UI thread instead, which also
	/// covers the other shape of this - a window whose message loop has already exited while the window
	/// itself still looks perfectly healthy.
	/// </para>
	/// </summary>
	public class WinformsIdleTimerDisposeRaceTests
	{
		private class DisposingTickWindow : WinformsSystemWindow
		{
			private readonly ImageBuffer backBuffer = new ImageBuffer(100, 100);

			private readonly ManualResetEventSlim tickEntered = new ManualResetEventSlim();

			/// <summary>
			/// The tick that was handed to this window from another thread as it went down.
			/// </summary>
			public Task TickDuringDispose { get; private set; }

			/// <summary>
			/// Whether that tick actually reached the production handler, rather than the test having raced
			/// past its own setup.
			/// </summary>
			public bool TickWasDelivered => tickEntered.IsSet;

			public override Graphics2D NewGraphics2D() => backBuffer.NewGraphics2D();

			public override void CopyBackBufferToScreen(Graphics displayGraphics)
			{
			}

			protected override void Dispose(bool disposing)
			{
				// The tick has to arrive from another thread, because the whole point is the marshaling
				// path: on the handle's own thread InvokeRequired is false and Control.Invoke is never
				// reached. Starting it here and then running base.Dispose underneath it puts it squarely in
				// the window where Disposing goes true, IsDisposed is still false and the handle still
				// answers as created - the gap the exception filter used to miss.
				if (disposing && TickDuringDispose == null)
				{
					TickDuringDispose = Task.Run(() =>
					{
						tickEntered.Set();
						InvokeIdleTick(this);
					});

					// Only wait for the tick to be inside the handler. It cannot come back before this
					// thread lets go of the UI thread, so waiting for completion here would deadlock until
					// the timeout on both the fixed and the broken code and prove nothing.
					tickEntered.Wait(TimeSpan.FromSeconds(5));
					TickDuringDispose.Wait(TimeSpan.FromMilliseconds(250));
				}

				base.Dispose(disposing);
			}
		}

		/// <summary>
		/// Calls the production idle-timer handler exactly as System.Timers.Timer would.
		/// </summary>
		private static void InvokeIdleTick(WinformsSystemWindow window)
		{
			MethodInfo handler = typeof(WinformsSystemWindow).GetMethod(
				"InvokePendingOnIdleActions",
				BindingFlags.Instance | BindingFlags.NonPublic);

			handler.Invoke(window, new object[] { null, (ElapsedEventArgs)null });
		}

		[Test]
		[NotInParallel]
		public async Task IdleTickDuringDisposeIsNotReportedAsUnhandled()
		{
			bool savedEnableAllowDrop = SystemWindow.EnableAllowDrop;
			SystemWindow.EnableAllowDrop = false;

			var reported = new List<Exception>();
			void Collect(Exception ex) => reported.Add(ex);

			UiThread.UnhandledException += Collect;

			DisposingTickWindow window = null;

			try
			{
				window = new DisposingTickWindow
				{
					AggSystemWindow = new SystemWindow(100, 100)
				};

				// The handler bails out before ever touching Invoke unless idle processing is on, which
				// normally happens when the first window starts the message loop.
				typeof(WinformsSystemWindow)
					.GetField("enableIdleProcessing", BindingFlags.Instance | BindingFlags.NonPublic)
					.SetValue(window, true);

				// Force the Win32 handle so the tick takes the marshaling path rather than the
				// "no handle yet" early out.
				IntPtr handle = window.Handle;
				await Assert.That(window.IsHandleCreated).IsTrue();

				// Something must be queued, otherwise there is nothing to marshal and no race to lose.
				UiThread.RunOnIdle(() => { });

				window.AggSystemWindow = null;
				window.Dispose();

				// Let a tick that outlived Dispose finish, so its exception is counted rather than racing
				// the assertions below.
				window.TickDuringDispose?.Wait(TimeSpan.FromSeconds(10));

				await Assert.That(window.TickWasDelivered).IsTrue();
				await Assert.That(reported).IsEmpty();
			}
			finally
			{
				UiThread.UnhandledException -= Collect;
				SystemWindow.EnableAllowDrop = savedEnableAllowDrop;

				window?.ReleaseOnIdleGuard();

				// MainWindowsFormsWindow latches onto the first window constructed and is only cleared in
				// OnClosed, which never runs for a window that was not shown.
				typeof(WinformsSystemWindow)
					.GetProperty(nameof(WinformsSystemWindow.MainWindowsFormsWindow), BindingFlags.Public | BindingFlags.Static)
					.SetValue(null, null);
			}
		}
	}
}
