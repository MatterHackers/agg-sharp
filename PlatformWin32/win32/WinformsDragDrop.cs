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
using System.Threading;
using System.Windows.Forms;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Turns on WinForms drag-drop for a window or a control, on the threads where that is legal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Drag-drop is OLE, and OLE needs an STA thread.</b> Setting <c>AllowDrop = true</c> does not
	/// register anything by itself; WinForms defers that to <c>Control.SetAcceptDrops</c>, which runs from
	/// <c>OnHandleCreated</c> and starts with <c>if (Application.OleRequired() != ApartmentState.STA) throw
	/// new ThreadStateException(...)</c>. On a thread that is already MTA - every thread-pool thread is -
	/// <c>CoInitializeEx</c> cannot switch it, so that throw is unconditional. Note where it happens: not at
	/// the assignment, but later, inside the handle-creation callback, so a try/catch around the assignment
	/// would never see it.
	/// </para>
	/// <para>
	/// <b>What that cost.</b> The throw escapes <c>OnHandleCreated</c> through <c>NativeWindow.Callback</c>
	/// into WinForms' unhandled-exception path, and with no <c>ThreadException</c> handler installed that is
	/// the modal <c>ThreadExceptionDialog</c> - a nested message loop nobody is there to dismiss on an
	/// unattended run, which then holds the window's thread forever. A real application never sees it
	/// (<c>[STAThread]</c> on Main), and neither does an automation run (<c>AutomationRunner</c> sets
	/// <see cref="SystemWindow.EnableAllowDrop"/> false for its duration); a test that shows a window from a
	/// pool thread without either is exactly the gap.
	/// </para>
	/// <para>
	/// So the apartment is asked first rather than the exception caught afterwards: a host that cannot have
	/// drag-drop loses drag-drop and says so, and keeps its window.
	/// </para>
	/// </remarks>
	internal static class WinformsDragDrop
	{
		/// <summary>
		/// Set once per process, so a suite that opens hundreds of windows on pool threads explains itself
		/// once instead of hundreds of times.
		/// </summary>
		private static int apartmentWarningWritten;

		/// <summary>
		/// Enables drag-drop on <paramref name="control"/> when the application wants it and this thread can
		/// legally have it; otherwise leaves it off.
		/// </summary>
		/// <param name="control">The form or control to accept drops.</param>
		/// <param name="description">Names the target in the one-time note, for a reader of a CI log.</param>
		public static void TryEnable(Control control, string description)
		{
			if (control == null)
			{
				return;
			}

			var apartment = Thread.CurrentThread.GetApartmentState();

			// The decision itself lives in SystemWindow, where the flag it reads does, and takes the
			// apartment as an argument so it can be tested off Windows.
			if (SystemWindow.ShouldEnableAllowDrop(SystemWindow.EnableAllowDrop, apartment))
			{
				control.AllowDrop = true;
				return;
			}

			if (SystemWindow.EnableAllowDrop
				&& Interlocked.Exchange(ref apartmentWarningWritten, 1) == 0)
			{
				Console.Error.WriteLine(
					$"Drag and drop is off for {description}: it needs an STA thread and this window is being"
					+ $" shown from a {apartment} one (every thread-pool thread is MTA). Everything else about the"
					+ " window works. Give the thread that calls ShowAsSystemWindow STA to get drag-drop back.");
			}
		}
	}
}
