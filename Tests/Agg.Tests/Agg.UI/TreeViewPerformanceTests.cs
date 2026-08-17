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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A measurement harness for the cost of opening and closing one row of a large TreeView.
	/// </summary>
	/// <remarks>
	/// Apps built on this widget stack report 400-500ms to expand or collapse a single node in a tree of
	/// a couple of thousand rows. This suite exists to put numbers on that: it builds a realistic tree out
	/// of the real production widgets, times the phases, and counts how many layout passes and widgets a
	/// single toggle actually touches. The assertions are deliberately far looser than the numbers we
	/// measure - they are a catastrophic-regression tripwire, not a performance gate. The value is the
	/// printed breakdown in the test log.
	/// <para>
	/// What the first run of this found, so the next reader does not have to re-derive it: a single toggle
	/// runs only ~12 layout passes over 7 widgets, and paints in ~4ms, but still cost ~46ms on a 2000 row
	/// tree. Nearly all of it was <c>GuiWidget.ScreenClipping.MarkRecalculate</c>, which walked the whole
	/// subtree of every widget whose LocalBounds or ParentToChildTransform is written. Repositioning the
	/// 100 sibling rows of a flow container therefore re-walked all 18,546 widgets several times over:
	/// a temporary stopwatch inside MarkRecalculate attributed 28.7ms of a 29.4ms expand (84,456 widget
	/// visits) to it, and 2006ms of the 2909ms build (4,115,983 visits).
	/// </para>
	/// <para>
	/// That push-down invalidation has since been replaced by an O(1) stamp validated on read (see
	/// <c>GuiWidget.ScreenClipping</c>), which took the same expand to ~0.4ms and the build from 4.1s to
	/// 0.75s. The counts below are what guard that: if the elapsed time ever tracks the widget count again,
	/// something has gone back to touching the whole tree.
	/// </para>
	/// </remarks>
	[NotInParallel]
	public class TreeViewPerformanceTests
	{
		private const int TopLevelNodeCount = 100;
		private const int ChildrenPerTopLevelNode = 19;

		/// <summary>
		/// Every fifth top level node gets a third level under its first child, so the tree is not a
		/// uniform two-deep grid - nesting is where the flow layout's fit-to-children cascades compound.
		/// </summary>
		private const int NestEveryNthTopLevelNode = 5;

		private const int GrandchildrenPerNestedChild = 3;

		[Test]
		public async Task ExpandAndCollapseOfOneNodeInALargeTree()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = CreateTempStaticDataRoot();

			try
			{
				StaticData.RootPath = tempRoot;

				using var harness = new TreeBenchmarkHarness();

				var buildTime = harness.Build();
				Console.WriteLine($"TreeView benchmark: {harness.NodeCount} TreeNodes, {harness.WidgetCount} widgets total");
				Console.WriteLine($"  build (construct + parent + eager layout): {buildTime.Milliseconds:0.0} ms, "
					+ $"{buildTime.LayoutPasses} layout passes");

				var firstDraw = harness.Draw();
				Console.WriteLine($"  first draw (offscreen 400x600):           {firstDraw.Milliseconds:0.0} ms, "
					+ $"{firstDraw.LayoutPasses} layout passes, {firstDraw.WidgetsDrawn} widget draws");

				// The node under test sits in the middle of the tree, so opening it moves every row below it.
				var target = harness.TopLevelNodes[TopLevelNodeCount / 2];

				var expandTimes = new List<double>();
				var collapseTimes = new List<double>();
				var expandPasses = new List<int>();

				for (int i = 0; i < 10; i++)
				{
					var expand = Measure(() => target.Expanded = true);
					var collapse = Measure(() => target.Expanded = false);

					expandTimes.Add(expand.Milliseconds);
					collapseTimes.Add(collapse.Milliseconds);
					expandPasses.Add(expand.LayoutPasses);
				}

				Console.WriteLine($"  expand   (layout only) x10: min {Min(expandTimes):0.0} ms, "
					+ $"median {Median(expandTimes):0.0} ms, max {Max(expandTimes):0.0} ms, "
					+ $"{Median(expandPasses.Select(p => (double)p)):0} layout passes each");
				Console.WriteLine($"  collapse (layout only) x10: min {Min(collapseTimes):0.0} ms, "
					+ $"median {Median(collapseTimes):0.0} ms, max {Max(collapseTimes):0.0} ms");

				// Now with a paint after each toggle, which is what the user actually waits for.
				var expandAndDraw = new List<double>();
				var collapseAndDraw = new List<double>();
				var drawAfterExpand = new List<double>();

				for (int i = 0; i < 3; i++)
				{
					var expand = Measure(() => target.Expanded = true);
					var draw = harness.Draw();
					expandAndDraw.Add(expand.Milliseconds + draw.Milliseconds);
					drawAfterExpand.Add(draw.Milliseconds);

					var collapse = Measure(() => target.Expanded = false);
					var collapseDraw = harness.Draw();
					collapseAndDraw.Add(collapse.Milliseconds + collapseDraw.Milliseconds);
				}

				Console.WriteLine($"  expand + paint x3:   min {Min(expandAndDraw):0.0} ms, median {Median(expandAndDraw):0.0} ms "
					+ $"(paint alone median {Median(drawAfterExpand):0.0} ms)");
				Console.WriteLine($"  collapse + paint x3: min {Min(collapseAndDraw):0.0} ms, median {Median(collapseAndDraw):0.0} ms");

				harness.ReportToggleAttribution(target);

				// Does the cost of one toggle follow the size of the node being opened, or the size of the
				// whole tree? Same toggle, smaller trees - if the time falls with the tree, the cost is in
				// work that scans everything, not in the rows that actually moved. The invalidation count
				// beside it says the same thing without a clock in it: one stamp per widget whose clipping
				// really did move, so it must stay of the order of the rows that moved.
				Console.WriteLine("  --- scaling: same single toggle, smaller trees ---");
				int smallTreeInvalidations = 0;
				foreach (int topCount in new[] { 25, 50 })
				{
					using var smaller = new TreeBenchmarkHarness(topCount);
					smaller.Build();
					smaller.Draw();

					var smallerTarget = smaller.TopLevelNodes[topCount / 2];
					var samples = new List<double>();
					for (int i = 0; i < 5; i++)
					{
						samples.Add(Measure(() => smallerTarget.Expanded = true).Milliseconds);
						smallerTarget.Expanded = false;
					}

					int invalidations = MeasureClippingInvalidations(() => smallerTarget.Expanded = true);
					smallerTarget.Expanded = false;

					if (topCount == 25)
					{
						smallTreeInvalidations = invalidations;
					}

					Console.WriteLine($"      {topCount} top level rows ({smaller.NodeCount} nodes, {smaller.WidgetCount} widgets): "
						+ $"expand min {Min(samples):0.0} ms, median {Median(samples):0.0} ms, "
						+ $"{invalidations} clipping invalidations");
				}

				int fullTreeInvalidations = MeasureClippingInvalidations(() => target.Expanded = true);
				target.Expanded = false;

				Console.WriteLine($"  one expand of the {harness.WidgetCount} widget tree: {fullTreeInvalidations} clipping "
					+ $"invalidations; of the quarter sized tree: {smallTreeInvalidations}");

				// The real guard, and the only assertion here that does not involve a clock. Invalidating a
				// widget's screen clipping is O(1) - one stamp on the widget that moved - so a toggle must
				// cost stamps in proportion to the rows it moves, never to the size of the tree. The eager
				// scheme this replaced marked every widget under each moved one: 84,456 widgets touched for
				// a toggle in a tree of 18,546. Quadrupling the tree may not even double this.
				await Assert.That(fullTreeInvalidations).IsLessThan(harness.WidgetCount)
					.Because("a toggle must not stamp as many widgets as there are in the whole tree");
				await Assert.That(fullTreeInvalidations).IsLessThan(smallTreeInvalidations * 4)
					.Because("a four times larger tree must not cost four times the invalidations for the same toggle");

				// A generous tripwire. We measure far below this; it only fires if a change makes a single
				// toggle catastrophically slow.
				await Assert.That(Min(expandTimes)).IsLessThan(2000)
					.Because("expanding one node of a 2000 row tree must not take seconds");
				await Assert.That(Min(collapseTimes)).IsLessThan(2000)
					.Because("collapsing one node of a 2000 row tree must not take seconds");
			}
			finally
			{
				StaticData.RootPath = savedRootPath;
				Directory.Delete(tempRoot, true);
			}
		}

		private static PhaseResult Measure(Action action)
		{
			int layoutStart = GuiWidget.LayoutCount;
			int drawStart = GuiWidget.DrawCount;

			var timer = Stopwatch.StartNew();
			action();
			timer.Stop();

			return new PhaseResult(
				timer.Elapsed.TotalMilliseconds,
				GuiWidget.LayoutCount - layoutStart,
				GuiWidget.DrawCount - drawStart);
		}

		/// <summary>
		/// How many widgets had their cached screen clipping stamped stale while the action ran. Deterministic
		/// where the timings are not, and it is the quantity the lazy scheme is about: one stamp per widget
		/// that moved, rather than one per widget in the subtree beneath it.
		/// </summary>
		private static int MeasureClippingInvalidations(Action action)
		{
			long start = GuiWidget.ScreenClippingInvalidationCount;
			action();
			return (int)(GuiWidget.ScreenClippingInvalidationCount - start);
		}

		private static double Min(IEnumerable<double> values) => values.Min();

		private static double Max(IEnumerable<double> values) => values.Max();

		private static double Median(IEnumerable<double> values)
		{
			var sorted = values.OrderBy(v => v).ToList();
			return sorted.Count % 2 == 1
				? sorted[sorted.Count / 2]
				: (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
		}

		private static string CreateTempStaticDataRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "AggTreeViewPerf_" + Path.GetRandomFileName());
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
		/// GuiWidget does not override Equals, but the framework's own ReferenceEqualityComparer is typed
		/// to object, so the generic collections here need this.
		/// </summary>
		private class WidgetIdentityComparer : IEqualityComparer<GuiWidget>
		{
			public static readonly WidgetIdentityComparer Instance = new WidgetIdentityComparer();

			public bool Equals(GuiWidget x, GuiWidget y) => ReferenceEquals(x, y);

			public int GetHashCode(GuiWidget obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}

		private readonly struct PhaseResult
		{
			public PhaseResult(double milliseconds, int layoutPasses, int widgetsDrawn)
			{
				this.Milliseconds = milliseconds;
				this.LayoutPasses = layoutPasses;
				this.WidgetsDrawn = widgetsDrawn;
			}

			public double Milliseconds { get; }

			/// <summary>
			/// Increments of the global <see cref="GuiWidget.LayoutCount"/>, which counts every OnLayout
			/// that actually ran its layout engine (visible and not layout locked).
			/// </summary>
			public int LayoutPasses { get; }

			public int WidgetsDrawn { get; }
		}

		/// <summary>
		/// A TreeView holding a realistic row container, built out of the production widgets, sized and
		/// double buffered so it can be laid out and painted with no window behind it.
		/// </summary>
		private class TreeBenchmarkHarness : IDisposable
		{
			private readonly GuiWidget root;
			private readonly TreeView treeView;
			private readonly FlowLayoutWidget treeNodeContainer;
			private readonly ThemeConfig theme;
			private readonly int topLevelNodeCount;

			public TreeBenchmarkHarness(int topLevelNodeCount = TopLevelNodeCount)
			{
				this.topLevelNodeCount = topLevelNodeCount;
				theme = new ThemeConfig();

				// DoubleBuffer gives the root a back buffer to paint into, which is what makes a
				// headless draw possible at all (NewGraphics2D walks up looking for one).
				root = new GuiWidget(400, 600) { DoubleBuffer = true };

				treeView = new TreeView(theme) { Name = "BenchmarkTree" };
				treeView.ScrollArea.HAnchor = HAnchor.Stretch;

				treeNodeContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
				treeView.AddChild(treeNodeContainer);

				root.AddChild(treeView);
			}

			public List<TreeNode> TopLevelNodes { get; } = new List<TreeNode>();

			public int NodeCount => TopLevelNodes.Sum(n => n.DescendantsAndSelf().Count());

			public int WidgetCount => root.Descendants<GuiWidget>().Count();

			public PhaseResult Build()
			{
				return Measure(() =>
				{
					for (int t = 0; t < topLevelNodeCount; t++)
					{
						var top = new TreeNode(theme) { Text = $"Top {t:000}", Name = $"Top {t:000}" };
						top.TreeView = treeView;

						for (int c = 0; c < ChildrenPerTopLevelNode; c++)
						{
							var child = new TreeNode(theme, true, top) { Text = $"Child {t:000}.{c:00}" };
							top.Nodes.Add(child);

							if (t % NestEveryNthTopLevelNode == 0 && c == 0)
							{
								for (int g = 0; g < GrandchildrenPerNestedChild; g++)
								{
									var grandchild = new TreeNode(theme, true, child) { Text = $"Grandchild {t:000}.{c:00}.{g}" };
									child.Nodes.Add(grandchild);
								}
							}
						}

						// Materialize the rows now rather than on the first paint, the way a real caller
						// building a tree off screen does (see TreeNode.EnsureContentBuilt).
						foreach (var node in top.DescendantsAndSelf())
						{
							node.EnsureContentBuilt();
						}

						treeNodeContainer.AddChild(top);
						TopLevelNodes.Add(top);
					}
				});
			}

			public PhaseResult Draw()
			{
				return Measure(() => root.OnDraw(root.NewGraphics2D()));
			}

			/// <summary>
			/// Re-run one expand and one collapse with a listener on every widget's Layout and BoundsChanged
			/// events, to attribute where the layout passes land.
			/// </summary>
			public void ReportToggleAttribution(TreeNode target)
			{
				var widgets = root.Descendants<GuiWidget>().ToList();

				var layoutsByWidget = new Dictionary<GuiWidget, int>(WidgetIdentityComparer.Instance);
				var boundsChangesByWidget = new Dictionary<GuiWidget, int>(WidgetIdentityComparer.Instance);
				var positionChangesByWidget = new Dictionary<GuiWidget, int>(WidgetIdentityComparer.Instance);

				void OnWidgetLayout(object sender, EventArgs e) => Bump(layoutsByWidget, (GuiWidget)sender);
				void OnWidgetBoundsChanged(object sender, EventArgs e) => Bump(boundsChangesByWidget, (GuiWidget)sender);
				void OnWidgetPositionChanged(object sender, EventArgs e) => Bump(positionChangesByWidget, (GuiWidget)sender);

				foreach (var widget in widgets)
				{
					widget.Layout += OnWidgetLayout;
					widget.BoundsChanged += OnWidgetBoundsChanged;
					widget.PositionChanged += OnWidgetPositionChanged;
				}

				try
				{
					target.Expanded = false;

					void ResetCounts()
					{
						layoutsByWidget.Clear();
						boundsChangesByWidget.Clear();
						positionChangesByWidget.Clear();
					}

					ResetCounts();
					var expand = Measure(() => target.Expanded = true);
					ReportPhase("expand", expand, layoutsByWidget, boundsChangesByWidget, positionChangesByWidget, target);

					ResetCounts();
					var collapse = Measure(() => target.Expanded = false);
					ReportPhase("collapse", collapse, layoutsByWidget, boundsChangesByWidget, positionChangesByWidget, target);
				}
				finally
				{
					foreach (var widget in widgets)
					{
						widget.Layout -= OnWidgetLayout;
						widget.BoundsChanged -= OnWidgetBoundsChanged;
						widget.PositionChanged -= OnWidgetPositionChanged;
					}
				}
			}

			private void ReportPhase(string label,
				PhaseResult phase,
				Dictionary<GuiWidget, int> layoutsByWidget,
				Dictionary<GuiWidget, int> boundsChangesByWidget,
				Dictionary<GuiWidget, int> positionChangesByWidget,
				TreeNode target)
			{
				var targetSubtree = new HashSet<GuiWidget>(target.Descendants<GuiWidget>(), WidgetIdentityComparer.Instance);
				targetSubtree.Add(target);

				int totalLayouts = layoutsByWidget.Values.Sum();
				int layoutsInsideTarget = layoutsByWidget.Where(kvp => targetSubtree.Contains(kvp.Key)).Sum(kvp => kvp.Value);
				int textWidgetLayouts = layoutsByWidget.Where(kvp => kvp.Key is TextWidget).Sum(kvp => kvp.Value);
				int textWidgetBoundsChanges = boundsChangesByWidget.Where(kvp => kvp.Key is TextWidget).Sum(kvp => kvp.Value);

				Console.WriteLine($"  --- {label} attribution (instrumented, {phase.Milliseconds:0.0} ms) ---");
				Console.WriteLine($"      {totalLayouts} layout invocations over {layoutsByWidget.Count} distinct widgets");
				Console.WriteLine($"      {layoutsInsideTarget} of those are inside the node being toggled, "
					+ $"{totalLayouts - layoutsInsideTarget} are outside it (rows that did not change)");
				Console.WriteLine($"      {boundsChangesByWidget.Values.Sum()} bounds changes over {boundsChangesByWidget.Count} distinct widgets");
				Console.WriteLine($"      {positionChangesByWidget.Values.Sum()} position changes over {positionChangesByWidget.Count} distinct widgets");
				Console.WriteLine($"      {textWidgetLayouts} TextWidget layouts, {textWidgetBoundsChanges} TextWidget bounds changes "
					+ "(text measurement is cached per printer, so bounds changes bound the re-measure count)");

				// Every LocalBounds or ParentToChildTransform write invalidates screen clipping. That used to
				// walk the writing widget's whole subtree, so this number - each event weighted by the size of
				// the subtree it sits on - was the real work a toggle did, and it dwarfed the layout count
				// above. Invalidation is O(1) now (GuiWidget.ScreenClipping stamps one widget and validates on
				// read), so the weighted figure no longer describes work done; it is kept as the size of the
				// blast radius the old design paid for, which is what makes the elapsed time above meaningful.
				var subtreeSizes = SubtreeSizes();
				long widgetsUnderInvalidations = boundsChangesByWidget.Concat(positionChangesByWidget)
					.Sum(kvp => (long)kvp.Value * subtreeSizes[kvp.Key]);

				Console.WriteLine($"      {boundsChangesByWidget.Values.Sum() + positionChangesByWidget.Values.Sum()} "
					+ "screen clipping invalidations, O(1) each "
					+ $"(~{widgetsUnderInvalidations} widget visits under the old eager subtree walk)");

				foreach (var entry in layoutsByWidget.OrderByDescending(kvp => kvp.Value).Take(6))
				{
					Console.WriteLine($"      top layout target: {Describe(entry.Key)} x{entry.Value}");
				}

				foreach (var entry in boundsChangesByWidget.Concat(positionChangesByWidget)
					.OrderByDescending(kvp => (long)kvp.Value * subtreeSizes[kvp.Key])
					.Take(4))
				{
					Console.WriteLine($"      widest clipping invalidation: {Describe(entry.Key)} "
						+ $"x{entry.Value} over a {subtreeSizes[entry.Key]} widget subtree");
				}
			}

			/// <summary>
			/// Widget-count of the subtree rooted at each widget, itself included.
			/// </summary>
			private Dictionary<GuiWidget, int> SubtreeSizes()
			{
				var sizes = new Dictionary<GuiWidget, int>(WidgetIdentityComparer.Instance);

				int Visit(GuiWidget widget)
				{
					int size = 1;
					foreach (var child in widget.Children)
					{
						size += Visit(child);
					}

					sizes[widget] = size;
					return size;
				}

				Visit(root);
				return sizes;
			}

			private static string Describe(GuiWidget widget)
			{
				var name = string.IsNullOrEmpty(widget.Name) ? widget.Text : widget.Name;
				return $"{widget.GetType().Name}{(string.IsNullOrEmpty(name) ? "" : $"('{name}')")}";
			}

			private static void Bump(Dictionary<GuiWidget, int> counts, GuiWidget widget)
			{
				counts.TryGetValue(widget, out int current);
				counts[widget] = current + 1;
			}

			public void Dispose()
			{
				root.Close();
			}
		}
	}
}
