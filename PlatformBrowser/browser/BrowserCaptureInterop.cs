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
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// The door a test runner outside the browser fetches a capture through.
	/// </summary>
	/// <remarks>
	/// <para><b>Why this exists at all.</b> <see cref="BrowserSystemWindow.CaptureScreenshotAsync"/> writes a
	/// real PNG, but it writes it into Emscripten's in-memory filesystem - a byte array inside the wasm heap
	/// that no <c>fs.readFile</c>, no download folder and no CDP command can see. Without a way to read it
	/// back out, a browser capture is only ever visible to the page that took it, which is no use to a golden
	/// image runner driving the page from the outside.</para>
	/// <para><b>The whole surface, deliberately.</b> One function, no session, no state: hand it the path a
	/// capture was written to and get the bytes. Taking the capture is the caller's job (the page's own code,
	/// or whatever hook a head exposes), and deciding what to compare it against is the runner's; the runner
	/// itself is a later step. Everything here is a byte for byte copy of what is on disk in the wasm FS, so a
	/// golden comparison is against the same PNG a desktop capture would have produced.</para>
	/// <para><b>Base64 rather than a byte array.</b> Crossing into JS copies either way, and a base64 string
	/// survives the plain-JSON return path of a CDP <c>Runtime.evaluate</c> - which is how a driver outside
	/// the browser actually calls this - where a typed array does not.</para>
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public static partial class BrowserCaptureInterop
	{
		/// <summary>
		/// Reads a file out of the wasm filesystem as base64 - a capture written by
		/// <see cref="BrowserSystemWindow.CaptureScreenshotAsync"/>, in the case this exists for.
		/// </summary>
		/// <remarks>
		/// Returns null rather than throwing for a path that is not there: a driver polling for a capture that
		/// is still in flight asks this question legitimately, and "not yet" is an answer, not a failure. The
		/// in-memory filesystem is emptied when the page unloads, so a runner has to fetch each capture before
		/// it navigates away.
		/// </remarks>
		/// <param name="path">The path the capture was written to.</param>
		/// <returns>The file's bytes, base64 encoded, or null if there is no such file.</returns>
		[JSExport]
		internal static string ReadCaptureAsBase64(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return null;
			}

			return Convert.ToBase64String(File.ReadAllBytes(path));
		}
	}
}
