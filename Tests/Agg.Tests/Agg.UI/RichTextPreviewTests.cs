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
	// agg-gui's preview_dirty_tests: a colour dialog's live preview on RichEditCore.
	public class RichTextPreviewTests
	{
		private static RichEditCore Core()
		{
			var core = new RichEditCore(new RichDoc(new[] { Block.Plain("hello world") }));
			core.SetSelection(new DocPos(0, 0), new DocPos(0, 5));
			return core;
		}

		private static Color? ColorAt(RichEditCore core) => RichTextCommands.StyleAt(core.Doc, new DocPos(0, 2)).TextColor;

		[Test]
		public async Task CommitKeepsTheLastPreviewAsOneUndoStep()
		{
			var core = Core();
			core.BeginPreview();
			for (int i = 0; i < 5; i++)
			{
				core.Exec(RichCommand.SetTextColor(new Color(i * 40, 0, 0)));

				// Feeding undo mid-preview (every drawn frame does) must not record the drag frames.
				core.FeedUndo(i * 10.0);
			}

			await Assert.That(core.IsPreviewing).IsTrue();
			core.CommitPreview();
			await Assert.That(core.IsPreviewing).IsFalse();
			await Assert.That(ColorAt(core)).IsEqualTo(new Color(160, 0, 0));

			core.Undo();
			await Assert.That(ColorAt(core)).IsNull();
			await Assert.That(core.CanUndo).IsFalse();
		}

		[Test]
		public async Task CancelRestoresTheStartWithNoUndoResidue()
		{
			var core = Core();
			core.BeginPreview();
			await Assert.That(core.IsPreviewDirty).IsFalse();
			core.Exec(RichCommand.SetTextColor(Color.Red));
			await Assert.That(core.IsPreviewDirty).IsTrue();

			// Undo and redo wait for the preview to end.
			await Assert.That(core.Undo()).IsFalse();
			await Assert.That(ColorAt(core)).IsEqualTo(Color.Red);
			core.BeginPreview();
			core.Exec(RichCommand.SetTextColor(Color.Blue));
			core.CancelPreview();
			await Assert.That(ColorAt(core)).IsNull();
			await Assert.That(core.Selection).IsEqualTo(new DocRange(new DocPos(0, 0), new DocPos(0, 5)));
			await Assert.That(core.IsPreviewing).IsFalse();
			await Assert.That(core.Undo()).IsFalse();
		}
	}
}
