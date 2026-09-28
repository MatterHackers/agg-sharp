/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The CodeEditor as agg-gui's plain TextArea: rows aligned one by one, the block aligned in the view,
	/// hint text while empty, and a read-only mode.</summary>
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class CodeEditorPlainTextTests
	{
		private static (GuiWidget root, CodeEditor editor) Build(string text)
		{
			var root = new GuiWidget(300, 200);
			var editor = new CodeEditor(text)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				ShowLineNumbers = false,
				WordWrap = true,
			};
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

		private static MouseEventArgs Mouse(Vector2 position) => new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, 0);

		[Test]
		public async Task EachRowAlignsOnItsOwnAndTheBlockAlignsInTheView()
		{
			(_, CodeEditor editor) = Build("ab\nlonger line");
			double padding = CodeEditor.TextPadding * GuiWidget.DeviceScale;
			await Assert.That(editor.PositionOfIndex(0).X).IsEqualTo(padding).Because("left and top by default");
			await Assert.That(editor.RowTop(0)).IsEqualTo(editor.Height - padding);

			editor.TextHAnchor = HAnchor.Right;
			await Assert.That(editor.PositionOfIndex(2).X).IsEqualTo(editor.Width - padding).Within(.001);
			await Assert.That(editor.PositionOfIndex(editor.Text.Length).X).IsEqualTo(editor.Width - padding).Within(.001);
			await Assert.That(editor.PositionOfIndex(0).X).IsGreaterThan(editor.PositionOfIndex(3).X).Because("the short row sits further right");

			editor.TextHAnchor = HAnchor.Center;
			double rowStart = editor.PositionOfIndex(0).X;
			double rowEnd = editor.PositionOfIndex(2).X;
			await Assert.That(rowStart - padding).IsEqualTo(editor.Width - padding - rowEnd).Within(.001);

			editor.TextVAnchor = VAnchor.Bottom;
			await Assert.That(editor.RowTop(1) - editor.LineHeight).IsEqualTo(padding).Within(.001);
			editor.TextVAnchor = VAnchor.Center;
			double above = editor.Height - padding - editor.RowTop(0);
			double below = editor.RowTop(1) - editor.LineHeight - padding;
			await Assert.That(above).IsEqualTo(below).Within(.001);

			// A click lands on the aligned text under it.
			Vector2 At(int index) => editor.PositionOfIndex(index) + new Vector2(1, editor.LineHeight / 2);
			editor.OnMouseDown(Mouse(At(1)));
			editor.OnMouseUp(Mouse(At(1)));
			await Assert.That(editor.Document.Caret).IsEqualTo(1);
			editor.OnMouseDown(Mouse(At(5)));
			editor.OnMouseUp(Mouse(At(5)));
			await Assert.That(editor.Document.Caret).IsEqualTo(5);
		}

		[Test]
		public async Task UpAndDownKeepTheOnScreenXOnAlignedRows()
		{
			foreach (HAnchor anchor in new[] { HAnchor.Center, HAnchor.Right })
			{
				// The caret at the end of the short row sits over the long row's fourth gap when centred and over its
				// end when right aligned; an x measured from each row's own start would land on its third gap both times.
				(GuiWidget root, CodeEditor editor) = Build("xxx\nxxxxx");
				editor.TextHAnchor = anchor;
				editor.Document.SetCaret(3);
				double x = editor.PositionOfIndex(3).X;
				SendKey(root, Keys.Down);
				int expected = anchor == HAnchor.Center ? 8 : 9;
				await Assert.That(editor.Document.Caret).IsEqualTo(expected).Because(anchor.ToString());
				await Assert.That(Math.Abs(editor.PositionOfIndex(editor.Document.Caret).X - x)).IsLessThan(editor.LineHeight).Because(anchor.ToString());

				SendKey(root, Keys.Up);
				await Assert.That(editor.Document.Caret).IsEqualTo(3).Because("back where it started: " + anchor);
			}
		}

		[Test]
		public async Task HintTextShowsOnlyWhileTheTextIsEmpty()
		{
			(_, CodeEditor editor) = Build("");
			editor.HintText = "Type something!";
			editor.HintColor = Color.Red;
			await Assert.That(RedPixels(editor)).IsGreaterThan(0);

			editor.Text = "x";
			await Assert.That(RedPixels(editor)).IsEqualTo(0);
		}

		[Test]
		public async Task ReadOnlyMovesAndCopiesButNeverEdits()
		{
			(GuiWidget root, CodeEditor editor) = Build("abc");
			editor.ReadOnly = true;
			editor.SystemClipboard = new SimulatedClipboard();
			SendKey(root, Keys.End);
			SendKey(root, Keys.Back);
			SendKey(root, Keys.Enter, '\r');
			SendKey(root, Keys.D, 'd');
			SendKey(root, Keys.Left | Keys.Shift);
			SendKey(root, Keys.C | Keys.Control, 'c');
			SendKey(root, Keys.X | Keys.Control, 'x');
			SendKey(root, Keys.V | Keys.Control, 'v');
			SendKey(root, Keys.Z | Keys.Control, 'z');
			SendKey(root, Keys.Delete);
			await Assert.That(editor.Text).IsEqualTo("abc");
			await Assert.That(editor.SystemClipboard.GetText()).IsEqualTo("c");
			await Assert.That(editor.Document.SelectedText).IsEqualTo("c");
		}

		private static int RedPixels(CodeEditor editor)
		{
			var image = new ImageBuffer((int)editor.Width, (int)editor.Height);
			editor.OnDraw(image.NewGraphics2D());
			int count = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (pixel.red > 200 && pixel.green < 80 && pixel.blue < 80)
					{
						count++;
					}
				}
			}

			return count;
		}
	}
}
