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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// One element of an SVG document: its local name (namespace prefixes dropped, so <c>xlink:href</c> is
	/// <c>href</c>), its properties and its child elements. The document's &lt;style&gt; rules and the <c>style</c>
	/// attribute's declarations are merged into <see cref="Attributes"/> over the presentation attributes they
	/// override, in CSS's cascade order (see <see cref="SvgCss"/>).
	/// </summary>
	public class SvgElement
	{
		private readonly List<SvgElement> children = new List<SvgElement>();

		public SvgElement(string name, SvgElement parent)
		{
			this.Name = name;
			this.Parent = parent;
		}

		public string Name { get; }

		public SvgElement Parent { get; }

		public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

		public IReadOnlyList<SvgElement> Children => this.children;

		/// <summary>The element's own text content (a &lt;text&gt; or &lt;style&gt; body), trimmed; empty when it has none.</summary>
		public string Text { get; internal set; } = "";

		/// <summary>The attribute or style property <paramref name="name"/>, or null when the element does not set it.</summary>
		public string this[string name] => this.Attributes.TryGetValue(name, out string value) ? value : null;

		/// <summary>
		/// The element's text runs (strings, untrimmed) and child elements in document order: what a &lt;text&gt;
		/// lays out, where <see cref="Text"/> loses the order between the runs and the &lt;tspan&gt;s.
		/// </summary>
		internal List<object> Content { get; } = new List<object>();

		internal void AddChild(SvgElement child) => this.children.Add(child);

		/// <summary>This element and all its descendants, depth first.</summary>
		public IEnumerable<SvgElement> DescendantsAndSelf()
		{
			yield return this;
			foreach (SvgElement child in this.children)
			{
				foreach (SvgElement descendant in child.DescendantsAndSelf())
				{
					yield return descendant;
				}
			}
		}
	}

	/// <summary>
	/// A parsed SVG document: the element tree under <see cref="Root"/> and its elements by id, the lookup
	/// &lt;use&gt; (and later paint servers) resolve references through. Render it with <see cref="SvgRenderer"/>.
	/// </summary>
	public class SvgDocument
	{
		private readonly Dictionary<string, SvgElement> elementsById = new Dictionary<string, SvgElement>();

		private SvgDocument(SvgElement root)
		{
			this.Root = root;
			foreach (SvgElement element in root.DescendantsAndSelf())
			{
				string id = element["id"];
				if (!string.IsNullOrEmpty(id) && !this.elementsById.ContainsKey(id))
				{
					this.elementsById[id] = element;
				}
			}
		}

		/// <summary>The outermost &lt;svg&gt; element.</summary>
		public SvgElement Root { get; }

		/// <summary>
		/// Decodes a raster image's bytes (PNG, JPEG, GIF...) to a straight-alpha image, or returns null. agg has no
		/// image codecs of its own, so &lt;image&gt; elements holding raster data draw nothing until the application
		/// supplies one (DataConverters2D's <c>ImageIO.LoadImage</c> over a MemoryStream does).
		/// </summary>
		public Func<byte[], ImageBuffer> ImageDecoder { get; set; }

		/// <summary>
		/// Returns the bytes an &lt;image&gt; href that is not a data: URI names - the href exactly as written, to be
		/// resolved relative to wherever the document came from - or null. Without one such images draw nothing.
		/// </summary>
		public Func<string, byte[]> ResourceResolver { get; set; }

		/// <summary>
		/// Picks the face text is drawn in: given one font-family name from a text's list (unquoted, e.g. "Noto Sans"
		/// or a generic such as "sans-serif") and whether the weight is bold, it returns a face or null to try the
		/// list's next name. Without one, or when no name resolves, text is drawn in the embedded Liberation Sans.
		/// </summary>
		public Func<string, bool, TypeFace> FontResolver { get; set; }

		/// <summary>How many SVG images deep this document is drawn; stops an image chain that includes itself.</summary>
		internal int NestingDepth { get; set; }

		/// <summary>The element with <paramref name="id"/> (a leading '#' and a <c>url(...)</c> wrapper are accepted), or null.</summary>
		public SvgElement GetElementById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}

			id = id.Trim();
			if (id.StartsWith("url(") && id.EndsWith(")"))
			{
				id = id.Substring(4, id.Length - 5).Trim().Trim('\'', '"');
			}

			return this.elementsById.TryGetValue(id.TrimStart('#'), out SvgElement element) ? element : null;
		}

		/// <summary>
		/// The document's size in user units: its width and height attributes, else its viewBox's size, else
		/// 100 by 100 (resvg's fallback).
		/// </summary>
		public (double Width, double Height) Size
		{
			get
			{
				RectangleDouble? viewBox = SvgViewport.ParseViewBox(this.Root["viewBox"]);
				double width = SvgLength.Parse(this.Root["width"], 0, viewBox?.Width ?? 100);
				double height = SvgLength.Parse(this.Root["height"], 0, viewBox?.Height ?? 100);
				return (width > 0 ? width : viewBox?.Width ?? 100, height > 0 ? height : viewBox?.Height ?? 100);
			}
		}

		public static SvgDocument Parse(string svgText)
		{
			var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
			using var reader = XmlReader.Create(new StringReader(svgText), settings);
			XDocument xml = XDocument.Load(reader);
			SvgElement root = Convert(xml.Root, null);
			SvgCss.Apply(root);
			return new SvgDocument(root);
		}

		public static SvgDocument Parse(Stream stream)
		{
			using var reader = new StreamReader(stream);
			return Parse(reader.ReadToEnd());
		}

		/// <summary>Parses <paramref name="svgText"/> and renders it at <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
		public static ImageBuffer RenderToImage(string svgText, int width, int height)
		{
			return SvgRenderer.RenderToImage(Parse(svgText), width, height);
		}

		private static SvgElement Convert(XElement xml, SvgElement parent)
		{
			var element = new SvgElement(xml.Name.LocalName, parent);
			foreach (XAttribute attribute in xml.Attributes())
			{
				if (!attribute.IsNamespaceDeclaration)
				{
					element.Attributes[attribute.Name.LocalName] = attribute.Value.Trim();
				}
			}

			element.Text = string.Concat(xml.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
			foreach (XNode node in xml.Nodes())
			{
				if (node is XText text)
				{
					element.Content.Add(text.Value);
				}
				else if (node is XElement child)
				{
					SvgElement converted = Convert(child, element);
					element.AddChild(converted);
					element.Content.Add(converted);
				}
			}

			return element;
		}
	}
}
