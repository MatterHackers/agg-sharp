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
using System.Collections.Generic;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Turns agg's file dialog filter string into an <c>&lt;input type="file"&gt;</c> <c>accept</c> attribute.
	/// </summary>
	/// <remarks>
	/// Pure, and public, for the same reason <see cref="BrowserPointer"/> is: this is the one part of the
	/// open dialog with a right and a wrong answer, and it runs in the desktop test suite.
	/// <para/>
	/// The two formats disagree about almost everything. agg's is
	/// <c>"Meshes|*.stl;*.amf|All Files|*.*"</c> - descriptions paired with glob patterns, and the
	/// description is what the user picks from a dropdown. <c>accept</c> is a flat comma-separated list of
	/// extensions or MIME types with no descriptions at all, because the browser's picker has no filter
	/// dropdown to put them in. So the groups are flattened and the descriptions dropped; a user who wanted
	/// "only AMF" gets a picker that also lets them choose an STL, which is the browser's model and not
	/// something a page can change.
	/// </remarks>
	public static class BrowserFileFilter
	{
		/// <summary>
		/// Builds the <c>accept</c> value for <paramref name="filter"/>, or an empty string for "any file".
		/// </summary>
		/// <remarks>
		/// Empty is returned for three different inputs, and they mean the same thing to a picker: no filter
		/// at all, a filter with no usable patterns in it, and a filter that includes an all-files group
		/// (<c>*.*</c> or <c>*</c>). That last one is the case worth knowing about - a filter of
		/// <c>"Meshes|*.stl|All Files|*.*"</c> has to produce <em>no</em> accept, because the user is
		/// entitled to pick anything and an accept listing only <c>.stl</c> would stop them. There is no
		/// browser equivalent of the dropdown that would have let them switch.
		/// </remarks>
		public static string ToAcceptAttribute(string filter)
		{
			var extensions = new List<string>();

			foreach (string pattern in Patterns(filter))
			{
				if (IsAllFiles(pattern))
				{
					return string.Empty;
				}

				string extension = ToExtension(pattern);

				// Deduplicated because agg's groups overlap constantly - "Meshes|*.stl;*.obj|STL|*.stl" is
				// an ordinary filter - and a repeated entry in accept is noise at best.
				if (extension != null && !extensions.Contains(extension))
				{
					extensions.Add(extension);
				}
			}

			return string.Join(",", extensions);
		}

		/// <summary>
		/// Whether a pattern matches every file, in which case there is no accept to build.
		/// </summary>
		public static bool IsAllFiles(string pattern) => pattern == "*" || pattern == "*.*";

		/// <summary>
		/// The <c>accept</c> extension a single glob pattern becomes, or null for one that cannot be
		/// expressed.
		/// </summary>
		/// <remarks>
		/// <c>accept</c> understands exactly two things: a MIME type and a leading-dot extension. It has no
		/// globbing at all, so only the <c>*.ext</c> shape survives translation - a pattern like
		/// <c>backup?.stl</c>, which the Windows common dialog would honour, has no browser equivalent and is
		/// dropped rather than turned into something that would match the wrong files. Lower-cased because
		/// the match is case-insensitive either way and a mixed-case accept list reads like it might not be.
		/// </remarks>
		public static string ToExtension(string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return null;
			}

			string trimmed = pattern.Trim();

			if (!trimmed.StartsWith("*.", StringComparison.Ordinal))
			{
				return null;
			}

			string extension = trimmed.Substring(1);

			// Anything still holding a wildcard past the leading "*." - "*.st?" - is a glob accept cannot
			// express. Same rule as the shape check above, applied to the tail.
			if (extension.Length < 2 || extension.Contains('*') || extension.Contains('?'))
			{
				return null;
			}

			return extension.ToLowerInvariant();
		}

		/// <summary>
		/// Every glob pattern in an agg filter string, in order, with the descriptions discarded.
		/// </summary>
		/// <remarks>
		/// The format is positional pairs with no escaping, so a trailing unpaired element (a description
		/// with no patterns) is dropped rather than treated as an error - the same forgiveness
		/// <c>LinuxFileDialogProvider.ParseFilter</c> extends, and for the same reason: throwing out of a
		/// menu click over a typo in a filter string helps nobody.
		/// </remarks>
		private static IEnumerable<string> Patterns(string filter)
		{
			if (string.IsNullOrWhiteSpace(filter))
			{
				yield break;
			}

			string[] fields = filter.Split('|');

			// Step by two - descriptions are the even fields - and stop before an unpaired tail.
			for (int i = 1; i < fields.Length; i += 2)
			{
				foreach (string pattern in fields[i].Split(';'))
				{
					string trimmed = pattern.Trim();
					if (trimmed.Length > 0)
					{
						yield return trimmed;
					}
				}
			}
		}
	}
}
