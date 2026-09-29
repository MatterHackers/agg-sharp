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

using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's embedded Noto Emoji, chained behind the site's text faces as agg-gui chains it behind its font.
	public class EmojiFontTests
	{
		[Test]
		public async Task ChainOntoSetsTheFallbackAndKeepsAnExistingOne()
		{
			var face = new TypeFace();
			EmojiFont.ChainOnto(face);
			await Assert.That(face.Fallback).IsSameReferenceAs(EmojiFont.TypeFace);

			var other = new TypeFace();
			var chained = new TypeFace { Fallback = other };
			EmojiFont.ChainOnto(chained);
			await Assert.That(chained.Fallback).IsSameReferenceAs(other);
		}

		[Test]
		[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })] // the app opens About's MarkdownWidget
		public async Task TheSiteDrawsEmojisInItsDefaultFaces()
		{
			_ = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);

			await Assert.That(AggContext.DefaultFont.Fallback).IsSameReferenceAs(EmojiFont.TypeFace);
			await Assert.That(AggContext.DefaultFontBold.Fallback).IsSameReferenceAs(EmojiFont.TypeFace);
		}
	}
}
