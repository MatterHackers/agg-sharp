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

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A dropped file reaches a widget as a mouse event carrying <see cref="MouseEventArgs.DragFiles"/> -
	/// there is no drop API in agg. Every host has to synthesize the same sequence, so the sequence itself
	/// lives in <see cref="FileDropDispatcher"/> and is tested here, on any desktop, without a real drag.
	/// </summary>
	public class FileDropDispatcherTests
	{
		[Test]
		public async Task HoveringAnAcceptedFileOverTheTargetIsAccepted()
		{
			var (window, target) = MakeWindow();

			bool accepted = FileDropDispatcher.Hover(window, new List<string> { "/tmp/part.mcx" }, 60, 50);

			await Assert.That(accepted).IsTrue();
			await Assert.That(target.LastHoverFiles).IsNotNull();
			await Assert.That(target.LastHoverFiles[0]).IsEqualTo("/tmp/part.mcx");
		}

		[Test]
		public async Task HoveringAFileTheTargetRefusesIsNotAccepted()
		{
			var (window, target) = MakeWindow();

			bool accepted = FileDropDispatcher.Hover(window, new List<string> { "/tmp/virus.exe" }, 60, 50);

			await Assert.That(accepted).IsFalse();
			await Assert.That(target.LastHoverFiles).IsNotNull();
		}

		[Test]
		public async Task HoveringAwayFromTheTargetIsNotAccepted()
		{
			var (window, target) = MakeWindow();

			bool accepted = FileDropDispatcher.Hover(window, new List<string> { "/tmp/part.mcx" }, 300, 250);

			await Assert.That(accepted).IsFalse();
			await Assert.That(target.LastHoverFiles).IsNull();
		}

		[Test]
		public async Task ADropDeliversTheFilesToTheTargetWhereItLanded()
		{
			var (window, target) = MakeWindow();

			bool delivered = FileDropDispatcher.Drop(window, new List<string> { "/tmp/part.mcx" }, 60, 50);

			await Assert.That(delivered).IsTrue();
			await Assert.That(target.DroppedFiles).IsNotNull();
			await Assert.That(target.DroppedFiles[0]).IsEqualTo("/tmp/part.mcx");

			// The target sits at (50, 40), so a drop at (60, 50) is 10 in from its own corner.
			await Assert.That(target.DropPosition.X).IsEqualTo(10);
			await Assert.That(target.DropPosition.Y).IsEqualTo(10);
		}

		/// <summary>
		/// A drag that leaves has to take the hover highlight with it. Windows gets this for free - its next
		/// real mouse move carries no files - but on mac nothing else is delivered while a drag is in flight,
		/// so the host has to say so, with the same off-screen move a pointer exit uses.
		/// </summary>
		[Test]
		public async Task ADragThatLeavesClearsTheHover()
		{
			var (window, target) = MakeWindow();

			FileDropDispatcher.Hover(window, new List<string> { "/tmp/part.mcx" }, 60, 50);
			await Assert.That(target.HoveredWithFiles).IsTrue();

			FileDropDispatcher.Exit(window);

			await Assert.That(target.HoveredWithFiles).IsFalse();
		}

		[Test]
		public async Task FileUrlsBecomePosixPaths()
		{
			var paths = FileDropDispatcher.PathsFromFileUrls(new[]
			{
				"file:///Users/someone/My%20Parts/a%20part.mcx",
				"/already/a/path.stl",
				null,
				string.Empty,
			});

			await Assert.That(paths.Count).IsEqualTo(2);
			await Assert.That(paths[0]).IsEqualTo("/Users/someone/My Parts/a part.mcx");
			await Assert.That(paths[1]).IsEqualTo("/already/a/path.stl");
		}

		private static (SystemWindow window, DropTarget target) MakeWindow()
		{
			var window = new SystemWindow(400, 300);
			var target = new DropTarget()
			{
				OriginRelativeParent = new Vector2(50, 40),
			};

			window.AddChild(target);

			return (window, target);
		}

		/// <summary>Accepts .mcx and nothing else, the way the application's tab bar and 3D view do.</summary>
		private class DropTarget : GuiWidget
		{
			public DropTarget()
				: base(100, 80)
			{
			}

			public List<string> LastHoverFiles { get; private set; }

			public bool HoveredWithFiles { get; private set; }

			public List<string> DroppedFiles { get; private set; }

			public Vector2 DropPosition { get; private set; }

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				// agg hands a move to every child, not just the one under it, so a widget that wants to light
				// up only under the pointer has to check - the way View3DWidget does.
				if (mouseEvent.DragFiles?.Count > 0
					&& PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					LastHoverFiles = new List<string>(mouseEvent.DragFiles);
					HoveredWithFiles = true;
					mouseEvent.AcceptDrop = mouseEvent.DragFiles.TrueForAll(
						file => Path.GetExtension(file).ToLowerInvariant() == ".mcx");
				}
				else
				{
					HoveredWithFiles = false;
				}

				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.DragFiles?.Count > 0)
				{
					DroppedFiles = new List<string>(mouseEvent.DragFiles);
					DropPosition = mouseEvent.Position;
				}

				base.OnMouseUp(mouseEvent);
			}
		}
	}
}
