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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A library tree fills in asynchronously: the nodes are handed to a TreeView that is already on
	/// screen, and <see cref="TreeNode"/> only parents its rows into the widget tree on the next paint
	/// (<c>OnDraw</c> sees <c>isDirty</c> and calls <c>RebuildContentSection</c>). So the widget tree grows
	/// in the middle of a frame that the container above it has already been painted for.
	/// <para>
	/// On the one frame that happens, the first several rows painted in the top left corner of the window -
	/// outside the scrolling area, outside the dialog, on top of the application's toolbar - and were
	/// correct again the next frame.
	/// </para>
	/// </summary>
	[NotInParallel]
	public class TreeViewLateContentClippingTests
	{
		/// <summary>
		/// The bug as the user sees it: on the frame the tree fills in, no row may paint - or be allowed by
		/// its clip to paint - outside the scrolling area that holds it.
		/// </summary>
		[Test]
		public async Task RowsAddedBetweenFramesStayInsideTheScrollAreaOnTheFrameTheyAppear()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = CreateTempStaticDataRoot();

			try
			{
				StaticData.RootPath = tempRoot;

				using var harness = new LateFillTreeHarness();

				// Frame one: the dialog is up and the tree is empty, exactly as the plugin opens.
				harness.Draw();

				// The library content arrives. Nodes are built detached and handed to the live tree; their
				// rows are left for the next paint to materialize, which is what the library panels do.
				harness.FillTreeLeavingRowsForTheNextPaint(rowCount: 40);

				// Frame two: the frame the user sees the glitch on.
				harness.Draw();

				var rows = harness.PaintedRows;
				var viewport = harness.ScrollAreaBounds;

				await Assert.That(rows.Count).IsGreaterThan(5)
					.Because("if the rows never painted, the assertions below prove nothing");

				foreach (var row in rows)
				{
					await Assert.That(row.Clip.Left).IsGreaterThanOrEqualTo(viewport.Left - 1)
						.Because($"'{row.Name}' was allowed to paint left of the scrolling area ({row.Clip}, viewport {viewport})");
					await Assert.That(row.Clip.Right).IsLessThanOrEqualTo(viewport.Right + 1)
						.Because($"'{row.Name}' was allowed to paint right of the scrolling area ({row.Clip}, viewport {viewport})");
					await Assert.That(row.Clip.Top).IsLessThanOrEqualTo(viewport.Top + 1)
						.Because($"'{row.Name}' was allowed to paint above the scrolling area ({row.Clip}, viewport {viewport})");
					await Assert.That(row.Clip.Bottom).IsGreaterThanOrEqualTo(viewport.Bottom - 1)
						.Because($"'{row.Name}' was allowed to paint below the scrolling area ({row.Clip}, viewport {viewport})");
				}

				// The clip is only half the story - a row can be inside the clip and still be drawn stacked
				// on the row above it. Where the row actually landed has to be in the viewport too.
				foreach (var row in rows.Where(r => r.Visible))
				{
					await Assert.That(row.Bounds.Top).IsLessThanOrEqualTo(viewport.Top + 1)
						.Because($"'{row.Name}' painted above the top of the scrolling area ({row.Bounds}, viewport {viewport})");
				}
			}
			finally
			{
				StaticData.RootPath = savedRootPath;

				try
				{
					Directory.Delete(tempRoot, true);
				}
				catch (IOException)
				{
					// A held icon file must not turn a real assertion failure above into a cleanup exception.
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
		}

		/// <summary>
		/// The pre-pass that fixes the bug above only runs while the process-wide count of nodes waiting for
		/// rows says there is work to do, and the nodes that drive that count are made dirty off the UI
		/// thread - a library panel filling in after an await - while the UI thread is clearing them. So the
		/// flag and the count have to move as one step. Reading the flag, writing it, then adjusting the
		/// count as three steps lets two threads both see a clean node and both claim the count for it, and
		/// the single clear that follows only gives one back: the count and the flag part company for the
		/// rest of the session.
		/// </summary>
		[Test]
		public async Task DirtyFlagAndAwaitingCountStayInStepUnderConcurrentUpdates()
		{
			var node = new TreeNode(new ThemeConfig());

			// The flag is private by design - it is only meaningful together with the count it feeds - so the
			// race has to be driven through the real property rather than a stand-in for it.
			var setIsDirty = (Action<TreeNode, bool>)typeof(TreeNode)
				.GetProperty("IsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetSetMethod(nonPublic: true)
				.CreateDelegate(typeof(Action<TreeNode, bool>));

			var awaitingCount = typeof(TreeNode).GetField("nodesAwaitingContent", BindingFlags.Static | BindingFlags.NonPublic);

			int countBefore = (int)awaitingCount.GetValue(null);

			const int iterations = 500000;
			const int dirtyThreads = 3;

			// Everyone has to be inside the loop at once for the window between the compare and the count to
			// be reachable at all, so the threads are held until they are all ready.
			using var allReady = new Barrier(dirtyThreads + 1);

			Task Hammer(bool value) => Task.Factory.StartNew(
				() =>
				{
					allReady.SignalAndWait();

					for (int i = 0; i < iterations; i++)
					{
						setIsDirty(node, value);
					}
				},
				TaskCreationOptions.LongRunning);

			var hammering = Enumerable.Range(0, dirtyThreads).Select(_ => Hammer(true)).Append(Hammer(false)).ToArray();

			await Task.WhenAll(hammering);

			// However they interleaved, the node ends clean, so it owes the count nothing.
			setIsDirty(node, false);

			await Assert.That((int)awaitingCount.GetValue(null)).IsEqualTo(countBefore)
				.Because("the count drifted away from the flag, so a TreeView will skip a pre-pass it needs or run one it does not");

			node.Close();
		}

		private static string CreateTempStaticDataRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "AggTreeLateFill_" + Path.GetRandomFileName());
			Directory.CreateDirectory(Path.Combine(root, "Icons"));

			// TreeExpandWidget loads these on first draw; without them StaticData throws in DEBUG.
			WriteBlankIcon(root, "fa-angle-right_12.png", 12, 12);
			WriteBlankIcon(root, "fa-angle-down_12.png", 12, 12);

			return root;
		}

		private static void WriteBlankIcon(string rootPath, string iconName, int width, int height)
		{
			var image = new ImageBuffer(width, height);
			image.NewGraphics2D().Clear(Color.White);
			ImageIO.SaveImageData(Path.Combine(rootPath, "Icons", iconName), image);
		}

		/// <summary>
		/// A window with a toolbar strip across the top and a dialog inset below it, the dialog holding a
		/// TreeView. The strip and the inset are what make "escaped the container" visible at all: a row that
		/// gets the window for a clip lands on top of them.
		/// </summary>
		private class LateFillTreeHarness : IDisposable
		{
			private readonly GuiWidget root;
			private readonly TreeView treeView;
			private readonly FlowLayoutWidget contentPanel;
			private readonly ThemeConfig theme = new ThemeConfig();
			private readonly List<TreeNode> nodes = new List<TreeNode>();

			public LateFillTreeHarness()
			{
				// DoubleBuffer gives the root a back buffer to paint into, which is what makes a
				// headless draw possible at all (NewGraphics2D walks up looking for one).
				root = new GuiWidget(400, 600) { DoubleBuffer = true, Name = "Window" };

				var toolbar = new GuiWidget(400, 40)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Top,
					Name = "Toolbar"
				};
				root.AddChild(toolbar);

				// The dialog: inset from every edge, so anything painting at the window origin is
				// unambiguously outside it.
				var dialog = new GuiWidget(300, 400)
				{
					OriginRelativeParent = new VectorMath.Vector2(50, 60),
					Name = "Dialog"
				};
				root.AddChild(dialog);

				treeView = new TreeView(theme)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					Name = "PartsTree"
				};
				dialog.AddChild(treeView);

				contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					Name = "ContentPanel"
				};
				treeView.AddChild(contentPanel);

				root.PerformLayout();
			}

			public List<PaintedRow> PaintedRows { get; } = new List<PaintedRow>();

			/// <summary>
			/// Where the scrolling area painted, in the surface's own coordinates. Rows outside this are the bug.
			/// </summary>
			public RectangleDouble ScrollAreaBounds { get; private set; }

			/// <summary>
			/// Build a root node with children the way a library panel does - detached, then handed to the
			/// live tree with the child rows still unbuilt, so the next paint materializes them.
			/// </summary>
			public void FillTreeLeavingRowsForTheNextPaint(int rowCount)
			{
				var rootNode = new TreeNode(theme) { Text = "Parts", Name = "Parts" };

				using (treeView.LayoutLock())
				{
					for (int i = 0; i < rowCount; i++)
					{
						var child = new TreeNode(theme, true, rootNode)
						{
							Text = $"Part {i:00}",
							Name = $"Part {i:00}"
						};
						rootNode.Nodes.Add(child);
						nodes.Add(child);
					}

					rootNode.Expanded = true;
					nodes.Add(rootNode);

					contentPanel.RemoveChildren();
					contentPanel.AddChild(rootNode);
					rootNode.TreeView = treeView;
				}

				treeView.PerformLayout();
			}

			/// <summary>
			/// Paint one frame, recording where each row landed and what clip it was given at the moment it
			/// was painted. Both have to be sampled during the draw: the rows are parented mid-frame, so the
			/// layout that tidies everything up has already run by the time the frame ends.
			/// </summary>
			public void Draw()
			{
				PaintedRows.Clear();

				void RecordRow(object sender, DrawEventArgs e)
				{
					var widget = (GuiWidget)sender;
					var bounds = widget.LocalBounds;
					e.Graphics2D.GetTransform().transform(ref bounds);

					PaintedRows.Add(new PaintedRow(
						widget.Parent is TreeNode node ? node.Text : widget.Name,
						bounds,
						e.Graphics2D.GetClippingRect(),
						widget.ActuallyVisibleOnScreen()));
				}

				void RecordScrollArea(object sender, DrawEventArgs e)
				{
					var bounds = treeView.LocalBounds;
					e.Graphics2D.GetTransform().transform(ref bounds);
					ScrollAreaBounds = bounds;
				}

				var recording = nodes.ToList();
				foreach (var node in recording)
				{
					node.TitleBar.BeforeDraw += RecordRow;
				}

				treeView.BeforeDraw += RecordScrollArea;

				try
				{
					root.OnDraw(root.NewGraphics2D());
				}
				finally
				{
					treeView.BeforeDraw -= RecordScrollArea;
					foreach (var node in recording)
					{
						node.TitleBar.BeforeDraw -= RecordRow;
					}
				}
			}

			public void Dispose()
			{
				root.Close();
			}
		}

		private readonly struct PaintedRow
		{
			public PaintedRow(string name, RectangleDouble bounds, RectangleDouble clip, bool visible)
			{
				this.Name = name;
				this.Bounds = bounds;
				this.Clip = clip;
				this.Visible = visible;
			}

			public string Name { get; }

			/// <summary>Where the row's own rectangle landed on the surface being painted.</summary>
			public RectangleDouble Bounds { get; }

			/// <summary>The clipping rectangle in force as the row painted.</summary>
			public RectangleDouble Clip { get; }

			public bool Visible { get; }
		}
	}
}
