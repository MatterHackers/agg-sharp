//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
//
// C# Port port by: Lars Brubaker
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
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Font
{
    public class GlyphWithUnderline : IVertexSource
    {
        int state = 0;
        IVertexSource underline;
        IVertexSource glyph;

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
            switch(state)
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
        static StyledTypeFaceImageCache instance;

        Dictionary<TypeFace, Dictionary<double, Dictionary<char, ImageBuffer>>> typeFaceImageCache = new Dictionary<TypeFace, Dictionary<double, Dictionary<char, ImageBuffer>>>();

        // private so you can't use it by accident (it is a singlton)
        StyledTypeFaceImageCache()
        {
        }

        public static Dictionary<char, ImageBuffer> GetCorrectCache(TypeFace typeFace, double emSizeInPoints)
        {
            // check if the cache is getting too big and if so prune it (or just delete it and start over).

            Dictionary<double, Dictionary<char, ImageBuffer>> foundTypeFaceSizes;
            Instance.typeFaceImageCache.TryGetValue(typeFace, out foundTypeFaceSizes);
            if (foundTypeFaceSizes == null)
            {
                // add in the type face
                foundTypeFaceSizes = new Dictionary<double, Dictionary<char, ImageBuffer>>();
                Instance.typeFaceImageCache.Add(typeFace, foundTypeFaceSizes);
            }

            Dictionary<char, ImageBuffer> foundTypeFaceSize;
            foundTypeFaceSizes.TryGetValue(emSizeInPoints, out foundTypeFaceSize);
            if (foundTypeFaceSize == null)
            {
                // add in the point size
                foundTypeFaceSize = new Dictionary<char, ImageBuffer>();
                foundTypeFaceSizes.Add(emSizeInPoints, foundTypeFaceSize);
            }

            return foundTypeFaceSize;
        }

        static StyledTypeFaceImageCache Instance
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
        TypeFace typeFace;

        const int PointsPerInch = 72;
        const int PixelsPerInch = 96;

        double emSizeInPixels;
        double currentEmScalling;
        bool flatenCurves = true;

        public StyledTypeFace(TypeFace typeFace, double emSizeInPoints, bool underline = false, bool flatenCurves = true)
        {
            this.typeFace = typeFace;
            emSizeInPixels = emSizeInPoints / PointsPerInch * PixelsPerInch;
            currentEmScalling = emSizeInPixels / typeFace.UnitsPerEm;
            DoUnderline = underline;
            FlatenCurves = flatenCurves;
        }

        public bool DoUnderline { get; set; }

        /// <summary>
        /// <para>If true the font will have it's curves flattened to the current point size when retrieved.</para>
        /// <para>You may want to disable this so you can flaten the curve after other transforms have been applied,</para>
        /// <para>such as skewing or scalling.  Rotation and Translation will not alter how a curve is flattened.</para>
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
                return typeFace.Ascent * currentEmScalling;
            }
        }

        public double DescentInPixels
        {
            get
            {
                return typeFace.Descent * currentEmScalling;
            }
        }

        public double XHeightInPixels
        {
            get
            {
                return typeFace.X_height * currentEmScalling;
            }
        }

        public double CapHeightInPixels
        {
            get
            {
                return typeFace.Cap_height * currentEmScalling;
            }
        }

        public RectangleDouble BoundingBoxInPixels
        {
            get
            {
                RectangleDouble pixelBounds = new RectangleDouble(typeFace.BoundingBox);
                pixelBounds *= currentEmScalling;
                return pixelBounds;
            }
        }

        public double UnderlineThicknessInPixels
        {
            get
            {
                return typeFace.Underline_thickness * currentEmScalling;
            }
        }

        public double UnderlinePositionInPixels
        {
            get
            {
                return typeFace.Underline_position * currentEmScalling;
            }
        }

        public ImageBuffer GetImageForCharacter(char character, double xFraction, double yFraction)
        {
            if (xFraction > 1 || xFraction < 0 || yFraction > 1 || yFraction < 0)
            {
                throw new ArgumentException("The x and y fractions must both be between 0 and 1.");
            }

            ImageBuffer imageForCharacter;
            Dictionary<char, ImageBuffer> characterImageCache = StyledTypeFaceImageCache.GetCorrectCache(this.typeFace, this.emSizeInPixels);
            characterImageCache.TryGetValue(character, out imageForCharacter);
            if (imageForCharacter != null)
            {
                return imageForCharacter;
            }

            IVertexSource glyphForCharacter = GetGlyphForCharacter(character);
            if(glyphForCharacter == null)
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

            ImageBuffer charImage = new ImageBuffer(Math.Max((int)(bounds.Width + .5), 1), Math.Max((int)(bounds.Height + .5), 1), 32, new BlenderBGRA());
            charImage.NewGraphics2D().Render(glyphForCharacter, xFraction, yFraction, RGBA_Bytes.Black);
            characterImageCache[character] = charImage;

            return charImage;
        }

        public IVertexSource GetGlyphForCharacter(char character)
        {
            // scale it to the correct size.
            IVertexSource sourceGlyph = typeFace.GetGlyphForCharacter(character);
            if (sourceGlyph != null)
            {
                if (DoUnderline)
                {
                    sourceGlyph = new GlyphWithUnderline(sourceGlyph, typeFace.GetAdvanceForCharacter(character), typeFace.Underline_position, typeFace.Underline_thickness);
                }
                Affine glyphTransform = Affine.NewIdentity();
                glyphTransform *= Affine.NewScaling(currentEmScalling);
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
            return typeFace.GetAdvanceForCharacter(character, nextCharacterToKernWith) * currentEmScalling;
        }

        public double GetAdvanceForCharacter(char character)
        {
            return typeFace.GetAdvanceForCharacter(character) * currentEmScalling;
        }
    }
}
