/*
Copyright (c) 2025, Lars Brubaker
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
using HtmlAgilityPack;
using Markdig.Agg;
using Markdig.Renderers.Agg;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	[NotInParallel(MatterHackers.Agg.Tests.SharedStateKeys.MarkdownWidget)] // MarkdownWidget writes its static Theme and queues UiThread work
	public class MarkdownTableTests
	{
		[Test]
		public async Task PipeTablesRenderHeaderAndBodyCells()
		{
			var root = RenderMarkdown(
				"""
				| Name | Qty |
				| --- | ---: |
				| Bolt | 4 |
				| Nut | 8 |
				""");

			var tables = root.Descendants<AggTable>().ToList();
			await Assert.That(tables.Count).IsEqualTo(1);

			var rows = tables[0].Children.OfType<AggTableRow>().ToList();
			await Assert.That(rows.Count).IsEqualTo(3);
			await Assert.That(rows[0].IsHeadingRow).IsTrue();
			await Assert.That(rows.Skip(1).All(row => !row.IsHeadingRow)).IsTrue();
			await Assert.That(rows.Sum(row => row.Cells.Count)).IsEqualTo(6);
		}

		[Test]
		public async Task PipeTablesMeasureVisibleCellWidths()
		{
			var root = RenderMarkdown(
				"""
				| Feature | Status | Notes |
				| :--- | :---: | ---: |
				| Headings | Ready | 6 levels |
				| Tables | Ready | Pipe and grid |
				""");

			var table = root.Descendants<AggTable>().Single();
			root.Width = 800;
			root.PerformLayout();

			var cellWidths = table.Children
				.OfType<AggTableRow>()
				.SelectMany(row => row.Cells)
				.ToList();

			await Assert.That(cellWidths.All(cell => cell.ContentWidth > 0)).IsTrue();
			await Assert.That(cellWidths.All(cell => cell.Width > 40)).IsTrue();
		}

		[Test]
		public async Task GridTablesRenderAggTable()
		{
			var root = RenderMarkdown(
				"""
				+------+-----+
				| Name | Qty |
				+======+=====+
				| Bolt | 4   |
				+------+-----+
				| Nut  | 8   |
				+------+-----+
				""");

			await Assert.That(root.Descendants<AggTable>().Count()).IsEqualTo(1);
		}

		[Test]
		public async Task AlignmentMarkersApplyExpectedCellAnchors()
		{
			var root = RenderMarkdown(
				"""
				| Left | Center | Right |
				| :--- | :---: | ---: |
				| A | B | C |
				""");

			var headerRow = root
				.Descendants<AggTable>()
				.Single()
				.Children
				.OfType<AggTableRow>()
				.First();

			await Assert.That(headerRow.Cells[0].FlowHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(headerRow.Cells[1].FlowHAnchor).IsEqualTo(HAnchor.Center);
			await Assert.That(headerRow.Cells[2].FlowHAnchor).IsEqualTo(HAnchor.Right);
		}

		[Test]
		public async Task AlignmentMarkersPositionTextWithinCells()
		{
			var root = RenderMarkdown(
				"""
				| L | C | R |
				| :--- | :---: | ---: |
				| VeryWideLeftContent | VeryWideCenterContent | VeryWideRightContent |
				""");

			root.Width = 900;
			root.PerformLayout();

			var headerRow = root
				.Descendants<AggTable>()
				.Single()
				.Children
				.OfType<AggTableRow>()
				.First();

			var leftText = headerRow.Cells[0].Descendants<MarkdownTextWidget>().Single(widget => widget.Text == "L");
			var centerText = headerRow.Cells[1].Descendants<MarkdownTextWidget>().Single(widget => widget.Text == "C");
			var rightText = headerRow.Cells[2].Descendants<MarkdownTextWidget>().Single(widget => widget.Text == "R");

			var leftBounds = leftText.TransformToParentSpace(headerRow.Cells[0], leftText.LocalBounds);
			var centerBounds = centerText.TransformToParentSpace(headerRow.Cells[1], centerText.LocalBounds);
			var rightBounds = rightText.TransformToParentSpace(headerRow.Cells[2], rightText.LocalBounds);

			await Assert.That(leftBounds.Left).IsLessThan(centerBounds.Left);
			await Assert.That(centerBounds.Left).IsLessThan(rightBounds.Left);
		}

		[Test]
		public async Task EdgeAlignedCellsUseSymmetricInsets()
		{
			var root = RenderMarkdown(
				"""
				| Feature | Status | Notes |
				| --- | --- | ---: |
				| Headings | Ready | 6 levels |
				| Tables | Ready | Pipe and grid |
				""");

			root.Width = 900;
			root.PerformLayout();

			var headerRow = root
				.Descendants<AggTable>()
				.Single()
				.Children
				.OfType<AggTableRow>()
				.First();

			var leftText = headerRow.Cells[0].Descendants<MarkdownTextWidget>().Single(widget => widget.Text == "Feature");
			var rightText = headerRow.Cells[2].Descendants<MarkdownTextWidget>().Single(widget => widget.Text == "Notes");

			var leftBounds = leftText.TransformToParentSpace(headerRow.Cells[0], leftText.LocalBounds);
			var rightBounds = rightText.TransformToParentSpace(headerRow.Cells[2], rightText.LocalBounds);

			var leftInset = leftBounds.Left - headerRow.Cells[0].LocalBounds.Left;
			var rightInset = headerRow.Cells[2].LocalBounds.Right - rightBounds.Right;

			await Assert.That(System.Math.Abs(leftInset - rightInset)).IsLessThan(1);
		}

		[Test]
		public async Task PipeTablesRenderCellGridlines()
		{
			var root = RenderMarkdown(
				"""
				| Feature | Status |
				| --- | --- |
				| Tables | Ready |
				| Images | Local |
				""");

			var table = root
				.Descendants<AggTable>()
				.Single();
			var rows = table.Children.OfType<AggTableRow>().ToList();
			var cells = rows
				.SelectMany(row => row.Cells)
				.ToList();

			await Assert.That(cells.All(cell => cell.Border.Left > 0)).IsTrue();
			await Assert.That(table.Children.OfType<HorizontalLine>().Count()).IsGreaterThanOrEqualTo(rows.Count + 1);
		}

		[Test]
		public async Task PipeTablesRenderVisibleHorizontalSeparators()
		{
			var theme = new ThemeConfig();
			var markdownWidget = new MarkdownWidget(theme, scrollContent: false)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Markdown =
				"""
				| Feature | Status |
				| --- | --- |
				| Headings | Ready |
				| Tables | Ready |
				"""
			};
			var container = new GuiWidget(600, 220)
			{
				DoubleBuffer = true,
				BackgroundColor = Color.White
			};
			container.AddChild(markdownWidget);
			container.PerformLayout();
			container.BackBuffer.NewGraphics2D().Clear(Color.White);
			container.OnDraw(container.BackBuffer.NewGraphics2D());

			var table = markdownWidget.Descendants<AggTable>().Single();
			var rows = table.Children.OfType<AggTableRow>().ToList();
			var rowBounds = rows[1].TransformToParentSpace(container, rows[1].LocalBounds);
			var tableBounds = table.TransformToParentSpace(container, table.LocalBounds);
			var separatorY = (int)System.Math.Round(rowBounds.Top);
			var foundSeparatorPixel = false;

			for (int y = separatorY - 2; y <= separatorY + 2 && !foundSeparatorPixel; y++)
			{
				for (int x = (int)System.Math.Round(tableBounds.Left) + 5; x <= (int)System.Math.Round(tableBounds.Right) - 5; x++)
				{
					if (container.BackBuffer.GetPixel(x, y) != Color.White)
					{
						foundSeparatorPixel = true;
						break;
					}
				}
			}

			await Assert.That(foundSeparatorPixel).IsTrue();
		}

		[Test]
		public async Task PipeTablesUseStripingBoldHeadersAndSemiTransparentBorders()
		{
			var theme = new ThemeConfig();
			var root = new GuiWidget();
			var document = new AggMarkdownDocument
			{
				Markdown =
				"""
				| Feature | Status |
				| --- | --- |
				| Headings | Ready |
				| Tables | Ready |
				| Images | Ready |
				"""
			};

			document.Parse(theme, root);

			var rows = root
				.Descendants<AggTable>()
				.Single()
				.Children
				.OfType<AggTableRow>()
				.ToList();
			var allCells = rows.SelectMany(row => row.Cells).ToList();

			await Assert.That(rows[0].BackgroundColor.Alpha0To255).IsEqualTo(0);
			await Assert.That(rows[2].BackgroundColor.Alpha0To255).IsGreaterThan(0);
			await Assert.That(rows[0].Descendants<TextWidget>().Where(widget => widget.Text == "Feature" || widget.Text == "Status").All(widget => widget.Bold)).IsTrue();
			await Assert.That(allCells.All(cell => cell.BorderColor == new Color(theme.TextColor, 150))).IsTrue();
		}

		[Test]
		public async Task StyledHtmlUsesStripedRowsBoldHeadersAndNoHeaderBackgroundFill()
		{
			var html = AggMarkdownDocument.ToStyledHtml(
				"""
				| Left | Center | Right |
				| :--- | :---: | ---: |
				| A | B | C |
				| D | E | F |
				""",
				new ThemeConfig());

			var document = new HtmlDocument();
			document.LoadHtml(html);

			var headerCell = document.DocumentNode.SelectSingleNode("//th");
			var bodyRows = document.DocumentNode.SelectNodes("//tbody/tr");

			await Assert.That(headerCell).IsNotNull();
			await Assert.That(bodyRows?.Count).IsEqualTo(2);
			await Assert.That(headerCell.GetAttributeValue("style", string.Empty)).Contains("font-weight:700");
			await Assert.That(headerCell.GetAttributeValue("style", string.Empty)).Contains("text-align:left");
			await Assert.That(headerCell.GetAttributeValue("style", string.Empty)).DoesNotContain("background-color");
			await Assert.That(bodyRows[1].GetAttributeValue("style", string.Empty)).Contains("background-color");
		}

		[Test]
		public async Task InlineMarkdownInsideCellsStillRenders()
		{
			var root = RenderMarkdown(
				"""
				| Item | Notes |
				| --- | --- |
				| Bolt | **Strong** text |
				| Docs | [Guide](https://example.com) |
				""");

			var table = root.Descendants<AggTable>().Single();

			await Assert.That(table.Descendants<TextLinkX>().Count()).IsEqualTo(1);
			await Assert.That(table.Descendants<TextWidget>().Any(widget => widget.Text == "Strong" && widget.Bold)).IsTrue();
		}

		[Test]
		public async Task ParagraphsAroundTablesStillRender()
		{
			var root = RenderMarkdown(
				"""
				Intro paragraph.

				| Name | Qty |
				| --- | --- |
				| Bolt | 4 |

				Outro paragraph.
				""");

			var textWidgets = root.Descendants<TextWidget>().ToList();
			await Assert.That(root.Descendants<AggTable>().Count()).IsEqualTo(1);
			await Assert.That(textWidgets.Any(widget => widget.Text == "Intro")).IsTrue();
			await Assert.That(textWidgets.Any(widget => widget.Text == "Outro")).IsTrue();
		}

		[Test]
		public async Task InvalidTableSyntaxDoesNotRenderAggTable()
		{
			var root = RenderMarkdown(
				"""
				| Name | Qty |
				| Bolt | 4 |
				""");

			await Assert.That(root.Descendants<AggTable>().Any()).IsFalse();
		}

		[Test]
		public async Task MatchingTextPipelineStillRendersTables()
		{
			var root = RenderMarkdown(
				"""
				| Name | Qty |
				| --- | --- |
				| Bolt | 4 |
				| Nut | 8 |
				""",
				matchingText: "Bolt");

			await Assert.That(root.Descendants<AggTable>().Count()).IsEqualTo(1);
		}

		[Test]
		public async Task TableStretchesToContainerWidthAfterLayout()
		{
			var root = RenderMarkdown(
				"""
				| Name | Qty |
				| --- | ---: |
				| Bolt | 4 |
				| Nut | 8 |
				""");

			root.Width = 800;
			root.PerformLayout();

			var table = root.Descendants<AggTable>().Single();

			await Assert.That(table.HAnchor).IsEqualTo(HAnchor.Stretch);
			await Assert.That(table.Width).IsGreaterThan(0);
			await Assert.That(table.Height).IsGreaterThan(0);
		}

		private static GuiWidget RenderMarkdown(string markdown, string matchingText = null)
		{
			var root = new GuiWidget();
			var document = new AggMarkdownDocument
			{
				Markdown = markdown,
				MatchingText = matchingText
			};

			document.Parse(new ThemeConfig(), root);

			return root;
		}
	}
}
