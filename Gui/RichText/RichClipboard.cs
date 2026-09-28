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
using System.Linq;

namespace MatterHackers.Agg.UI.RichText
{
	/// <summary>
	/// The process-wide styled clipboard slot for <see cref="RichTextEdit"/> (agg-gui's rich_clipboard). The system
	/// clipboard only carries plain text, so Copy also keeps the styled fragment here under a fingerprint - the plain
	/// text it wrote to the system clipboard. Paste reuses the fragment only while the system clipboard still holds
	/// that text; once anything else is copied, the fingerprint no longer matches and paste falls back to plain text.
	/// </summary>
	public static class RichClipboard
	{
		private static string fingerprint;
		private static List<Block> blocks;

		/// <summary>Stores <paramref name="fragment"/> under the plain text Copy wrote to the system clipboard.</summary>
		public static void Set(string plainText, IEnumerable<Block> fragment)
		{
			fingerprint = plainText;
			blocks = fragment.Select(b => b.Clone()).ToList();
		}

		/// <summary>
		/// A copy of the stored fragment when its fingerprint matches <paramref name="plainText"/> (just read from the
		/// system clipboard), else null. Copies, so one Copy pastes any number of times.
		/// </summary>
		public static List<Block> Matching(string plainText)
		{
			// Native clipboards (Windows especially) hand '\n' back as "\r\n"; compare line endings normalized.
			if (blocks == null || plainText == null || Normalize(fingerprint) != Normalize(plainText))
			{
				return null;
			}

			return blocks.Select(b => b.Clone()).ToList();
		}

		/// <summary>Drops the stored fragment.</summary>
		public static void Clear()
		{
			fingerprint = null;
			blocks = null;
		}

		private static string Normalize(string text) => text?.Replace("\r\n", "\n").Replace('\r', '\n');
	}
}
