/*
Copyright(c) 2026, Lars Brubaker, John Lewin
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

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class EmphasisInlineX : FlowLayoutWidget, IWrapChildrenSeparatly
	{
		private char delimiter;

		/// <param name="delimiter">The emphasis character: '*' or '_' for emphasis, '~' for strikethrough.</param>
		/// <param name="delimiterCount">How many delimiters open the span: 1 is italic, 2 is bold (for '*' and '_').</param>
		public EmphasisInlineX(char delimiter, int delimiterCount = 2)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			this.delimiter = delimiter;
			this.DelimiterCount = delimiterCount;
		}

		public char Delimiter => delimiter;

		public int DelimiterCount { get; }

		/// <summary>True when this span slants its text: a single '*' or '_'.</summary>
		public bool IsItalic => (delimiter == '*' || delimiter == '_') && DelimiterCount == 1;

		/// <summary>The span's HTML element, for copying a selection as rich text.</summary>
		public string WrapHtml(string innerHtml) => delimiter == '~'
			? $"<del>{innerHtml}</del>"
			: IsItalic ? $"<em>{innerHtml}</em>" : $"<strong>{innerHtml}</strong>";

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			// Markdig nests '***x***' as one span inside another, and the inner span (or a link) arrives here
			// already holding its words, so the style goes onto every word below the child, not just a direct one.
			foreach (var textWidget in childToAdd.DescendantsAndSelf<TextWidget>())
			{
				switch (delimiter)
				{
					case '~':
						textWidget.StrikeThrough = true;
						break;

					case '*':
					case '_':
						if (IsItalic)
						{
							textWidget.Italic = true;
						}
						else
						{
							textWidget.Bold = true;
						}

						break;

					// '^' superscript, '+' inserted and '=' marked have no style of their own yet and stay bold
					default:
						textWidget.Bold = true;
						break;
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	/// <summary>
	/// A Agg renderer for an <see cref="EmphasisInline"/>.
	/// </summary>
	/// <seealso cref="EmphasisInline" />
	public class AggEmphasisInlineRenderer : AggObjectRenderer<EmphasisInline>
	{
		protected override void Write(AggRenderer renderer, EmphasisInline obj)
		{
			renderer.Push(new EmphasisInlineX(obj.DelimiterChar, obj.DelimiterCount));
			renderer.WriteChildren(obj);
			renderer.Pop();
		}
	}
}
