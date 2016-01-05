using MatterHackers.Agg.Transform;
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
// Class TypeFace.cs
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MatterHackers.Agg.Font
{
	public class TypeFace
	{
		private class Glyph
		{
			public int horiz_adv_x;
			public int unicode;
			public string glyphName;
			public PathStorage glyphData = new PathStorage();
		}

		private class Panos_1
		{
			// these are defined in the order in which they are present in the panos-1 attribute.
			private enum Family { Any, No_Fit, Latin_Text_and_Display, Latin_Script, Latin_Decorative, Latin_Pictorial };

			private enum Serif_Style { Any, No_Fit, Cove, Obtuse_Cove, Square_Cove, Obtuse_Square_Cove, Square, Thin, Bone, Exaggerated, Triangle, Normal_Sans, Obtuse_Sans, Perp_Sans, Flared, Rounded };

			private enum Weight { Any, No_Fit, Very_Light_100, Light_200, Thin_300, Book_400_same_as_CSS1_normal, Medium_500, Demi_600, Bold_700_same_as_CSS1_bold, Heavy_800, Black_900, Extra_Black_Nord_900_force_mapping_to_CSS1_100_900_scale };

			private enum Proportion { Any, No_Fit, Old_Style, Modern, Even_Width, Expanded, Condensed, Very_Expanded, Very_Condensed, Monospaced };

			private enum Contrast { Any, No_Fit, None, Very_Low, Low, Medium_Low, Medium, Medium_High, High, Very_High };

			private enum Stroke_Variation { Any, No_Fit, No_Variation, Gradual_Diagonal, Gradual_Transitional, Gradual_Vertical, Gradual_Horizontal, Rapid_Vertical, Rapid_Horizontal, Instant_Horizontal, Instant_Vertical };

			private enum Arm_Style { Any, No_Fit, Straight_Arms_Horizontal, Straight_Arms_Wedge, Straight_Arms_Vertical, Straight_Arms_Single_Serif, Straight_Arms_Double_Serif, Non_Straight_Arms_Horizontal, Non_Straight_Arms_Wedge, Non_Straight_Arms_Vertical_90, Non_Straight_Arms_Single_Serif, Non_Straight_Arms_Double_Serif };

			private enum Letterform { Any, No_Fit, Normal_Contact, Normal_Weighted, Normal_Boxed, Normal_Flattened, Normal_Rounded, Normal_Off_Center, Normal_Square, Oblique_Contact, Oblique_Weighted, Oblique_Boxed, Oblique_Flattened, Oblique_Rounded, Oblique_Off_Center, Oblique_Square };

			private enum Midline { Any, No_Fit, Standard_Trimmed, Standard_Pointed, Standard_Serifed, High_Trimmed, High_Pointed, High_Serifed, Constant_Trimmed, Constant_Pointed, Constant_Serifed, Low_Trimmed, Low_Pointed, Low_Serifed };

			private enum XHeight { Any, No_Fit, Constant_Small, Constant_Standard, Constant_Large, Ducking_Small, Ducking_Standard, Ducking_Large };

			private Family family;
			private Serif_Style serifStyle;
			private Weight weight;
			private Proportion proportion;
			private Contrast contrast;
			private Stroke_Variation strokeVariation;
			private Arm_Style armStyle;
			private Letterform letterform;
			private Midline midline;
			private XHeight xHeight;

			public Panos_1(String SVGPanos1String)
			{
				int tempInt;
				String[] valuesString = SVGPanos1String.Split(' ');
				if (int.TryParse(valuesString[0], out tempInt))
					family = (Family)tempInt;
				if (int.TryParse(valuesString[1], out tempInt))
					serifStyle = (Serif_Style)tempInt;
				if (int.TryParse(valuesString[2], out tempInt))
					weight = (Weight)tempInt;
				if (int.TryParse(valuesString[3], out tempInt))
					proportion = (Proportion)tempInt;
				if (int.TryParse(valuesString[4], out tempInt))
					contrast = (Contrast)tempInt;
				if (int.TryParse(valuesString[5], out tempInt))
					strokeVariation = (Stroke_Variation)tempInt;
				if (int.TryParse(valuesString[6], out tempInt))
					armStyle = (Arm_Style)tempInt;
				if (int.TryParse(valuesString[7], out tempInt))
					letterform = (Letterform)tempInt;
				if (int.TryParse(valuesString[8], out tempInt))
					midline = (Midline)tempInt;
				if (int.TryParse(valuesString[0], out tempInt))
					xHeight = (XHeight)tempInt;
			}
		}

		private String fontId;
		private int horiz_adv_x;
		private String fontFamily;
		private int font_weight;
		private String font_stretch;
		private int unitsPerEm;
		private Panos_1 panose_1;
		private int ascent;

		public int Ascent { get { return ascent; } }

		private int descent;

		public int Descent { get { return descent; } }

		private int x_height;

		public int X_height { get { return x_height; } }

		private int cap_height;

		public int Cap_height { get { return cap_height; } }

		private RectangleInt boundingBox;

		public RectangleInt BoundingBox { get { return boundingBox; } }

		private int underline_thickness;

		public int Underline_thickness { get { return underline_thickness; } }

		private int underline_position;

		public int Underline_position { get { return underline_position; } }

		private String unicode_range;

		private Glyph missingGlyph;

		private Dictionary<int, Glyph> glyphs = new Dictionary<int, Glyph>(); // a glyph is indexed by the string it represents, usually one character, but sometimes multiple
		private Dictionary<Char, Dictionary<Char, int>> HKerns = new Dictionary<char, Dictionary<char, int>>();

		public int UnitsPerEm
		{
			get
			{
				return unitsPerEm;
			}
		}

		private static String GetSubString(String source, String start, String end)
		{
			int startIndex = 0;
			return GetSubString(source, start, end, ref startIndex);
		}

		private static String GetSubString(String source, String start, String end, ref int startIndex)
		{
			int startPos = source.IndexOf(start, startIndex);
			if (startPos >= 0)
			{
				int endPos = source.IndexOf(end, startPos + start.Length);

				int length = endPos - (startPos + start.Length);
				startIndex = endPos + end.Length; // advance our start position to the last position used
				return source.Substring(startPos + start.Length, length);
			}

			return null;
		}

		private static String GetStringValue(String source, String name)
		{
			String element = GetSubString(source, name + "=\"", "\"");
			return element;
		}

		private static bool GetIntValue(String source, String name, out int outValue, ref int startIndex)
		{
			String element = GetSubString(source, name + "=\"", "\"", ref startIndex);
			if (int.TryParse(element, NumberStyles.Number, null, out outValue))
			{
				return true;
			}

			return false;
		}

		private static bool GetIntValue(String source, String name, out int outValue)
		{
			int startIndex = 0;
			return GetIntValue(source, name, out outValue, ref startIndex);
		}

		public static TypeFace LoadFrom(string content)
		{
			TypeFace fontUnderConstruction = new TypeFace();
			fontUnderConstruction.ReadSVG(content);

			return fontUnderConstruction;
		}

		public static TypeFace LoadSVG(String filename)
		{
			TypeFace fontUnderConstruction = new TypeFace();

			string svgContent = "";
			using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				using (StreamReader reader = new StreamReader(fileStream))
				{
					svgContent = reader.ReadToEnd();
				}
			}
			fontUnderConstruction.ReadSVG(svgContent);

			return fontUnderConstruction;
		}

		private Glyph CreateGlyphFromSVGGlyphData(String SVGGlyphData)
		{
			Glyph newGlyph = new Glyph();
			if (!GetIntValue(SVGGlyphData, "horiz-adv-x", out newGlyph.horiz_adv_x))
			{
				newGlyph.horiz_adv_x = horiz_adv_x;
			}

			newGlyph.glyphName = GetStringValue(SVGGlyphData, "glyph-name");
			String unicodeString = GetStringValue(SVGGlyphData, "unicode");

			if (unicodeString != null)
			{
				if (unicodeString.Length == 1)
				{
					newGlyph.unicode = (int)unicodeString[0];
				}
				else
				{
					if (unicodeString.Split(';').Length > 1 && unicodeString.Split(';')[1].Length > 0)
					{
						throw new NotImplementedException("We do not currently support glyphs longer than one character.  You need to wirite the seach so that it will find them if you want to support this");
					}

					if (int.TryParse(unicodeString, NumberStyles.Number, null, out newGlyph.unicode) == false)
					{
						// see if it is a unicode
						String hexNumber = GetSubString(unicodeString, "&#x", ";");
						int.TryParse(hexNumber, NumberStyles.HexNumber, null, out newGlyph.unicode);
					}
				}
			}

			String dString = GetStringValue(SVGGlyphData, "d");

			if (dString == null || dString.Length == 0)
			{
				return newGlyph;
			}

			newGlyph.glyphData.ParseSvgDString(dString);

			return newGlyph;
		}

		public void ReadSVG(String svgContent)
		{
			int startIndex = 0;
			String fontElementString = GetSubString(svgContent, "<font", ">", ref startIndex);
			fontId = GetStringValue(fontElementString, "id");
			GetIntValue(fontElementString, "horiz-adv-x", out horiz_adv_x);

			String fontFaceString = GetSubString(svgContent, "<font-face", "/>", ref startIndex);
			fontFamily = GetStringValue(fontFaceString, "font-family");
			GetIntValue(fontFaceString, "font-weight", out font_weight);
			font_stretch = GetStringValue(fontFaceString, "font-stretch");
			GetIntValue(fontFaceString, "units-per-em", out unitsPerEm);
			panose_1 = new Panos_1(GetStringValue(fontFaceString, "panose-1"));
			GetIntValue(fontFaceString, "ascent", out ascent);
			GetIntValue(fontFaceString, "descent", out descent);
			GetIntValue(fontFaceString, "x-height", out x_height);
			GetIntValue(fontFaceString, "cap-height", out cap_height);

			String bboxString = GetStringValue(fontFaceString, "bbox");
			String[] valuesString = bboxString.Split(' ');
			int.TryParse(valuesString[0], out boundingBox.Left);
			int.TryParse(valuesString[1], out boundingBox.Bottom);
			int.TryParse(valuesString[2], out boundingBox.Right);
			int.TryParse(valuesString[3], out boundingBox.Top);

			GetIntValue(fontFaceString, "underline-thickness", out underline_thickness);
			GetIntValue(fontFaceString, "underline-position", out underline_position);
			unicode_range = GetStringValue(fontFaceString, "unicode-range");

			String missingGlyphString = GetSubString(svgContent, "<missing-glyph", "/>", ref startIndex);
			missingGlyph = CreateGlyphFromSVGGlyphData(missingGlyphString);

			String nextGlyphString = GetSubString(svgContent, "<glyph", "/>", ref startIndex);
			while (nextGlyphString != null)
			{
				// get the data and put it in the glyph dictionary
				Glyph newGlyph = CreateGlyphFromSVGGlyphData(nextGlyphString);
				if (newGlyph.unicode > 0)
				{
					glyphs.Add(newGlyph.unicode, newGlyph);
				}

				nextGlyphString = GetSubString(svgContent, "<glyph", "/>", ref startIndex);
			}
		}

		internal IVertexSource GetGlyphForCharacter(char character)
		{
			// TODO: check for multi character glyphs (we don't currently support them in the reader).
			Glyph glyph;
			if (glyphs.TryGetValue(character, out glyph))
			{
				PathStorage writeableGlyph = new PathStorage();
				writeableGlyph.ShareVertexData(glyph.glyphData);
				return writeableGlyph;
			}

			return null;
		}

		internal int GetAdvanceForCharacter(char character, char nextCharacterToKernWith)
		{
			// TODO: check for kerning and adjust
			Glyph glyph;
			if (glyphs.TryGetValue(character, out glyph))
			{
				return glyph.horiz_adv_x;
			}

			return 0;
		}

		internal int GetAdvanceForCharacter(char character)
		{
			Glyph glyph;
			if (glyphs.TryGetValue(character, out glyph))
			{
				return glyph.horiz_adv_x;
			}

			return 0;
		}

		public void ShowDebugInfo(Graphics2D graphics2D)
		{
			StyledTypeFace typeFaceNameStyle = new StyledTypeFace(this, 30);
			TypeFacePrinter fontNamePrinter = new TypeFacePrinter(this.fontFamily + " - 30 point", typeFaceNameStyle);

			RectangleDouble bounds = typeFaceNameStyle.BoundingBoxInPixels;
			double origX = 10 - bounds.Left;
			double x = origX;
			double y = 10 - typeFaceNameStyle.DescentInPixels;
			int width = 50;
			RGBA_Bytes boundingBoxColor = new RGBA_Bytes(0, 0, 0);
			RGBA_Bytes originColor = new RGBA_Bytes(0, 0, 0);
			RGBA_Bytes ascentColor = new RGBA_Bytes(255, 0, 0);
			RGBA_Bytes descentColor = new RGBA_Bytes(255, 0, 0);
			RGBA_Bytes xHeightColor = new RGBA_Bytes(12, 25, 200);
			RGBA_Bytes capHeightColor = new RGBA_Bytes(12, 25, 200);
			RGBA_Bytes underlineColor = new RGBA_Bytes(0, 150, 55);

			// the origin
			graphics2D.Line(x, y, x + width, y, originColor);

			graphics2D.Rectangle(x + bounds.Left, y + bounds.Bottom, x + bounds.Right, y + bounds.Top, boundingBoxColor);

			x += typeFaceNameStyle.BoundingBoxInPixels.Width * 1.5;

			width = width * 3;

			double temp = typeFaceNameStyle.AscentInPixels;
			graphics2D.Line(x, y + temp, x + width, y + temp, ascentColor);

			temp = typeFaceNameStyle.DescentInPixels;
			graphics2D.Line(x, y + temp, x + width, y + temp, descentColor);

			temp = typeFaceNameStyle.XHeightInPixels;
			graphics2D.Line(x, y + temp, x + width, y + temp, xHeightColor);

			temp = typeFaceNameStyle.CapHeightInPixels;
			graphics2D.Line(x, y + temp, x + width, y + temp, capHeightColor);

			temp = typeFaceNameStyle.UnderlinePositionInPixels;
			graphics2D.Line(x, y + temp, x + width, y + temp, underlineColor);

			Affine textTransform;
			textTransform = Affine.NewIdentity();
			textTransform *= Affine.NewTranslation(10, origX);

			VertexSourceApplyTransform transformedText = new VertexSourceApplyTransform(textTransform);
			fontNamePrinter.Render(graphics2D, RGBA_Bytes.Black, transformedText);

			graphics2D.Render(transformedText, RGBA_Bytes.Black);

			// render the legend
			StyledTypeFace legendFont = new StyledTypeFace(this, 12);
			Vector2 textPos = new Vector2(x + width / 2, y + typeFaceNameStyle.EmSizeInPixels * 1.5);
			graphics2D.Render(new TypeFacePrinter("Descent"), textPos, descentColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("Underline"), textPos, underlineColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("X Height"), textPos, xHeightColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("CapHeight"), textPos, capHeightColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("Ascent"), textPos, ascentColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("Origin"), textPos, originColor); textPos.y += legendFont.EmSizeInPixels;
			graphics2D.Render(new TypeFacePrinter("Bounding Box"), textPos, boundingBoxColor);
		}
	}
}