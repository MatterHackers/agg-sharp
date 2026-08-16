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
using System.IO;
using System.Reflection;
using MatterHackers.RenderCore;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// Serves the canned WGSL embedded in this assembly, one module per source key.
	/// <para>
	/// The keys are the compat layer's <c>GlShaderKeys</c> constants, spelled here as literals on
	/// purpose: this project references RenderCore and the wgpu binding and nothing else, so it cannot
	/// see RenderGl. A test asserts the two lists agree, which is the right place for that coupling -
	/// the alternative would be a project reference from the backend up into the layer that sits on top
	/// of it.
	/// </para>
	/// <para>
	/// Each module declares one <c>vertexMain</c> and two fragment entry points, <c>fragmentMain</c> and
	/// <c>fragmentMainFlat</c>; four modules times three entry points is the twelve canned combos the
	/// port plan counts.
	/// </para>
	/// </summary>
	public class WgslShaderSources : IShaderSourceProvider
	{
		/// <summary>Unlit, per-vertex color. Matches <c>GlShaderKeys.PositionColor</c>.</summary>
		public const string PositionColor = "PositionColor";

		/// <summary>Lit, per-vertex color. Matches <c>GlShaderKeys.PositionColorLit</c>.</summary>
		public const string PositionColorLit = "PositionColorLit";

		/// <summary>Unlit, textured. Matches <c>GlShaderKeys.PositionTexture</c>.</summary>
		public const string PositionTexture = "PositionTexture";

		/// <summary>Lit, textured. Matches <c>GlShaderKeys.PositionTextureLit</c>.</summary>
		public const string PositionTextureLit = "PositionTextureLit";

		private static readonly string[] Keys =
		{
			PositionColor,
			PositionColorLit,
			PositionTexture,
			PositionTextureLit,
		};

		private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Every key this provider serves.</summary>
		public static IReadOnlyList<string> AllModuleKeys => Keys;

		/// <inheritdoc/>
		public string TryGetSource(string sourceKey)
		{
			if (string.IsNullOrEmpty(sourceKey))
			{
				return null;
			}

			if (this.cache.TryGetValue(sourceKey, out string cached))
			{
				return cached;
			}

			if (Array.IndexOf(Keys, sourceKey) < 0)
			{
				return null;
			}

			string source = ReadEmbedded(sourceKey);
			this.cache[sourceKey] = source;
			return source;
		}

		private static string ReadEmbedded(string sourceKey)
		{
			var assembly = typeof(WgslShaderSources).Assembly;
			string resourceName = typeof(WgslShaderSources).Namespace + ".Shaders." + sourceKey + ".wgsl";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					// A key in the list with no resource behind it is a build configuration error, not a
					// caller error, so it says so rather than returning null and looking like an unknown key.
					throw new InvalidOperationException(
						$"Embedded shader '{resourceName}' is missing from {assembly.GetName().Name}. "
						+ "Check the EmbeddedResource glob in WebGpuRender.csproj.");
				}

				using (var reader = new StreamReader(stream))
				{
					return reader.ReadToEnd();
				}
			}
		}
	}
}
