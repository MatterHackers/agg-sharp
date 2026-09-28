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

using System.Threading.Tasks;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// Rich-text layout performance: per-paragraph reuse across edits and linear-time prefix measuring.
	public class RichTextLayoutCacheTests
	{
		[Test]
		public async Task UnchangedParagraphsReuseTheirLayout()
		{
			var resolver = RichTextLayout.DefaultResolver();
			var cache = new RichTextLayoutCache();
			var doc = new RichDoc(new[] { Block.Plain("first paragraph"), Block.Plain("second"), Block.Plain("third") });
			var before = RichTextLayout.Layout(doc, 200, 12, resolver, cache);
			await Assert.That(cache.LastReuseCount).IsEqualTo(0);

			// Edit the middle paragraph in place and insert one at the top: only those two are measured.
			RichTextEdits.InsertText(doc, new DocPos(1, 6), "!", InlineStyle.Default);
			doc.Blocks.Insert(0, Block.Plain("new"));
			var after = RichTextLayout.Layout(doc, 200, 12, resolver, cache);
			await Assert.That(cache.LastReuseCount).IsEqualTo(2);
			await Assert.That(ReferenceEquals(after.Blocks[1], before.Blocks[0])).IsTrue();
			await Assert.That(ReferenceEquals(after.Blocks[3], before.Blocks[2])).IsTrue();
			await Assert.That(after.Blocks[2].Lines[0].Fragments[0].Text).IsEqualTo("second!");

			// A list number change is a layout change, and so is a new width.
			doc.Blocks[3].List = ListKind.Ordered;
			doc.Blocks[2].List = ListKind.Ordered;
			RichTextLayout.Layout(doc, 200, 12, resolver, cache);
			await Assert.That(cache.LastReuseCount).IsEqualTo(2);
			RichTextLayout.Layout(doc, 300, 12, resolver, cache);
			await Assert.That(cache.LastReuseCount).IsEqualTo(0);
		}

		[Test]
		public async Task CaretMovesKeepTheLayoutAndEditsRebuildIt()
		{
			var root = new GuiWidget(300, 200);
			var editor = new RichTextEdit(new RichDoc(new[] { Block.Plain("one two"), Block.Plain("three") })) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			root.AddChild(editor);
			root.PerformLayout();
			var layout = editor.Layout;

			editor.Core.SetCaret(new DocPos(1, 2));
			editor.Core.SetSelection(new DocPos(0, 0), new DocPos(1, 1));
			editor.Core.SetCaret(new DocPos(0, 3));
			editor.Core.Exec(RichCommand.ToggleBold); // collapsed: arms the next typed style only
			await Assert.That(ReferenceEquals(editor.Layout, layout)).IsTrue();

			editor.Core.Insert("!");
			await Assert.That(ReferenceEquals(editor.Layout, layout)).IsFalse();
			await Assert.That(editor.Layout.Blocks[0].Lines[0].Fragments.Count).IsGreaterThan(1);
			layout = editor.Layout;
			editor.Undo();
			await Assert.That(ReferenceEquals(editor.Layout, layout)).IsFalse();
		}

		[Test]
		public async Task PrefixWidthsAccumulateToTheMeasuredWidth()
		{
			var layout = RichTextLayout.Layout(new RichDoc(new[] { Block.Plain("Kerning AV test") }), 1000, 12, RichTextLayout.DefaultResolver());
			var fragment = layout.Blocks[0].Lines[0].Fragments[0];
			await Assert.That(fragment.PrefixWidth(0)).IsEqualTo(0);
			await Assert.That(fragment.PrefixWidth(fragment.Text.Length)).IsEqualTo(fragment.Width).Within(1e-9);
			await Assert.That(fragment.PrefixWidth(4)).IsGreaterThan(fragment.PrefixWidth(3));
			await Assert.That(fragment.PrefixWidth(999)).IsEqualTo(fragment.Width).Within(1e-9);
		}
	}
}
