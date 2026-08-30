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
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A native close request - macOS's red button and Cmd-Q, WM_CLOSE, WM_DELETE_WINDOW - closes the
	/// application. In single window mode the provider keeps re-pointing the host at whatever dialog is on
	/// top, so the window the host is drawing is usually not the window the close is about - and running the
	/// close against the dialog skips every ShouldClose/Closed handler the shell has (window bounds
	/// persistence, save on exit) while the platform destroys the window anyway. The native half cannot be
	/// synthesised without a platform; the decision it makes is <see cref="PlatformCloseArbitration"/>'s and
	/// runs everywhere.
	/// </summary>
	public class PlatformCloseRoutingTests
	{
		[Test]
		public async Task TheShellClosesEvenWhileADialogIsOnTop()
		{
			var shell = new SystemWindow(400, 300) { IsApplicationShell = true };
			var dialog = new SystemWindow(200, 100);

			bool shellClosed = false;
			bool dialogAsked = false;
			shell.Closed += (s, e) => shellClosed = true;
			dialog.ShouldClose += (s, e) => dialogAsked = true;

			var provider = new StackedWindowProvider(shell, dialog);

			bool mayClose = PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: true,
				provider: provider,
				activeWindow: dialog,
				setPlatformClosing: null);

			await Assert.That(mayClose).IsTrue();
			await Assert.That(shellClosed).IsTrue();
			await Assert.That(dialogAsked).IsFalse();
		}

		[Test]
		public async Task AShellThatVetoesKeepsItsNativeWindow()
		{
			// What MatterCAD does on the first ask: cancel, persist the open tabs, then close itself on idle.
			var shell = new SystemWindow(400, 300) { IsApplicationShell = true };
			var dialog = new SystemWindow(200, 100);

			shell.ShouldClose += (s, e) => e.Cancel = true;

			var provider = new StackedWindowProvider(shell, dialog);

			var platformClosing = new List<bool>();

			bool mayClose = PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: true,
				provider: provider,
				activeWindow: dialog,
				setPlatformClosing: platformClosing.Add);

			await Assert.That(mayClose).IsFalse();
			await Assert.That(shell.HasBeenClosed).IsFalse();

			// Nothing was started, so nothing has to be undone.
			await Assert.That(platformClosing.Count).IsEqualTo(0);
		}

		[Test]
		public async Task AVetoOnTheSecondAskPutsTheHostBack()
		{
			// GuiWidget.Close asks again, and an application that opened a "save first?" dialog on the first
			// ask cancels on the second. The host must not be left believing it is mid-close.
			var shell = new SystemWindow(400, 300) { IsApplicationShell = true };

			int asks = 0;
			shell.ShouldClose += (s, e) => e.Cancel = ++asks > 1;

			var platformClosing = new List<bool>();

			bool mayClose = PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: true,
				provider: new StackedWindowProvider(shell),
				activeWindow: shell,
				setPlatformClosing: platformClosing.Add);

			await Assert.That(mayClose).IsFalse();
			await Assert.That(shell.HasBeenClosed).IsFalse();
			await Assert.That(platformClosing).IsEquivalentTo(new[] { true, false });
		}

		[Test]
		public async Task WithoutAProviderTheActiveWindowIsTheApplication()
		{
			// Every demo and every test host: one window, no provider, and it is its own shell.
			var window = new SystemWindow(400, 300);

			bool mayClose = PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: false,
				provider: null,
				activeWindow: window,
				setPlatformClosing: null);

			await Assert.That(mayClose).IsTrue();
			await Assert.That(window.HasBeenClosed).IsTrue();
		}

		[Test]
		public async Task AWindowThatIsAlreadyGoneLetsThePlatformClose()
		{
			var window = new SystemWindow(400, 300);
			window.Close();

			await Assert.That(PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: false,
				provider: null,
				activeWindow: window,
				setPlatformClosing: null)).IsTrue();

			await Assert.That(PlatformCloseArbitration.HandlePlatformCloseRequest(
				singleWindowMode: false,
				provider: null,
				activeWindow: null,
				setPlatformClosing: null)).IsTrue();
		}

		/// <summary>
		/// The other half of "already gone": closing a window that never got a platform window must not throw
		/// out of the provider either.
		/// </summary>
		/// <remarks>
		/// <see cref="AWindowThatIsAlreadyGoneLetsThePlatformClose"/> above looks like it covers this and does
		/// not - it hands <see cref="PlatformCloseArbitration"/> a null provider, so no provider code runs. The
		/// window's own <c>Close()</c> in its first line is what reaches one: <c>SystemWindow.OnClosed</c> calls
		/// the ambient provider's <see cref="ISystemWindowProvider.CloseSystemWindow"/> for every window,
		/// including one that was never shown and so has a null <c>PlatformWindow</c>. That is why it failed on
		/// Windows and passed everywhere else - the mac and X11 providers guard, the WinForms one did not - and
		/// why it only failed once some earlier test in the process had created the provider.
		/// <para>
		/// So the real provider for this OS is built the way <c>ShowAsSystemWindow</c> builds it, from the
		/// configured type name, and asked to close a window it never showed. Every leg then pins its own
		/// platform's provider: the contract is one contract, and the platform that breaks it is the platform
		/// whose CI turns red.
		/// </para>
		/// </remarks>
		[Test]
		public async Task ThisPlatformsProviderClosesAWindowThatWasNeverShown()
		{
			var provider = AggContext.CreateInstanceFrom<ISystemWindowProvider>(
				AggContext.Config.ProviderTypes.SystemWindowProvider);

			await Assert.That(provider).IsNotNull()
				.Because("the configured provider type has to resolve, or this test is asserting nothing");

			var neverShown = new SystemWindow(400, 300);

			await Assert.That(neverShown.PlatformWindow).IsNull()
				.Because("a window that was never shown is exactly the case under test");

			// The bug this pins was an unconditional systemWindow.PlatformWindow.CloseSystemWindow(...).
			provider.CloseSystemWindow(neverShown);

			await Assert.That(provider.OpenWindows).DoesNotContain(neverShown)
				.Because("a close has to finish its bookkeeping, not stop at the missing platform window");
		}

		[Test]
		public async Task TheShellIsTheFirstOpenWindowOnlyInSingleWindowMode()
		{
			// A provider that gives every window its own platform window (WebGpuX11WindowProvider and friends)
			// leaves SingleWindowMode alone, and there the window that was asked to close really is the one to
			// close - looking past it would take the whole application down with a dialog.
			var shell = new SystemWindow(400, 300) { IsApplicationShell = true };
			var dialog = new SystemWindow(200, 100);
			var provider = new StackedWindowProvider(shell, dialog);

			await Assert.That(PlatformCloseArbitration.ShellWindowForClose(
				singleWindowMode: true, provider: provider, activeWindow: dialog)).IsEqualTo(shell);

			await Assert.That(PlatformCloseArbitration.ShellWindowForClose(
				singleWindowMode: false, provider: provider, activeWindow: dialog)).IsEqualTo(dialog);
		}

		/// <summary>
		/// A stand-in for <see cref="SingleWindowProvider"/> with the one property the routing depends on: the
		/// shell first and the dialogs stacked above it. The real one needs a platform window to show anything.
		/// </summary>
		private class StackedWindowProvider : ISystemWindowProvider
		{
			private readonly List<SystemWindow> openWindows;

			public StackedWindowProvider(params SystemWindow[] windows)
			{
				this.openWindows = new List<SystemWindow>(windows);
			}

			public IReadOnlyList<SystemWindow> OpenWindows => this.openWindows;

			public SystemWindow TopWindow => this.openWindows.Count > 0 ? this.openWindows[this.openWindows.Count - 1] : null;

			public void ShowSystemWindow(SystemWindow systemWindow)
			{
				this.openWindows.Add(systemWindow);
			}

			public void CloseSystemWindow(SystemWindow systemWindow)
			{
				this.openWindows.Remove(systemWindow);
			}
		}
	}
}
