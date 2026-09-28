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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Other
{
	/// <summary>
	/// TUnit's IsEquivalentTo / IsNotEquivalentTo on a collection ignore order unless given
	/// CollectionOrdering.Matching, and the unordered match is quadratic when values repeat. A 600x600 frame
	/// is 1.44M bytes of mostly 255: one unordered compare took 15 s on a fast Mac and more than the 5 minute
	/// hang-dump timeout on the Windows CI runner (TransCurveDemoTests.DraggingACurvePointMovesIt, run
	/// 36455643812). It also passes for two frames whose pixels are merely shuffled. A pixel buffer compare
	/// must say CollectionOrdering.Matching, which is linear and checks every byte in place.
	/// </summary>
	public class PixelBufferAssertionTests
	{
		[Test]
		public async Task PixelBufferEquivalenceChecksOrder()
		{
			string testsRoot = TestsRoot();
			var unordered = new List<string>();
			foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
				if (relative.StartsWith("bin/") || relative.StartsWith("obj/"))
				{
					continue;
				}

				string[] lines = File.ReadAllLines(file);
				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i];
					if (line.Contains("EquivalentTo(") && line.Contains("GetBuffer()") && !line.Contains("CollectionOrdering"))
					{
						unordered.Add($"{relative}:{i + 1}");
					}
				}
			}

			await Assert.That(unordered).IsEmpty()
				.Because("compare pixel buffers with CollectionOrdering.Matching: " + string.Join(", ", unordered));
		}

		private static string TestsRoot([CallerFilePath] string sourceFilePath = null)
		{
			// This file is at Tests/Agg.Tests/Other/PixelBufferAssertionTests.cs
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), ".."));
		}
	}
}
