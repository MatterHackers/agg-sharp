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
using System.Threading;
using MatterHackers.Agg.Font;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's monospaced code font: Liberation Mono, embedded in this assembly, standing in for the
	/// Cascadia Code agg-gui's Code Editor loads - a code editor wants fixed-width columns.
	/// </summary>
	public static class CodeFont
	{
		private const string ResourceName = "MatterHackers.AggSharpDemo.Fonts.LiberationMono-Regular.ttf";

		private static readonly Lazy<TypeFace> Instance = new Lazy<TypeFace>(Load, LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>Gets the loaded face, read from the assembly on first use.</summary>
		public static TypeFace TypeFace => Instance.Value;

		private static TypeFace Load()
		{
			using var stream = typeof(CodeFont).Assembly.GetManifestResourceStream(ResourceName)
				?? throw new InvalidOperationException(
					$"The font resource '{ResourceName}' is missing; AggSharpDemo.csproj embeds it from liberation-fonts-ttf-1.07.0.");
			var typeFace = new TypeFace();
			typeFace.LoadTTF(stream);
			return typeFace;
		}
	}
}
