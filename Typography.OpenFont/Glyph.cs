﻿//Apache2, 2017, WinterDev
//Apache2, 2014-2016, Samuel Carlsson, WinterDev

using System;
using System.Text;
namespace Typography.OpenFont
{

    public class TtfGlyph
    {
        GlyphPointF[] glyphPoints;
        ushort[] _contourEndPoints;

        ushort _orgAdvWidth;
        bool _hasOrgAdvWidth;

        Bounds _bounds;
        public static readonly TtfGlyph Empty = new TtfGlyph(new GlyphPointF[0], new ushort[0], Bounds.Zero, null);

#if DEBUG
        public readonly int dbugId;
        static int s_debugTotalId;
#endif
        internal TtfGlyph(
            GlyphPointF[] glyphPoints,
            ushort[] contourEndPoints,
            Bounds bounds,
            byte[] glyphInstructions)
        {

#if DEBUG
            this.dbugId = s_debugTotalId++;
#endif
            this.glyphPoints = glyphPoints;
            _contourEndPoints = contourEndPoints;
            _bounds = bounds;
            GlyphInstructions = glyphInstructions;
        }


        public Bounds Bounds { get { return _bounds; } }
        public ushort[] EndPoints { get { return _contourEndPoints; } }
        public GlyphPointF[] GlyphPoints { get { return glyphPoints; } }
        public ushort OriginalAdvanceWidth
        {
            get { return _orgAdvWidth; }
            set
            {
                _orgAdvWidth = value;
                _hasOrgAdvWidth = true;
            }
        }
        public bool HasOriginalAdvancedWidth { get { return _hasOrgAdvWidth; } }
        //--------------

        internal static void OffsetXY(TtfGlyph glyph, short dx, short dy)
        {

            //change data on current glyph
            GlyphPointF[] glyphPoints = glyph.glyphPoints;
            for (int i = glyphPoints.Length - 1; i >= 0; --i)
            {
                glyphPoints[i] = glyphPoints[i].Offset(dx, dy);
            }
            //-------------------------
            Bounds orgBounds = glyph._bounds;
            glyph._bounds = new Bounds(
               (short)(orgBounds.XMin + dx),
               (short)(orgBounds.YMin + dy),
               (short)(orgBounds.XMax + dx),
               (short)(orgBounds.YMax + dy));

        }
        internal byte[] GlyphInstructions { get; set; }

        public bool HasGlyphInstructions { get { return this.GlyphInstructions != null; } }

        internal static void TransformNormalWith2x2Matrix(TtfGlyph glyph, float m00, float m01, float m10, float m11)
        {

            //http://stackoverflow.com/questions/13188156/whats-the-different-between-vector2-transform-and-vector2-transformnormal-i
            //http://www.technologicalutopia.com/sourcecode/xnageometry/vector2.cs.htm

            //change data on current glyph
            float new_xmin = 0;
            float new_ymin = 0;
            float new_xmax = 0;
            float new_ymax = 0;


            GlyphPointF[] glyphPoints = glyph.glyphPoints;
            for (int i = glyphPoints.Length - 1; i >= 0; --i)
            {
                GlyphPointF p = glyphPoints[i];
                float x = p.P.X;
                float y = p.P.Y;

                float newX, newY;
                //please note that this is transform normal***
                glyphPoints[i] = new GlyphPointF(
                   newX = (float)Math.Round((x * m00) + (y * m10)),
                   newY = (float)Math.Round((x * m01) + (y * m11)),
                   p.onCurve);

                //short newX = xs[i] = (short)Math.Round((x * m00) + (y * m10));
                //short newY = ys[i] = (short)Math.Round((x * m01) + (y * m11));
                //------
                if (newX < new_xmin)
                {
                    new_xmin = newX;
                }
                if (newX > new_xmax)
                {
                    new_xmax = newX;
                }
                //------
                if (newY < new_ymin)
                {
                    new_ymin = newY;
                }
                if (newY > new_ymax)
                {
                    new_ymax = newY;
                }
            }
            //TODO: review here
            glyph._bounds = new Bounds(
               (short)new_xmin, (short)new_ymin,
               (short)new_xmax, (short)new_ymax);

        }

