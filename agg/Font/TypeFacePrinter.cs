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
// Class StringPrinter.cs
//
// Class to output the vertex source of a string as a run of glyphs.
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Font
{
	public enum Justification
	{
		Left,
		Center,
		Right
	}

	public enum Baseline
	{
		BoundsTop,
		BoundsCenter,
		TextCenter,
		Text,
		BoundsBottom
	}

	public class TypeFacePrinter : VertexSourceLegacySupport, IVertexSourceRenderIdentity
	{
		/// <summary>
		/// When true (the default), each text line's baseline Y is snapped to the nearest
		/// whole pixel using the (y + 0.5).floor() convention, and Render() nudges the result
		/// so the snapped baselines land on whole *device* pixels. Y only — horizontal subpixel
		/// positioning is preserved. Improves the crispness of horizontal stems at 1:1 scale.
		/// </summary>
		public static bool SnapBaselinesToWholePixels { get; set; } = true;

		private string text = "";

		private Vector2 totalSizeCache;

		private Justification justification;

		public Justification Justification
		{
			get => justification;

			set
			{
				justification = value;
				InvalidateVertices();
			}
		}

		private Baseline baseline;

		public Baseline Baseline
		{
			get => baseline;

			set
			{
				baseline = value;
				InvalidateVertices();
			}
		}

		// DrawFromHintedCache picks the render path rather than changing Vertices(), so it does not invalidate.
		public bool DrawFromHintedCache { get; set; }

		private StyledTypeFace typeFaceStyle;

		public StyledTypeFace TypeFaceStyle
		{
			get
			{
				return typeFaceStyle;
			}

			set
			{
				if (value != typeFaceStyle)
				{
					typeFaceStyle = value;
					totalSizeCache = default(Vector2);
					InvalidateVertices();
				}
			}
		}

		public string Text
		{
			get
			{
				return text;
			}

			set
			{
				if (text != value)
				{
					totalSizeCache.X = 0;
					text = value;
					InvalidateVertices();
				}
			}
		}

		private Vector2 origin;

		public Vector2 Origin
		{
			get => origin;

			set
			{
				origin = value;
				InvalidateVertices();
			}
		}

		private double resolutionScale = 1;

		public double ResolutionScale
		{
			get => resolutionScale;

			set
			{
				resolutionScale = value;
				InvalidateVertices();
			}
		}

		/// <summary>
		/// Everything that decides which vertices <see cref="Vertices"/> emits, as one comparable value - see
		/// <see cref="IVertexSourceRenderIdentity"/>. Null while there is no typeface to shape with, since a
		/// printer in that state cannot describe glyphs it has no way to produce.
		/// </summary>
		/// <remarks>
		/// The list is the contract: the string, the outlines it is shaped with
		/// (<see cref="Font.TypeFace"/> and the em size in pixels), everything that moves a glyph relative to
		/// the others (<see cref="Justification"/>, <see cref="Baseline"/>, <see cref="Origin"/>, and
		/// <see cref="SnapBaselinesToWholePixels"/>, which rounds each line's baseline), and everything that
		/// changes an outline's own vertices (<see cref="StyledTypeFace.DoUnderline"/> adds geometry,
		/// <see cref="StyledTypeFace.FlattenCurves"/> and <see cref="ResolutionScale"/> change how curves are
		/// flattened).
		/// <para>
		/// <b><see cref="Origin"/> is in it because <see cref="Vertices"/> bakes it into the positions</b>
		/// rather than leaving it to the caller's transform - that is the path
		/// <see cref="Graphics2D.DrawString(string, double, double, double, Justification, Baseline, Color, bool, Color, bool)"/>
		/// uses. A printer moved by the graphics transform instead keeps one identity, which is how a widget
		/// that draws the same label at a new screen position every frame stays recognisable.
		/// </para>
		/// <para>
		/// It names the <see cref="Font.TypeFace"/> and the em size rather than the
		/// <see cref="StyledTypeFace"/> holding them, because a <see cref="StyledTypeFace"/> is routinely
		/// constructed per draw - <see cref="TypeFacePrinter(string, double, Vector2, Justification, Baseline, bool)"/>
		/// makes a new one every time - while the <see cref="Font.TypeFace"/> behind it is loaded once and
		/// shared.
		/// </para>
		/// </remarks>
		public object RenderIdentity
		{
			get
			{
				if (TypeFaceStyle == null)
				{
					return null;
				}

				return new TextRunIdentity(
					text,
					TypeFaceStyle.TypeFace,
					TypeFaceStyle.EmSizeInPixels,
					TypeFaceStyle.DoUnderline,
					TypeFaceStyle.FlattenCurves,
					ResolutionScale,
					Justification,
					Baseline,
					Origin,
					SnapBaselinesToWholePixels);
			}
		}

		public TypeFacePrinter(string text = "", double pointSize = 12, Vector2 origin = default(Vector2), Justification justification = Justification.Left, Baseline baseline = Baseline.Text, bool bold = false)
			: this(text,
				  bold ? new StyledTypeFace(AggContext.DefaultFontBold, pointSize) : new StyledTypeFace(AggContext.DefaultFont, pointSize),
				  origin,
				  justification,
				  baseline)
		{
		}

		public TypeFacePrinter(string text, StyledTypeFace typeFaceStyle, Vector2 origin = default(Vector2), Justification justification = Justification.Left, Baseline baseline = Baseline.Text)
		{
			this.TypeFaceStyle = typeFaceStyle;
			this.text = text;
			this.Justification = justification;
			this.Origin = origin;
			this.Baseline = baseline;
		}

		public TypeFacePrinter(string text, TypeFacePrinter copyPropertiesFrom)
			: this(text, copyPropertiesFrom.TypeFaceStyle, copyPropertiesFrom.Origin, copyPropertiesFrom.Justification, copyPropertiesFrom.Baseline)
		{
		}

		public RectangleDouble LocalBounds
		{
			get
			{
				Vector2 size = GetSize();
				RectangleDouble bounds;

				switch (Justification)
				{
					case Justification.Left:
						bounds = new RectangleDouble(0, TypeFaceStyle.DescentInPixels, size.X, size.Y + TypeFaceStyle.DescentInPixels);
						break;

					case Justification.Center:
						bounds = new RectangleDouble(-size.X / 2, TypeFaceStyle.DescentInPixels, size.X / 2, size.Y + TypeFaceStyle.DescentInPixels);
						break;

					case Justification.Right:
						bounds = new RectangleDouble(-size.X, TypeFaceStyle.DescentInPixels, 0, size.Y + TypeFaceStyle.DescentInPixels);
						break;

					default:
						throw new NotImplementedException();
				}

				switch (Baseline)
				{
					case Font.Baseline.BoundsCenter:
						bounds.Offset(0, -TypeFaceStyle.AscentInPixels / 2);
						break;

					default:
						break;
				}

				bounds.Offset(Origin);
				return bounds;
			}
		}

		public void Render(Graphics2D graphics2D, Color color, IVertexSourceProxy vertexSourceToApply)
		{
			vertexSourceToApply.VertexSource = this;
			Rewind(0);
			if (DrawFromHintedCache)
			{
				// TODO: make this work
				graphics2D.Render(vertexSourceToApply, color);
			}
			else
			{
				graphics2D.Render(vertexSourceToApply, color);
			}
		}

		public void Render(Graphics2D graphics2D, Color color)
		{
			if (DrawFromHintedCache)
			{
				RenderFromCache(graphics2D, color);
			}
			else
			{
				Rewind(0);
				graphics2D.Render(GetDeviceSnappedSource(graphics2D), color);
			}
		}

		/// <summary>
		/// Returns this printer, optionally wrapped in the local Y nudge needed to land its
		/// whole-pixel baselines on whole *device* pixels.
		/// </summary>
		/// <remarks>
		/// Integer local baselines are useless on their own: widget offsets and TextWidget's
		/// yOffsetForText are fractional doubles that ride in on the graphics2D transform, so a
		/// baseline snapped to 0 can still be rasterized at device y 8.4. Only a translation-only,
		/// unit-scale transform can be corrected this way - under scale or rotation a whole-pixel
		/// local nudge is not a whole-pixel device nudge, and applying one would just misplace the
		/// text. X translation is deliberately left alone to preserve horizontal subpixel positioning.
		/// <para>
		/// The nudge must round the *total* device baseline once. Rounding transform.ty on its own
		/// would add a second rounding on top of the one Vertices() already did, so the same true
		/// device baseline would land on different pixels depending on how the Y was split between
		/// Origin.Y and the transform (Origin.Y 8.6 + ty 0.6 would render a pixel below Origin.Y 9.2
		/// + ty 0). Instead we give back the residue the local snap discarded, leaving the first
		/// line's device baseline at exactly Math.Floor(unsnappedBaseline + ty + 0.5).
		/// </para>
		/// <para>
		/// Multi-line text with a fractional ty can still see a line's spacing differ by a pixel from
		/// the theoretical ideal, because the per-line snap was computed before this nudge was known.
		/// Every line does still land on a whole device pixel, which is what the snap is for.
		/// </para>
		/// </remarks>
		private IVertexSource GetDeviceSnappedSource(Graphics2D graphics2D)
		{
			if (!SnapBaselinesToWholePixels)
			{
				return this;
			}

			Affine transform = graphics2D.GetTransform();
			if (Math.Abs(transform.sx - 1) > 1e-6
				|| Math.Abs(transform.sy - 1) > 1e-6
				|| Math.Abs(transform.shx) > 1e-6
				|| Math.Abs(transform.shy) > 1e-6)
			{
				return this;
			}

			double unsnappedBaselineY = GetFirstLineBaselineY();
			double snappedBaselineY = Math.Floor(unsnappedBaselineY + 0.5);
			double deltaY = Math.Floor(unsnappedBaselineY + transform.ty + 0.5) - snappedBaselineY - transform.ty;
			// With ty == 0 the two floors are identical, so the common whole-pixel case allocates nothing.
			if (deltaY == 0)
			{
				return this;
			}

			return new VertexSourceApplyTransform(this, Affine.NewTranslation(0, deltaY));
		}

		/// <summary>
		/// The first line's local baseline Y before snapping - exactly the value <see cref="Vertices"/>
		/// computes for line 0. Shared with the device nudge so the two cannot drift apart.
		/// </summary>
		private double GetFirstLineBaselineY()
		{
			return GetBaseline(Vector2.Zero).Y + Origin.Y;
		}

		private void RenderFromCache(Graphics2D graphics2D, Color color)
		{
			if (text != null && text.Length > 0)
			{
				Vector2 currentOffset = Vector2.Zero;

				currentOffset = GetBaseline(currentOffset);
				// remove the decent and 1 pixel that were put into the cache image to give space for descenders
				currentOffset.Y += Origin.Y + TypeFaceStyle.DescentInPixels - 1;

				string[] lines = text.Split('\n');
				var transformX = graphics2D.GetTransform().tx;
                foreach (string line in lines)
				{
					currentOffset = GetXPositionForLineBasedOnJustification(currentOffset, line);
					currentOffset.X += Origin.X;

					for (int currentChar = 0; currentChar < line.Length; currentChar++)
					{
						ImageBuffer currentGlyphImage = TypeFaceStyle.GetImageForCharacter(line[currentChar], 0, 0, color);

						if (currentGlyphImage != null)
						{
							if (transformX + currentOffset.X < graphics2D.Width)
							{
								graphics2D.Render(currentGlyphImage, currentOffset);
							}
						}

						// get the advance for the next character
						currentOffset.X += TypeFaceStyle.GetAdvanceForCharacter(line, currentChar);
					}

					// before we go onto the next line we need to move down a line
					currentOffset.X = 0;
					currentOffset.Y -= TypeFaceStyle.EmSizeInPixels;
				}
			}
		}

		public override IEnumerable<VertexData> Vertices()
		{
			if (text != null && text.Length > 0)
			{
				var currentOffset = new Vector2(0, 0);

				currentOffset = GetBaseline(currentOffset);

				string[] lines = text.Split('\n');
				foreach (string line in lines)
				{
					currentOffset = GetXPositionForLineBasedOnJustification(currentOffset, line);

					// Snap this line's baseline to a whole pixel so its horizontal stems land on pixel
					// edges. Origin.Y is folded in first because Origin is baked into the vertex
					// positions below rather than carried by the transform - that is the path
					// Graphics2D.DrawString(text, x, y) uses to place text.
					//
					// The snap is applied to the *rendered* baseline while currentOffset.Y keeps
					// running unsnapped, so line spacing cannot accumulate drift: with a 12.6 pixel
					// em the baselines land at 0, -13, -25, -38 rather than 0, -13, -26, -39.
					double lineBaselineY = currentOffset.Y + Origin.Y;
					if (SnapBaselinesToWholePixels)
					{
						lineBaselineY = Math.Floor(lineBaselineY + 0.5);
					}

					for (int currentChar = 0; currentChar < line.Length; currentChar++)
					{
						IVertexSource currentGlyph = TypeFaceStyle.GetGlyphForCharacter(line[currentChar], ResolutionScale);

						if (currentGlyph != null)
						{
							foreach (VertexData vertexData in currentGlyph.Vertices())
							{
								if (vertexData.Command != FlagsAndCommand.Stop)
								{
									var offsetVertex = new VertexData(
										vertexData.Command,
										new Vector2(
											vertexData.Position.X + currentOffset.X + Origin.X,
											vertexData.Position.Y + lineBaselineY));
									yield return offsetVertex;
								}
							}
						}

						// get the advance for the next character
						currentOffset.X += TypeFaceStyle.GetAdvanceForCharacter(line, currentChar);
					}

					// before we go onto the next line we need to move down a line
					currentOffset.X = 0;
					currentOffset.Y -= TypeFaceStyle.EmSizeInPixels;
				}
			}

			var endVertex = new VertexData(FlagsAndCommand.Stop, Vector2.Zero);
			yield return endVertex;
		}

		private Vector2 GetXPositionForLineBasedOnJustification(Vector2 currentOffset, string line)
		{
			Vector2 size = GetSize(line);
			switch (Justification)
			{
				case Justification.Left:
					currentOffset.X = 0;
					break;

				case Justification.Center:
					currentOffset.X = -size.X / 2;
					break;

				case Justification.Right:
					currentOffset.X = -size.X;
					break;

				default:
					throw new NotImplementedException();
			}

			return currentOffset;
		}

		private Vector2 GetBaseline(Vector2 currentOffset)
		{
			switch (Baseline)
			{
				case Baseline.Text:
					currentOffset.Y = 0;
					break;

				case Baseline.BoundsTop:
					currentOffset.Y = -TypeFaceStyle.AscentInPixels;
					break;

				case Baseline.BoundsCenter:
					currentOffset.Y = -TypeFaceStyle.AscentInPixels / 2;
					break;

				default:
					throw new NotImplementedException();
			}

			return currentOffset;
		}

		public Vector2 GetSize(string text = null)
		{
			if (text == null)
			{
				text = this.text;
			}

			if (text != this.text)
			{
				GetSize(0, Math.Max(0, text.Length - 1), out Vector2 calculatedSize, text);
				return calculatedSize;
			}

			if (totalSizeCache.X == 0
				&& text != null)
			{
				GetSize(0, Math.Max(0, text.Length - 1), out Vector2 calculatedSize, text);
				totalSizeCache = calculatedSize;
			}

			return totalSizeCache;
		}

		public void GetSize(int characterToMeasureStartIndexInclusive, int characterToMeasureEndIndexInclusive, out Vector2 offset, string text = null)
		{
			if (text == null)
			{
				text = this.text;
			}

			offset.X = 0;
			offset.Y = TypeFaceStyle.EmSizeInPixels;

			double currentLineX = 0;

			for (int i = characterToMeasureStartIndexInclusive; i < characterToMeasureEndIndexInclusive; i++)
			{
				if (text[i] == '\n')
				{
					if (i + 1 < characterToMeasureEndIndexInclusive && (text[i + 1] == '\n') && text[i] != text[i + 1])
					{
						i++;
					}

					currentLineX = 0;
					offset.Y += TypeFaceStyle.EmSizeInPixels;
				}
				else
				{
					currentLineX += TypeFaceStyle.GetAdvanceForCharacter(text, i);

					if (currentLineX > offset.X)
					{
						offset.X = currentLineX;
					}
				}
			}

			if (text.Length > characterToMeasureEndIndexInclusive)
			{
				if (text[characterToMeasureEndIndexInclusive] == '\n')
				{
					offset.Y += TypeFaceStyle.EmSizeInPixels;
				}
				else
				{
					offset.X += TypeFaceStyle.GetAdvanceForCharacter(text, characterToMeasureEndIndexInclusive);
				}
			}
		}

		public int NumLines()
		{
			int characterToMeasureStartIndexInclusive = 0;
			int characterToMeasureEndIndexInclusive = text.Length - 1;
			return NumLines(characterToMeasureStartIndexInclusive, characterToMeasureEndIndexInclusive);
		}

		public int NumLines(int characterToMeasureStartIndexInclusive, int characterToMeasureEndIndexInclusive)
		{
			int numLines = 1;

			characterToMeasureStartIndexInclusive = Math.Max(0, Math.Min(characterToMeasureStartIndexInclusive, text.Length - 1));
			characterToMeasureEndIndexInclusive = Math.Max(-1, Math.Min(characterToMeasureEndIndexInclusive, text.Length - 1));
			for (int i = characterToMeasureStartIndexInclusive; i <= characterToMeasureEndIndexInclusive; i++)
			{
				if (text[i] == '\n')
				{
					numLines++;
				}
			}

			return numLines;
		}

		private Dictionary<char, double> fastAdvance = new Dictionary<char, double>();

		public void GetOffset(int characterToMeasureStartIndexInclusive, int characterToMeasureEndIndexInclusive, out Vector2 offset)
		{
			offset = Vector2.Zero;

			characterToMeasureEndIndexInclusive = Math.Min(text.Length - 1, characterToMeasureEndIndexInclusive);

			var startIndex = characterToMeasureStartIndexInclusive;
			// find the first '\n' before the characterIndex
			for (int i = characterToMeasureStartIndexInclusive; i <= characterToMeasureEndIndexInclusive; i++)
			{
				if (text[i] == '\n')
				{
					startIndex = i + 1;
					offset.Y -= TypeFaceStyle.EmSizeInPixels;
				}
			}
			characterToMeasureStartIndexInclusive = startIndex;

			for (int index = characterToMeasureStartIndexInclusive; index <= characterToMeasureEndIndexInclusive; index++)
			{
				if (text[index] == '\n')
				{
					offset.X = 0;
					offset.Y -= TypeFaceStyle.EmSizeInPixels;
				}
				else
				{
					if (!fastAdvance.ContainsKey(text[index]))
					{
						fastAdvance[text[index]] = TypeFaceStyle.GetAdvanceForCharacter(text, index);
					}

					offset.X += fastAdvance[text[index]];
				}
			}
		}

		// this will return the position to the left of the requested character.
		public Vector2 GetOffsetLeftOfCharacterIndex(int characterIndex)
		{
			GetOffset(0, characterIndex - 1, out Vector2 offset);
			return offset;
		}

		// If the Text is "TEXT" and the position is less than half the distance to the center
		// of "T" the return value will be 0 if it is between the center of 'T' and the center of 'E'
		// it will be 1 and so on.
		public int GetCharacterIndexToStartBefore(Vector2 position)
		{
			int clostestIndex = -1;
			double clostestXDistSquared = double.MaxValue;
			double clostestYDistSquared = double.MaxValue;
			var offset = new Vector2(0, TypeFaceStyle.EmSizeInPixels * NumLines() - TypeFaceStyle.EmSizeInPixels * .5);
			int characterToMeasureStartIndexInclusive = 0;
			int characterToMeasureEndIndexInclusive = text.Length - 1;
			if (text.Length > 0)
			{
				characterToMeasureStartIndexInclusive = Math.Max(0, Math.Min(characterToMeasureStartIndexInclusive, text.Length - 1));
				characterToMeasureEndIndexInclusive = Math.Max(0, Math.Min(characterToMeasureEndIndexInclusive, text.Length - 1));
				for (int i = characterToMeasureStartIndexInclusive; i <= characterToMeasureEndIndexInclusive; i++)
				{
					CheckForBetterClickPosition(ref position, ref clostestIndex, ref clostestXDistSquared, ref clostestYDistSquared, ref offset, i);

					if (text[i] == '\r')
					{
						throw new Exception("All \\r's should have been converted to \\n's.");
					}

					if (text[i] == '\n')
					{
						offset.X = 0;
						offset.Y -= TypeFaceStyle.EmSizeInPixels;
					}
					else
					{
						GetOffset(i, i, out Vector2 nextSize);

						offset.X += nextSize.X;
					}
				}

				CheckForBetterClickPosition(ref position, ref clostestIndex, ref clostestXDistSquared, ref clostestYDistSquared, ref offset, characterToMeasureEndIndexInclusive + 1);
			}

			return clostestIndex;
		}

		private static void CheckForBetterClickPosition(ref Vector2 position, ref int clostestIndex, ref double clostestXDistSquared, ref double clostestYDistSquared, ref Vector2 offset, int i)
		{
			Vector2 delta = position - offset;
			double deltaYLengthSquared = delta.Y * delta.Y;
			if (deltaYLengthSquared < clostestYDistSquared)
			{
				clostestYDistSquared = deltaYLengthSquared;
				clostestXDistSquared = delta.X * delta.X;
				clostestIndex = i;
			}
			else if (deltaYLengthSquared == clostestYDistSquared)
			{
				double deltaXLengthSquared = delta.X * delta.X;
				if (deltaXLengthSquared < clostestXDistSquared)
				{
					clostestXDistSquared = deltaXLengthSquared;
					clostestIndex = i;
				}
			}
		}

		/// <summary>
		/// The value <see cref="RenderIdentity"/> builds: two runs with equal identities emit identical
		/// vertices. See <see cref="RenderIdentity"/> for why each field is here.
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
		private sealed class TextRunIdentity : IEquatable<TextRunIdentity>
		{
			private readonly string text;
			private readonly TypeFace typeFace;
			private readonly double emSizeInPixels;
			private readonly bool underline;
			private readonly bool flattenCurves;
			private readonly double resolutionScale;
			private readonly Justification justification;
			private readonly Baseline baseline;
			private readonly Vector2 origin;
			private readonly bool snapBaselines;

			internal TextRunIdentity(
				string text,
				TypeFace typeFace,
				double emSizeInPixels,
				bool underline,
				bool flattenCurves,
				double resolutionScale,
				Justification justification,
				Baseline baseline,
				Vector2 origin,
				bool snapBaselines)
			{
				this.text = text;
				this.typeFace = typeFace;
				this.emSizeInPixels = emSizeInPixels;
				this.underline = underline;
				this.flattenCurves = flattenCurves;
				this.resolutionScale = resolutionScale;
				this.justification = justification;
				this.baseline = baseline;
				this.origin = origin;
				this.snapBaselines = snapBaselines;
			}

			public bool Equals(TextRunIdentity other)
			{
				return other != null
					&& this.text == other.text
					&& object.ReferenceEquals(this.typeFace, other.typeFace)
					&& BitConverter.DoubleToInt64Bits(this.emSizeInPixels) == BitConverter.DoubleToInt64Bits(other.emSizeInPixels)
					&& this.underline == other.underline
					&& this.flattenCurves == other.flattenCurves
					&& BitConverter.DoubleToInt64Bits(this.resolutionScale) == BitConverter.DoubleToInt64Bits(other.resolutionScale)
					&& this.justification == other.justification
					&& this.baseline == other.baseline
					&& BitConverter.DoubleToInt64Bits(this.origin.X) == BitConverter.DoubleToInt64Bits(other.origin.X)
					&& BitConverter.DoubleToInt64Bits(this.origin.Y) == BitConverter.DoubleToInt64Bits(other.origin.Y)
					&& this.snapBaselines == other.snapBaselines;
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
				hash.Add(BitConverter.DoubleToInt64Bits(this.resolutionScale));
				hash.Add((int)this.justification);
				hash.Add((int)this.baseline);
				hash.Add(BitConverter.DoubleToInt64Bits(this.origin.X));
				hash.Add(BitConverter.DoubleToInt64Bits(this.origin.Y));
				hash.Add(this.snapBaselines);

				return hash.ToHashCode();
			}
		}
	}
}