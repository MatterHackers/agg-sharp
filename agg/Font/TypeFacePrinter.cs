using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2011
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

namespace MatterHackers.Agg.Font
{
	public enum Justification { Left, Center, Right };

	public enum Baseline { BoundsTop, BoundsCenter, TextCenter, Text, BoundsBottom };

	public class TypeFacePrinter : VertexSourceLegacySupport
	{
		private String text = "";

		private Vector2 totalSizeCache;

		public Justification Justification { get; set; }

		public Baseline Baseline { get; set; }

		public bool DrawFromHintedCache { get; set; }

		StyledTypeFace typeFaceStyle;
		public StyledTypeFace TypeFaceStyle
		{
			get { return typeFaceStyle; }
			
			set
			{
				if (value != typeFaceStyle)
				{
					typeFaceStyle = value;
					totalSizeCache = new Vector2();
				}
			}
		}

		public String Text
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
				}
			}
		}

		public Vector2 Origin { get; set; }

		public double ResolutionScale { get; set; } = 1;

		public TypeFacePrinter(String text = "", double pointSize = 12, Vector2 origin = new Vector2(), Justification justification = Justification.Left, Baseline baseline = Baseline.Text, bool bold = false)
			: this(text, 
				  bold ? new StyledTypeFace(LiberationSansBoldFont.Instance, pointSize)  : new StyledTypeFace(LiberationSansFont.Instance, pointSize), 
				  origin, justification, baseline)
		{
		}

		public TypeFacePrinter(String text, StyledTypeFace typeFaceStyle, Vector2 origin = new Vector2(), Justification justification = Justification.Left, Baseline baseline = Baseline.Text)
		{
			this.TypeFaceStyle = typeFaceStyle;
			this.text = text;
			this.Justification = justification;
			this.Origin = origin;
			this.Baseline = baseline;
		}

		public TypeFacePrinter(String text, TypeFacePrinter copyPropertiesFrom)
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
			rewind(0);
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
				rewind(0);
				graphics2D.Render(this, color);
			}
		}

		private void RenderFromCache(Graphics2D graphics2D, Color color)
		{
			if (text != null && text.Length > 0)
			{
				Vector2 currentOffset = Vector2.Zero;

				currentOffset = GetBaseline(currentOffset);
				// remove the decent and 1 pixel that were put into the cache image to give space for descenders
				currentOffset.Y += (Origin.Y + TypeFaceStyle.DescentInPixels - 1);

				string[] lines = text.Split('\n');
				foreach (string line in lines)
				{
					currentOffset = GetXPositionForLineBasedOnJustification(currentOffset, line);
					currentOffset.X += Origin.X;

					for (int currentChar = 0; currentChar < line.Length; currentChar++)
					{
						ImageBuffer currentGlyphImage = TypeFaceStyle.GetImageForCharacter(line[currentChar], 0, 0, color);

						if (currentGlyphImage != null)
						{
							graphics2D.Render(currentGlyphImage, currentOffset);
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
				Vector2 currentOffset = new Vector2(0, 0);

				currentOffset = GetBaseline(currentOffset);

				string[] lines = text.Split('\n');
				foreach (string line in lines)
				{
					currentOffset = GetXPositionForLineBasedOnJustification(currentOffset, line);

					for (int currentChar = 0; currentChar < line.Length; currentChar++)
					{
						IVertexSource currentGlyph = TypeFaceStyle.GetGlyphForCharacter(line[currentChar], ResolutionScale);

						if (currentGlyph != null)
						{
							foreach (VertexData vertexData in currentGlyph.Vertices())
							{
								if (vertexData.command != ShapePath.FlagsAndCommand.Stop)
								{
									VertexData offsetVertex = new VertexData(vertexData.command, vertexData.position + currentOffset + Origin);
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

			VertexData endVertex = new VertexData(ShapePath.FlagsAndCommand.Stop, Vector2.Zero);
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
				Vector2 calculatedSize;
				GetSize(0, Math.Max(0, text.Length - 1), out calculatedSize, text);
				return calculatedSize;
			}

			if (totalSizeCache.X == 0)
			{
				Vector2 calculatedSize;
				GetSize(0, Math.Max(0, text.Length - 1), out calculatedSize, text);
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
					currentLineX = 0;
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
			characterToMeasureEndIndexInclusive = Math.Max(0, Math.Min(characterToMeasureEndIndexInclusive, text.Length - 1));
			for (int i = characterToMeasureStartIndexInclusive; i < characterToMeasureEndIndexInclusive; i++)
			{
				if (text[i] == '\n')
				{
					if (i + 1 < characterToMeasureEndIndexInclusive && (text[i + 1] == '\n') && text[i] != text[i + 1])
					{
						i++;
					}
					numLines++;
				}
			}

			return numLines;
		}

		public void GetOffset(int characterToMeasureStartIndexInclusive, int characterToMeasureEndIndexInclusive, out Vector2 offset)
		{
			offset = Vector2.Zero;

			characterToMeasureEndIndexInclusive = Math.Min(text.Length - 1, characterToMeasureEndIndexInclusive);

			for (int index = characterToMeasureStartIndexInclusive; index <= characterToMeasureEndIndexInclusive; index++)
			{
				if (text[index] == '\n')
				{
					offset.X = 0;
					offset.Y -= TypeFaceStyle.EmSizeInPixels;
				}
				else
				{
					offset.X += TypeFaceStyle.GetAdvanceForCharacter(text, index);
				}
			}
		}

		// this will return the position to the left of the requested character.
		public Vector2 GetOffsetLeftOfCharacterIndex(int characterIndex)
		{
			Vector2 offset;
			GetOffset(0, characterIndex - 1, out offset);
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
			Vector2 offset = new Vector2(0, TypeFaceStyle.EmSizeInPixels * NumLines());
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
						Vector2 nextSize;
						GetOffset(i, i, out nextSize);

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
	}
}