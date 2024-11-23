/*
Copyright(c) 2024, Lars Brubaker, John Lewin
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using System.Runtime.CompilerServices;
using Markdig.Agg;
using Markdig.Helpers;
using Markdig.Renderers.Agg;
using Markdig.Renderers.Agg.Inlines;
using Markdig.Syntax;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers
{
	public class TextWordX : TextWidget
	{
		public TextWordX(ThemeConfig theme)
			: base("", pointSize: 10, textColor: theme.TextColor)
		{
			this.AutoExpandBoundsToText = true;
		}
	}

	public class TextSpaceX : TextWidget, ISkipIfFirst
	{
		public TextSpaceX(ThemeConfig theme)
			: base("", pointSize: 10, textColor: theme.TextColor)
		{
			this.AutoExpandBoundsToText = true;
		}
	}

	public class LineBreakX : GuiWidget, IHardBreak
	{
		public LineBreakX()
		{
		}
	}

	/// <summary>
	/// Agg renderer for a Markdown <see cref="AggMarkdownDocument"/> object.
	/// </summary>
	/// <seealso cref="RendererBase" />
	public class AggRenderer : RendererBase
	{
		private readonly Stack<GuiWidget> stack = new Stack<GuiWidget>();
		private char[] buffer;
		private ThemeConfig theme;

		public GuiWidget RootWidget { get; }

		public string BaseUri { get; set; } = "https://www.matterhackers.com/";

		public List<MarkdownDocumentLink> ChildLinks { get; internal set; }

		public AggRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public AggRenderer(GuiWidget rootWidget, ThemeConfig theme)
		{
			this.theme = theme;

			buffer = new char[1024];
			RootWidget = rootWidget;

			stack.Push(rootWidget);

			// Default block renderers
			ObjectRenderers.Add(new AggCodeBlockRenderer(theme));
			ObjectRenderers.Add(new AggListRenderer(theme));
			ObjectRenderers.Add(new AggHeadingRenderer());
			ObjectRenderers.Add(new AggParagraphRenderer());
			ObjectRenderers.Add(new AggQuoteBlockRenderer());
			ObjectRenderers.Add(new AggThematicBreakRenderer());

			// Default inline renderers
			ObjectRenderers.Add(new AggAutolinkInlineRenderer());
			ObjectRenderers.Add(new AggCodeInlineRenderer(theme));
			ObjectRenderers.Add(new AggDelimiterInlineRenderer());
			ObjectRenderers.Add(new AggEmphasisInlineRenderer());
			ObjectRenderers.Add(new AggLineBreakInlineRenderer());
			ObjectRenderers.Add(new AggLinkInlineRenderer());
			ObjectRenderers.Add(new AggLiteralInlineRenderer());

			ObjectRenderers.Add(new AggMatchingTextRenderer(theme));

			// Extension renderers
			ObjectRenderers.Add(new AggTableRenderer());
			//ObjectRenderers.Add(new AggTaskListRenderer());
		}

		/// <inheritdoc/>
		public override object Render(MarkdownObject markdownObject)
		{
			Write(markdownObject);
			UiThread.RunOnIdle(() =>
			{
				// TODO: investigate why this is required, layout should have already done this
				// but it didn't. markdown that looks like the following will not layout correctly without this
				// string badLayoutMarkdown = "I [s]()\n\nT";
				if (RootWidget?.Parent?.Parent != null)
				{
					RootWidget.Parent.Parent.Width = RootWidget.Parent.Parent.Width - 1;
				}
			});
			return RootWidget;
		}

		/// <summary>
		/// Writes the inlines of a leaf inline.
		/// </summary>
		/// <param name="leafBlock">The leaf block.</param>
		/// <returns>This instance</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteLeafInline(LeafBlock leafBlock)
		{
			if (leafBlock == null) throw new ArgumentNullException(nameof(leafBlock));
			var inline = (Syntax.Inlines.Inline)leafBlock.Inline;
			while (inline != null)
			{
				Write(inline);
				inline = inline.NextSibling;
			}
		}

		/// <summary>
		/// Writes the lines of a <see cref="LeafBlock"/>
		/// </summary>
		/// <param name="leafBlock">The leaf block.</param>
		public void WriteLeafRawLines(LeafBlock leafBlock)
		{
			if (leafBlock == null) throw new ArgumentNullException(nameof(leafBlock));
			if (leafBlock.Lines.Lines != null)
			{
				var lines = leafBlock.Lines;
				var slices = lines.Lines;
				for (var i = 0; i < lines.Count; i++)
				{
					if (i != 0)
						//if (stack.Peek() is FlowLayoutWidget)
						//{
						//	this.Pop();
						//	this.Push(new ParagraphX());
						//}
						WriteInline(new LineBreakX()); // new LineBreak());

					WriteText(ref slices[i].Slice);
				}
			}
		}

		internal void Push(GuiWidget o)
		{
			stack.Push(o);
		}

		internal void Pop()
		{
			var popped = stack.Pop();

			if (stack.Count > 0)
			{
				var top = stack.Peek();
				using (top.LayoutLock())
				{
					top.AddChild(popped);
				}
			}
		}

		internal void WriteBlock(GuiWidget block)
		{
			stack.Peek().AddChild(block);
		}

		internal void WriteInline(GuiWidget inline)
		{
			AddInline(stack.Peek(), inline);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteText(ref StringSlice slice)
		{
			if (slice.Start > slice.End)
			{
				return;
			}

			WriteText(slice.Text, slice.Start, slice.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteText(string text)
		{
			var words = text.Split(' ');
			bool first = true;

			foreach (var word in words)
			{
				if (!first)
				{
					WriteInline(new TextSpaceX(theme)
					{
						Text = " "
					});
				}

				if (word.Length > 0)
				{
					WriteInline(new TextWordX(theme)
					{
						Text = word
					});
				}

				first = false;
			}
		}

		internal void WriteText(string text, int offset, int length)
		{
			if (text == null)
			{
				return;
			}

			if (offset == 0 && text.Length == length)
			{
				WriteText(text);
			}
			else
			{
				if (length > buffer.Length)
				{
					buffer = text.ToCharArray();
					WriteText(new string(buffer, offset, length));
				}
				else
				{
					text.CopyTo(offset, buffer, 0, length);
					WriteText(new string(buffer, 0, length));
				}
			}
		}

		private static void AddInline(GuiWidget parent, GuiWidget inline)
		{
			parent.AddChild(inline);
		}
	}
}
