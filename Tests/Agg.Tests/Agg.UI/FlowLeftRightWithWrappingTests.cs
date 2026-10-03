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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Where a <see cref="FlowLeftRightWithWrapping"/> breaks its rows and where it draws their items, measured in
	/// device pixels at 1x and 2x.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> and restore it
	/// in a finally. Every widget is built after the scale is set.
	/// </remarks>
	public class FlowLeftRightWithWrappingTests
	{
		/// <summary>
		/// ContentWidth - every item's width and margin end to end - is known before the flow has wrapped, so a
		/// host can size a window to fit it on one row. It used to read 0 until the first wrap pass.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ContentWidthIsKnownBeforeAnyWrap(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLeftRightWithWrapping();
				var items = new[]
				{
					new GuiWidget(50 * scale, 20 * scale) { Margin = new BorderDouble(2, 0) },
					new GuiWidget(30 * scale, 20 * scale) { Margin = new BorderDouble(4, 0) },
					new GuiWidget(70 * scale, 20 * scale),
				};
				foreach (var item in items)
				{
					flow.AddChild(item);
				}

				var expected = items.Sum(item => item.Width + item.DeviceMargin.Width);
				await Assert.That(expected).IsEqualTo((50 + 4 + 30 + 8 + 70) * scale);
				await Assert.That(flow.ContentWidth).IsEqualTo(expected)
					.Because($"the flow has no parent and has never wrapped, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// Widening the parent re-wraps the flow at the new width, and narrowing it wraps it again.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ResizingTheParentRewrapsAtTheNewWidth(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = NewFlow(scale, 4);
				var parent = new GuiWidget(206 * scale, 500 * scale);
				parent.AddChild(flow);
				await Assert.That(FilledRows(flow)).IsEqualTo(2)
					.Because($"200 units of items plus 12 of row padding and margin do not fit in 206, at {scale}x");

				parent.Width = 212 * scale;
				await Assert.That(FilledRows(flow)).IsEqualTo(1)
					.Because($"they fit in 212, at {scale}x");

				parent.Width = 160 * scale;
				await Assert.That(FilledRows(flow)).IsEqualTo(2)
					.Because($"three items and the row's 12 units fit in 160 and the fourth wraps, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// A bordered row's border takes room from its items: rows after the first carry <see
		/// cref="FlowLeftRightWithWrapping.RowBorder"/>, so their items wrap before they would draw into it.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ARowsBorderCountsInItsWrapWidth(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				// a hard break first, so the items land in the second, bordered, row
				var flow = new FlowLeftRightWithWrapping()
				{
					RowMargin = 0,
					RowPadding = 0,
					RowBorder = new BorderDouble(5, 0),
				};
				flow.AddChild(new HardBreak());
				var items = Enumerable.Range(0, 4).Select(i => new GuiWidget(50 * scale, 20 * scale)).ToList();
				foreach (var item in items)
				{
					flow.AddChild(item);
				}

				var parent = new GuiWidget(205 * scale, 500 * scale);
				parent.AddChild(flow);

				// every item draws inside the flow
				foreach (var item in items)
				{
					double right = DrawnRight(item, flow);
					await Assert.That(right).IsLessThanOrEqualTo(flow.Width - 5 * scale + 0.001)
						.Because($"200 units of items and a 10 unit border do not fit in 205, at {scale}x");
				}

				await Assert.That(items[3].Parent).IsNotEqualTo(items[0].Parent);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// The flow's own margin sits outside its width, so it takes nothing more from the rows.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task TheFlowsOwnMarginDoesNotNarrowItsRows(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = NewFlow(scale, 4);
				flow.Margin = new BorderDouble(10, 0);
				// 212 units of items and row chrome in a flow 212 units wide inside its margin
				var parent = new GuiWidget(232 * scale, 500 * scale);
				parent.AddChild(flow);

				await Assert.That(flow.Width).IsEqualTo(212 * scale).Within(0.001);
				await Assert.That(FilledRows(flow)).IsEqualTo(1)
					.Because($"the row fits the flow's own width exactly, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// A right-aligned row ends its last item at the row's inner right edge.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ARightAlignedRowEndsAtItsInnerRightEdge(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLeftRightWithWrapping()
				{
					ContentHAnchor = HAnchor.Right,
				};
				var first = new GuiWidget(50 * scale, 20 * scale);
				var last = new GuiWidget(50 * scale, 20 * scale);
				flow.AddChild(first);
				flow.AddChild(last);

				var parent = new GuiWidget(300 * scale, 500 * scale);
				parent.AddChild(flow);

				// the row's margin is 3 units and its padding 3 units each side
				double lastRight = DrawnRight(last, flow);
				await Assert.That(lastRight).IsEqualTo(294 * scale).Within(0.001)
					.Because($"300 units less 3 of row margin and 3 of row padding, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// Right alignment and <see cref="FlowLeftRightWithWrapping.Proportional"/> spacing leave room for the
		/// flow's own padding and a row's border, so a row ends at its inner right edge.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0, false, false)]
		[Arguments(2.0, false, false)]
		[Arguments(1.0, true, false)]
		[Arguments(2.0, true, false)]
		[Arguments(1.0, false, true)]
		[Arguments(2.0, false, true)]
		public async Task AlignedRowsEndInsideTheFlowsPaddingAndTheRowsBorder(double scale, bool bordered, bool proportional)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = new FlowLeftRightWithWrapping()
				{
					Padding = bordered ? 0 : 5,
					RowBorder = bordered ? new BorderDouble(5, 0) : 0,
					Proportional = proportional,
				};
				flow.ContentHAnchor = proportional ? HAnchor.Left : HAnchor.Right;
				if (bordered)
				{
					// a hard break first, so the items land in the second, bordered, row
					flow.AddChild(new HardBreak());
				}

				var first = new GuiWidget(50 * scale, 20 * scale);
				var last = new GuiWidget(50 * scale, 20 * scale);
				flow.AddChild(first);
				flow.AddChild(last);

				var parent = new GuiWidget(300 * scale, 500 * scale);
				parent.AddChild(flow);

				// proportional spacing ends each row with a spacer; right alignment ends it with the last item
				GuiWidget rowEnd = proportional ? last.Parent.Children.Last() : last;
				await Assert.That(DrawnRight(rowEnd, flow)).IsEqualTo(289 * scale).Within(0.001)
					.Because($"300 units less 5 of flow padding or row border, 3 of row margin and 3 of row padding, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// The first item of every row is drawn one row margin plus one row padding in from the flow's left edge.
		/// </summary>
		/// <remarks>
		/// A row gets its padding before its stretch anchor, so while it still fits its (empty) content it takes
		/// bounds reaching one padding left of its origin, and keeps them when stretched. The flow layout places
		/// items from that drawn edge (see <see cref="FlowLayoutPaddingTests"/>), not from the origin.
		/// </remarks>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task EachRowsFirstItemIsDrawnOnePaddingIn(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = NewFlow(scale, 4);
				var parent = new GuiWidget(160 * scale, 500 * scale);
				parent.AddChild(flow);

				foreach (var row in flow.Children.Where(row => row.Children.Count > 0))
				{
					GuiWidget item = row.Children[0];
					await Assert.That(DrawnLeft(item, flow)).IsEqualTo(6 * scale).Within(0.001)
						.Because($"3 units of row margin and 3 of row padding, at {scale}x");
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// A flow narrowed each time it grows taller (a scroll bar appearing beside it) re-wraps into more rows during
		/// its own settle; its Fit height still ends enclosing every row, so the bottom row is not clipped.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task AFlowNarrowedWhileItGrowsStillEnclosesEveryRow(double scale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = scale;
				var flow = NewFlow(scale, 12);
				flow.HAnchor = HAnchor.Left;
				flow.Width = 212 * scale;
				var parent = new GuiWidget(300 * scale, 500 * scale);
				double lastHeight = flow.Height;
				flow.BoundsChanged += (s, e) =>
				{
					if (flow.Height > lastHeight
						&& flow.Width > 62 * scale)
					{
						lastHeight = flow.Height;
						flow.Width -= 50 * scale;
					}
				};
				parent.AddChild(flow);

				await Assert.That(flow.Width).IsEqualTo(62 * scale)
					.Because($"each growth takes 50 off 212 until the width is 62, at {scale}x");
				await Assert.That(FilledRows(flow)).IsEqualTo(12)
					.Because($"one 50 unit item and 12 units of row chrome fit in 62, at {scale}x");

				var rowsBounds = flow.GetMinimumBoundsToEncloseChildren(true);
				await Assert.That(flow.LocalBounds.Height).IsGreaterThanOrEqualTo(rowsBounds.Height - 0.001)
					.Because($"a Fit flow is as tall as its rows, at {scale}x");

				var bottomItem = flow.Children.Last(row => row.Children.Count > 0).Children.Last();
				double drawnBottom = bottomItem.TransformToParentSpace(flow, new Vector2(bottomItem.LocalBounds.Left, bottomItem.LocalBounds.Bottom)).Y;
				await Assert.That(drawnBottom).IsGreaterThanOrEqualTo(flow.LocalBounds.Bottom - 0.001)
					.Because($"the bottom row draws inside the flow, at {scale}x");
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static FlowLeftRightWithWrapping NewFlow(double scale, int itemCount)
		{
			var flow = new FlowLeftRightWithWrapping();
			for (int i = 0; i < itemCount; i++)
			{
				flow.AddChild(new GuiWidget(50 * scale, 20 * scale));
			}

			return flow;
		}

		// Where an item draws in the flow. An item's Position is its edge in its row's space, whose origin is not
		// the row's edge, so it cannot be added to the row's Position.
		private static double DrawnLeft(GuiWidget item, GuiWidget flow)
		{
			return item.TransformToParentSpace(flow, new Vector2(item.LocalBounds.Left, item.LocalBounds.Bottom)).X;
		}

		/// <summary>How far <paramref name="item"/>'s right edge is drawn from <paramref name="flow"/>'s drawn left edge.</summary>
		private static double DrawnRight(GuiWidget item, GuiWidget flow)
		{
			// From the drawn edge, not the origin: a padded flow's left edge sits one padding left of its origin.
			return item.TransformToParentSpace(flow, new Vector2(item.LocalBounds.Right, item.LocalBounds.Bottom)).X - flow.LocalBounds.Left;
		}

		private static int FilledRows(FlowLeftRightWithWrapping flow)
		{
			return flow.Children.Count(row => row.Children.Count > 0);
		}
	}
}
