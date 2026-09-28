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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// Where the trans_curve demos get their glyphs: each glyph's fillable outline at a pen position, and where
	/// the pen goes next.
	/// </summary>
	public abstract class CurveTextFont
	{
		/// <summary>The outline of <paramref name="character"/> with its origin at the pen, or null for none.</summary>
		public abstract IVertexSource GlyphAt(char character, double x, double y);

		/// <summary>The pen's x after <c>text[index]</c> placed at (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public abstract double NextPenX(string text, int index, double x, double y);
	}

	/// <summary>
	/// The C++ demos' Times New Roman at 40 pixels, as Liberation Serif (metric-compatible with it) read by
	/// agg-sharp's TrueType engine. As in C++ the outlines are flattened (conv_curve, at the approximation
	/// scale the demo passes) before anything else sees them.
	/// </summary>
	public class TrueTypeCurveTextFont : CurveTextFont
	{
		private static readonly Lazy<TypeFace> Regular = new Lazy<TypeFace>(() => Load("LiberationSerif-Regular.ttf"), LazyThreadSafetyMode.ExecutionAndPublication);

		private static readonly Lazy<TypeFace> Italic = new Lazy<TypeFace>(() => Load("LiberationSerif-Italic.ttf"), LazyThreadSafetyMode.ExecutionAndPublication);

		private readonly StyledTypeFace typeFace;

		private readonly double curveApproximationScale;

		public TrueTypeCurveTextFont(bool italic, double curveApproximationScale)
		{
			// C++ font_engine height(40) is the em in pixels; StyledTypeFace takes points (72 per 96 pixels).
			this.typeFace = new StyledTypeFace(italic ? Italic.Value : Regular.Value, 40.0 * StyledTypeFace.PointsPerInch / StyledTypeFace.PixelsPerInch, flattenCurves: false);
			this.curveApproximationScale = curveApproximationScale;
		}

		public override IVertexSource GlyphAt(char character, double x, double y)
		{
			IVertexSource glyph = this.typeFace.GetGlyphForCharacter(character);
			if (glyph == null)
			{
				return null;
			}

			return new FlattenCurves(new VertexSourceApplyTransform(glyph, Affine.NewTranslation(x, y)))
			{
				ResolutionScale = this.curveApproximationScale,
			};
		}

		public override double NextPenX(string text, int index, double x, double y) => x + this.typeFace.GetAdvanceForCharacter(text, index);

		private static TypeFace Load(string fileName)
		{
			string resourceName = "MatterHackers.AggSharpDemo.Fonts." + fileName;
			using var stream = typeof(TrueTypeCurveTextFont).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The font resource '{resourceName}' is missing from {typeof(TrueTypeCurveTextFont).Assembly.GetName().Name}; "
					+ "AggSharpDemo.csproj embeds it from liberation-fonts-ttf-1.07.0.");
			var typeFace = new TypeFace();
			typeFace.LoadTTF(stream);
			return typeFace;
		}
	}

	/// <summary>
	/// AGG's built-in gsv_text vector font stroked into outlines (24 high, 18 wide, 2px round). C++ can draw
	/// this headless too, so tools/cpp-renderer/src/demo_trans_curve.cpp renders the demos with it and the
	/// trans_curve goldens prove the transforms byte for byte; the demos themselves draw
	/// <see cref="TrueTypeCurveTextFont"/>.
	/// </summary>
	public class GsvCurveTextFont : CurveTextFont
	{
		private readonly gsv_text glyph = new gsv_text();

		private readonly Stroke outline;

		public GsvCurveTextFont()
		{
			this.glyph.size(24.0, 18.0);
			this.outline = new Stroke(this.glyph, 2.0)
			{
				LineJoin = LineJoin.Round,
				LineCap = LineCap.Round,
			};
		}

		public override IVertexSource GlyphAt(char character, double x, double y)
		{
			this.glyph.text(character.ToString());
			this.glyph.start_point(x, y);
			return this.outline;
		}

		public override double NextPenX(string text, int index, double x, double y)
		{
			this.glyph.text(text[index].ToString());
			this.glyph.start_point(x, y);

			// gsv_text's last vertex is where its pen ends up: the next glyph's start. Rewind does not move the
			// pen back (in C++ either), so start_point above is what makes this walk begin at the glyph's start.
			this.glyph.Rewind(0);
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = this.glyph.Vertex(out double vx, out _)))
			{
				if (ShapePath.IsVertex(command))
				{
					x = vx;
				}
			}

			return x;
		}
	}
}
