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
	public class GlyphWithUnderline : IVertexSource
	{
		private int state = 0;
		private IVertexSource underline;
		private IVertexSource glyph;

		public GlyphWithUnderline(IVertexSource glyph, int advanceForCharacter, int Underline_position, int Underline_thickness)
		{
			underline = new RoundedRect(new RectangleDouble(0, Underline_position, advanceForCharacter, Underline_position + Underline_thickness), 0);
			this.glyph = glyph;
		}

		public IEnumerable<VertexData> Vertices()
		{
			// return all the data for the glyph
			foreach (VertexData vertexData in glyph.Vertices())
			{
				if (ShapePath.is_stop(vertexData.command))
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

		public void rewind(int path_id)
		{
			underline.rewind(0);
			glyph.rewind(path_id);
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			ShapePath.FlagsAndCommand cmd = ShapePath.FlagsAndCommand.CommandStop;
			switch (state)
			{
				case 0:
					cmd = glyph.vertex(out x, out y);
					if (ShapePath.is_stop(cmd))
					{
						state++;
						goto case 1;
					}
					return cmd;

				case 1:
					cmd = underline.vertex(out x, out y);
					break;
			}
			return cmd;
		}
	}

	public class StyledTypeFaceImageCache
	{
		private static StyledTypeFaceImageCache instance;

		private Dictionary<TypeFace, Dictionary<RGBA_Bytes, Dictionary<double, Dictionary<char, ImageBuffer>>>> typeFaceImageCache = new Dictionary<TypeFace, Dictionary<RGBA_Bytes, Dictionary<double, Dictionary<char, ImageBuffer>>>>();

		// private so you can't use it by accident (it is a singleton)
		private StyledTypeFaceImageCache()
		{
		}

		public static Dictionary<char, ImageBuffer> GetCorrectCache(TypeFace typeFace, RGBA_Bytes color, double emSizeInPoints)
		{
			lock(typeFace)
			{
				// TODO: check if the cache is getting too big and if so prune it (or just delete it and start over).

				Dictionary<RGBA_Bytes, Dictionary<double, Dictionary<char, ImageBuffer>>> foundTypeFaceColor;
				if (!Instance.typeFaceImageCache.TryGetValue(typeFace, out foundTypeFaceColor))
				{
					// add in the type face
					foundTypeFaceColor = new Dictionary<RGBA_Bytes, Dictionary<double, Dictionary<char, ImageBuffer>>>();
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

		private static StyledTypeFaceImageCache Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new StyledTypeFaceImageCache();
				}

				return instance;
			}
		}
	}

    public class StyledTypeFace
    {
        public TypeFace TypeFace { get; private set; }

		private const int PointsPerInch = 72;
		private const int PixelsPerInch = 96;

		private double emSizeInPixels;
		private double currentEmScaling;
		private bool flatenCurves = true;

		public StyledTypeFace(TypeFace typeFace, double emSizeInPoints, bool underline = false, bool flatenCurves = true)
		{
			this.TypeFace = typeFace;
			emSizeInPixels = emSizeInPoints / PointsPerInch * PixelsPerInch;
			currentEmScaling = emSizeInPixels / typeFace.UnitsPerEm;
			DoUnderline = underline;
			FlatenCurves = flatenCurves;
		}

		public bool DoUnderline { get; set; }

		/// <summary>
		/// <para>If true the font will have it's curves flattened to the current point size when retrieved.</para>
		/// <para>You may want to disable this so you can flatten the curve after other transforms have been applied,</para>
		/// <para>such as skewing or scaling.  Rotation and Translation will not alter how a curve is flattened.</para>
		/// </summary>
		public bool FlatenCurves
		{
			get
			{
				return flatenCurves;
			}

			set
			{
				flatenCurves = value;
			}
		}

		/// <summary>
		/// Sets the Em size for the font in pixels.
		/// </summary>
		public double EmSizeInPixels
		{
			get
			{
				return emSizeInPixels;
			}
		}

		/// <summary>
		/// Sets the Em size for the font assuming there are 72 points per inch and there are 96 pixels per inch.
		/// </summary>
		public double EmSizeInPoints
		{
			get
			{
				return emSizeInPixels / PixelsPerInch * PointsPerInch;
			}
		}

		public double AscentInPixels
		{
			get
			{
				return TypeFace.Ascent * currentEmScaling;
			}
		}

		public double DescentInPixels
		{
			get
			{
				return TypeFace.Descent * currentEmScaling;
			}
		}

		public double XHeightInPixels
		{
			get
			{
				return TypeFace.X_height * currentEmScaling;
			}
		}

		public double CapHeightInPixels
		{
			get
			{
				return TypeFace.Cap_height * currentEmScaling;
			}
		}

		public RectangleDouble BoundingBoxInPixels
		{
			get
			{
				RectangleDouble pixelBounds = new RectangleDouble(TypeFace.BoundingBox);
				pixelBounds *= currentEmScaling;
				return pixelBounds;
			}
		}

		public double UnderlineThicknessInPixels
		{
			get
			{
				return TypeFace.Underline_thickness * currentEmScaling;
			}
		}

		public double UnderlinePositionInPixels
		{
			get
			{
				return TypeFace.Underline_position * currentEmScaling;
			}
		}

		public ImageBuffer GetImageForCharacter(char character, double xFraction, double yFraction, RGBA_Bytes color)
		{
			if (xFraction > 1 || xFraction < 0 || yFraction > 1 || yFraction < 0)
			{
				throw new ArgumentException("The x and y fractions must both be between 0 and 1.");
			}

			ImageBuffer imageForCharacter;
			Dictionary<char, ImageBuffer> characterImageCache = StyledTypeFaceImageCache.GetCorrectCache(this.TypeFace, color, this.emSizeInPixels);
			characterImageCache.TryGetValue(character, out imageForCharacter);
			if (imageForCharacter != null)
			{
				return imageForCharacter;
			}

			IVertexSource glyphForCharacter = GetGlyphForCharacter(character);
			if (glyphForCharacter == null)
			{
				return null;
			}

			glyphForCharacter.rewind(0);
			double x, y;
			ShapePath.FlagsAndCommand curCommand = glyphForCharacter.vertex(out x, out y);
			RectangleDouble bounds = new RectangleDouble(x, y, x, y);
			while (curCommand != ShapePath.FlagsAndCommand.CommandStop)
			{
				bounds.ExpandToInclude(x, y);
				curCommand = glyphForCharacter.vertex(out x, out y);
			}

			int descentExtraHeight = (int)(-DescentInPixels + .5);
			ImageBuffer charImage = new ImageBuffer(Math.Max((int)(bounds.Width + .5), 1) + 1, Math.Max((int)(EmSizeInPixels + descentExtraHeight + .5), 1) + 1, 32, new BlenderPreMultBGRA());
			charImage.OriginOffset = new VectorMath.Vector2(0, descentExtraHeight);
			Graphics2D graphics = charImage.NewGraphics2D();
			graphics.Render(glyphForCharacter, xFraction, yFraction + descentExtraHeight, color);
			characterImageCache[character] = charImage;

			return charImage;
		}

		public IVertexSource GetGlyphForCharacter(char character)
		{
			// scale it to the correct size.
			IVertexSource sourceGlyph = TypeFace.GetGlyphForCharacter(character);
			if (sourceGlyph != null)
			{
				if (DoUnderline)
				{
					sourceGlyph = new GlyphWithUnderline(sourceGlyph, TypeFace.GetAdvanceForCharacter(character), TypeFace.Underline_position, TypeFace.Underline_thickness);
				}
				Affine glyphTransform = Affine.NewIdentity();
				glyphTransform *= Affine.NewScaling(currentEmScaling);
				IVertexSource characterGlyph = new VertexSourceApplyTransform(sourceGlyph, glyphTransform);

				if (FlatenCurves)
				{
					characterGlyph = new FlattenCurves(characterGlyph);
				}

				return characterGlyph;
			}

			return null;
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