using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

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
// Class StyledTypeFace.cs
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.Font
{
	public class GlyphWithUnderline : VertexSourceLegacySupport
	{
		private IVertexSource underline;
		private IVertexSource glyph;

		public GlyphWithUnderline(IVertexSource glyph, int advanceForCharacter, int Underline_position, int Underline_thickness)
		{
			underline = new RoundedRect(new RectangleDouble(0, Underline_position, advanceForCharacter, Underline_position + Underline_thickness), 0);
			this.glyph = glyph;
		}

		public override IEnumerable<VertexData> Vertices()
		{
			// return all the data for the glyph
			foreach (VertexData vertexData in glyph.Vertices())
			{
				if (ShapePath.IsStop(vertexData.Command))
				{
					break;
				}
				yield return vertexData;
			}

			// then the underline
			foreach (VertexData vertexData in underline.Vertices())
			{
				yield return vertexData;
			}
		}
	}

	public class StyledTypeFaceImageCache
	{
		private static readonly StyledTypeFaceImageCache instance = new StyledTypeFaceImageCache();

		// Guards typeFaceImageCache and the leaf per-character dictionaries it hands out.
		// A single static lock (rather than locking the caller-supplied TypeFace) so that
		// concurrent callers with different TypeFaces still serialize access to the shared cache.
		internal static readonly object SyncRoot = new object();

		// Upper bound on the total number of cached glyph images across all (TypeFace, color, size)
		// styles. A glyph ImageBuffer is roughly emSizeInPixels^2 * 4 bytes, so at a typical UI em of
		// ~20px that is ~1.6KB per image and 8192 images is ~13MB worst case (~100MB at a large 64px
		// em). When an insert would exceed the cap the whole cache is cleared and repopulates on
		// demand. Internal (not const) so tests can lower it to force eviction.
		internal static int MaxCachedImages = 8192;

		// Total images across all leaf dictionaries; guarded by SyncRoot.
		private int cachedImageCount;

        // Keys: TypeFace, Color, FontSize, Character
        private Dictionary<TypeFace, Dictionary<Color, Dictionary<double, Dictionary<int, ImageBuffer>>>> typeFaceImageCache = new Dictionary<TypeFace, Dictionary<Color, Dictionary<double, Dictionary<int, ImageBuffer>>>>();

		// private so you can't use it by accident (it is a singleton)
		private StyledTypeFaceImageCache()
		{
		}

		internal static int CachedImageCount
		{
			get
			{
				lock (SyncRoot)
				{
					return Instance.cachedImageCount;
				}
			}
		}

		internal static bool TryGetImage(TypeFace typeFace, Color color, double emSizeInPoints, int character, out ImageBuffer image)
		{
			lock (SyncRoot)
			{
				return GetCorrectCache(typeFace, color, emSizeInPoints).TryGetValue(character, out image);
			}
		}

		/// <summary>
		/// Caches <paramref name="image"/> for the character, or keeps the image already cached for it.
		/// </summary>
		/// <returns>
		/// The instance the cache retains, which is <paramref name="image"/> only when this call was the one
		/// that inserted it. Callers must use the returned instance rather than the one they passed in.
		/// </returns>
		/// <remarks>
		/// Rendering happens outside the lock, so two threads can both miss on the same character, both render
		/// it, and both arrive here. First store wins: replacing the entry would give identical pixels under a
		/// new instance, and consumers key on the instance - a texture or mask cache keyed on the glyph image
		/// would double its entries, and every reference handed out before the swap would point at an image the
		/// cache no longer knows about. Returning the retained instance is what lets the losing thread join the
		/// winner instead of walking away with an orphan.
		/// </remarks>
		internal static ImageBuffer StoreImage(TypeFace typeFace, Color color, double emSizeInPoints, int character, ImageBuffer image)
		{
			lock (SyncRoot)
			{
				var characterImageCache = GetCorrectCache(typeFace, color, emSizeInPoints);
				if (characterImageCache.TryGetValue(character, out ImageBuffer alreadyCached))
				{
					// Keeping first only ever keeps an entry that is present in the live cache under this
					// lock, so it cannot resurrect anything the cap evicted: an eviction empties the
					// dictionaries, and a store arriving afterwards misses here and inserts normally.
					return alreadyCached;
				}

				if (Instance.cachedImageCount + 1 > MaxCachedImages)
				{
					// Simplest provably-correct eviction: drop everything and let renders
					// repopulate on demand. The leaf dictionary must be re-fetched because
					// Clear() orphaned the one we navigated to above.
					Clear();
					characterImageCache = GetCorrectCache(typeFace, color, emSizeInPoints);
				}

				Instance.cachedImageCount++;
				characterImageCache[character] = image;

				return image;
			}
		}

		/// <summary>
		/// Drops every cached glyph image.
		/// </summary>
		/// <remarks>
		/// Public rather than internal as groundwork, not because anything calls it from outside today:
		/// nothing does, and nothing needs to yet, because the hinted-cache path these images serve never
		/// takes the LCD path - it blits pre-rendered glyph images and is unaffected by
		/// <c>LcdRenderSettings</c>. What is coming is the settings-toggle invalidation chain (the LCD plan's
		/// stage 8), where the application layer that owns the toggle UI lives in another assembly and has to
		/// be able to drop every cache holding pixels rendered under the old settings. This is one of them,
		/// and internal would put it out of reach.
		/// <para>
		/// It is also the eviction the cap uses, and the reason the cap can be as simple as it is: dropping
		/// everything is provably correct where evicting a chosen entry has to answer "which one".
		/// </para>
		/// </remarks>
		public static void Clear()
		{
			lock (SyncRoot)
			{
				Instance.typeFaceImageCache.Clear();
				Instance.cachedImageCount = 0;
			}
		}

		private static Dictionary<int, ImageBuffer> GetCorrectCache(TypeFace typeFace, Color color, double emSizeInPoints)
		{
			lock (SyncRoot)
			{
				Dictionary<Color, Dictionary<double, Dictionary<int, ImageBuffer>>> foundTypeFaceColor;
				if (!Instance.typeFaceImageCache.TryGetValue(typeFace, out foundTypeFaceColor))
				{
					// add in the type face
					foundTypeFaceColor = new Dictionary<Color, Dictionary<double, Dictionary<int, ImageBuffer>>>();
					Instance.typeFaceImageCache.Add(typeFace, foundTypeFaceColor);
				}

				Dictionary<double, Dictionary<int, ImageBuffer>> foundTypeFaceSizes;
				if (!foundTypeFaceColor.TryGetValue(color, out foundTypeFaceSizes))
				{
					// add in the type face
					foundTypeFaceSizes = new Dictionary<double, Dictionary<int, ImageBuffer>>();
					foundTypeFaceColor.Add(color, foundTypeFaceSizes);
				}

				Dictionary<int, ImageBuffer> foundTypeFaceSize;
				if (!foundTypeFaceSizes.TryGetValue(emSizeInPoints, out foundTypeFaceSize))
				{
					// add in the point size
					foundTypeFaceSize = new Dictionary<int, ImageBuffer>();
					foundTypeFaceSizes.Add(emSizeInPoints, foundTypeFaceSize);
				}

				return foundTypeFaceSize;
			}
		}

		private static StyledTypeFaceImageCache Instance => instance;
	}

	public class StyledTypeFace
	{
		public TypeFace TypeFace { get; private set; }

		public const int PointsPerInch = 72;
		public const int PixelsPerInch = 96;

		private double emSizeInPixels;
		private double currentEmScaling;
		private bool flattenCurves = true;

		public StyledTypeFace(TypeFace typeFace, double emSizeInPoints, bool underline = false, bool flattenCurves = true)
		{
			this.TypeFace = typeFace;
			emSizeInPixels = emSizeInPoints / PointsPerInch * PixelsPerInch;
			currentEmScaling = emSizeInPixels / typeFace.UnitsPerEm;
			DoUnderline = underline;
			FlattenCurves = flattenCurves;
		}

		public bool DoUnderline { get; set; }

		/// <summary>
		/// Whether this face's glyphs and advances follow the process-wide <see cref="TextStyleSettings"/>. Off by
		/// default, so text drawn as geometry (a CAD text object, SVG text, a demo's label) stays exactly the font's
		/// whatever the settings say; UI text - <c>TextWidget</c> and what is built on it, the code and rich text
		/// editors - turns it on.
		/// </summary>
		public bool ApplyTextStyleSettings { get; set; }

		/// <summary>True when this face opts in and a setting is away from its default.</summary>
		internal bool IsStyled => ApplyTextStyleSettings && !TextStyleSettings.GlyphStyleIsIdentity;

		/// <summary>
		/// <para>If true the font will have it's curves flattened to the current point size when retrieved.</para>
		/// <para>You may want to disable this so you can flatten the curve after other transforms have been applied,</para>
		/// <para>such as skewing or scaling.  Rotation and Translation will not alter how a curve is flattened.</para>
		/// </summary>
		public bool FlattenCurves
		{
			get => flattenCurves;
			set => flattenCurves = value;
		}

		/// <summary>
		/// Sets the Em size for the font in pixels.
		/// </summary>
		public double EmSizeInPixels => emSizeInPixels;

		/// <summary>
		/// Sets the Em size for the font assuming there are 72 points per inch and there are 96 pixels per inch.
		/// </summary>
		public double EmSizeInPoints => emSizeInPixels / PixelsPerInch * PointsPerInch;

		public double AscentInPixels => TypeFace.Ascent * currentEmScaling;

		public double DescentInPixels => TypeFace.Descent * currentEmScaling;

		public double XHeightInPixels => TypeFace.X_height * currentEmScaling;

		public double CapHeightInPixels => TypeFace.Cap_height * currentEmScaling;

		public RectangleDouble BoundingBoxInPixels
		{
			get
			{
				RectangleDouble pixelBounds = new RectangleDouble(TypeFace.BoundingBox);
				pixelBounds *= currentEmScaling;
				return pixelBounds;
			}
		}

		public double UnderlineThicknessInPixels => TypeFace.Underline_thickness * currentEmScaling;

		public double UnderlinePositionInPixels => TypeFace.Underline_position * currentEmScaling;

		public ImageBuffer GetImageForCharacter(char character, double xFraction, double yFraction, Color color)
		{
			return GetImageForCodePoint(character, xFraction, yFraction, color);
		}

		/// <summary>
		/// The cached image of one code point's glyph, which may come from a <see cref="Font.TypeFace.Fallback"/>
		/// face. <see cref="GetImageForCharacter"/> for a code point outside the BMP.
		/// </summary>
		public ImageBuffer GetImageForCodePoint(int codePoint, double xFraction, double yFraction, Color color)
		{
			if (xFraction > 1 || xFraction < 0 || yFraction > 1 || yFraction < 0)
			{
				throw new ArgumentException("The x and y fractions must both be between 0 and 1.");
			}

			// The cache key names the face, colour and size but not the style, so a styled glyph is drawn fresh
			// rather than sharing an entry with the same face drawn plain.
			bool styled = IsStyled;
			if (!styled && StyledTypeFaceImageCache.TryGetImage(this.TypeFace, color, emSizeInPixels, codePoint, out ImageBuffer imageForCharacter))
			{
				return imageForCharacter;
			}

			IVertexSource glyphForCharacter = GetGlyphForCodePoint(codePoint, 1);
			if (glyphForCharacter == null)
			{
				return null;
			}

			var bounds = glyphForCharacter.GetBounds();

			var charImage = new ImageBuffer(
				Math.Max((int)(bounds.Right + .5), 1) + 1,
				Math.Max((int)Math.Ceiling(EmSizeInPixels + (-DescentInPixels) + .5), 1) + 1,
				32,
				new BlenderPreMultBGRA());

			var graphics = charImage.NewGraphics2D();
			graphics.Render(glyphForCharacter, xFraction, yFraction + (-DescentInPixels) + 1, color);

			if (styled)
			{
				return charImage;
			}

			// Rendering happens outside the lock, so another thread may have cached this character while we
			// were drawing it. Return whatever the cache kept, not necessarily what we just drew, so that
			// every caller of this character holds the same instance.
			return StyledTypeFaceImageCache.StoreImage(this.TypeFace, color, emSizeInPixels, codePoint, charImage);
		}

		public IVertexSource GetGlyphForCharacter(char character, double resolutionScale = 1)
		{
			return GetGlyphForCodePoint(character, resolutionScale);
		}

		/// <summary>
		/// One code point's outline at this size, taken from <see cref="TypeFace"/> or, when it has no glyph for
		/// it, from the first <see cref="Font.TypeFace.Fallback"/> that does. Null when no face in the chain has it
		/// and the primary has nothing to draw for a missing glyph.
		/// </summary>
		public IVertexSource GetGlyphForCodePoint(int codePoint, double resolutionScale = 1)
		{
			TypeFace face = TypeFace.ResolveFace(codePoint);

			// scale it to the correct size.
			IVertexSource sourceGlyph = face.GetGlyphForCodePoint(codePoint);
			if (sourceGlyph != null)
			{
				if (DoUnderline)
				{
					// The primary face's underline, converted to the drawing face's units, so a fallback glyph's
					// underline lines up with its neighbours'. For the primary itself the factor is exactly 1.
					double toFaceUnits = (double)face.UnitsPerEm / TypeFace.UnitsPerEm;
					sourceGlyph = new GlyphWithUnderline(
						sourceGlyph,
						face.GetAdvanceForCodePoint(codePoint),
						(int)Math.Round(TypeFace.Underline_position * toFaceUnits),
						(int)Math.Round(TypeFace.Underline_thickness * toFaceUnits));
				}

				if (IsStyled)
				{
					return GetStyledGlyph(sourceGlyph, EmScalingFor(face), resolutionScale);
				}

				var glyphTransform = Affine.NewIdentity();
				glyphTransform *= Affine.NewScaling(EmScalingFor(face));
				IVertexSource characterGlyph = new VertexSourceApplyTransform(sourceGlyph, glyphTransform);

				if (FlattenCurves)
				{
					characterGlyph = new FlattenCurves(characterGlyph)
					{
						ResolutionScale = resolutionScale
					};
				}

				return characterGlyph;
			}

			return null;
		}

		/// <summary>
		/// The advance, in pixels, of the glyph that starts at <paramref name="characterIndex"/>. A surrogate pair
		/// is one glyph: its leading half carries the whole advance and its trailing half none, so index-based
		/// measuring (carets, selections, wrapping) stays aligned with the string.
		/// </summary>
		public double GetAdvanceForCharacter(string line, int characterIndex)
		{
			if (char.IsSurrogate(line[characterIndex]))
			{
				int codePoint = GetCodePointAt(line, characterIndex);
				return codePoint < 0 ? 0 : GetAdvanceForCodePoint(codePoint);
			}

			if (characterIndex < line.Length - 1)
			{
				// pass the next char so the typeFaceStyle can do kerning if it needs to.
				return GetAdvanceForCharacter(line[characterIndex], line[characterIndex + 1]);
			}
			else
			{
				return GetAdvanceForCharacter(line[characterIndex]);
			}
		}

		public double GetAdvanceForCharacter(char character, char nextCharacterToKernWith)
		{
			// TypeFace does no kerning yet, so the next character does not change the advance
			return GetAdvanceForCodePoint(character);
		}

		public double GetAdvanceForCharacter(char character)
		{
			return GetAdvanceForCodePoint(character);
		}

		/// <summary>
		/// One code point's advance in pixels, from the face that draws it (see <see cref="GetGlyphForCodePoint"/>).
		/// </summary>
		public double GetAdvanceForCodePoint(int codePoint)
		{
			TypeFace face = TypeFace.ResolveFace(codePoint);
			double advance = face.GetAdvanceForCodePoint(codePoint) * EmScalingFor(face);
			if (IsStyled)
			{
				// Interval piles a fixed spacing on top of the font's advance, as agg-gui's shape_text does. Width is
				// deliberately not here: agg-gui scales the outline only and keeps the pen walk, so wide glyphs
				// crowd and narrow ones open up.
				advance += TextStyleSettings.Interval * emSizeInPixels;
			}

			return advance;
		}

		/// <summary>
		/// A glyph under the <see cref="TextStyleSettings"/> glyph style: scaled to size and by Width, sheared by
		/// a third of Faux Italic (agg-gui's <c>x += y * faux_italic / 3</c>), then offset by Faux Weight.
		/// </summary>
		/// <remarks>
		/// The weight offset is agg-gui's port of AGG's <c>truetype_lcd.cpp</c>: flatten, stretch Y by 100, run the
		/// contour, then squash Y back. The stretch makes the offset act almost only horizontally, so stems gain
		/// weight while horizontal strokes stay thin, as a real bold does. The contour needs straight segments,
		/// so a weighted glyph is flattened even when <see cref="FlattenCurves"/> is off.
		/// </remarks>
		private IVertexSource GetStyledGlyph(IVertexSource sourceGlyph, double emScaling, double resolutionScale)
		{
			double shear = TextStyleSettings.FauxItalic / 3;
			var glyphTransform = new Affine(emScaling * TextStyleSettings.Width, 0, emScaling * shear, emScaling, 0, 0);
			IVertexSource characterGlyph = new VertexSourceApplyTransform(sourceGlyph, glyphTransform);

			double weightInPixels = TextStyleSettings.FauxWeightInPixels(emSizeInPixels);
			if (FlattenCurves || weightInPixels != 0)
			{
				characterGlyph = new FlattenCurves(characterGlyph)
				{
					ResolutionScale = resolutionScale
				};
			}

			if (weightInPixels != 0)
			{
				const double YStretch = 100;
				var stretched = new VertexSourceApplyTransform(characterGlyph, Affine.NewScaling(1, YStretch));
				var contour = new Contour(stretched)
				{
					Width = weightInPixels,
					// Not auto-detected: that would grow every polygon, holes included, and a grown hole thins its glyph.
					// Left to the winding, outer contours and holes move in opposite directions and the stroke thickens.
					AutoDetectOrientation = false,
				};
				characterGlyph = new VertexSourceApplyTransform(contour, Affine.NewScaling(1, 1 / YStretch));
			}

			return characterGlyph;
		}

		/// <summary>
		/// The code point of the glyph that starts at <paramref name="index"/> in <paramref name="text"/>: a
		/// surrogate pair's combined value at its leading half, -1 at its trailing half (no glyph starts there),
		/// and otherwise the char itself - an unpaired surrogate included, as it always was.
		/// </summary>
		public static int GetCodePointAt(string text, int index)
		{
			char character = text[index];
			if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
			{
				return char.ConvertToUtf32(character, text[index + 1]);
			}

			if (char.IsLowSurrogate(character) && index > 0 && char.IsHighSurrogate(text[index - 1]))
			{
				return -1;
			}

			return character;
		}

		// A fallback face has its own units per em; scaling it by emSizeInPixels keeps its glyphs the same size
		private double EmScalingFor(TypeFace face)
		{
			return face == TypeFace ? currentEmScaling : emSizeInPixels / face.UnitsPerEm;
		}
	}
}
