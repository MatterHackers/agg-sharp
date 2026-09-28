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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's rich_text layout_tests and editor tests: wrapping, markers, caret geometry, and the widget's
	// keyboard and mouse editing, offscreen.
	public class RichTextEditTests
	{
		private static DocLayout Lay(RichDoc doc, double width) => RichTextLayout.Layout(doc, width, 12, RichTextLayout.DefaultResolver());

		private static (GuiWidget Root, RichTextEdit Editor) Build(RichDoc doc)
		{
			var root = new GuiWidget(300, 200);
			var editor = new RichTextEdit(doc) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			root.AddChild(editor);
			root.PerformLayout();
			editor.Focus();
			return (root, editor);
		}

		private static void SendKey(GuiWidget root, Keys keyDown, char keyPressed = '\0')
		{
			var down = new KeyEventArgs(keyDown);
			root.OnKeyDown(down);
			if (!down.SuppressKeyPress && keyPressed != '\0')
			{
				root.OnKeyPress(new KeyPressEventArgs(keyPressed));
			}
		}

		[Test]
		public async Task WrapsAtSpacesAndCoversEveryCharacter()
		{
			var doc = new RichDoc(new[] { Block.Plain("the quick brown fox jumps over the lazy dog") });
			var wide = Lay(doc, 10000);
			var narrow = Lay(doc, 80);
			await Assert.That(wide.Blocks[0].Lines.Count).IsEqualTo(1);
			await Assert.That(narrow.Blocks[0].Lines.Count).IsGreaterThan(2);
			await Assert.That(narrow.Blocks[0].Lines.All(l => l.Width <= 80 || l.Fragments.Count == 1)).IsTrue();
			await Assert.That(narrow.Height).IsGreaterThan(wide.Height);

			// No line starts with the space it broke at.
			await Assert.That(narrow.Blocks[0].Lines.All(l => !l.Fragments[0].Text.StartsWith(" "))).IsTrue();
		}

		[Test]
		public async Task BoldIsWiderAndBiggerSizeIsTaller()
		{
			var plain = Lay(new RichDoc(new[] { Block.Plain("Width") }), 1000);
			var bold = Lay(new RichDoc(new[] { new Block(new TextRun("Width", InlineStyle.Default with { Bold = true })) }), 1000);
			var big = Lay(new RichDoc(new[] { new Block(new TextRun("Width", InlineStyle.Default with { FontSize = 24 })) }), 1000);
			await Assert.That(bold.Blocks[0].Lines[0].Width).IsGreaterThan(plain.Blocks[0].Lines[0].Width);
			await Assert.That(big.Height).IsGreaterThan(plain.Height * 1.5);
		}

		[Test]
		public async Task ListsGetMarkersAndNumbersRestartAfterAParagraph()
		{
			var blocks = new[] { Block.Plain("a"), Block.Plain("b"), Block.Plain("gap"), Block.Plain("c"), Block.Plain("d") };
			blocks[0].List = blocks[1].List = blocks[3].List = ListKind.Ordered;
			blocks[4].List = ListKind.Bullet;
			var layout = Lay(new RichDoc(blocks), 400);
			await Assert.That(string.Join(",", layout.Blocks.Select(b => b.Marker ?? "-"))).IsEqualTo("1.,2.,-,1.,•");
			await Assert.That(layout.Blocks[0].TextLeft).IsEqualTo(RichTextLayout.ListGutterPx);
		}

		[Test]
		public async Task CaretAndHitTestRoundTrip()
		{
			var doc = new RichDoc(new[] { Block.Plain("hello world"), Block.Plain("second") });
			var layout = Lay(doc, 400);
			foreach (var pos in new[] { new DocPos(0, 0), new DocPos(0, 5), new DocPos(0, 11), new DocPos(1, 3) })
			{
				var caret = RichTextGeometry.CaretRect(layout, pos);
				await Assert.That(RichTextGeometry.HitTest(layout, caret.X, caret.Top + (caret.Height / 2))).IsEqualTo(pos);
			}

			var centered = new RichDoc(new[] { Block.Plain("hi") });
			centered.Blocks[0].Align = Justification.Center;
			await Assert.That(RichTextGeometry.CaretRect(Lay(centered, 400), default).X).IsGreaterThan(150);
		}

		[Test]
		public async Task TypingFormattingAndUndoThroughKeys()
		{
			var (root, editor) = Build(new RichDoc());
			foreach (char c in "ab")
			{
				SendKey(root, Keys.None, c);
			}

			SendKey(root, Keys.B | Keys.Control, 'b');
			SendKey(root, Keys.None, 'c');
			SendKey(root, Keys.Enter, '\r');
			SendKey(root, Keys.None, 'd');
			await Assert.That(editor.Core.PlainText).IsEqualTo("abc\nd");
			await Assert.That(editor.Core.Doc.Blocks[0].Runs.Select(r => r.Style.Bold).ToArray()).IsEquivalentTo(new[] { false, true });

			SendKey(root, Keys.Z | Keys.Control, 'z');
			await Assert.That(editor.Core.PlainText).IsEqualTo("abc\n");
			SendKey(root, Keys.Z | Keys.Control, 'z');
			await Assert.That(editor.Core.PlainText).IsEqualTo("abc");
		}

		[Test]
		public async Task ShiftArrowsSelectAndCopyAsPlainText()
		{
			var (root, editor) = Build(new RichDoc(new[] { Block.Plain("one"), Block.Plain("two") }));
			SendKey(root, Keys.Right);
			SendKey(root, Keys.Down | Keys.Shift);
			await Assert.That(editor.Core.Selection).IsEqualTo(new DocRange(new DocPos(0, 1), new DocPos(1, 1)));
			await Assert.That(editor.SelectedPlainText).IsEqualTo("ne\nt");
			SendKey(root, Keys.Back, '\b');
			await Assert.That(editor.Core.PlainText).IsEqualTo("owo");
		}

		[Test]
		public async Task ClickPlacesTheCaretAndDrawingPaintsText()
		{
			var (root, editor) = Build(new RichDoc(new[] { Block.Plain("hello world") }));
			var target = editor.CaretBounds(new DocPos(0, 6));
			var click = new MouseEventArgs(MouseButtons.Left, 1, target.Left, target.Center.Y, 0);
			editor.OnMouseDown(click);
			editor.OnMouseUp(click);
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(0, 6));

			var image = new ImageBuffer(300, 200);
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			root.OnDraw(graphics);
			int dark = 0;
			for (int x = 0; x < 300; x++)
			{
				for (int y = 150; y < 200; y++)
				{
					dark += image.GetPixel(x, y).Red0To255 < 128 ? 1 : 0;
				}
			}

			await Assert.That(dark).IsGreaterThan(20);
		}
	}
}
