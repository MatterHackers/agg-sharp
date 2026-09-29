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

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// Conditional processing as usvg does it (switch.rs): an element is drawn only when it has no
	/// requiredExtensions, every requiredFeatures entry is an SVG 1.1 feature usvg supports, and its systemLanguage
	/// (if any) names the user's language, "en" - an entry's region suffix is ignored, so "en-GB" matches. A switch
	/// draws only its first SVG element child that passes.
	/// </summary>
	internal static class SvgSwitch
	{
		private const string Language = "en";

		private static readonly HashSet<string> Features = new HashSet<string>(new[]
		{
			"SVGDOM-static", "SVG-static", "CoreAttribute", "Structure", "BasicStructure", "ContainerAttribute",
			"ConditionalProcessing", "Image", "Style", "Shape", "Text", "BasicText", "PaintAttribute",
			"BasicPaintAttribute", "OpacityAttribute", "GraphicsAttribute", "BasicGraphicsAttribute", "Marker",
			"Gradient", "Pattern", "Clip", "BasicClip", "Mask", "Filter", "BasicFilter", "XlinkAttribute",
		}.Select(feature => "http://www.w3.org/TR/SVG11/feature#" + feature));

		// usvg's element set: it drops anything else while parsing, so a switch never picks it.
		private static readonly HashSet<string> SvgElements = new HashSet<string>
		{
			"a", "circle", "clipPath", "defs", "ellipse", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite",
			"feConvolveMatrix", "feDiffuseLighting", "feDisplacementMap", "feDistantLight", "feDropShadow", "feFlood",
			"feFuncA", "feFuncB", "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode",
			"feMorphology", "feOffset", "fePointLight", "feSpecularLighting", "feSpotLight", "feTile", "feTurbulence",
			"filter", "g", "image", "line", "linearGradient", "marker", "mask", "path", "pattern", "polygon", "polyline",
			"radialGradient", "rect", "stop", "style", "svg", "switch", "symbol", "text", "textPath", "tref", "tspan", "use",
		};

		/// <summary>Whether <paramref name="element"/>'s conditional attributes let it draw.</summary>
		public static bool ConditionsPass(SvgElement element)
		{
			if (element["requiredExtensions"] != null)
			{
				return false;
			}

			if (element["requiredFeatures"] is string features && features.Split(' ').Any(feature => !Features.Contains(feature)))
			{
				return false;
			}

			return !(element["systemLanguage"] is string languages)
				|| languages.Split(',').Select(language => language.Trim()).Any(language => language == Language || language.StartsWith(Language + "-"));
		}

		/// <summary>The one child of <paramref name="switchElement"/> it draws, or null.</summary>
		public static SvgElement Chosen(SvgElement switchElement)
		{
			return switchElement.Children.FirstOrDefault(child => SvgElements.Contains(child.Name) && ConditionsPass(child));
		}
	}
}
