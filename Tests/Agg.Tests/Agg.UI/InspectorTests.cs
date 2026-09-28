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
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The widget inspector (agg-gui's InspectorNode / InspectorPanel): the tree snapshot, expansion,
	/// selection across refreshes, and the panel's hover, click and highlight.</summary>
	/// <remarks>
	/// Serialized against everything that pumps <see cref="UiThread"/>: every InspectorPanel queues its refresh
	/// timer and its edits on UiThread's process-wide idle queue, and a window another test has open drains that
	/// queue from its own thread. Unserialized, that pump applied a clicked edit before the test's own
	/// ApplyPendingEdits could check it was still pending, and could re-collect a panel's model mid-assert.
	/// </remarks>
	[NotInParallel(SharedStateKeys.UiThreadAndKeyboard)]
	public class InspectorTests
	{
		/// <summary>A page with a box (margin and padding) holding a leaf, a hidden widget and a disabled one.</summary>
		private static (GuiWidget Page, GuiWidget Box, GuiWidget Leaf) SampleTree()
		{
			var page = new GuiWidget(600, 400) { Name = "Page" };
			var box = new GuiWidget(200, 100)
			{
				Name = "Box",
				Position = new Vector2(20, 30),
				Margin = new BorderDouble(4),
				Padding = new BorderDouble(6),
			};
			page.AddChild(box);
			var leaf = new GuiWidget(60, 20) { Name = "Leaf", Position = new Vector2(10, 5) };
			box.AddChild(leaf);
			page.AddChild(new GuiWidget(10, 10) { Name = "Hidden", Visible = false });
			page.AddChild(new TextWidget("Off") { Name = "Disabled", Enabled = false });
			return (page, box, leaf);
		}

		[Test]
		public async Task CollectSnapshotsEveryWidgetDepthFirstInRootCoordinates()
		{
			var (page, box, leaf) = SampleTree();

			var nodes = InspectorModel.Collect(page);

			await Assert.That(nodes.Select(n => n.Name).ToArray()).IsEquivalentTo(new[] { "Page", "Box", "Leaf", "Hidden", "Disabled" });
			await Assert.That(nodes.Select(n => n.Depth).ToArray()).IsEquivalentTo(new[] { 0, 1, 2, 1, 1 });
			InspectorNode leafNode = nodes[2];
			await Assert.That(leafNode.Widget).IsEqualTo(leaf);
			await Assert.That(leafNode.TypeName).IsEqualTo("GuiWidget");
			await Assert.That(leafNode.Path).IsEquivalentTo(new[] { 0, 0 });
			await Assert.That(leafNode.ScreenBounds).IsEqualTo(new RectangleDouble(30, 35, 90, 55));
			await Assert.That(nodes[1].ChildCount).IsEqualTo(1);
			await Assert.That(nodes[1].Padding).IsEqualTo(new BorderDouble(6));
			await Assert.That(nodes[3].Visible).IsFalse();
			await Assert.That(nodes[4].Enabled).IsFalse();
			await Assert.That(nodes[4].Label).IsEqualTo("TextWidget \"Disabled\"");
		}

		[Test]
		public async Task RowsFollowExpansionWithEveryNodeExpandedAtFirst()
		{
			var (page, box, _) = SampleTree();
			var model = new InspectorModel(page);

			// agg-gui opens every node it has not seen before.
			await Assert.That(model.VisibleRows().Select(n => n.Name).ToArray()).IsEquivalentTo(new[] { "Page", "Box", "Leaf", "Hidden", "Disabled" });

			model.SetExpanded(model.NodeOf(box), false);
			await Assert.That(model.VisibleRows().Select(n => n.Name).ToArray()).IsEquivalentTo(new[] { "Page", "Box", "Hidden", "Disabled" });

			// A widget added later starts open too.
			var added = new GuiWidget(5, 5) { Name = "Added" };
			added.AddChild(new GuiWidget(2, 2) { Name = "Inner" });
			page.AddChild(added);
			model.Refresh();
			await Assert.That(model.VisibleRows().Select(n => n.Name).ToArray()).Contains("Inner");

			model.SetExpanded(model.NodeOf(page), false);
			await Assert.That(model.VisibleRows().Select(n => n.Name).ToArray()).IsEquivalentTo(new[] { "Page" });
			model.SetExpanded(model.NodeOf(box), true);
			model.SetExpanded(model.NodeOf(page), true);
			await Assert.That(model.VisibleRows().Select(n => n.Name).ToArray()).Contains("Leaf");
		}

		[Test]
		public async Task RefreshFollowsTheLiveTreeAndKeepsTheSelectionByWidget()
		{
			var (page, box, leaf) = SampleTree();
			var model = new InspectorModel(page);
			model.Select(model.NodeOf(leaf));
			await Assert.That(model.IsExpanded(model.NodeOf(box))).IsTrue().Because("selecting reveals the row");

			await Assert.That(model.Refresh()).IsFalse().Because("nothing changed");

			// A move is picked up and the selection follows the widget to its new node.
			box.Position = new Vector2(100, 30);
			await Assert.That(model.Refresh()).IsTrue();
			await Assert.That(model.Selected.Widget).IsEqualTo(leaf);
			await Assert.That(model.Selected.ScreenBounds.Left).IsEqualTo(110);

			page.AddChild(new GuiWidget(5, 5) { Name = "Added" });
			model.Refresh();
			await Assert.That(model.Nodes.Last().Name).IsEqualTo("Added");

			// A removed widget takes its selection with it.
			box.RemoveChild(leaf);
			model.Refresh();
			await Assert.That(model.Selected).IsNull();
		}

		[Test]
		public async Task HeaderIsATintedBarTitledWidgetInspectorWithTheCountAtItsRight()
		{
			var (page, box, leaf) = SampleTree();
			var panel = new InspectorPanel(page)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = new Vector2(340, 10),
				Size = new Vector2(250, 380),
				Style = new InspectorStyle { HeaderBackground = new Color(1, 2, 3) },
			};
			page.AddChild(panel);
			page.PerformLayout();
			panel.RefreshNow();

			GuiWidget header = panel.FindDescendant("Inspector Header");
			var title = (TextWidget)panel.FindDescendant("Inspector Title");
			var count = (TextWidget)panel.FindDescendant("Inspector Widget Count");
			await Assert.That(title.Text).IsEqualTo("Widget Inspector");
			await Assert.That(count.Text).IsEqualTo($"{panel.Model.Nodes.Count} widgets");
			await Assert.That(header.BackgroundColor).IsEqualTo(new Color(1, 2, 3));
			await Assert.That(header.Height).IsEqualTo(InspectorPanel.HeaderHeight * GuiWidget.DeviceScale);
			await Assert.That(header.Width).IsEqualTo(panel.Width - panel.Padding.Width);
			await Assert.That(count.BoundsRelativeToParent.Right).IsGreaterThan(header.Width - 11 * GuiWidget.DeviceScale).Because("the count sits at the right");
			await Assert.That(title.BoundsRelativeToParent.Left).IsLessThan(13 * GuiWidget.DeviceScale);
		}

		[Test]
		public async Task HoveringARowHighlightsItsWidgetAndClickingSelectsOrExpands()
		{
			var (page, box, leaf) = SampleTree();
			var panel = new InspectorPanel(page)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = new Vector2(340, 10),
				Size = new Vector2(250, 380),
			};
			page.AddChild(panel);
			page.PerformLayout();
			panel.RefreshNow();
			InspectorTreeView tree = panel.Tree;

			Vector2 RowPoint(string name, double x = -1)
			{
				int index = tree.Rows.ToList().FindIndex(n => n.Name == name);
				RectangleDouble row = tree.RowBounds(index);
				return tree.TransformToParentSpace(page, new Vector2(x < 0 ? row.Center.X : x, row.Center.Y));
			}

			// Hover the Box row: the model hovers Box and the page draws the blue bounds band over it.
			var boxRow = RowPoint("Box");
			page.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, boxRow.X, boxRow.Y, 0));
			await Assert.That(panel.Model.Hovered?.Widget).IsEqualTo(box);

			var image = new ImageBuffer(600, 400);
			page.OnDraw(image.NewGraphics2D());
			Color inBox = image.GetPixel(120, 80);
			await Assert.That(inBox.blue).IsGreaterThan(inBox.red).Because("the bounds band is blue");
			await Assert.That(image.GetPixel(22, 32).green).IsGreaterThan(inBox.green).Because("the padding band adds green inside the edge");
			await Assert.That(image.GetPixel(18, 80).red).IsGreaterThan((byte)0).Because("the margin band is amber");

			// Box starts open; its triangle closes and reopens it. The row's label selects.
			var boxTriangle = RowPoint("Box", tree.TriangleRight(panel.Model.NodeOf(box)) - 3);
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, boxTriangle.X, boxTriangle.Y, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, boxTriangle.X, boxTriangle.Y, 0));
			await Assert.That(tree.Rows.Select(n => n.Name)).DoesNotContain("Leaf");
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, boxTriangle.X, boxTriangle.Y, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, boxTriangle.X, boxTriangle.Y, 0));
			await Assert.That(tree.Rows.Select(n => n.Name)).Contains("Leaf");
			await Assert.That(panel.Model.Selected).IsNull();

			var leafRow = RowPoint("Leaf");
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, leafRow.X, leafRow.Y, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, leafRow.X, leafRow.Y, 0));
			await Assert.That(panel.Model.Selected?.Widget).IsEqualTo(leaf);
			await Assert.That(InspectorPropertiesView.PropertiesOf(panel.Model.Selected)).Contains(("depth", "2"));

			// Leaving the tree clears the hover; the selection keeps its outline.
			page.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 100, 300, 0));
			await Assert.That(panel.Model.Hovered).IsNull();
			page.Close();
		}

		[Test]
		public async Task EditsApplyEveryFieldAndStopAtZero()
		{
			var (page, box, leaf) = SampleTree();
			await Assert.That(new InspectorEdit(box, InspectorEditField.MarginLeft, 9).Apply()).IsTrue();
			new InspectorEdit(box, InspectorEditField.PaddingTop, -3).Apply();
			new InspectorEdit(box, InspectorEditField.HAnchor, (int)HAnchor.Stretch).Apply();
			new InspectorEdit(box, InspectorEditField.VAnchor, (int)VAnchor.Top).Apply();
			new InspectorEdit(leaf, InspectorEditField.Visible, 0).Apply();
			new InspectorEdit(leaf, InspectorEditField.Width, 75).Apply();

			await Assert.That(box.Margin).IsEqualTo(new BorderDouble(9, 4, 4, 4));
			await Assert.That(box.Padding).IsEqualTo(new BorderDouble(6, 6, 6, 0)).Because("a size never goes below zero");
			await Assert.That(box.HAnchor).IsEqualTo(HAnchor.Stretch);
			await Assert.That(box.VAnchor).IsEqualTo(VAnchor.Top);
			await Assert.That(leaf.Visible).IsFalse();
			await Assert.That(leaf.Width).IsEqualTo(75);
			await Assert.That(InspectorEdit.NextHAnchor(HAnchor.Right)).IsEqualTo(HAnchor.Absolute);
			await Assert.That(InspectorEdit.NextVAnchor(VAnchor.Absolute)).IsEqualTo(VAnchor.Fit);

			leaf.Close();
			await Assert.That(new InspectorEdit(leaf, InspectorEditField.Visible, 1).Apply()).IsFalse().Because("a closed widget is left alone");
			page.Close();
		}

		[Test]
		public async Task ClickingAValueQueuesAnEditThatLandsOnIdle()
		{
			var (page, box, _) = SampleTree();
			page.Height = 700;
			var panel = new InspectorPanel(page)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = new Vector2(340, 10),
				Size = new Vector2(250, 680),
			};
			page.AddChild(panel);
			page.PerformLayout();
			panel.RefreshNow();
			panel.Model.Select(panel.Model.NodeOf(box));
			InspectorPropertiesView properties = panel.Properties;
			// every row shows, down to children, once the split gives the pane more than its default
			panel.PropertiesHeight = 400;
			page.PerformLayout();

			void ClickValue(string row, bool rightHalf)
			{
				int index = InspectorPropertiesView.RowsOf(panel.Model.Selected).FindIndex(r => r.Name == row);
				RectangleDouble strip = properties.ValueBounds(index);
				var local = new Vector2(rightHalf ? strip.Right - 3 : strip.Left + 3, strip.YCenter);
				Vector2 point = properties.TransformToParentSpace(page, local);
				page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
				page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
			}

			// The right half of margin.left steps it up by agg-gui's inset step; nothing changes until the idle.
			ClickValue("margin.left", rightHalf: true);
			await Assert.That(panel.PendingEdits.Count).IsEqualTo(1);
			await Assert.That(box.Margin.Left).IsEqualTo(4);
			panel.ApplyPendingEdits();
			await Assert.That(box.Margin.Left).IsEqualTo(4 + InspectorPropertyRow.InsetStep(4));
			await Assert.That(InspectorPropertiesView.PropertiesOf(panel.Model.Selected)).Contains(("margin.left", "6.5"))
				.Because("the pane re-collects after the edit lands");

			// The left half steps down; an anchor picks the next in the cycle; visible toggles.
			ClickValue("margin.top", rightHalf: false);
			ClickValue("h_anchor", rightHalf: false);
			ClickValue("visible", rightHalf: true);
			panel.ApplyPendingEdits();
			await Assert.That(box.Margin.Top).IsEqualTo(4 - InspectorPropertyRow.InsetStep(4));
			await Assert.That(box.HAnchor).IsEqualTo(InspectorEdit.NextHAnchor(HAnchor.Absolute));
			await Assert.That(box.Visible).IsFalse();

			// A read-only row (x, and padding, as agg-gui's) queues nothing.
			ClickValue("x", rightHalf: true);
			ClickValue("pad.right", rightHalf: true);
			await Assert.That(panel.PendingEdits.Count).IsEqualTo(0);
			page.Close();
		}

		/// <summary>The sample page with an inspector panel down its right side, laid out and collected.</summary>
		private static (GuiWidget Page, GuiWidget Box, GuiWidget Leaf, InspectorPanel Panel) PageWithPanel()
		{
			var (page, box, leaf) = SampleTree();
			var panel = new InspectorPanel(page)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = new Vector2(340, 10),
				Size = new Vector2(250, 380),
			};
			page.AddChild(panel);
			page.PerformLayout();
			panel.RefreshNow();
			return (page, box, leaf, panel);
		}

		[Test]
		public async Task DraggingTheSplitResizesThePanesDownToTheirMinimums()
		{
			var (page, _, _, panel) = PageWithPanel();
			InspectorSplitBar split = panel.SplitBar;
			await Assert.That(panel.PropertiesHeight).IsEqualTo(InspectorPanel.DefaultPropertiesHeight);
			// agg-gui's 6-unit gap: the properties end 2 below the line and the tree starts 4 above it.
			await Assert.That(split.Height).IsEqualTo(InspectorSplitBar.LogicalGap);
			await Assert.That(panel.Properties.Height).IsEqualTo(InspectorPanel.DefaultPropertiesHeight - InspectorSplitBar.LogicalLine);

			// A press 4 below the line is past the gap, in the properties pane, yet inside agg-gui's 5-unit grab zone.
			Vector2 below = split.TransformToParentSpace(page, new Vector2(100, InspectorSplitBar.LogicalLine - 4));
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, below.X, below.Y, 0));
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, below.X, below.Y - 10, 0));
			await Assert.That(panel.PropertiesHeight).IsEqualTo(InspectorPanel.DefaultPropertiesHeight - 4 - 10).Within(.001);
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, below.X, below.Y - 10, 0));
			panel.PropertiesHeight = InspectorPanel.DefaultPropertiesHeight;
			page.PerformLayout();

			// Grab 4 units above the line (past the gap into the tree) and drag up 40: the line moves 40.
			Vector2 grab = split.TransformToParentSpace(page, new Vector2(100, InspectorSplitBar.LogicalLine + 4));
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, grab.X, grab.Y, 0));
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, grab.X, grab.Y + 40, 0));
			await Assert.That(panel.PropertiesHeight).IsEqualTo(InspectorPanel.DefaultPropertiesHeight + 4 + 40).Within(.001);

			// Past either end each pane keeps 60.
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, grab.X, -500, 0));
			await Assert.That(panel.PropertiesHeight).IsEqualTo(InspectorPanel.MinPaneHeight);
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, grab.X, 5000, 0));
			await Assert.That(panel.PropertiesHeight).IsEqualTo(panel.ListAreaHeight - InspectorPanel.MinPaneHeight);
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, grab.X, 5000, 0));
			page.PerformLayout();
			await Assert.That(panel.Tree.Height).IsEqualTo(InspectorPanel.MinPaneHeight - (InspectorSplitBar.LogicalGap - InspectorSplitBar.LogicalLine));

			// Released, moving the mouse no longer drags.
			page.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, grab.X, 100, 0));
			await Assert.That(panel.PropertiesHeight).IsEqualTo(panel.ListAreaHeight - InspectorPanel.MinPaneHeight);
			page.Close();
		}

		[Test]
		public async Task RowsKeepAggGuiOrderAndTheBoxPreviewShowsTheWidgetShape()
		{
			var (page, box, leaf, panel) = PageWithPanel();
			panel.Model.Select(panel.Model.NodeOf(box));
			List<InspectorPropertyRow> rows = InspectorPropertiesView.RowsOf(panel.Model.Selected);
			await Assert.That(rows.Select(r => r.Name).ToArray()).IsEquivalentTo(new[]
			{
				"x", "y", "width", "height", "depth",
				"margin.left", "margin.right", "margin.top", "margin.bottom",
				"pad.left", "pad.right", "pad.top", "pad.bottom",
				"h_anchor", "v_anchor", "visible", "enabled", "children",
			});

			// agg-gui's tints by kind; padding is read-only.
			InspectorValueKind KindOf(string name) => rows.First(r => r.Name == name).Kind;
			await Assert.That(KindOf("margin.top")).IsEqualTo(InspectorValueKind.Margin);
			await Assert.That(KindOf("pad.top")).IsEqualTo(InspectorValueKind.Padding);
			await Assert.That(rows.First(r => r.Name == "pad.top").Field).IsNull();
			await Assert.That(KindOf("h_anchor")).IsEqualTo(InspectorValueKind.Anchor);
			await Assert.That(KindOf("visible")).IsEqualTo(InspectorValueKind.Flag);

			// A widget with no padding has no padding rows.
			await Assert.That(InspectorPropertiesView.RowsOf(panel.Model.NodeOf(leaf)).Select(r => r.Name)).DoesNotContain("pad.left");

			// The default pane has no room below the rows; a tall one draws the 2:1 box, blue at its centre.
			await Assert.That(panel.Properties.BoxPreviewBounds()).IsNull();
			panel.PropertiesHeight = 1000;
			page.PerformLayout();
			await Assert.That(panel.Properties.BoxPreviewBounds()).IsNull().Because("the panel is too short for the room");

			page.Height = 700;
			panel.Height = 680;
			page.PerformLayout();
			RectangleDouble preview = panel.Properties.BoxPreviewBounds().Value;
			await Assert.That(preview.Width).IsEqualTo(preview.Height * 2).Within(.001);

			var image = new ImageBuffer(600, 700);
			page.OnDraw(image.NewGraphics2D());
			Vector2 inBox = panel.Properties.TransformToParentSpace(page, new Vector2(preview.Left + 3, preview.YCenter));
			Color inside = image.GetPixel((int)inBox.X, (int)inBox.Y);
			Vector2 beside = panel.Properties.TransformToParentSpace(page, new Vector2(preview.Left - 3, preview.YCenter));
			Color outside = image.GetPixel((int)beside.X, (int)beside.Y);
			await Assert.That(inside.blue - inside.red).IsGreaterThan(outside.blue - outside.red).Because("the preview is tinted blue");
			page.Close();
		}

		[Test]
		public async Task ArrowKeysMoveTheSelectionAndExpandAsAggGuiTreeView()
		{
			var (page, box, leaf, panel) = PageWithPanel();
			InspectorTreeView tree = panel.Tree;
			InspectorModel model = panel.Model;
			void Press(Keys key) => page.OnKeyDown(new KeyEventArgs(key));

			// Clicking the Page row selects it and gives the tree the keys.
			RectangleDouble pageRow = tree.RowBounds(0);
			Vector2 click = tree.TransformToParentSpace(page, new Vector2(pageRow.Center.X, pageRow.Center.Y));
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, click.X, click.Y, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, click.X, click.Y, 0));
			await Assert.That(tree.Focused).IsTrue();

			Press(Keys.Down);
			await Assert.That(model.Selected.Widget).IsEqualTo(box);
			Press(Keys.Right);
			await Assert.That(model.Selected.Widget).IsEqualTo(leaf).Because("right on an expanded row moves down");
			Press(Keys.Left);
			await Assert.That(model.Selected.Widget).IsEqualTo(box).Because("left on a leaf moves to its parent");
			Press(Keys.Left);
			await Assert.That(model.IsExpanded(model.Selected)).IsFalse().Because("left collapses an expanded row");
			Press(Keys.Right);
			await Assert.That(model.IsExpanded(model.Selected)).IsTrue().Because("right expands a collapsed row");
			await Assert.That(model.Selected.Widget).IsEqualTo(box);
			Press(Keys.Enter);
			await Assert.That(model.IsExpanded(model.Selected)).IsFalse().Because("enter toggles");
			Press(Keys.Up);
			await Assert.That(model.Selected.Widget).IsEqualTo(page);
			Press(Keys.Up);
			await Assert.That(model.Selected.Widget).IsEqualTo(page).Because("the cursor stops at the first row");
			page.Close();
		}

		[Test]
		public async Task SavedStateRestoresExpansionSelectionAndSplitByPosition()
		{
			var (page, box, leaf, panel) = PageWithPanel();
			panel.Model.Select(panel.Model.NodeOf(leaf));
			panel.Model.SetExpanded(panel.Model.NodeOf(page), false);
			panel.PropertiesHeight = 150;
			InspectorSavedState saved = panel.SavedState;
			// the panel itself is in the page, after the sample's five widgets; everything else starts open
			await Assert.That(saved.Expanded.Take(5).ToArray()).IsEquivalentTo(new[] { false, true, true, true, true });
			await Assert.That(saved.Selected).IsEqualTo(2);
			page.Close();

			// A new run: new widgets, same shape. The state waits for the first collection.
			var (nextPage, nextBox, nextLeaf) = SampleTree();
			var next = new InspectorPanel(nextPage) { HAnchor = HAnchor.Absolute, VAnchor = VAnchor.Absolute, Size = new Vector2(250, 380) };
			int changes = 0;
			next.SavedStateChanged += (s, e) => changes++;
			next.ApplySavedState(saved);
			await Assert.That(next.PropertiesHeight).IsEqualTo(150);
			nextPage.AddChild(next);
			next.RefreshNow();
			await Assert.That(next.Model.Selected?.Widget).IsEqualTo(nextLeaf);
			await Assert.That(next.Model.IsExpanded(next.Model.NodeOf(nextPage))).IsFalse();
			await Assert.That(next.Model.IsExpanded(next.Model.NodeOf(nextBox))).IsTrue();
			await Assert.That(changes).IsGreaterThan(0).Because("an app saving on change hears about it");
			nextPage.Close();
		}
	}
}
