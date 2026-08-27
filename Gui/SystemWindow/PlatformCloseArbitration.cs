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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Decides what a native "the user wants this window gone" request means to agg: which window is actually
	/// being closed, and whether the host may tear its native window down.
	/// </summary>
	/// <remarks>
	/// The request arrives differently on every host - the red button or Cmd-Q on macOS, WM_CLOSE on Windows,
	/// WM_DELETE_WINDOW on X11, beforeunload in a browser - but the arbitration afterwards is identical, and
	/// getting it wrong is the same bug everywhere: a dialog is asked instead of the shell, so none of the
	/// shell's ShouldClose/Closed handlers run and the platform destroys the window anyway.
	/// </remarks>
	public static class PlatformCloseArbitration
	{
		/// <summary>
		/// Runs a native close request - the red button, Cmd-Q, the window manager - against the application
		/// rather than against whatever window happens to be on top, and reports whether the platform may go
		/// ahead and tear its window down.
		/// </summary>
		/// <param name="singleWindowMode">Whether dialogs are drawn inside the one platform window.</param>
		/// <param name="provider">The provider holding the open windows, if there is one.</param>
		/// <param name="activeWindow">The window currently being drawn and given events.</param>
		/// <param name="setPlatformClosing">
		/// Sets (and, if the close does not take, clears) the host's "the platform is already closing" flag.
		/// </param>
		/// <remarks>
		/// Static and parameterised because the decision it makes - which window is asked, and whether the
		/// native window may go away - is the whole bug, and none of it needs a platform to exercise.
		/// </remarks>
		public static bool HandlePlatformCloseRequest(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow,
			Action<bool> setPlatformClosing)
		{
			// The user closed the application, not the dialog drawn inside it. Asking the dialog runs none of
			// the shell's ShouldClose/Closed handlers - window bounds persistence, save on exit - and the
			// native window is torn down immediately afterwards regardless, so that work is simply lost.
			var shellWindow = ShellWindowForClose(singleWindowMode, provider, activeWindow);

			if (shellWindow == null || shellWindow.HasBeenClosed)
			{
				return true;
			}

			// Only the shell decides whether the application may close: an open dialog does not veto here.
			// In single window mode a dialog is a widget drawn inside this window, so its titlebar button is
			// the only close that belongs to it - the red button, Cmd-Q, the X, Alt-F4 and the frame's close
			// button have always meant "close the application", and applications that want to refuse
			// mid-dialog do it in their own ShouldClose ("do you want to save?" and friends).
			var shouldClose = new ShouldCloseEventArgs();
			shellWindow.OnShouldClose(shouldClose);

			if (shouldClose.Cancel)
			{
				return false;
			}

			// The agg close runs first so widgets get their Closed events while the window is still alive. It
			// calls back through the provider into CloseSystemWindow, which the flag makes a no-op - the
			// platform is already in the middle of closing us.
			setPlatformClosing?.Invoke(true);
			shellWindow.Close();

			if (!shellWindow.HasBeenClosed)
			{
				// Close asks OnShouldClose a second time and an application may cancel on that one (having
				// just put up its "save first?" dialog on the first ask). Letting the platform destroy the
				// window anyway is exactly the "closed with no Closed events" bug, so the shell that is still
				// open keeps its native window.
				setPlatformClosing?.Invoke(false);
				return false;
			}

			return true;
		}

		/// <summary>
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// </summary>
		/// <remarks>
		/// In single window mode the host's active window is the window being drawn and given the events,
		/// which the provider re-points at every dialog that opens. Closing that only dismisses the dialog -
		/// the shell stays up, the event loop keeps running, and the process never exits. The provider keeps
		/// the shell first in <see cref="ISystemWindowProvider.OpenWindows"/> and takes the dialogs above it
		/// down with it, so closing that one window is the whole application closing.
		/// </remarks>
		public static SystemWindow ShellWindowForClose(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow)
		{
			if (singleWindowMode && provider != null)
			{
				var openWindows = provider.OpenWindows;

				if (openWindows.Count > 0)
				{
					return openWindows[0];
				}
			}

			return activeWindow;
		}
	}
}
