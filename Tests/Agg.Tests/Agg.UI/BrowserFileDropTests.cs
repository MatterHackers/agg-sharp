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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser's file drag-and-drop with the browser taken out: the events <c>input.js</c> sends are fed
	/// to <see cref="BrowserFileDrop"/> directly, and what reaches a widget - and what lands in the staging
	/// directory - is checked. The frame tick is a list the test runs on demand, so the one-hover-behind
	/// answer is visible.
	/// </summary>
	public class BrowserFileDropTests
	{
		[Test]
		public async Task AHoverWithHiddenNamesCarriesStandInsNamedFromTheMimeType()
		{
			var paths = BrowserFileDrop.HoverPaths(new[] { string.Empty, string.Empty }, new[] { "video/mp4", "image/jpeg" }, out bool allKnown);

			await Assert.That(allKnown).IsTrue();
			await Assert.That(paths.Select(Path.GetFileName).ToList())
				.IsEquivalentTo(new[] { "dragged-1.mp4", "dragged-2.jpg" }, CollectionOrdering.Matching);
			await Assert.That(Path.GetDirectoryName(paths[0])).IsEqualTo(BrowserFileDrop.HoverDirectory);
		}

		[Test]
		public async Task AHoverWithAnUnknownTypeIsAGuess()
		{
			var paths = BrowserFileDrop.HoverPaths(new[] { string.Empty }, new[] { string.Empty }, out bool allKnown);

			await Assert.That(allKnown).IsFalse();
			await Assert.That(Path.GetFileName(paths[0])).IsEqualTo("dragged-1");
		}

		[Test]
		public async Task AHoverWithRealNamesUsesThem()
		{
			var paths = BrowserFileDrop.HoverPaths(new[] { "../part.mcx" }, new[] { string.Empty }, out bool allKnown);

			await Assert.That(allKnown).IsTrue();
			await Assert.That(Path.GetFileName(paths[0])).IsEqualTo("part.mcx");
		}

		[Test]
		public async Task AnAcceptedHoverShowsACopyFromTheNextDragover()
		{
			var (drop, tick, target) = MakeDrop();

			// The first dragover's answer is not known yet - agg has not been ticked.
			bool first = drop.Handle(DragOver(60, 50, "video/mp4"));
			await Assert.That(first).IsFalse();

			RunTick(tick);
			await Assert.That(target.LastHoverFiles).IsNotNull();
			await Assert.That(Path.GetFileName(target.LastHoverFiles[0])).IsEqualTo("dragged-1.mp4");

			await Assert.That(drop.Handle(DragOver(60, 50, "video/mp4"))).IsTrue();
		}

		[Test]
		public async Task ARefusedKnownTypeShowsNoDrop()
		{
			var (drop, tick, _) = MakeDrop();

			drop.Handle(DragOver(60, 50, "image/png"));
			RunTick(tick);

			await Assert.That(drop.Handle(DragOver(60, 50, "image/png"))).IsFalse();
		}

		/// <summary>
		/// An .stl or .mcx has no MIME type a browser knows, so its hover cannot say what it is. Showing "no
		/// drop" there would stop the browser ever firing the drop on a widget that wanted it.
		/// </summary>
		[Test]
		public async Task ARefusedGuessStillShowsACopy()
		{
			var (drop, tick, _) = MakeDrop();

			drop.Handle(DragOver(60, 50, string.Empty));
			RunTick(tick);

			await Assert.That(drop.Handle(DragOver(60, 50, string.Empty))).IsTrue();
		}

		[Test]
		public async Task HoverPositionsAreConvertedLikePointerEvents()
		{
			var (drop, tick, target) = MakeDrop();

			// CSS (30, 50) at dpr 2 on a 300 device-pixel-high canvas is agg (60, 200).
			drop.Handle(DragOver(30, 50, "video/mp4"));
			RunTick(tick);

			await Assert.That(target.LastHoverPosition.X).IsEqualTo(60 - 50);
			await Assert.That(target.LastHoverPosition.Y).IsEqualTo(200 - 40);
		}

		[Test]
		public async Task ADragThatLeavesClearsTheHover()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(DragOver(30, 50, "video/mp4"));
			RunTick(tick);
			await Assert.That(target.HoveredWithFiles).IsTrue();

			drop.Handle(new BrowserFileDragEvent { Type = "dragleave" });
			RunTick(tick);

			await Assert.That(target.HoveredWithFiles).IsFalse();
			await Assert.That(drop.Handle(DragOver(500, 500, "video/mp4"))).IsFalse();
		}

		[Test]
		public async Task ADropStagesEveryChunkAndDeliversTheStagedPaths()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "clip.mp4" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1, 2, 3 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 4, 5 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "clip.mp4" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });

			// Nothing reaches agg until the tick.
			await Assert.That(target.DroppedFiles).IsNull();
			RunTick(tick);

			await Assert.That(target.DroppedFiles).IsNotNull();
			await Assert.That(target.DroppedFiles.Select(Path.GetFileName).ToList())
				.IsEquivalentTo(new[] { "clip.mp4", "clip (2).mp4" }, CollectionOrdering.Matching);
			await Assert.That(File.ReadAllBytes(target.DroppedFiles[0]))
				.IsEquivalentTo(new byte[] { 1, 2, 3, 4, 5 }, CollectionOrdering.Matching);

			// An empty file is still a file.
			await Assert.That(new FileInfo(target.DroppedFiles[1]).Length).IsEqualTo(0);
			await Assert.That(Path.GetDirectoryName(target.DroppedFiles[0]))
				.StartsWith(Path.Combine(BrowserFileStaging.StagingRoot, "drop-"));
			await Assert.That(target.DropPosition.X).IsEqualTo(60 - 50);
			await Assert.That(target.DropPosition.Y).IsEqualTo(200 - 40);
			await Assert.That(drop.IsStaging).IsFalse();

			Directory.Delete(Path.GetDirectoryName(target.DroppedFiles[0]), recursive: true);
		}

		[Test]
		public async Task AFileThatCouldNotBeReadIsNotDelivered()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "good.stl" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 7 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "bad.stl" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 8 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfileerror", Name = "NotReadableError" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(target.DroppedFiles.Select(Path.GetFileName).ToList())
				.IsEquivalentTo(new[] { "good.stl" }, CollectionOrdering.Matching);

			string directory = Path.GetDirectoryName(target.DroppedFiles[0]);
			await Assert.That(File.Exists(Path.Combine(directory, "bad.stl"))).IsFalse();

			Directory.Delete(directory, recursive: true);
		}

		[Test]
		public async Task ADropWithNothingReadableClearsTheHoverAndLeavesNoDirectory()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(DragOver(30, 50, "video/mp4"));
			RunTick(tick);

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			string directory = drop.StagingDirectory;
			await Assert.That(Directory.Exists(directory)).IsTrue();

			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "bad.mp4" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfileerror", Name = "NotReadableError" });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(target.DroppedFiles).IsNull();
			await Assert.That(target.HoveredWithFiles).IsFalse();
			await Assert.That(Directory.Exists(directory)).IsFalse();
		}

		/// <summary>
		/// A write that fails part way (the wasm heap ran out, say) must not hand the widget a truncated
		/// file as if it were the whole one.
		/// </summary>
		[Test]
		public async Task AChunkThatCannotBeWrittenIsNotDelivered()
		{
			var (drop, tick, target) = MakeDrop(path => Path.GetFileName(path) == "bad.mp4"
				? new ThrowingStream()
				: new FileStream(path, FileMode.CreateNew, FileAccess.Write));

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			string directory = drop.StagingDirectory;
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "bad.mp4", Size = 3 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1, 2, 3 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "good.mp4", Size = 1 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 9 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(target.DroppedFiles.Select(Path.GetFileName).ToList())
				.IsEquivalentTo(new[] { "good.mp4" }, CollectionOrdering.Matching);
			await Assert.That(File.Exists(Path.Combine(directory, "bad.mp4"))).IsFalse();

			Directory.Delete(directory, recursive: true);
		}

		[Test]
		public async Task AFileWithoutADropStartIsIgnored()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "stray.mp4", Size = 1 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(target.DroppedFiles).IsNull();
			await Assert.That(drop.IsStaging).IsFalse();
		}

		/// <summary>
		/// A file is sized up front from what the browser says it is (so the in-memory file system does not
		/// regrow it slice by slice), but ends at what was actually written.
		/// </summary>
		[Test]
		public async Task AStagedFileEndsAtTheBytesWritten()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "clip.mp4", Size = 5 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1, 2, 3 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(File.ReadAllBytes(target.DroppedFiles[0]))
				.IsEquivalentTo(new byte[] { 1, 2, 3 }, CollectionOrdering.Matching);

			BrowserFileStaging.Release(target.DroppedFiles[0]);
		}

		/// <summary>
		/// The hover answer is written on the tick, so a leave queued behind a hover has to clear it there
		/// too - clearing it when the leave arrived let the hover that ran after it light the answer again.
		/// </summary>
		[Test]
		public async Task ALeaveQueuedBehindAHoverLeavesNoDropShowing()
		{
			var (drop, tick, _) = MakeDrop();

			drop.Handle(DragOver(30, 50, "video/mp4"));
			drop.Handle(new BrowserFileDragEvent { Type = "dragleave" });
			RunTick(tick);

			await Assert.That(drop.Handle(DragOver(500, 500, "video/mp4"))).IsFalse();
		}

		[Test]
		public async Task ADropQueuedBehindAHoverLeavesNoDropShowing()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(DragOver(30, 50, "video/mp4"));
			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "clip.mp4", Size = 1 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			await Assert.That(drop.Handle(DragOver(500, 500, "video/mp4"))).IsFalse();

			BrowserFileStaging.Release(target.DroppedFiles[0]);
		}

		[Test]
		public async Task ReleasingADroppedFileRemovesItsDropDirectory()
		{
			var (drop, tick, target) = MakeDrop();

			drop.Handle(new BrowserFileDragEvent { Type = "dropstart", OffsetX = 30, OffsetY = 50 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropfile", Name = "clip.mp4", Size = 1 });
			drop.Handle(new BrowserFileDragEvent { Type = "dropchunk", Bytes = new byte[] { 1 } });
			drop.Handle(new BrowserFileDragEvent { Type = "dropend" });
			RunTick(tick);

			string directory = Path.GetDirectoryName(target.DroppedFiles[0]);
			BrowserFileStaging.Release(target.DroppedFiles[0]);

			await Assert.That(Directory.Exists(directory)).IsFalse();
		}

		/// <summary>Release is for staged drops only; anything else it is handed is left alone.</summary>
		[Test]
		public async Task ReleasingAFileOutsideADropDirectoryDeletesNothing()
		{
			string directory = BrowserFileStaging.CreateRequestDirectory("open");
			string path = Path.Combine(directory, "part.stl");
			File.WriteAllBytes(path, new byte[] { 1 });

			BrowserFileStaging.Release(path);
			BrowserFileStaging.Release(null);
			BrowserFileStaging.Release(Path.Combine(Path.GetTempPath(), "drop-looks-like-one", "x.stl"));

			await Assert.That(File.Exists(path)).IsTrue();

			Directory.Delete(directory, recursive: true);
		}

		private static BrowserFileDragEvent DragOver(double offsetX, double offsetY, string mimeType)
			=> new BrowserFileDragEvent
			{
				Type = "dragover",
				OffsetX = offsetX,
				OffsetY = offsetY,
				Names = new[] { string.Empty },
				MimeTypes = new[] { mimeType },
			};

		private static void RunTick(List<Action> tick)
		{
			var pending = tick.ToList();
			tick.Clear();

			foreach (Action action in pending)
			{
				action();
			}
		}

		/// <summary>
		/// A 200x150 CSS canvas at devicePixelRatio 2 (400x300 device pixels) with a target at (50, 40) that
		/// takes videos and nothing else.
		/// </summary>
		private static (BrowserFileDrop drop, List<Action> tick, VideoTarget target) MakeDrop(
			Func<string, Stream> openStagedFile = null)
		{
			var window = new SystemWindow(400, 300);
			var target = new VideoTarget()
			{
				OriginRelativeParent = new Vector2(50, 40),
			};

			window.AddChild(target);

			var tick = new List<Action>();
			var drop = new BrowserFileDrop(() => window, () => new BrowserBackingSize(400, 300, 2), tick.Add, openStagedFile);

			return (drop, tick, target);
		}

		/// <summary>A staged file whose writes fail, standing in for a write the file system refused.</summary>
		private class ThrowingStream : MemoryStream
		{
			public override void Write(byte[] buffer, int offset, int count)
				=> throw new IOException("No space left in the staging file system.");

			public override void Write(ReadOnlySpan<byte> buffer)
				=> throw new IOException("No space left in the staging file system.");
		}

		private class VideoTarget : GuiWidget
		{
			public VideoTarget()
				: base(200, 200)
			{
			}

			public List<string> LastHoverFiles { get; private set; }

			public Vector2 LastHoverPosition { get; private set; }

			public bool HoveredWithFiles { get; private set; }

			public List<string> DroppedFiles { get; private set; }

			public Vector2 DropPosition { get; private set; }

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				// agg hands a move to every child, so only a hover over this widget counts; see DropTarget.
				this.HoveredWithFiles = mouseEvent.DragFiles?.Count > 0
					&& this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y);

				if (this.HoveredWithFiles)
				{
					this.LastHoverFiles = mouseEvent.DragFiles;
					this.LastHoverPosition = mouseEvent.Position;
					mouseEvent.AcceptDrop = mouseEvent.DragFiles.TrueForAll(path => Path.GetExtension(path) == ".mp4");
				}

				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.DragFiles?.Count > 0)
				{
					this.DroppedFiles = mouseEvent.DragFiles;
					this.DropPosition = mouseEvent.Position;
				}

				base.OnMouseUp(mouseEvent);
			}
		}
	}
}
