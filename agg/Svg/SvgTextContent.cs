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
using System.Text;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The text a &lt;text&gt; element lays out, as usvg's svgtree (text.rs) prepares it: each element's runs and
	/// child elements in order, with whitespace handled per xml:space and a &lt;tref&gt; replaced by the text it links to.
	/// Only &lt;tspan&gt;, &lt;tref&gt;, &lt;a&gt; (read as a tspan) and a &lt;textPath&gt; directly in the &lt;text&gt; are
	/// kept; any other child, and all its text, is dropped.
	/// </summary>
	internal static class SvgTextContent
	{
		/// <summary>SVG's element names, as usvg knows them: a tref linked to anything else draws nothing.</summary>
		private static readonly HashSet<string> SvgElementNames = new HashSet<string>
		{
			"a", "circle", "clipPath", "defs", "ellipse", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite",
			"feConvolveMatrix", "feDiffuseLighting", "feDisplacementMap", "feDistantLight", "feDropShadow", "feFlood",
			"feFuncA", "feFuncB", "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode",
			"feMorphology", "feOffset", "fePointLight", "feSpecularLighting", "feSpotLight", "feTile", "feTurbulence",
			"filter", "g", "image", "line", "linearGradient", "marker", "mask", "path", "pattern", "polygon", "polyline",
			"radialGradient", "rect", "stop", "style", "svg", "switch", "symbol", "text", "textPath", "tref", "tspan", "use",
		};

		/// <summary>
		/// Each element under <paramref name="text"/> (itself included) that lays out, with its content: strings,
		/// whitespace already handled, and the child elements that lay out. A tref's content is its linked text.
		/// </summary>
		public static Dictionary<SvgElement, List<object>> Resolve(SvgElement text, SvgDocument document)
		{
			// The <text>'s xml:space, else the nearest ancestor's that sets one.
			bool preserve = false;
			for (SvgElement e = text; e != null; e = e.Parent)
			{
				if (e["space"] != null)
				{
					preserve = e["space"] == "preserve";
					break;
				}
			}

			var content = new Dictionary<SvgElement, List<object>>();
			var nodes = new List<TextNode>();
			Build(text, preserve, 0, document, content, nodes);
			TrimAcrossNodes(nodes, preserve);

			foreach (List<object> items in content.Values)
			{
				for (int i = 0; i < items.Count; i++)
				{
					if (items[i] is TextNode node)
					{
						items[i] = node.Text;
					}
				}
			}

			return content;
		}

		private static void Build(SvgElement element, bool preserve, int depth, SvgDocument document, Dictionary<SvgElement, List<object>> content, List<TextNode> nodes)
		{
			var items = new List<object>();
			content[element] = items;
			foreach (object item in element.Content)
			{
				if (item is string run)
				{
					var node = new TextNode { Text = TrimText(run, preserve), Parent = element, Depth = depth };
					items.Add(node);
					nodes.Add(node);
				}
				else if (item is SvgElement child
					&& (child.Name == "tspan" || child.Name == "tref" || child.Name == "a" || (child.Name == "textPath" && element.Name == "text")))
				{
					items.Add(child);
					bool childPreserve = Preserve(child, preserve);
					if (child.Name == "tref")
					{
						// The tref's own children are never drawn, only the text it links to.
						var linked = new List<object>();
						content[child] = linked;
						if (LinkedText(child, document) is string linkedText)
						{
							var node = new TextNode { Text = TrimText(linkedText, childPreserve), Parent = child, Depth = depth + 1 };
							linked.Add(node);
							nodes.Add(node);
						}
					}
					else
					{
						Build(child, childPreserve, depth + 1, document, content, nodes);
					}
				}
			}
		}

		/// <summary>
		/// All the character data in the element a tref links to (by a local "#id" only), markup and all, or null
		/// when there is none or the link is not to a known SVG element.
		/// </summary>
		private static string LinkedText(SvgElement tref, SvgDocument document)
		{
			string href = tref["href"]?.Trim();
			if (document == null || href == null || !href.StartsWith("#"))
			{
				return null;
			}

			SvgElement linked = document.GetElementById(href.Substring(1).Trim());
			if (linked == null || !SvgElementNames.Contains(linked.Name))
			{
				return null;
			}

			var text = new StringBuilder();
			void Gather(SvgElement e)
			{
				foreach (object item in e.Content)
				{
					if (item is string run)
					{
						text.Append(run);
					}
					else if (item is SvgElement child)
					{
						Gather(child);
					}
				}
			}

			Gather(linked);
			return text.Length == 0 ? null : text.ToString();
		}

		/// <summary>An element's own xml:space, else <paramref name="inherited"/>: any value but "preserve" is default.</summary>
		private static bool Preserve(SvgElement element, bool inherited) => element["space"] is string space ? space == "preserve" : inherited;

		/// <summary>Newlines and tabs become spaces; under xml:space="default" runs of spaces become one.</summary>
		private static string TrimText(string text, bool preserve)
		{
			var result = new StringBuilder(text.Length);
			char previous = '0';
			foreach (char raw in text)
			{
				char c = raw == '\r' || raw == '\n' || raw == '\t' ? ' ' : raw;
				if (!preserve && c == ' ' && previous == ' ')
				{
					continue;
				}

				previous = c;
				result.Append(c);
			}

			return result.ToString();
		}

		/// <summary>
		/// usvg's trim_text_nodes, Chrome's behaviour: the text nodes are handled as one, so a space at the end of
		/// one node and the start of the next is kept once, and the first node's leading and last node's trailing
		/// spaces go - each node judged by its own parent's xml:space (not an inherited one).
		/// </summary>
		private static void TrimAcrossNodes(List<TextNode> nodes, bool textPreserve)
		{
			if (nodes.Count == 1)
			{
				if (!textPreserve)
				{
					TextNode node = nodes[0];
					if (node.Text == " ")
					{
						node.Text = "";
					}
					else if (node.Text.Length > 1)
					{
						if (node.Text[0] == ' ')
						{
							node.Text = node.Text.Substring(1);
						}

						if (node.Text.EndsWith(' '))
						{
							node.Text = node.Text.Substring(0, node.Text.Length - 1);
						}
					}
				}

				return;
			}

			int last = nodes.Count - 1;
			TextNode lastNonEmpty = null;
			for (int i = 0; i < last; i++)
			{
				TextNode node1 = nodes[i];
				TextNode node2 = nodes[i + 1];
				if (node1.Text.Length == 0 && lastNonEmpty != null)
				{
					node1 = lastNonEmpty;
				}

				bool preserve1 = Preserve(node1.Parent, textPreserve);
				bool preserve2 = Preserve(node2.Parent, textPreserve);

				// >text<..>text<
				//  1  2    3  4 - read once, before this pair changes either.
				char? c1 = node1.Text.Length > 0 ? node1.Text[0] : null;
				char? c2 = node1.Text.Length > 0 ? node1.Text[^1] : null;
				char? c3 = node2.Text.Length > 0 ? node2.Text[0] : null;
				char? c4 = node2.Text.Length > 0 ? node2.Text[^1] : null;

				if (nodes[i].Depth < node2.Depth)
				{
					// Into a deeper element: '<text>Text <tspan> text</tspan>' drops the tspan's space.
					if (c3 == ' ' && !preserve2)
					{
						node2.Text = node2.Text.Substring(1);
					}
				}
				else if (c2 == ' ' && c3 == ' ')
				{
					if (!preserve1 && !preserve2)
					{
						node1.Text = node1.Text.Substring(0, node1.Text.Length - 1);
					}
					else if (preserve1 && !preserve2)
					{
						node2.Text = node2.Text.Substring(1);
					}
				}

				if (i == 0 && c1 == ' ' && !preserve1 && node1.Text.Length > 0)
				{
					node1.Text = node1.Text.Substring(1);
				}
				else if (i == last - 1 && c4 == ' ' && node2.Text.Length > 0 && !preserve2)
				{
					node2.Text = node2.Text.Substring(0, node2.Text.Length - 1);
				}

				if (i == last - 1 && c2 == ' ' && node1.Text.Length > 0 && node2.Text.Length == 0 && node1.Text.EndsWith(' '))
				{
					node1.Text = node1.Text.Substring(0, node1.Text.Length - 1);
				}

				if (node1.Text.Trim().Length > 0)
				{
					lastNonEmpty = node1;
				}
			}
		}

		private sealed class TextNode
		{
			public string Text;
			/// <summary>The element the text is in, whose own xml:space judges it.</summary>
			public SvgElement Parent;
			/// <summary>0 for text directly in the &lt;text&gt;, one more per element in between.</summary>
			public int Depth;
		}
	}
}
