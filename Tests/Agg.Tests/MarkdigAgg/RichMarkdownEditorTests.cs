/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Threading.Tasks;
using Markdig.Agg.Editing;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// The toolbar driving the real editing widget through the composite editor.
	/// </summary>
	public class RichMarkdownEditorTests
	{
		// Windows bindings, set per editor so no test touches the shared static.
		private static RichMarkdownEditor Make(string markdown)
		{
			var container = new GuiWidget(500, 300, SizeLimitsToSet.None);
			var editor = new RichMarkdownEditor(new ThemeConfig())
			{
				Markdown = markdown,
			};
			editor.Editor.UseMacKeyBindings = false;
			container.AddChild(editor);
			container.PerformLayout();
			return editor;
		}

		private static void Key(RichMarkdownEditor editor, Keys keys) => editor.Editor.OnKeyDown(new KeyEventArgs(keys));

		[Test]
		public async Task BoldFromTheToolbarIsOneUndoStep()
		{
			var editor = Make("Hello world\n");
			editor.Editor.Selection = new RichSelection(new DocPosition(0, 0), new DocPosition(0, 5));

			editor.Toolbar.BoldButton.InvokeClick();
			await Assert.That(editor.Markdown).IsEqualTo("**Hello** world\n");
			await Assert.That(editor.Toolbar.BoldButton.IsPressed).IsTrue();

			Key(editor, Keys.Z | Keys.Control);
			await Assert.That(editor.Markdown).IsEqualTo("Hello world\n");
			await Assert.That(editor.Toolbar.BoldButton.IsPressed).IsFalse();
		}

		[Test]
		public async Task AClickThatChangesNothingLeavesNoUndoStep()
		{
			var editor = Make("Hello\n");
			editor.Editor.Selection = RichSelection.At(new DocPosition(0, 2));

			editor.Toolbar.AlignLeftButton.InvokeClick();

			await Assert.That(editor.Editor.History.CanUndo).IsFalse();
		}

		[Test]
		public async Task ControlKOpensTheLinkRow()
		{
			var editor = Make("Hello world\n");
			editor.Editor.Selection = new RichSelection(new DocPosition(0, 0), new DocPosition(0, 5));

			Key(editor, Keys.K | Keys.Control);
			await Assert.That(editor.Toolbar.LinkEditor.Visible).IsTrue();

			editor.Toolbar.LinkEditor.Url = "example.com";
			editor.Toolbar.LinkEditor.Accept();
			await Assert.That(editor.Markdown).IsEqualTo("[Hello](https://example.com) world\n");
		}

		[Test]
		public async Task TypingAfterBoldAtACaretTypesBold()
		{
			var editor = Make("Hello world\n");
			editor.Editor.Selection = RichSelection.At(new DocPosition(0, 5));

			editor.Toolbar.BoldButton.InvokeClick();
			await Assert.That(editor.Editor.Focused).IsTrue();
			await Assert.That(editor.Toolbar.BoldButton.IsPressed).IsTrue();

			editor.Editor.OnKeyPress(new KeyPressEventArgs('X'));
			await Assert.That(editor.Markdown).IsEqualTo("Hello**X** world\n");
		}
	}
}
