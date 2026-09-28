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
using System.Reflection;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>
	/// One row of the SVG Test window: a resvg-test-suite SVG, its reference.png, and what agg-sharp renders from it.
	/// The reference is decoded and the SVG rendered on first use, so building the window costs nothing until a
	/// row scrolls into view.
	/// </summary>
	public class SvgTestSample
	{
		private const string ResourcePrefix = "MatterHackers.AggSharpDemo.SvgTests/";

		private readonly byte[] svg;
		private readonly byte[] referencePng;
		private ImageBuffer reference;
		private ImageBuffer render;
		private string renderError;
		private SvgCompareResult? score;
		private ImageBuffer diff;

		public SvgTestSample(string name, byte[] svg, byte[] referencePng)
		{
			this.Name = name;
			this.svg = svg;
			this.referencePng = referencePng;
		}

		/// <summary>The sample's path in the suite without its extension, e.g. "shapes/rect/simple-case".</summary>
		public string Name { get; }

		/// <summary>agg-gui's SVG Test rows, in its order; each is an .svg and its .png under Art/SvgTests.</summary>
		public static IReadOnlyList<string> Names { get; } = new[]
		{
			"shapes/rect/simple-case",
			"shapes/circle/simple-case",
			"shapes/ellipse/simple-case",
			"shapes/line/simple-case",
			"shapes/line/with-transform",
			"shapes/polygon/simple-case",
			"shapes/polyline/simple-case",
			"shapes/path/M-L-L-Z",
			"shapes/path/M-C",
			"shapes/path/M-C-S",
			"shapes/path/M-Q",
			"shapes/path/M-Q-T",
			"shapes/path/M-A",
			"shapes/path/M-L-Z-A",
			"painting/fill/named-color",
			"painting/fill/currentColor",
			"painting/fill/rgb-color",
			"painting/fill/hsl-with-alpha",
			"painting/fill/linear-gradient-on-shape",
			"painting/fill/radial-gradient-on-shape",
			"painting/fill-rule/nonzero",
			"painting/fill-rule/evenodd",
			"painting/opacity/50percent",
			"painting/opacity/group-opacity",
			"painting/opacity/mixed-group-opacity",
			"painting/stroke/line-as-curve-1",
			"painting/stroke/line-as-curve-2",
			"painting/stroke/linear-gradient",
			"painting/stroke/radial-gradient",
			"paint-servers/linearGradient/gradientUnits=userSpaceOnUse",
			"paint-servers/linearGradient/gradientUnits=objectBoundingBox-with-percent",
			"paint-servers/linearGradient/gradientTransform",
			"paint-servers/linearGradient/gradientTransform-and-transform",
			"paint-servers/linearGradient/spreadMethod=reflect",
			"paint-servers/linearGradient/spreadMethod=repeat",
			"paint-servers/linearGradient/many-stops",
			"paint-servers/linearGradient/single-stop-with-opacity-used-by-stroke",
			"paint-servers/radialGradient/gradientUnits=userSpaceOnUse",
			"paint-servers/radialGradient/gradientUnits=objectBoundingBox-with-percent",
			"paint-servers/radialGradient/gradientTransform",
			"paint-servers/radialGradient/focal-point-correction",
			"paint-servers/radialGradient/spreadMethod=reflect",
			"paint-servers/radialGradient/spreadMethod=repeat",
			"paint-servers/radialGradient/many-stops",
			"paint-servers/pattern/simple-case",
			"paint-servers/pattern/patternUnits=userSpaceOnUse-with-percent",
			"paint-servers/pattern/patternContentUnits-with-viewBox",
			"paint-servers/pattern/transform-and-patternTransform",
			"structure/image/embedded-png",
			"structure/image/embedded-jpeg-as-image-jpeg",
			"structure/image/embedded-gif",
			"structure/image/embedded-svg",
			"structure/image/preserveAspectRatio=none",
			"structure/image/raster-image-and-size-with-odd-numbers",
			"text/text/simple-case",
			"text/tspan/sequential",
			"text/text-anchor/middle-on-text",
			"text/text-decoration/underline",
		};

		/// <summary>The SVG source text.</summary>
		public string SvgText => System.Text.Encoding.UTF8.GetString(this.svg);

		/// <summary>reference.png, decoded; its size is the size every render of this sample is made at.</summary>
		public ImageBuffer Reference => this.reference ??= ImageIO.LoadImage(new MemoryStream(this.referencePng));

		/// <summary>agg-sharp's render at the reference's size, or null when rendering threw (see <see cref="RenderError"/>).</summary>
		public ImageBuffer Render
		{
			get
			{
				if (this.render == null && this.renderError == null)
				{
					try
					{
						this.render = RenderSvg(this.svg, this.Reference.Width, this.Reference.Height, this.Name);
					}
					catch (Exception e)
					{
						this.renderError = e.Message;
					}
				}

				return this.render;
			}
		}

		public string RenderError => this.Render == null ? this.renderError : null;

		/// <summary>How the render measures against the reference; a render that threw fails outright.</summary>
		public SvgCompareResult Score => this.score ??= this.Render == null
			? new SvgCompareResult(false, 1, this.Reference.Width * this.Reference.Height, 255)
			: SvgCompare.Compare(SvgCompare.ToRgba(this.Render), SvgCompare.ToRgba(this.Reference));

		public bool Passes => this.Score.Pass;

		/// <summary>The per-pixel difference of render and reference, shown while the render is held down.</summary>
		public ImageBuffer Diff => this.diff ??= SvgCompare.DiffImage(this.Reference, this.Render ?? new ImageBuffer(this.Reference.Width, this.Reference.Height));

		/// <summary>
		/// Renders SVG bytes to a straight-alpha image of the given size, the viewBox stretched to fill it as a
		/// resvg reference is. Raster images are decoded with ImageIO, and an image href that is a file is read
		/// from the embedded suite relative to the sample <paramref name="name"/> (a path under the suite's tests/).
		/// </summary>
		public static ImageBuffer RenderSvg(byte[] svg, int width, int height, string name = "")
		{
			SvgDocument document = SvgDocument.Parse(System.Text.Encoding.UTF8.GetString(svg));
			document.ImageDecoder = bytes => ImageIO.LoadImage(new MemoryStream(bytes));
			document.ResourceResolver = href => ReadResource(ResolveHref(name, href));
			document.FontResolver = ResolveFont;
			return SvgRenderer.RenderToImage(document, width, height);
		}

		private static readonly Lazy<TypeFace> NotoSans = new Lazy<TypeFace>(() =>
		{
			using Stream stream = typeof(SvgTestSample).Assembly.GetManifestResourceStream("MatterHackers.AggSharpDemo.Fonts.NotoSans-Regular.ttf");
			var face = new TypeFace();
			return face.LoadTTF(stream) ? face : null;
		});

		/// <summary>
		/// The suite's references are drawn in Noto Sans, so "Noto Sans" and the generic sans-serif get the embedded
		/// Noto Sans Regular; bold weights and other families fall back to agg/Svg's Liberation Sans.
		/// </summary>
		public static TypeFace ResolveFont(string family, bool bold)
		{
			return !bold && (family == "Noto Sans" || family == "sans-serif") ? NotoSans.Value : null;
		}

		/// <summary>
		/// The suite-relative path <paramref name="href"/> names from the sample <paramref name="name"/>: samples live
		/// under the suite's tests/ folder, so "../../../resources/x.png" from "structure/image/y" is "resources/x.png".
		/// </summary>
		public static string ResolveHref(string name, string href)
		{
			var parts = ("tests/" + name).Split('/').SkipLast(1).ToList();
			foreach (string part in href.Replace('\\', '/').Split('/'))
			{
				if (part == "..")
				{
					if (parts.Count > 0)
					{
						parts.RemoveAt(parts.Count - 1);
					}
				}
				else if (part != "." && part != "")
				{
					parts.Add(part);
				}
			}

			return string.Join("/", parts);
		}

		/// <summary>The embedded suite file at the suite-relative <paramref name="path"/>, or null.</summary>
		private static byte[] ReadResource(string path)
		{
			Assembly assembly = typeof(SvgTestSample).Assembly;
			string resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.StartsWith(ResourcePrefix) && n.Substring(ResourcePrefix.Length).Replace('\\', '/') == path);
			if (resource == null)
			{
				return null;
			}

			using Stream stream = assembly.GetManifestResourceStream(resource);
			var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		/// <summary>Every sample in <see cref="Names"/> order, read from the demo assembly's embedded resources.</summary>
		public static List<SvgTestSample> LoadAll()
		{
			Assembly assembly = typeof(SvgTestSample).Assembly;

			// RecursiveDir puts the OS's separator into the logical names, so compare them with '/'.
			Dictionary<string, string> resources = assembly.GetManifestResourceNames()
				.Where(n => n.StartsWith(ResourcePrefix))
				.ToDictionary(n => n.Substring(ResourcePrefix.Length).Replace('\\', '/'), n => n);

			byte[] Read(string key)
			{
				using Stream stream = assembly.GetManifestResourceStream(resources[key]);
				var memory = new MemoryStream();
				stream.CopyTo(memory);
				return memory.ToArray();
			}

			return Names.Select(name => new SvgTestSample(name, Read(name + ".svg"), Read(name + ".png"))).ToList();
		}
	}
}
