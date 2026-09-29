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
	/// Where the canvas's drag-and-drop events enter managed code: unpacks what <c>input.js</c>'s
	/// <c>attachFileDrop</c> hands over and passes it to the page's one <see cref="BrowserFileDrop"/>.
	/// </summary>
	/// <remarks>
	/// Deliberately as thin as <see cref="BrowserInputEvents"/>, for the same reason: every decision is in
	/// <see cref="BrowserFileDrop"/>, which the desktop suite drives, and reading named properties off a JS
	/// object is all that is left here.
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public static partial class BrowserFileDropEvents
	{
		/// <summary>One page shows one agg window, so one drop session serves it.</summary>
		private static readonly BrowserFileDrop FileDrop = new BrowserFileDrop(
			() => BrowserSystemWindow.Current?.AggSystemWindow,
			() => BrowserSystemWindow.Current?.Backing ?? default(BrowserBackingSize),
			UiThread.RunOnIdle);

		/// <summary>
		/// Takes one drag event and answers whether a dragover should show as a copy. Exported as well as
		/// handed to <c>attachFileDrop</c>, so a drop can be replayed from devtools or a test page.
		/// </summary>
		[JSExport]
		internal static bool DispatchFileDragEvent(JSObject dragEvent)
		{
			if (dragEvent == null
				|| BrowserSystemWindow.Current == null
				|| !IPlatformWindow.EnablePlatformWindowInput)
			{
				return false;
			}

			// Released as soon as its properties are read: a dropchunk's object holds a 4 MB slice, which the
			// JS heap would otherwise keep until the managed proxy happened to be collected.
			using JSObject disposeAfterReading = dragEvent;

			try
			{
				return FileDrop.Handle(new BrowserFileDragEvent
				{
					Type = dragEvent.GetPropertyAsString("type"),
					OffsetX = dragEvent.GetPropertyAsDouble("offsetX"),
					OffsetY = dragEvent.GetPropertyAsDouble("offsetY"),
					Names = SplitLines(dragEvent.GetPropertyAsString("names")),
					MimeTypes = SplitLines(dragEvent.GetPropertyAsString("types")),
					Name = dragEvent.GetPropertyAsString("name"),
					Size = (long)dragEvent.GetPropertyAsDouble("size"),
					Bytes = dragEvent.GetPropertyAsByteArray("bytes"),
				});
			}
			catch (Exception dropException)
			{
				// Inside a DOM listener or a promise continuation, where an escaping exception reaches only the
				// browser console. See BrowserInputEvents.DispatchInputEvent.
				Console.Error.WriteLine($"BrowserFileDropEvents could not handle a drag event: {dropException}");
				UiThread.ReportUnhandledException(dropException);
				return false;
			}
		}

		/// <summary>
		/// The per-file lists come over newline-joined (a marshalled object has no string-array getter);
		/// <c>input.js</c> replaces any newline inside a name first, so the split is exact.
		/// </summary>
		private static string[] SplitLines(string joined)
			=> joined == null ? Array.Empty<string>() : joined.Split('\n');
	}
}
