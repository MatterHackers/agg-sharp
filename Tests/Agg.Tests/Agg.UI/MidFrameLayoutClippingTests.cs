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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A paint pass walks the tree once, and every child's clipping rectangle is worked out from the widget
	/// positions and sizes that are current at the moment that child is reached - not from the ones that were
	/// current when its parent was reached. Anything that changes layout part way through a frame (a widget
	/// that grows or moves from inside some descendant's OnDraw) therefore leaves the parent painting under a
	/// clip taken at the old layout while its later siblings get clips taken at the new one.
	///
	/// That would be harmless if <see cref="GuiWidget"/>.DrawChild ever intersected the child's clip with the
	/// clip already in force on the surface (the parent's, held in oldClippingRect). It does not - it
	/// *replaces* it - so a child can be handed a clip that reaches outside everything its ancestors were
	/// confined to and paint over unrelated chrome. Tree rows painting across the application tool bar were
	/// this.
	/// </summary>
	[NotInParallel]
	public class MidFrameLayoutClippingTests
	{
		/// <summary>
		/// What a widget's clip and screen rectangle were at the instant it painted.
		/// </summary>
		private sealed class PaintRecord
		{
			public string Name;
			public RectangleDouble Clip;
			public RectangleDouble Bounds;
		}

		[Test]
		public async Task ChildClipNeverEscapesTheClipItsParentPaintedUnder()
		{
			// Double buffered so the root can hand out a Graphics2D with no system window in play.
			var root = new GuiWidget(400, 600)
			{
				DoubleBuffer = true,
				Name = "Root"
			};

			// Across the top of the window - the chrome that must never be painted over.
			var toolbar = new GuiWidget(400, 40)
			{
				Name = "Toolbar",
				OriginRelativeParent = new Vector2(0, 560)
			};
			root.AddChild(toolbar);

			// Inset from every edge, so anything landing outside it is unambiguously an escape.
			var dialog = new GuiWidget(300, 400)
			{
				Name = "Dialog",
				OriginRelativeParent = new Vector2(50, 60)
			};
			root.AddChild(dialog);

			// Drawn first, and grows the dialog from inside the frame the dialog is already painting.
			var mutator = new GuiWidget(20, 20)
			{
				Name = "Mutator",
				OriginRelativeParent = new Vector2(10, 10)
			};
			dialog.AddChild(mutator);

			// Taller than the dialog: normally the dialog's clip is all that keeps it off the tool bar.
			var tallList = new GuiWidget(280, 800)
			{
				Name = "TallList",
				OriginRelativeParent = new Vector2(10, 0)
			};
			dialog.AddChild(tallList);

			var footer = new GuiWidget(280, 30)
			{
				Name = "Footer",
				OriginRelativeParent = new Vector2(10, 40)
			};
			dialog.AddChild(footer);

			var header = new GuiWidget(280, 30)
			{
				Name = "Header",
				OriginRelativeParent = new Vector2(10, 360)
			};
			dialog.AddChild(header);

			var painted = new List<PaintRecord>();
			var sampled = new[] { dialog, mutator, tallList, footer, header };
			var handlers = new List<(GuiWidget widget, System.EventHandler<DrawEventArgs> handler)>();

			foreach (var widget in sampled)
			{
				GuiWidget captured = widget;
				void Sample(object s, DrawEventArgs e)
				{
					var bounds = captured.LocalBounds;
					e.Graphics2D.GetTransform().transform(ref bounds);

					painted.Add(new PaintRecord
					{
						Name = captured.Name,
						Clip = e.Graphics2D.GetClippingRect(),
						Bounds = bounds
					});
				}

				widget.BeforeDraw += Sample;
				handlers.Add((widget, Sample));
			}

			// The mid frame layout change. The dialog grows upward while its own paint pass is in flight, so
			// its remaining children are clipped to the taller dialog even though the dialog itself is still
			// painting under the clip it was given at its old height.
			void GrowDialog(object s, DrawEventArgs e) => dialog.Height = 740;
			mutator.AfterDraw += GrowDialog;

			try
			{
				root.OnDraw(root.NewGraphics2D());
			}
			finally
			{
				mutator.AfterDraw -= GrowDialog;
				foreach (var (widget, handler) in handlers)
				{
					widget.BeforeDraw -= handler;
				}
			}

			PaintRecord dialogPaint = painted.FirstOrDefault(p => p.Name == "Dialog");
			await Assert.That(dialogPaint).IsNotNull()
				.Because("the dialog has to have painted for its clip to be the yardstick");

			// Everything the dialog painted after the mutation. These are the ones whose clips are worked out
			// against the new layout while the surface is still clipped to the old one.
			List<PaintRecord> afterMutation = painted
				.SkipWhile(p => p.Name != "Mutator")
				.Skip(1)
				.ToList();

			await Assert.That(afterMutation.Count).IsGreaterThanOrEqualTo(3)
				.Because("the assertions below are only meaningful if several widgets painted after the layout changed");

			// One pixel of slack for the outward rounding DrawChild does on the clip rectangle.
			const double tolerance = 1;
			RectangleDouble allowed = dialogPaint.Clip;

			var escapes = new StringBuilder();
			foreach (var record in afterMutation)
			{
				bool inside = record.Clip.Left >= allowed.Left - tolerance
					&& record.Clip.Bottom >= allowed.Bottom - tolerance
					&& record.Clip.Right <= allowed.Right + tolerance
					&& record.Clip.Top <= allowed.Top + tolerance;

				if (!inside)
				{
					escapes.AppendLine($"{record.Name} clip {Describe(record.Clip)} is outside the dialog's clip {Describe(allowed)} (widget at {Describe(record.Bounds)})");
				}
			}

			await Assert.That(escapes.ToString()).IsEqualTo(string.Empty)
				.Because("no descendant may be given a clip that reaches outside the clip its parent painted under");
		}

		private static string Describe(RectangleDouble rect)
		{
			return $"[L{rect.Left} B{rect.Bottom} R{rect.Right} T{rect.Top}]";
		}
	}
}
