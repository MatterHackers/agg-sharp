using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

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
        private Dictionary<TypeFace, Dictionary<Color, Dictionary<double, Dictionary<char, ImageBuffer>>>> typeFaceImageCache = new Dictionary<TypeFace, Dictionary<Color, Dictionary<double, Dictionary<char, ImageBuffer>>>>();

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

		internal static bool TryGetImage(TypeFace typeFace, Color color, double emSizeInPoints, char character, out ImageBuffer image)
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
		internal static ImageBuffer StoreImage(TypeFace typeFace, Color color, double emSizeInPoints, char character, ImageBuffer image)
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

		private static Dictionary<char, ImageBuffer> GetCorrectCache(TypeFace typeFace, Color color, double emSizeInPoints)
		{
			lock (SyncRoot)
			{
				Dictionary<Color, Dictionary<double, Dictionary<char, ImageBuffer>>> foundTypeFaceColor;
				if (!Instance.typeFaceImageCache.TryGetValue(typeFace, out foundTypeFaceColor))
				{
					// add in the type face
					foundTypeFaceColor = new Dictionary<Color, Dictionary<double, Dictionary<char, ImageBuffer>>>();
					Instance.typeFaceImageCache.Add(typeFace, foundTypeFaceColor);
				}

				Dictionary<double, Dictionary<char, ImageBuffer>> foundTypeFaceSizes;
				if (!foundTypeFaceColor.TryGetValue(color, out foundTypeFaceSizes))
				{
					// add in the type face
					foundTypeFaceSizes = new Dictionary<double, Dictionary<char, ImageBuffer>>();
					foundTypeFaceColor.Add(color, foundTypeFaceSizes);
				}

				Dictionary<char, ImageBuffer> foundTypeFaceSize;
				if (!foundTypeFaceSizes.TryGetValue(emSizeInPoints, out foundTypeFaceSize))
				{
					// add in the point size
					foundTypeFaceSize = new Dictionary<char, ImageBuffer>();
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
			if (xFraction > 1 || xFraction < 0 || yFraction > 1 || yFraction < 0)
			{
				throw new ArgumentException("The x and y fractions must both be between 0 and 1.");
			}

			if (StyledTypeFaceImageCache.TryGetImage(this.TypeFace, color, emSizeInPixels, character, out ImageBuffer imageForCharacter))
			{
				return imageForCharacter;
			}

			IVertexSource glyphForCharacter = GetGlyphForCharacter(character, 1);
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

			// Rendering happens outside the lock, so another thread may have cached this character while we
			// were drawing it. Return whatever the cache kept, not necessarily what we just drew, so that
			// every caller of this character holds the same instance.
			return StyledTypeFaceImageCache.StoreImage(this.TypeFace, color, emSizeInPixels, character, charImage);
		}

		public IVertexSource GetGlyphForCharacter(char character, double resolutionScale = 1)
		{
			// scale it to the correct size.
			IVertexSource sourceGlyph = TypeFace.GetGlyphForCharacter(character);
			if (sourceGlyph != null)
			{
				if (DoUnderline)
				{
					sourceGlyph = new GlyphWithUnderline(sourceGlyph, TypeFace.GetAdvanceForCharacter(character), TypeFace.Underline_position, TypeFace.Underline_thickness);
				}

				var glyphTransform = Affine.NewIdentity();
				glyphTransform *= Affine.NewScaling(currentEmScaling);
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

		public double GetAdvanceForCharacter(string line, int characterIndex)
		{
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
			return TypeFace.GetAdvanceForCharacter(character, nextCharacterToKernWith) * currentEmScaling;
		}

		public double GetAdvanceForCharacter(char character)
		{
			return TypeFace.GetAdvanceForCharacter(character) * currentEmScaling;
		}
	}
}