        internal static TtfGlyph Clone(TtfGlyph original)
        {
            //---------------------- 

            return new TtfGlyph(
                Utils.CloneArray(original.glyphPoints),
                Utils.CloneArray(original._contourEndPoints),
                original.Bounds,
                original.GlyphInstructions != null ? Utils.CloneArray(original.GlyphInstructions) : null);
        }

        /// <summary>
        /// append data from src to dest, dest data will changed***
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        internal static void AppendGlyph(TtfGlyph dest, TtfGlyph src)
        {
            int org_dest_len = dest._contourEndPoints.Length;
            int src_contour_count = src._contourEndPoints.Length;
            ushort org_last_point = (ushort)(dest._contourEndPoints[org_dest_len - 1] + 1); //since start at 0 

            dest.glyphPoints = Utils.ConcatArray(dest.glyphPoints, src.glyphPoints);
            dest._contourEndPoints = Utils.ConcatArray(dest._contourEndPoints, src._contourEndPoints);

            //offset latest append contour  end points
            int newlen = dest._contourEndPoints.Length;
            for (int i = org_dest_len; i < newlen; ++i)
            {
                dest._contourEndPoints[i] += (ushort)org_last_point;
            }
            //calculate new bounds
            Bounds destBound = dest.Bounds;
            Bounds srcBound = src.Bounds;
            short newXmin = (short)Math.Min(destBound.XMin, srcBound.XMin);
            short newYMin = (short)Math.Min(destBound.YMin, srcBound.YMin);
            short newXMax = (short)Math.Max(destBound.XMax, srcBound.XMax);
            short newYMax = (short)Math.Max(destBound.YMax, srcBound.YMax);

            dest._bounds = new Bounds(newXmin, newYMin, newXMax, newYMax);
        }


        public GlyphClassKind GlyphClass { get; set; }
        internal ushort MarkClassDef { get; set; }
        public short MinX
        {
            get { return _bounds.XMin; }
        }
        public short MaxX
        {
            get { return _bounds.XMax; }
        }
        public short MinY
        {
            get { return _bounds.YMin; }
        }
        public short MaxY
        {
            get { return _bounds.YMax; }
        }

#if DEBUG
        public override string ToString()
        {
            var stbuilder = new StringBuilder();
            stbuilder.Append("class=" + GlyphClass.ToString());
            if (MarkClassDef != 0)
            {
                stbuilder.Append(",mark_class=" + MarkClassDef);
            }
            return stbuilder.ToString();
        }
#endif 
    }

    //https://www.microsoft.com/typography/otspec/gdef.htm
    public enum GlyphClassKind : byte
    {
        //1 	Base glyph (single character, spacing glyph)
        //2 	Ligature glyph (multiple character, spacing glyph)
        //3 	Mark glyph (non-spacing combining glyph)
        //4 	Component glyph (part of single character, spacing glyph)
        //
        // The font developer does not have to classify every glyph in the font, 
        //but any glyph not assigned a class value falls into Class zero (0). 
        //For instance, class values might be useful for the Arabic glyphs in a font, but not for the Latin glyphs. 
        //Then the GlyphClassDef table will list only Arabic glyphs, and-by default-the Latin glyphs will be assigned to Class 0. 
        //Component glyphs can be put together to generate ligatures. 
        //A ligature can be generated by creating a glyph in the font that references the component glyphs, 
        //or outputting the component glyphs in the desired sequence. 
        //Component glyphs are not used in defining any GSUB or GPOS formats.
        //
        Zero = 0,//class0, classZero
        Base,
        Ligature,
        Mark,
        Component
    }
}
