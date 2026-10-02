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
using Markdig.Renderers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// GitHub-style alignment HTML (the form the markdown editor stores) renders aligned in the viewer.
	/// </summary>
	public class MarkdownAlignmentTests
	{
		[Test]
		public async Task DivAlignPairAlignsTheBlocksBetween()
		{
			var root = RenderMarkdown(
				"""
				Before

				<div align="center">

				# Title

				Centered

				</div>

				<div align="right">

				Righted

				</div>

				After
				""");

			await Assert.That(RowContaining<HeadingRowX>(root, "Title").ContentHAnchor).IsEqualTo(HAnchor.Center);
			await Assert.That(RowContaining<ParagraphX>(root, "Centered").ContentHAnchor).IsEqualTo(HAnchor.Center);
			await Assert.That(RowContaining<ParagraphX>(root, "Righted").ContentHAnchor).IsEqualTo(HAnchor.Right);
			await Assert.That(RowContaining<ParagraphX>(root, "Before").ContentHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(RowContaining<ParagraphX>(root, "After").ContentHAnchor).IsEqualTo(HAnchor.Left);
		}

		[Test]
		public async Task CenteredWordSitsRightOfALeftAlignedOne()
		{
			var root = RenderMarkdown(
				"""
				Left

				<div align="center">

				Middle

				</div>

				<div align="right">

				Edge

				</div>
				""");

			root.Width = 600;
			root.PerformLayout();

			var left = WordX(root, "Left");
			var middle = WordX(root, "Middle");
			var edge = WordX(root, "Edge");

			await Assert.That(middle).IsGreaterThan(left + 100);
			await Assert.That(edge).IsGreaterThan(middle + 100);
		}

		[Test]
		public async Task UnmatchedTagsDegradeGracefully()
		{
			var root = RenderMarkdown(
				"""
				</div>

				Plain

				<div>

				<div align="center">

				Inner

				</div>

				StillPlain

				<div align="right">

				ToTheEnd
				""");

			await Assert.That(RowContaining<ParagraphX>(root, "Plain").ContentHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(RowContaining<ParagraphX>(root, "Inner").ContentHAnchor).IsEqualTo(HAnchor.Center);
			// The inner </div> closes the centered div, not the outer plain one.
			await Assert.That(RowContaining<ParagraphX>(root, "StillPlain").ContentHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(RowContaining<ParagraphX>(root, "ToTheEnd").ContentHAnchor).IsEqualTo(HAnchor.Right);
		}

		[Test]
		public async Task OneLineAlignedElementsRenderTheirText()
		{
			var root = RenderMarkdown(
				"""
				<p align="center">Hello &amp; <b>world</b></p>

				<h2 align="right">Title</h2>

				<p>NotShown</p>
				""");

			var paragraph = RowContaining<ParagraphX>(root, "Hello");
			await Assert.That(paragraph.ContentHAnchor).IsEqualTo(HAnchor.Center);
			await Assert.That(paragraph.Descendants<TextWidget>().Any(t => t.Text == "&")).IsTrue();
			await Assert.That(paragraph.Descendants<TextWidget>().Any(t => t.Text == "world")).IsTrue();

			var heading = RowContaining<HeadingRowX>(root, "Title");
			await Assert.That(heading.ContentHAnchor).IsEqualTo(HAnchor.Right);
			await Assert.That(heading.Level).IsEqualTo(2);

			// HTML other than alignment keeps showing nothing.
			await Assert.That(root.Descendants<TextWidget>().Any(t => t.Text.Contains("NotShown"))).IsFalse();
		}

		[Test]
		public async Task OneLineAlignedDivShowsItsText()
		{
			// AI-written notes often center a line as a one-line div; it used to render as nothing.
			var root = RenderMarkdown("<div align=\"center\">Centered: version 3</div>\n");

			var paragraph = RowContaining<ParagraphX>(root, "Centered:");
			await Assert.That(paragraph.ContentHAnchor).IsEqualTo(HAnchor.Center);
		}

		[Test]
		public async Task MarkupOnlyElementAddsNoEmptyRow()
		{
			var root = RenderMarkdown("<p align=\"center\"><img src=\"logo.png\"></p>");

			await Assert.That(root.Descendants<ParagraphX>().Any()).IsFalse();
		}

		[Test]
		public async Task DataAlignAttributeDoesNotAlign()
		{
			var root = RenderMarkdown(
				"""
				<div data-align="center">

				Plain

				</div>

				<p data-align="center">NotShown</p>
				""");

			await Assert.That(RowContaining<ParagraphX>(root, "Plain").ContentHAnchor).IsEqualTo(HAnchor.Left);
			await Assert.That(root.Descendants<TextWidget>().Any(t => t.Text == "NotShown")).IsFalse();
		}

		[Test]
		public async Task TwoElementsOnOneLineAreNotGluedTogether()
		{
			var root = RenderMarkdown("<p align=\"center\">a</p><p>b</p>");

			await Assert.That(root.Descendants<TextWidget>().Any(t => t.Text.Contains("b"))).IsFalse();
		}

		private static T RowContaining<T>(GuiWidget root, string word)
			where T : GuiWidget
		{
			return root.Descendants<T>().Single(row => row.Descendants<TextWidget>().Any(text => text.Text == word));
		}

		private static double WordX(GuiWidget root, string word)
		{
			var widget = root.Descendants<TextWidget>().Single(text => text.Text == word);
			return widget.TransformToParentSpace(root, Vector2.Zero).X;
		}

		private static GuiWidget RenderMarkdown(string markdown)
		{
			var root = new GuiWidget();
			var document = new AggMarkdownDocument
			{
				Markdown = markdown
			};

			document.Parse(new ThemeConfig(), root);
			return root;
		}
	}
}
