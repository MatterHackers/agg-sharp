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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Other
{
	/// <summary>
	/// TUnit's IsEquivalentTo / IsNotEquivalentTo on a collection ignore order unless given
	/// CollectionOrdering.Matching. A test that means "this sequence" but compares unordered passes for a
	/// shuffled result: a wrap-mode walk, an event order or an argv list in any order all looked right.
	/// So every equivalence compare in Tests/Agg.Tests has to say which it means - Matching for a sequence,
	/// Any for a genuine set such as dictionary keys or a directory scan.
	///
	/// The unordered match is also quadratic when values repeat. A 600x600 frame is 1.44M bytes of mostly
	/// 255: one unordered compare took 15 s on a fast Mac and more than the 5 minute hang-dump timeout on the
	/// Windows CI runner (TransCurveDemoTests.DraggingACurvePointMovesIt, run 36455643812). A pixel buffer
	/// compare must say CollectionOrdering.Matching, which is linear and checks every byte in place.
	/// </summary>
	public class PixelBufferAssertionTests
	{
		[Test]
		public async Task EveryEquivalenceStatesItsOrdering()
		{
			var unstated = EquivalenceCalls()
				.Where(call => !call.Text.Contains("CollectionOrdering."))
				.Select(call => call.Location)
				.ToList();

			await Assert.That(unstated).IsEmpty()
				.Because("pass CollectionOrdering.Matching for a sequence or CollectionOrdering.Any for a set: " + string.Join(", ", unstated));
		}

		[Test]
		public async Task PixelBufferEquivalenceChecksOrder()
		{
			var unordered = EquivalenceCalls()
				.Where(call => call.Text.Contains("GetBuffer()") && !call.Text.Contains("CollectionOrdering.Matching"))
				.Select(call => call.Location)
				.ToList();

			await Assert.That(unordered).IsEmpty()
				.Because("compare pixel buffers with CollectionOrdering.Matching: " + string.Join(", ", unordered));
		}

		/// <summary>
		/// Every IsEquivalentTo( / IsNotEquivalentTo( call under Tests/Agg.Tests with its whole argument
		/// list, which may run over several lines, and the receiver on the same line (where a GetBuffer()
		/// usually sits). This file is skipped: its own string literals name the calls.
		/// </summary>
		private static IEnumerable<(string Location, string Text)> EquivalenceCalls([CallerFilePath] string thisFile = null)
		{
			string testsRoot = TestsRoot();
			foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
				if (relative.StartsWith("bin/") || relative.StartsWith("obj/")
					|| Path.GetFullPath(file) == Path.GetFullPath(thisFile))
				{
					continue;
				}

				string text = File.ReadAllText(file);
				foreach (Match match in Regex.Matches(text, @"Is(Not)?EquivalentTo\("))
				{
					int close = MatchingParen(text, match.Index + match.Length);
					int lineStart = text.LastIndexOf('\n', match.Index) + 1;
					int line = 1 + text.Take(match.Index).Count(c => c == '\n');
					yield return ($"{relative}:{line}", text.Substring(lineStart, close - lineStart));
				}
			}
		}

		/// <summary>
		/// The index of the ')' closing a call whose argument list starts at <paramref name="start"/>,
		/// skipping parentheses inside string and char literals.
		/// </summary>
		private static int MatchingParen(string text, int start)
		{
			int depth = 1;
			char quote = '\0';
			for (int i = start; i < text.Length; i++)
			{
				char c = text[i];
				if (quote != '\0')
				{
					if (c == '\\')
					{
						i++;
					}
					else if (c == quote)
					{
						quote = '\0';
					}
				}
				else if (c == '"' || c == '\'')
				{
					quote = c;
				}
				else if (c == '(')
				{
					depth++;
				}
				else if (c == ')' && --depth == 0)
				{
					return i;
				}
			}

			return text.Length;
		}

		private static string TestsRoot([CallerFilePath] string sourceFilePath = null)
		{
			// This file is at Tests/Agg.Tests/Other/PixelBufferAssertionTests.cs
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), ".."));
		}
	}
}
