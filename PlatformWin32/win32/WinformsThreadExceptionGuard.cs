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
using System.Windows.Forms;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Keeps an exception that reaches a WinForms message loop from becoming a modal dialog.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What it is preventing.</b> With no <c>ThreadException</c> subscriber, WinForms answers an
	/// exception that escapes a window procedure with the modal <c>ThreadExceptionDialog</c>. That is a
	/// nested message loop: on a developer's machine it is a dialog to read, but on an unattended run
	/// nobody dismisses it, so the loop never returns, the window never closes, and everything waiting on
	/// that thread waits forever. It has cost a whole CI shard twice - once from a widget that threw while
	/// drawing, once from drag-drop registration on an MTA thread - and each time the exception itself was
	/// the small half of the problem.
	/// </para>
	/// <para>
	/// <b>Per thread, at the pump.</b> <c>Application.ThreadException</c> is not application-wide; adding to
	/// it registers on <c>ThreadContext.FromCurrent()</c>, so a handler subscribed on the main thread does
	/// nothing at all for a window pumped on another one - which is exactly where the wedges have been.
	/// <see cref="InstallForCurrentPump"/> is therefore called from the thread that is about to call
	/// <c>Application.Run</c>, the same idiom AutomationRunner's thread registration documents, and every
	/// pump this library starts is covered without anyone having to remember to ask.
	/// </para>
	/// <para>
	/// <b>Not SetUnhandledExceptionMode.</b> The mode may only be changed before the thread has created a
	/// window, and the only honest place to install this is after <c>Show()</c>, so calling it here would
	/// risk an InvalidOperationException for no gain: with a subscriber attached, the default Automatic mode
	/// already routes to the handler instead of the dialog. AutomationRunner still sets it (defensively, by
	/// reflection, swallowing failure) before it shows anything, and this composes with that rather than
	/// fighting it - <c>ThreadException</c> is multicast, so both handlers run, and its capture keeps the
	/// first exception either way.
	/// </para>
	/// </remarks>
	internal static class WinformsThreadExceptionGuard
	{
		/// <summary>
		/// Per thread, because the event is: one message loop, one subscription. A second window shown on
		/// the same thread must not add a second handler and double every report.
		/// </summary>
		[ThreadStatic]
		private static bool installedOnThisThread;

		/// <summary>
		/// Subscribes this thread's WinForms exception handler, once. Call from the thread that is becoming
		/// a message loop.
		/// </summary>
		public static void InstallForCurrentPump()
		{
			if (installedOnThisThread)
			{
				return;
			}

			installedOnThisThread = true;

			Application.ThreadException += (sender, e) =>
			{
				Console.Error.WriteLine(
					$"An exception reached the WinForms message loop, reported instead of shown: {e.Exception}");

				// The channel tests and the automation harness already listen on, so whatever was running
				// still fails - it just fails alone, instead of taking the process's UI thread with it.
				UiThread.ReportUnhandledException(e.Exception);
			};
		}

		/// <summary>
		/// Forgets this thread's subscription, so a later pump on the same thread installs a fresh one. Call
		/// when <c>Application.Run</c> returns.
		/// </summary>
		/// <remarks>
		/// WinForms disposes the thread's <c>ThreadContext</c> when its message loop ends, and the
		/// <c>ThreadException</c> subscription lives on that context - so the handler is gone whether we
		/// forget it or not. Only the flag would survive, and a thread that runs a second pump (a pool
		/// thread, which is what the tests hand these windows) would then skip reinstalling and pump with no
		/// guard at all.
		/// </remarks>
		public static void ForgetCurrentPump()
		{
			installedOnThisThread = false;
		}
	}
}
