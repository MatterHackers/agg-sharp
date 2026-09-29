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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI.RichText;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's rich_text editor scroll: a document taller than the editor scrolls, and the caret stays in view.
	public class RichTextScrollTests
	{
		private static (GuiWidget Root, RichTextEdit Editor) Build(int paragraphs)
		{
			var root = new GuiWidget(300, 120);
			var doc = new RichDoc(Enumerable.Range(1, paragraphs).Select(i => Block.Plain($"paragraph {i}")));
			var editor = new RichTextEdit(doc) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			root.AddChild(editor);
			root.PerformLayout();
			editor.Focus();
			return (root, editor);
		}

		private static bool CaretIsInView(RichTextEdit editor)
		{
			var caret = editor.CaretBounds(editor.Core.Caret);
			return caret.Bottom >= editor.Padding.Bottom - 0.001 && caret.Top <= editor.Height - editor.Padding.Top + 0.001;
		}

		[Test]
		public async Task AnEditorAwayFromTheOriginDrawsItsFirstLine()
		{
			// The padding-box clip was built in the editor's own coordinates but intersected with the screen-space
			// clip, so an editor 40px up its parent lost its top 40px - the whole first line.
			var root = new GuiWidget(300, 200);
			var editor = new RichTextEdit(new RichDoc(new[] { Block.Plain("MMMM first"), Block.Plain("second") }))
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(20, 40, 20, 20),
				BackgroundColor = Color.White,
				TextColor = Color.Black,
			};
			root.AddChild(editor);
			root.PerformLayout();

			var image = new ImageBuffer(300, 200);
			image.NewGraphics2D().Clear(Color.White);
			root.OnDraw(image.NewGraphics2D());

			// The first line's glyphs sit in the editor's top 30px.
			var bounds = editor.TransformToScreenSpace(editor.LocalBounds);
			int dark = 0;
			for (int y = (int)bounds.Top - 30; y < (int)bounds.Top; y++)
			{
				for (int x = (int)bounds.Left; x < (int)bounds.Left + 80; x++)
				{
					if (image.GetPixel(x, y).Red0To255 < 128)
					{
						dark++;
					}
				}
			}

			await Assert.That(dark).IsGreaterThan(20);
		}

		[Test]
		public async Task ShortDocumentsDoNotScroll()
		{
			var (root, editor) = Build(2);
			await Assert.That(editor.MaxScroll).IsEqualTo(0);
			editor.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
			await Assert.That(editor.ScrollOffset).IsEqualTo(0);
		}

		[Test]
		public async Task CaretMovesKeepItInViewAndTheWheelScrolls()
		{
			var (root, editor) = Build(40);
			await Assert.That(editor.MaxScroll).IsGreaterThan(0);

			root.OnKeyDown(new KeyEventArgs(Keys.End | Keys.Control));
			await Assert.That(editor.Core.Caret.Block).IsEqualTo(39);
			await Assert.That(editor.ScrollOffset).IsEqualTo(editor.MaxScroll).Within(0.001);
			await Assert.That(CaretIsInView(editor)).IsTrue();

			// A click lands on the scrolled document, not the unscrolled one.
			var target = editor.CaretBounds(new DocPos(38, 3));
			var click = new MouseEventArgs(MouseButtons.Left, 1, target.Left, target.Center.Y, 0);
			editor.OnMouseDown(click);
			editor.OnMouseUp(click);
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(38, 3));

			// The wheel scrolls up without moving the caret, and is consumed only while it moves the view.
			double before = editor.ScrollOffset;
			var wheel = new MouseEventArgs(MouseButtons.None, 0, 50, 50, 120);
			editor.OnMouseWheel(wheel);
			await Assert.That(editor.ScrollOffset).IsLessThan(before);
			await Assert.That(wheel.WheelDelta).IsEqualTo(0);
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(38, 3));

			root.OnKeyDown(new KeyEventArgs(Keys.Home | Keys.Control));
			await Assert.That(editor.ScrollOffset).IsEqualTo(0);
			var atTop = new MouseEventArgs(MouseButtons.None, 0, 50, 50, 120);
			editor.OnMouseWheel(atTop);
			await Assert.That(atTop.WheelDelta).IsEqualTo(120);

			// Deleting most of the document pulls the scroll back inside the new end.
			root.OnKeyDown(new KeyEventArgs(Keys.End | Keys.Control));
			editor.Core.SetSelection(new DocPos(1, 0), editor.Core.Doc.EndPos);
			editor.Core.Backspace();
			await Assert.That(editor.ScrollOffset).IsEqualTo(0);

			editor.OnDraw(new ImageBuffer(300, 120).NewGraphics2D());
		}

		[Test]
		public async Task TheScrollBarDragsAndJumps()
		{
			var (root, editor) = Build(40);
			double x = editor.Width - 3;
			await Assert.That(editor.IsOnScrollBar(x, 60)).IsTrue();
			await Assert.That(editor.IsOnScrollBar(editor.Width / 2, 60)).IsFalse();

			// Grab the thumb at the top and drag it down: the document scrolls, the caret stays put.
			var press = new MouseEventArgs(MouseButtons.Left, 1, x, editor.Height - 5, 0);
			editor.OnMouseDown(press);
			editor.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, x, editor.Height - 45, 0));
			double dragged = editor.ScrollOffset;
			await Assert.That(dragged).IsGreaterThan(0);
			await Assert.That(editor.Core.Caret).IsEqualTo(new DocPos(0, 0));

			// Far past the end clamps to the bottom.
			editor.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, x, -500, 0));
			await Assert.That(editor.ScrollOffset).IsEqualTo(editor.MaxScroll);
			editor.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, -500, 0));

			// A press on the track above the thumb jumps back up toward it.
			editor.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, editor.Height - 3, 0));
			editor.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, editor.Height - 3, 0));
			await Assert.That(editor.ScrollOffset).IsLessThan(editor.MaxScroll);
		}

		[Test]
		public async Task DraggingASelectionPastTheEdgeScrollsAndExtendsIt()
		{
			var (root, editor) = Build(40);
			var start = editor.CaretBounds(new DocPos(1, 0));
			editor.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, start.Left + 1, start.Center.Y, 0));

			// Below the text area: one step scrolls down and selects to the bottom visible line.
			editor.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 40, -30, 0));
			await Assert.That(editor.ScrollOffset).IsGreaterThan(0);
			await Assert.That(editor.Core.Anchor).IsEqualTo(new DocPos(1, 0));
			await Assert.That(editor.Core.Caret.Block).IsGreaterThan(1);
			await Assert.That(CaretIsInView(editor)).IsTrue();
			editor.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 40, -30, 0));
		}
	}
}
