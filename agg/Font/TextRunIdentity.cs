//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026, Lars Brubaker
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
//
// Class TextRunIdentity.cs
//
// The value TypeFacePrinter.RenderIdentity builds for a run of text.
//----------------------------------------------------------------------------
using System;
using System.Runtime.CompilerServices;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Font
{
	/// <summary>
	/// The value <see cref="TypeFacePrinter.RenderIdentity"/> builds: two runs with equal identities emit identical
	/// vertices. See <see cref="TypeFacePrinter.RenderIdentity"/> for why each field is here.
	/// </summary>
	/// <remarks>
	/// A class rather than a struct because it is handed out as an <see cref="object"/>: a struct would
	/// be boxed anyway, and a boxed struct without an explicit <see cref="object.Equals(object)"/>
	/// override falls back to reflective field comparison on every lookup.
	/// <para>
	/// The <see cref="Font.TypeFace"/> is compared by reference: a typeface is loaded once and shared, and
	/// two separately parsed copies of the same font file are two objects whose glyph outlines nobody has
	/// promised are identical.
	/// </para>
	/// <para>
	/// The doubles are compared by bit pattern - value equality with no epsilon that could let a changed
	/// size keep serving a raster of the previous one.
	/// </para>
	/// </remarks>
	internal sealed class TextRunIdentity : IEquatable<TextRunIdentity>
	{
		private readonly string text;
		private readonly TypeFace typeFace;
		private readonly double emSizeInPixels;
		private readonly bool underline;
		private readonly bool flattenCurves;
		private readonly bool fauxItalic;
		private readonly double resolutionScale;
		private readonly Justification justification;
		private readonly Baseline baseline;
		private readonly Vector2 origin;
		private readonly bool snapBaselines;
		private readonly double lineSpacing;
		private readonly object style;

		internal TextRunIdentity(
			string text,
			TypeFace typeFace,
			double emSizeInPixels,
			bool underline,
			bool flattenCurves,
			bool fauxItalic,
			double resolutionScale,
			Justification justification,
			Baseline baseline,
			Vector2 origin,
			bool snapBaselines,
			double lineSpacing,
			object style)
		{
			this.text = text;
			this.typeFace = typeFace;
			this.emSizeInPixels = emSizeInPixels;
			this.underline = underline;
			this.flattenCurves = flattenCurves;
			this.fauxItalic = fauxItalic;
			this.resolutionScale = resolutionScale;
			this.justification = justification;
			this.baseline = baseline;
			this.origin = origin;
			this.snapBaselines = snapBaselines;
			this.lineSpacing = lineSpacing;
			this.style = style;
		}

		public bool Equals(TextRunIdentity other)
		{
			return other != null
				&& this.text == other.text
				&& object.ReferenceEquals(this.typeFace, other.typeFace)
				&& BitConverter.DoubleToInt64Bits(this.emSizeInPixels) == BitConverter.DoubleToInt64Bits(other.emSizeInPixels)
				&& this.underline == other.underline
				&& this.flattenCurves == other.flattenCurves
				&& this.fauxItalic == other.fauxItalic
				&& BitConverter.DoubleToInt64Bits(this.resolutionScale) == BitConverter.DoubleToInt64Bits(other.resolutionScale)
				&& this.justification == other.justification
				&& this.baseline == other.baseline
				&& BitConverter.DoubleToInt64Bits(this.origin.X) == BitConverter.DoubleToInt64Bits(other.origin.X)
				&& BitConverter.DoubleToInt64Bits(this.origin.Y) == BitConverter.DoubleToInt64Bits(other.origin.Y)
				&& this.snapBaselines == other.snapBaselines
				&& BitConverter.DoubleToInt64Bits(this.lineSpacing) == BitConverter.DoubleToInt64Bits(other.lineSpacing)
				&& object.Equals(this.style, other.style);
		}

		public override bool Equals(object obj)
		{
			return this.Equals(obj as TextRunIdentity);
		}

		public override int GetHashCode()
		{
			var hash = default(HashCode);
			hash.Add(this.text);
			hash.Add(RuntimeHelpers.GetHashCode(this.typeFace));
			hash.Add(BitConverter.DoubleToInt64Bits(this.emSizeInPixels));
			hash.Add(this.underline);
			hash.Add(this.flattenCurves);
			hash.Add(this.fauxItalic);
			hash.Add(BitConverter.DoubleToInt64Bits(this.resolutionScale));
			hash.Add((int)this.justification);
			hash.Add((int)this.baseline);
			hash.Add(BitConverter.DoubleToInt64Bits(this.origin.X));
			hash.Add(BitConverter.DoubleToInt64Bits(this.origin.Y));
			hash.Add(this.snapBaselines);
			hash.Add(this.style);

			return hash.ToHashCode();
		}
	}
}
