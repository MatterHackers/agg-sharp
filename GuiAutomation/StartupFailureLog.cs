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
using System.IO;
using System.Text;
using System.Threading;

namespace MatterHackers.GuiAutomation
{
	/// <summary>
	/// Where an application records a failure that killed its own startup, so that the shallow automation
	/// failure it causes - a click on a widget the dead application never built - can say what actually went
	/// wrong.
	/// </summary>
	/// <remarks>
	/// The application boots on its own threads (a window Load event, a <c>Task.Run</c> under it), and a test
	/// host's console capture is per test: TUnit files an ambient <c>Console.Write</c> under whichever test it
	/// believes is running, which under shard load is one of the several automation tests waiting their turn
	/// for the application gate rather than the one that will fail. That test then passes, and a passing
	/// test's captured output is never printed - which is how a startup exception that WAS written to stderr
	/// left no trace at all in a four-shard log, and why the boot path cannot rely on the console to carry it.
	/// So the failure is recorded here instead, in a plain process wide static that needs no ambient context,
	/// and read back out by <see cref="AutomationRunner.WidgetNotFoundMessage"/> in the failing test's own
	/// thread, where the capture does attribute it correctly.
	/// <para/>
	/// <see cref="ImmediateSink"/> is the belt to that braces: the description also goes straight to the
	/// process's real standard error handle, underneath any per-test redirection, so a run whose test never
	/// fails visibly (a hang, a crashed host) still leaves the stack somewhere in the shard log.
	/// </remarks>
	public static class StartupFailureLog
	{
		private static string lastFailureDescription;

		private static TextWriter immediateSink;

		private static bool immediateSinkResolved;

		private static readonly object locker = new object();

		/// <summary>
		/// Where a failure is written the moment it is recorded. Defaults to the process's real standard error
		/// handle - deliberately not <see cref="Console.Error"/>, which a test host redirects per test.
		/// </summary>
		/// <remarks>Settable so a test can read what was written without spraying the suite's own stderr.</remarks>
		public static TextWriter ImmediateSink
		{
			get
			{
				lock (locker)
				{
					if (!immediateSinkResolved)
					{
						immediateSinkResolved = true;
						immediateSink = OpenRealStandardError();
					}

					return immediateSink;
				}
			}

			set
			{
				lock (locker)
				{
					immediateSinkResolved = true;
					immediateSink = value;
				}
			}
		}

		/// <summary>
		/// The last startup failure recorded in this process, as type, message and full stack - or null if
		/// nothing has failed.
		/// </summary>
		public static string LastFailureDescription => Volatile.Read(ref lastFailureDescription);

		/// <summary>
		/// Records a failure that stopped the application from finishing its startup.
		/// </summary>
		/// <param name="phase">What was being brought up, named the way the caller reports it (e.g. "Startup").</param>
		/// <param name="exception">The failure. Its type, message and full stack are all kept.</param>
		public static void Record(string phase, Exception exception)
		{
			if (exception == null)
			{
				return;
			}

			var description = new StringBuilder()
				.Append("Application ").Append(string.IsNullOrEmpty(phase) ? "startup" : phase)
				.Append(" failed at ").Append(DateTime.Now.ToString("HH:mm:ss.fff"))
				.Append(" on thread ").Append(Thread.CurrentThread.ManagedThreadId)
				.AppendLine(":")
				.AppendLine(exception.ToString())
				.ToString();

			Volatile.Write(ref lastFailureDescription, description);

			WriteImmediately(description);
		}

		/// <summary>
		/// Forgets any recorded failure. Called as an application boots, so one test's startup crash is never
		/// reported against the next test's unrelated failure.
		/// </summary>
		public static void Reset()
		{
			Volatile.Write(ref lastFailureDescription, null);
		}

		/// <summary>
		/// Appends the recorded startup failure, if there is one, to a failure message that is about to be
		/// thrown - so the shallow symptom carries the deep cause.
		/// </summary>
		/// <param name="message">The failure message to extend.</param>
		/// <returns><paramref name="message"/> unchanged when no startup failure was recorded.</returns>
		public static string AppendTo(string message)
		{
			string failure = LastFailureDescription;

			if (string.IsNullOrEmpty(failure))
			{
				return message;
			}

			return message
				+ Environment.NewLine
				+ "The application never finished starting up, so its widgets were never built. "
				+ "The startup failure was:"
				+ Environment.NewLine
				+ failure;
		}

		private static void WriteImmediately(string description)
		{
			try
			{
				var sink = ImmediateSink;
				if (sink != null)
				{
					sink.Write(description);
					sink.Flush();
				}
			}
			catch
			{
				// Diagnostics may never be the reason a startup failure turns into a second failure.
			}
		}

		private static TextWriter OpenRealStandardError()
		{
			try
			{
				// AutoFlush: this is written from a process that may be about to die, and a buffered
				// description is one that never reaches the log it exists for.
				return new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
			}
			catch
			{
				// No standard error handle at all - a windowed or browser host. The recorded description is
				// still read back by the automation runner, which is the sink that matters for a test.
				return null;
			}
		}
	}
}
