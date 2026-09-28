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
using System.Reflection;
using MatterHackers.RenderCore;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// Serves the canned WGSL embedded in this assembly, one module per source key.
	/// <para>
	/// The keys are the compat layer's <c>GlShaderKeys</c> constants, spelled here as literals on
	/// purpose: this project references RenderCore and the wgpu binding and nothing else, so it cannot
	/// see RenderGl. A test asserts the two lists agree, which is the right place for that coupling -
	/// the alternative would be a project reference from the backend up into the layer that sits on top
	/// of it.
	/// </para>
	/// <para>
	/// Each module declares one <c>vertexMain</c> and two fragment entry points, <c>fragmentMain</c> and
	/// <c>fragmentMainFlat</c>; four modules times three entry points is the twelve canned combos the
	/// port plan counts.
	/// </para>
	/// </summary>
	public class WgslShaderSources : IShaderSourceProvider
	{
		/// <summary>Unlit, per-vertex color. Matches <c>GlShaderKeys.PositionColor</c>.</summary>
		public const string PositionColor = "PositionColor";

		/// <summary>Lit, per-vertex color. Matches <c>GlShaderKeys.PositionColorLit</c>.</summary>
		public const string PositionColorLit = "PositionColorLit";

		/// <summary>Unlit, textured. Matches <c>GlShaderKeys.PositionTexture</c>.</summary>
		public const string PositionTexture = "PositionTexture";

		/// <summary>Lit, textured. Matches <c>GlShaderKeys.PositionTextureLit</c>.</summary>
		public const string PositionTextureLit = "PositionTextureLit";

		/// <summary>
		/// The native 3D scene pipeline: mesh vertex stage, lit/textured shading with the wireframe
		/// overlay, the depth prepass and the selection mask. Consumed by the scene renderer, not by the
		/// compat layer, so it has no <c>GlShaderKeys</c> counterpart.
		/// </summary>
		public const string NodeDesignerScene = "NodeDesignerScene";

		/// <summary>
		/// The scene compositor's full-screen passes: copy, the transparency resolve and the selection
		/// outline composite.
		/// </summary>
		public const string NodeDesignerPostProcess = "NodeDesignerPostProcess";

		/// <summary>
		/// The SSAA target's box-downsample composite (<c>SsaaRenderTarget.ShaderModuleKey</c>): an N x N
		/// supersampled texture averaged onto a rectangle of the destination.
		/// </summary>
		public const string SsaaDownsample = "SsaaDownsample";

		/// <summary>
		/// GpuCompOp's composite: a premultiplied layer onto a copy of the destination through an SVG compositing
		/// operator (BlenderCompOpBGRA's formulas).
		/// </summary>
		public const string CompOpComposite = "CompOpComposite";

		/// <summary>GpuBlur's passes: a stack blur along one axis of a premultiplied layer, optionally coloured by a table.</summary>
		public const string GpuBlur = "GpuBlur";

		/// <summary>GpuRegionBlur's passes: agg's software blurs over a box of the frame, in their own arithmetic.</summary>
		public const string GpuRegionBlur = "GpuRegionBlur";

		/// <summary>GpuGradientFill's pass: span_gradient evaluated per pixel under a path's coverage layer.</summary>
		public const string GpuGradient = "GpuGradient";

		/// <summary>GpuColorLut's pass: a copy of the frame's red, green and blue mapped through a 256-entry table.</summary>
		public const string GpuColorLut = "GpuColorLut";

		/// <summary>GpuImageFilter's pass: C++ AGG's image span generators evaluated per pixel under a path's coverage layer.</summary>
		public const string GpuImageFilter = "GpuImageFilter";

		/// <summary>GpuGouraudFill's passes: span_gouraud_rgba evaluated per pixel at the rasterizer's coverage of each triangle.</summary>
		public const string GpuGouraud = "GpuGouraud";

		/// <summary>
		/// Appended to a module key for its variant that writes linear light: a straight-alpha colour's sRGB channels
		/// are converted to linear before blending, so a linear float target (rgba16float) accumulates what C++ AGG's
		/// float (rgba32) buffers do. Only the modules in <see cref="LinearOutputKeys"/> have one.
		/// </summary>
		public const string LinearStraightSuffix = "+LinearStraight";

		/// <summary>
		/// <see cref="LinearStraightSuffix"/> for a premultiplied output: the colour is unpremultiplied, converted and
		/// premultiplied again.
		/// </summary>
		public const string LinearPremultipliedSuffix = "+LinearPremultiplied";

		// The one line each linear-capable module routes its fragment outputs through (see PositionColor.wgsl).
		private const string OutputColorHook = "fn outputColor(c : vec4<f32>) -> vec4<f32> { return c; }";

		// sRGB's decoding curve, as SrgbLut's LinearFromSrgb and C++ sRGB_to_linear compute it.
		private const string SrgbToLinear =
			"fn srgbToLinear(c : vec3<f32>) -> vec3<f32> { return select(pow((c + vec3<f32>(0.055)) / 1.055, vec3<f32>(2.4)), c / 12.92, c <= vec3<f32>(0.04045)); }\n";

		private const string LinearStraightOutput =
			SrgbToLinear + "fn outputColor(c : vec4<f32>) -> vec4<f32> { return vec4<f32>(srgbToLinear(c.rgb), c.a); }";

		private const string LinearPremultipliedOutput =
			SrgbToLinear + "fn outputColor(c : vec4<f32>) -> vec4<f32> { if (c.a <= 0.0) { return c; } return vec4<f32>(srgbToLinear(clamp(c.rgb / c.a, vec3<f32>(0.0), vec3<f32>(1.0))) * c.a, c.a); }";

		private static readonly string[] LinearKeys = { PositionColor, PositionTexture, GpuGradient, SsaaDownsample };

		private static readonly string[] Keys =
		{
			PositionColor,
			PositionColorLit,
			PositionTexture,
			PositionTextureLit,
			NodeDesignerScene,
			NodeDesignerPostProcess,
			SsaaDownsample,
			CompOpComposite,
			GpuBlur,
			GpuRegionBlur,
			GpuGradient,
			GpuColorLut,
			GpuImageFilter,
			GpuGouraud,
		};

		private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>The modules with linear-light variants (<see cref="LinearStraightSuffix"/>).</summary>
		public static IReadOnlyList<string> LinearOutputKeys => LinearKeys;

		/// <summary>Every key this provider serves, not counting the linear-light variants.</summary>
		public static IReadOnlyList<string> AllModuleKeys => Keys;

		/// <inheritdoc/>
		public string TryGetSource(string sourceKey)
		{
			if (string.IsNullOrEmpty(sourceKey))
			{
				return null;
			}

			if (this.cache.TryGetValue(sourceKey, out string cached))
			{
				return cached;
			}

			string source;
			if (TrySplitLinearVariant(sourceKey, out string baseKey, out string output))
			{
				if (Array.IndexOf(LinearKeys, baseKey) < 0)
				{
					return null;
				}

				string baseSource = this.TryGetSource(baseKey);
				if (!baseSource.Contains(OutputColorHook, StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"Shader '{baseKey}' has lost its outputColor hook, so it has no linear-light variant.");
				}

				source = baseSource.Replace(OutputColorHook, output, StringComparison.Ordinal);
			}
			else if (Array.IndexOf(Keys, sourceKey) < 0)
			{
				return null;
			}
			else
			{
				source = ReadEmbedded(sourceKey);
			}

			this.cache[sourceKey] = source;
			return source;
		}

		private static bool TrySplitLinearVariant(string sourceKey, out string baseKey, out string output)
		{
			foreach (var (suffix, replacement) in new[] { (LinearStraightSuffix, LinearStraightOutput), (LinearPremultipliedSuffix, LinearPremultipliedOutput) })
			{
				if (sourceKey.EndsWith(suffix, StringComparison.Ordinal))
				{
					baseKey = sourceKey.Substring(0, sourceKey.Length - suffix.Length);
					output = replacement;
					return true;
				}
			}

			baseKey = null;
			output = null;
			return false;
		}

		private static string ReadEmbedded(string sourceKey)
		{
			var assembly = typeof(WgslShaderSources).Assembly;
			string resourceName = typeof(WgslShaderSources).Namespace + ".Shaders." + sourceKey + ".wgsl";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					// A key in the list with no resource behind it is a build configuration error, not a
					// caller error, so it says so rather than returning null and looking like an unknown key.
					throw new InvalidOperationException(
						$"Embedded shader '{resourceName}' is missing from {assembly.GetName().Name}. "
						+ "Check the EmbeddedResource glob in WebGpuRender.csproj.");
				}

				using (var reader = new StreamReader(stream))
				{
					return reader.ReadToEnd();
				}
			}
		}
	}
}
