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
using System.Linq;
using MatterHackers.AggSharpDemo.Demos;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// Every AGG demo the site lists. The sidebar and the Demos menu show them by <see cref="Groups"/>, each
	/// group alphabetical (<see cref="GroupEntries"/>); the creation order below is only the order they were ported.
	/// </summary>
	/// <remarks>
	/// A hand-written list on purpose, not PluginFinder or any other reflection: the browser build is
	/// trimmed, and the trimmer keeps only what is statically referenced - a demo found by scanning types
	/// would be silently dropped from the published site. Adding a demo means adding a line here.
	/// </remarks>
	public static class DemoRegistry
	{
		/// <summary>Every <see cref="AggDemo.Category"/>, in the order the groups are listed: what a demo draws
		/// (shapes, paths), then how it is rendered and coloured, then images and text.</summary>
		public static IReadOnlyList<string> Groups { get; } = new[]
		{
			"Shapes", "Paths & Strokes", "Rendering", "Color & Gradients", "Masks & Clipping", "Transforms", "Images", "Text",
		};

		/// <summary>The demo shown when none was asked for or remembered: the lion, AGG's signature picture.</summary>
		public const string DefaultDemoName = "lion";

		/// <summary>The demos of <paramref name="demos"/> in <paramref name="group"/>, alphabetical by name
		/// (ordinal on the lower-cased name, as the GUI demo's sidebar sorts its titles).</summary>
		public static IReadOnlyList<AggDemo> GroupEntries(IEnumerable<AggDemo> demos, string group)
		{
			return demos
				.Where(demo => demo.Category == group)
				.OrderBy(demo => demo.Name.ToLowerInvariant(), StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>Creates a fresh instance of every AGG demo. Each call returns new demos with default state.</summary>
		public static IReadOnlyList<AggDemo> CreateAggDemos()
		{
			return new AggDemo[]
			{
				new LionDemo(),
				new LionOutlineDemo(),
				new PerspectiveDemo(),
				new RoundedRectDemo(),
				new AaDemo(),
				new ConvDashMarkerDemo(),
				new BSplineDemo(),
				new GouraudDemo(),
				new RasterizersDemo(),
				new TransCurve1Demo(),
				new TransCurve2Demo(),
				new ConvStrokeDemo(),
				new ConvContourDemo(),
				new GammaCtrlDemo(),
				new GammaCorrectionDemo(),
				new AlphaMaskDemo(),
				new AlphaMask2Demo(),
				new IdeaDemo(),
				new CirclesDemo(),
				new GammaTunerDemo(),
				new ImageTransformsDemo(),
				new ImagePerspectiveDemo(),
				new ImageFiltersDemo(),
				new LionLensDemo(),
				new SimpleBlurDemo(),
				new CompositingDemo(),
				new Compositing2Demo(),
				new GradientsDemo(),
				new GradientFocalDemo(),
				new AlphaGradientDemo(),
				new BlurDemo(),
				new AaTestDemo(),
				new LineThicknessDemo(),
				new Rasterizers2Demo(),
				new LinePatternsDemo(),
				new ComponentRenderingDemo(),
				new MultiClipDemo(),
				new ScanlineBooleanDemo(),
				new ScanlineBoolean2Demo(),
				new LinePatternsClipDemo(),
				new Image1Demo(),
				new ImageAlphaDemo(),
				new RasterizerCompoundDemo(),
				new PatternFillDemo(),
				new PatternPerspectiveDemo(),
				new FlashRasterizerDemo(),
				new TransPolarDemo(),
				new DistortionsDemo(),
				new FlashRasterizer2Demo(),
				new BezierDivDemo(),
				new PatternResampleDemo(),
				new ImageResampleDemo(),
				new AlphaMask3Demo(),
				new GouraudMeshDemo(),
				new GsvTextDemo(),
				new ImageFltrGraphDemo(),
				new ImageFilters2Demo(),
				new BlendColorDemo(),
				new MolViewDemo(),
				new PolymorphicRendererDemo(),
				new RasterTextDemo(),
				new GraphTestDemo(),
				new TrueTypeTestDemo(),
			};
		}
	}
}
