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

using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Caret motion and delete bindings differ between Mac and Windows, and the difference is not
	/// cosmetic: on Mac the platform layer folds Command onto <see cref="Keys.Control"/>, so the very same
	/// key event that means "word jump" on Windows means "go to start of line" on Mac. These tests drive
	/// both branches on any host through <see cref="InternalTextEditWidget.UseMacKeyBindings"/>.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))] // Keyboard is global static state
	public class MacTextEditKeyBindingTests
	{
		private const string ThreeWords = "hello big world";

		private const string ThreeLines = "line1\nline2\nline3";

		/// <summary>
		/// Runs <paramref name="action"/> with the Mac/Windows binding seam pinned, restoring both it and
		/// the global keyboard state afterwards no matter how the test ends.
		/// </summary>
		private static async Task WithKeyBindings(bool useMacKeyBindings, Func<Task> action)
		{
			bool wasMac = InternalTextEditWidget.UseMacKeyBindings;
			Keyboard.Clear();
			try
			{
				InternalTextEditWidget.UseMacKeyBindings = useMacKeyBindings;
				await action();
			}
			finally
			{
				InternalTextEditWidget.UseMacKeyBindings = wasMac;
				Keyboard.Clear();
			}
		}

		private static InternalTextEditWidget EditorAt(string text, int cursor, bool multiLine = false)
		{
			var editWidget = new InternalTextEditWidget(text, 12, multiLine, 0)
			{
				CharIndexToInsertBefore = cursor
			};

			return editWidget;
		}

		private static void SendKeyDown(Keys keyData, InternalTextEditWidget editWidget)
		{
			editWidget.OnKeyDown(new KeyEventArgs(keyData));
		}

		/// <summary>
		/// Builds an editor whose caret sits at <paramref name="cursor"/> with a live selection running
		/// back to <paramref name="anchor"/>, which is the state an unshifted motion has to collapse.
		/// </summary>
		private static InternalTextEditWidget EditorSelecting(string text, int anchor, int cursor, bool multiLine = false)
		{
			var editWidget = EditorAt(text, cursor, multiLine);
			editWidget.SelectionIndexToStartBefore = anchor;
			editWidget.Selecting = true;

			return editWidget;
		}

		[Test]
		public async Task MacAltArrowsMoveByWord()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				// option-left from the end of "hello big world" lands on the "w"
				SendKeyDown(Keys.Alt | Keys.Left, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("hello big ".Length);

				SendKeyDown(Keys.Alt | Keys.Left, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("hello ".Length);

				SendKeyDown(Keys.Alt | Keys.Right, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("hello big ".Length);
			});
		}

		[Test]
		public async Task MacCommandLeftGoesToLineStartNotWordBoundary()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				// Command arrives as Keys.Control. Before the Mac bindings existed this did a word jump and
				// stopped at "world", which is the bug this test exists for.
				SendKeyDown(Keys.Control | Keys.Left, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(0);
			});
		}

		[Test]
		public async Task MacCommandRightGoesToLineEnd()
		{
			await WithKeyBindings(true, async () =>
			{
				// the middle line holds two words so that a word jump (index 10) and the end of the line
				// (index 14) cannot be confused for one another
				const string text = "line1\nbig word\nline3";
				var editWidget = EditorAt(text, "line1\n".Length, multiLine: true);

				SendKeyDown(Keys.Control | Keys.Right, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("line1\nbig word".Length);
			});
		}

		[Test]
		public async Task MacCommandUpAndDownGoToDocumentStartAndEnd()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeLines, "line1\nli".Length, multiLine: true);

				SendKeyDown(Keys.Control | Keys.Up, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(0);

				SendKeyDown(Keys.Control | Keys.Down, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(ThreeLines.Length);
			});
		}

		[Test]
		public async Task MacAltBackspaceDeletesPreviousWord()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				SendKeyDown(Keys.Alt | Keys.Back, editWidget);
				await Assert.That(editWidget.Text).IsEqualTo("hello big ");
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("hello big ".Length);
			});
		}

		[Test]
		public async Task MacCommandBackspaceDeletesToLineStart()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeLines, "line1\nline2".Length, multiLine: true);

				SendKeyDown(Keys.Control | Keys.Back, editWidget);
				await Assert.That(editWidget.Text).IsEqualTo("line1\n\nline3");
			});
		}

		[Test]
		public async Task MacShiftComposesWithWordAndLineMotion()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				SendKeyDown(Keys.Shift | Keys.Alt | Keys.Left, editWidget);
				await Assert.That(editWidget.Selection).IsEqualTo("world");

				editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				SendKeyDown(Keys.Shift | Keys.Control | Keys.Left, editWidget);
				await Assert.That(editWidget.Selection).IsEqualTo(ThreeWords);
			});
		}

		[Test]
		public async Task MacUnshiftedCommandArrowsCollapseSelection()
		{
			await WithKeyBindings(true, async () =>
			{
				// An unshifted Command-arrow is a plain caret motion on Mac, so it ends the selection just
				// as an unmodified arrow does. It used to leave the selection live, because the Control
				// modifier short circuits the "turn the selection off" arm at the top of OnKeyDown.
				var editWidget = EditorSelecting(ThreeWords, anchor: 6, cursor: ThreeWords.Length);
				SendKeyDown(Keys.Control | Keys.Left, editWidget);
				await Assert.That(editWidget.Selecting).IsFalse();
				await Assert.That(editWidget.Selection).IsEqualTo("");
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(0);

				editWidget = EditorSelecting(ThreeLines, anchor: "line1\n".Length, cursor: "line1\nli".Length, multiLine: true);
				SendKeyDown(Keys.Control | Keys.Right, editWidget);
				await Assert.That(editWidget.Selecting).IsFalse();
				await Assert.That(editWidget.Selection).IsEqualTo("");
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("line1\nline2".Length);

				editWidget = EditorSelecting(ThreeLines, anchor: "line1\n".Length, cursor: "line1\nli".Length, multiLine: true);
				SendKeyDown(Keys.Control | Keys.Up, editWidget);
				await Assert.That(editWidget.Selecting).IsFalse();
				await Assert.That(editWidget.Selection).IsEqualTo("");
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(0);

				editWidget = EditorSelecting(ThreeLines, anchor: "line1\n".Length, cursor: "line1\nli".Length, multiLine: true);
				SendKeyDown(Keys.Control | Keys.Down, editWidget);
				await Assert.That(editWidget.Selecting).IsFalse();
				await Assert.That(editWidget.Selection).IsEqualTo("");
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(ThreeLines.Length);
			});
		}

		[Test]
		public async Task UseMacKeyBindingsDefaultsToRunningOs()
		{
			// Every other test pins the seam, so nothing else covers the initializer. If it were ever
			// broken the whole Mac binding set would silently switch off on Mac and the suite would stay
			// green - which is exactly the failure a default-value test is for.
			await Assert.That(InternalTextEditWidget.UseMacKeyBindings).IsEqualTo(System.OperatingSystem.IsMacOS());
		}

		[Test]
		public async Task MacHomeAndEndStillGoToLineStartAndEnd()
		{
			await WithKeyBindings(true, async () =>
			{
				var editWidget = EditorAt(ThreeLines, "line1\nli".Length, multiLine: true);

				SendKeyDown(Keys.Home, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("line1\n".Length);

				SendKeyDown(Keys.End, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("line1\nline2".Length);
			});
		}

		[Test]
		public async Task WindowsControlLeftStillJumpsByWord()
		{
			await WithKeyBindings(false, async () =>
			{
				var editWidget = EditorAt(ThreeWords, ThreeWords.Length);

				SendKeyDown(Keys.Control | Keys.Left, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo("hello big ".Length);
			});
		}

		[Test]
		public async Task WindowsControlHomeStillGoesToDocumentStart()
		{
			await WithKeyBindings(false, async () =>
			{
				var editWidget = EditorAt(ThreeLines, "line1\nli".Length, multiLine: true);

				SendKeyDown(Keys.Control | Keys.Home, editWidget);
				await Assert.That(editWidget.CharIndexToInsertBefore).IsEqualTo(0);
			});
		}
	}
}
