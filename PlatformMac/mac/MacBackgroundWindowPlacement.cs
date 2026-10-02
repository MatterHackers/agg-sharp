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
using MatterHackers.Agg.Platform.Mac;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The AppKit half of <see cref="IPlatformWindow.ShowWindowsInBackground"/>: how a mac window is put on
	/// screen, raised and "activated" so that a test run never takes the user away from the app they are in.
	/// The WinForms counterpart is <c>BackgroundWindowPlacement</c>.
	/// </summary>
	/// <remarks>
	/// With the switch on, nothing here makes a window key or activates the application. Synthetic input is
	/// dispatched straight to agg widgets and the mac host gates nothing on isKeyWindow or [NSApp isActive],
	/// so a window that is neither still receives every test's input and still paints.
	/// <para>
	/// Placement: a window cannot be ordered relative to another application's window, and a plain
	/// <c>orderFront:</c> from an inactive app lands over the user's work. So a window goes directly above
	/// this process's own frontmost window - over its test window, the way a dialog should, yet still behind
	/// whatever the user has in front - and the first window, with nothing of ours to sit on, goes to the
	/// back with <c>orderBack:</c>.
	/// </para>
	/// <para>
	/// Occlusion: a window behind the user's app may be reported occluded, and then wgpu's Metal backend
	/// vends no drawable. MacWebGpuLayer already draws such a frame into a scratch target, so agg's whole
	/// draw still runs - only the present is skipped - and nothing waiting on a draw stalls.
	/// </para>
	/// </remarks>
	internal static class MacBackgroundWindowPlacement
	{
		/// <summary>NSWindowOrderingMode NSWindowAbove.</summary>
		private const long NSWindowAbove = 1;

		/// <summary>NSApplicationActivationPolicyAccessory: windows, but no Dock icon and no launch activation.</summary>
		private const long NSApplicationActivationPolicyAccessory = 1;

		/// <summary>True once finishLaunching has run; it is only ever run once per process.</summary>
		private static bool launchFinished;

		/// <summary>
		/// Settles the application's launch state for a window about to be shown. Done at show time, not
		/// when the NSApplication is bootstrapped, because the window constructor bootstraps it and
		/// AutomationRunner turns <see cref="IPlatformWindow.ShowWindowsInBackground"/> on only between
		/// constructing its window and showing it - a startup-time decision read the switch as off.
		/// </summary>
		/// <remarks>
		/// Normally: the Regular policy - an Accessory or (the unbundled default) Prohibited app cannot come
		/// to the front the way an application should - and finishLaunching, which does the work [NSApp run]
		/// would do on entry; without it a pumped app can end up unable to become frontmost.
		/// <para>
		/// In the background: the Accessory policy, so a test run puts no Dock icon in front of the user, and
		/// no finishLaunching. Its launch activation arrives a few pumps after the first window is shown and
		/// made the test process frontmost under either policy - measured; it is what took the user's app
		/// away before this existed. The switch never turns off again in a process, so skipping it for good
		/// costs a test run nothing.
		/// </para>
		/// </remarks>
		internal static void PrepareLaunchForShow(IntPtr nsApp)
		{
			bool background = IPlatformWindow.ShowWindowsInBackground;
			Send_B_q(nsApp, Sel("setActivationPolicy:"), background
				? NSApplicationActivationPolicyAccessory
				: AppKitConstants.NSApplicationActivationPolicyRegular);

			if (!background && !launchFinished)
			{
				launchFinished = true;
				Send_v(nsApp, Sel("finishLaunching"));
			}
		}

		/// <summary>
		/// Puts a window on screen key with the app active - or, in the background, behind the user's work.
		/// Also what Activate does, since activating a shown window is the same pair of calls.
		/// </summary>
		internal static void Show(IntPtr nsApp, IntPtr window)
		{
			if (IPlatformWindow.ShowWindowsInBackground)
			{
				OrderInBehindTheUser(nsApp, window);
				return;
			}

			Send_v_r(window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
			Send_v_B(nsApp, Sel("activateIgnoringOtherApps:"), YES);
		}

		/// <summary>Raises a window over the application's others; in the background, only among our own windows.</summary>
		internal static void BringToFront(IntPtr nsApp, IntPtr window)
		{
			if (IPlatformWindow.ShowWindowsInBackground)
			{
				OrderInBehindTheUser(nsApp, window);
				return;
			}

			Send_v_r(window, Sel("orderFront:"), IntPtr.Zero);
		}

		/// <summary>Orders a window in above our own frontmost window, or to the back; never key, never activating.</summary>
		private static void OrderInBehindTheUser(IntPtr nsApp, IntPtr window)
		{
			IntPtr anchor = FrontmostOtherWindow(nsApp, window);
			if (anchor == IntPtr.Zero)
			{
				Send_v_r(window, Sel("orderBack:"), IntPtr.Zero);
				return;
			}

			Send_v_q_q(window, Sel("orderWindow:relativeTo:"), NSWindowAbove, Send_q(anchor, Sel("windowNumber")));
		}

		/// <summary>This process's frontmost visible window other than <paramref name="window"/>, or zero.</summary>
		private static IntPtr FrontmostOtherWindow(IntPtr nsApp, IntPtr window)
		{
			// orderedWindows is front to back, so the first visible one that is not the window being placed
			// is the one it belongs directly above.
			IntPtr ordered = Send_r(nsApp, Sel("orderedWindows"));
			ulong count = ordered == IntPtr.Zero ? 0 : Send_Q(ordered, Sel("count"));
			for (ulong i = 0; i < count; i++)
			{
				IntPtr candidate = Send_r_Q(ordered, Sel("objectAtIndex:"), i);
				if (candidate != window && Send_B(candidate, Sel("isVisible")) != NO)
				{
					return candidate;
				}
			}

			return IntPtr.Zero;
		}
	}
}
