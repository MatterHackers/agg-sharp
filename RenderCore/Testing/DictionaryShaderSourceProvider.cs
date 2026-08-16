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

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// An <see cref="IShaderSourceProvider"/> backed by an in-memory dictionary. The real backends
	/// resolve keys out of embedded WGSL resources; this is the same contract with the file system
	/// left out, so a test can pin what a key resolves to.
	/// </summary>
	public class DictionaryShaderSourceProvider : IShaderSourceProvider
	{
		private readonly Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Adds or replaces the source for a key.</summary>
		/// <param name="sourceKey">The key callers will name.</param>
		/// <param name="source">The shader text.</param>
		public DictionaryShaderSourceProvider Add(string sourceKey, string source)
		{
			this.sources[sourceKey] = source;
			return this;
		}

		/// <inheritdoc/>
		public string TryGetSource(string sourceKey)
			=> this.sources.TryGetValue(sourceKey, out string source) ? source : null;
	}
}
