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
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Where the DOM's events enter managed code: unpacks what <c>input.js</c> hands over and queues it on the
	/// window that is showing.
	/// </summary>
	/// <remarks>
	/// <para>Deliberately thin. Every decision - which agg key a code is, which button a mask means, what a
	/// pinch is worth, where in agg's coordinate space a click landed - is made by the window and the
	/// translation tables it calls, all of which run in the desktop test suite. What is left here is reading
	/// named properties off a JS object, which is the one part no test process can do.</para>
	/// <para>JS fills every field of the bag on every event, including the ones the event has no use for, so
	/// this side never has to ask whether a property is there. That is the contract between this file and
	/// <c>input.js</c>; keep them in step.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public static partial class BrowserInputEvents
	{
		/// <summary>
		/// Queues one DOM input event: a pointer event, a wheel event, a key event, or a blur.
		/// </summary>
		/// <remarks>
		/// <c>[JSExport]</c> as well as being handed to <c>attachInput</c> as a marshalled callback, so a
		/// misbehaving event can be replayed by hand from devtools.
		/// </remarks>
		[JSExport]
		internal static void DispatchInputEvent(JSObject inputEvent)
		{
			BrowserSystemWindow window = BrowserSystemWindow.Current;
			if (window == null || inputEvent == null)
			{
				return;
			}

			try
			{
				string type = inputEvent.GetPropertyAsString("type");

				bool ctrlKey = inputEvent.GetPropertyAsBoolean("ctrlKey");
				bool shiftKey = inputEvent.GetPropertyAsBoolean("shiftKey");
				bool altKey = inputEvent.GetPropertyAsBoolean("altKey");
				bool metaKey = inputEvent.GetPropertyAsBoolean("metaKey");

				switch (type)
				{
					case "keydown":
					case "keyup":
						window.EnqueueKeyEvent(
							type,
							inputEvent.GetPropertyAsString("code"),
							inputEvent.GetPropertyAsString("key"),
							ctrlKey,
							shiftKey,
							altKey,
							metaKey);
						break;

					case "wheel":
						window.EnqueueWheelEvent(
							inputEvent.GetPropertyAsDouble("offsetX"),
							inputEvent.GetPropertyAsDouble("offsetY"),
							inputEvent.GetPropertyAsDouble("deltaX"),
							inputEvent.GetPropertyAsDouble("deltaY"),
							inputEvent.GetPropertyAsInt32("deltaMode"),
							ctrlKey,
							shiftKey,
							altKey,
							metaKey);
						break;

					case "blur":
						window.EnqueueFocusLost();
						break;

					default:
						// Everything else is a pointer event; the window decides what each type means.
						window.EnqueuePointerEvent(
							type,
							inputEvent.GetPropertyAsDouble("offsetX"),
							inputEvent.GetPropertyAsDouble("offsetY"),
							inputEvent.GetPropertyAsInt32("button"),
							inputEvent.GetPropertyAsInt32("buttons"),
							inputEvent.GetPropertyAsInt32("detail"),
							ctrlKey,
							shiftKey,
							altKey,
							metaKey);
						break;
				}
			}
			catch (Exception dispatchException)
			{
				// This runs inside a DOM listener, where an escaping exception is reported to the console by
				// the browser and to nobody by agg - so the automation channel is told here instead. Contained
				// rather than propagated for the same reason a bad frame is: one unreadable event must not stop
				// the page taking input.
				Console.Error.WriteLine($"BrowserInputEvents could not translate an input event: {dispatchException}");
				UiThread.ReportUnhandledException(dispatchException);
			}
		}

		/// <summary>
		/// Queues a canvas resize, in exact device pixels. JS owns the rounding and has already sized the
		/// canvas's backing store to match; see <see cref="BrowserBacking"/>.
		/// </summary>
		[JSExport]
		internal static void DispatchResize(double devicePixelWidth, double devicePixelHeight, double devicePixelRatio)
		{
			BrowserSystemWindow window = BrowserSystemWindow.Current;
			if (window == null)
			{
				return;
			}

			try
			{
				window.EnqueueBackingSize(devicePixelWidth, devicePixelHeight, devicePixelRatio);
			}
			catch (Exception resizeException)
			{
				Console.Error.WriteLine($"BrowserInputEvents could not queue a resize: {resizeException}");
				UiThread.ReportUnhandledException(resizeException);
			}
		}
	}
}
