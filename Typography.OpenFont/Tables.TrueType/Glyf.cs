﻿//Apache2, 2014-2016, Samuel Carlsson, WinterDev
using System;
using System.Collections.Generic;
using System.IO;
namespace Typography.OpenFont.Tables
{
    class Glyf : TableEntry
    {
        TtfGlyph[] _glyphs;
        public Glyf(GlyphLocations glyphLocations)
        {
            this.GlyphLocations = glyphLocations;
        }
        public TtfGlyph[] Glyphs
        {
            get { return _glyphs; }
        }
        public override string Name
        {
            get { return "glyf"; }
        }
        public GlyphLocations GlyphLocations
        {
            get;
            private set;
        }
        protected override void ReadContentFrom(BinaryReader reader)
        {

            uint tableOffset = this.Header.Offset;
            GlyphLocations locations = this.GlyphLocations;
            int glyphCount = locations.GlyphCount;
            _glyphs = new TtfGlyph[glyphCount];

            List<int> compositeGlyphs = new List<int>();

            for (int i = 0; i < glyphCount; i++)
            {
                reader.BaseStream.Seek(tableOffset + locations.Offsets[i], SeekOrigin.Begin);//reset                  
                uint length = locations.Offsets[i + 1] - locations.Offsets[i];
                if (length > 0)
                {
                    //https://www.microsoft.com/typography/OTSPEC/glyf.htm
                    //header, 
                    //Type 	Name 	Description
                    //SHORT 	numberOfContours 	If the number of contours is greater than or equal to zero, this is a single glyph; if negative, this is a composite glyph.
                    //SHORT 	xMin 	Minimum x for coordinate data.
                    //SHORT 	yMin 	Minimum y for coordinate data.
                    //SHORT 	xMax 	Maximum x for coordinate data.
                    //SHORT 	yMax 	Maximum y for coordinate data.
                    short contoursCount = reader.ReadInt16();
                    if (contoursCount >= 0)
                    {
                        Bounds bounds = Utils.ReadBounds(reader);
                        _glyphs[i] = ReadSimpleGlyph(reader, contoursCount, bounds);
                    }
                    else
                    {
                        //skip composite glyph,
                        //resolve later
                        compositeGlyphs.Add(i);
                    }
                }
                else
                {
                    _glyphs[i] = TtfGlyph.Empty;
                }
            }

            //--------------------------------
            //resolve composte glyphs 
            //--------------------------------
            foreach (int glyphIndex in compositeGlyphs)
            {
#if DEBUG
                if (glyphIndex == 7)
                {

                }
#endif
                _glyphs[glyphIndex] = ReadCompositeGlyph(_glyphs, reader, tableOffset, glyphIndex);
            }
        }

        static bool HasFlag(SimpleGlyphFlag target, SimpleGlyphFlag test)
        {
            return (target & test) == test;
        }
        static bool HasFlag(CompositeGlyphFlags target, CompositeGlyphFlags test)
        {
            return (target & test) == test;
        }
        static SimpleGlyphFlag[] ReadFlags(BinaryReader input, int flagCount)
        {
            var result = new SimpleGlyphFlag[flagCount];
            int i = 0;
            int repeatCount = 0;
            var flag = (SimpleGlyphFlag)0;
            while (i < flagCount)
            {
                if (repeatCount > 0)
                {
                    repeatCount--;
                }
                else
                {
                    flag = (SimpleGlyphFlag)input.ReadByte();
                    if (HasFlag(flag, SimpleGlyphFlag.Repeat))
                    {
                        repeatCount = input.ReadByte();
                    }
                }
                result[i++] = flag;
            }
            return result;
        }

        static short[] ReadCoordinates(BinaryReader input, int pointCount, SimpleGlyphFlag[] flags, SimpleGlyphFlag isByte, SimpleGlyphFlag signOrSame)
        {
            //https://www.microsoft.com/typography/OTSPEC/glyf.htm
            //Note: In the glyf table, the position of a point is not stored in absolute terms but as a vector relative to the previous point. 
            //The delta-x and delta-y vectors represent these (often small) changes in position.

            //Each flag is a single bit. Their meanings are shown below.
            //Bit	Flags  	        Description
            //0     On Curve 	 	If set, the point is on the curve; otherwise, it is off the curve.
            //1     x-Short Vector  If set, the corresponding x-coordinate is 1 byte long. If not set, 2 bytes.
            //2     y-Short Vector 	If set, the corresponding y-coordinate is 1 byte long. If not set, 2 bytes.
            //3     Repeat 	 	    If set, the next byte specifies the number of additional times this set of flags is to be repeated.
            //                      In this way, the number of flags listed can be smaller than the number of points in a character.
            //4     This x is same(Positive x-Short Vector) This flag has two meanings, depending on how the x-Short Vector flag is set.
            //                      If x-Short Vector is set, this bit describes the sign of the value, 
            //                      with 1 equalling positive and 0 negative. 
            //                      If the x-Short Vector bit is not set and this bit is set, then the current x-coordinate is the same as the previous x-coordinate. 
            //                      If the x-Short Vector bit is not set and this bit is also not set, the current x-coordinate is a signed 16-bit delta vector.
            //5     This y is same  (Positive y-Short Vector)  This flag has two meanings,
            //                      depending on how the y-Short Vector flag is set. 
            //                      If y-Short Vector is set, this bit describes the sign of the value,
            //                      with 1 equalling positive and 0 negative. 
            //                      If the y-Short Vector bit is not set and this bit is set, then the current y-coordinate is the same as the previous y-coordinate.
            //                      If the y-Short Vector bit is not set and this bit is also not set,
            //                      the current y-coordinate is a signed 16-bit delta vector.  
            //6  Reserved 	 	This bit is reserved. Set it to zero.
            //7  Reserved 	 	This bit is reserved. Set it to zero.

            var xs = new short[pointCount];
            int x = 0;
            for (int i = 0; i < pointCount; i++)
            {
                int dx;
                if (HasFlag(flags[i], isByte))
                {
                    byte b = input.ReadByte();
                    dx = HasFlag(flags[i], signOrSame) ? b : -b;
                }
                else
                {
                    if (HasFlag(flags[i], signOrSame))
                    {
                        dx = 0;
                    }
                    else
                    {
                        dx = input.ReadInt16();
                    }
                }
                x += dx;
                xs[i] = (short)x; // TODO: overflow?
            }
            return xs;
        }
        [Flags]
        enum SimpleGlyphFlag : byte
        {
            OnCurve = 1,
            XByte = 1 << 1,
            YByte = 1 << 2,
            Repeat = 1 << 3,
            XSignOrSame = 1 << 4,
            YSignOrSame = 1 << 5
        }

        static TtfGlyph ReadSimpleGlyph(BinaryReader reader, int contourCount, Bounds bounds)
        {
            //https://www.microsoft.com/typography/OTSPEC/glyf.htm
            //Simple Glyph Description
            //This is the table information needed if numberOfContours is greater than zero, that is, a glyph is not a composite.
            //Type 	Name 	Description
            //USHORT 	endPtsOfContours[n] 	Array of last points of each contour; n is the number of contours.
            //USHORT 	instructionLength 	Total number of bytes for instructions.
            //BYTE 	instructions[n] 	Array of instructions for each glyph; n is the number of instructions.
            //BYTE 	flags[n] 	Array of flags for each coordinate in outline; n is the number of flags.
            //BYTE or SHORT 	xCoordinates[ ] 	First coordinates relative to (0,0); others are relative to previous point.
            //BYTE or SHORT 	yCoordinates[ ] 	First coordinates relative to (0,0); others are relative to previous point.

            ushort[] endPoints = Utils.ReadUInt16Array(reader, contourCount);
            //-------------------------------------------------------
            ushort instructionLen = reader.ReadUInt16();
            byte[] instructions = reader.ReadBytes(instructionLen);
            //-------------------------------------------------------
            // TODO: should this take the max points rather?
            int pointCount = endPoints[contourCount - 1] + 1; // TODO: count can be zero?
            SimpleGlyphFlag[] flags = ReadFlags(reader, pointCount);
            short[] xs = ReadCoordinates(reader, pointCount, flags, SimpleGlyphFlag.XByte, SimpleGlyphFlag.XSignOrSame);
            short[] ys = ReadCoordinates(reader, pointCount, flags, SimpleGlyphFlag.YByte, SimpleGlyphFlag.YSignOrSame);

            int n = xs.Length;
            GlyphPointF[] glyphPoints = new GlyphPointF[n];
            for (int i = n - 1; i >= 0; --i)
            {
                bool onCurve = HasFlag(flags[i], SimpleGlyphFlag.OnCurve);
                glyphPoints[i] = new GlyphPointF(xs[i], ys[i], onCurve);
            }
            //-----------
            //lets build GlyphPoint set
            //-----------
            return new TtfGlyph(glyphPoints, endPoints, bounds, instructions);
        }


        [Flags]
        enum CompositeGlyphFlags : ushort
        {
            //These are the constants for the flags field:
            //Bit   Flags 	 	Description
            //0     ARG_1_AND_2_ARE_WORDS  	If this is set, the arguments are words; otherwise, they are bytes.
            //1     ARGS_ARE_XY_VALUES 	  	If this is set, the arguments are xy values; otherwise, they are points.
            //2     ROUND_XY_TO_GRID 	  	For the xy values if the preceding is true.
            //3     WE_HAVE_A_SCALE 	 	This indicates that there is a simple scale for the component. Otherwise, scale = 1.0.
            //4     RESERVED 	        	This bit is reserved. Set it to 0.
            //5     MORE_COMPONENTS 	    Indicates at least one more glyph after this one.
            //6     WE_HAVE_AN_X_AND_Y_SCALE 	  	The x direction will use a different scale from the y direction.
            //7     WE_HAVE_A_TWO_BY_TWO 	  	There is a 2 by 2 transformation that will be used to scale the component.
            //8     WE_HAVE_INSTRUCTIONS 	 	Following the last component are instructions for the composite character.
            //9     USE_MY_METRICS 	 	        If set, this forces the aw and lsb (and rsb) for the composite to be equal to those from this original glyph. This works for hinted and unhinted characters.
            //10    OVERLAP_COMPOUND 	 	    If set, the components of the compound glyph overlap. Use of this flag is not required in OpenType — that is, it is valid to have components overlap without having this flag set. It may affect behaviors in some platforms, however. (See Apple’s specification for details regarding behavior in Apple platforms.)
            //11    SCALED_COMPONENT_OFFSET 	The composite is designed to have the component offset scaled.
            //12    UNSCALED_COMPONENT_OFFSET 	The composite is designed not to have the component offset scaled.

            ARG_1_AND_2_ARE_WORDS = 1,
            ARGS_ARE_XY_VALUES = 1 << 1,
            ROUND_XY_TO_GRID = 1 << 2,
            WE_HAVE_A_SCALE = 1 << 3,
            RESERVED = 1 << 4,
            MORE_COMPONENTS = 1 << 5,
            WE_HAVE_AN_X_AND_Y_SCALE = 1 << 6,
            WE_HAVE_A_TWO_BY_TWO = 1 << 7,
            WE_HAVE_INSTRUCTIONS = 1 << 8,
            USE_MY_METRICS = 1 << 9,
            OVERLAP_COMPOUND = 1 << 10,
            SCALED_COMPONENT_OFFSET = 1 << 11,
            UNSCALED_COMPONENT_OFFSET = 1 << 12
        }

        TtfGlyph ReadCompositeGlyph(TtfGlyph[] createdGlyphs, BinaryReader reader, uint tableOffset, int compositeGlyphIndex)
        {
            //------------------------------------------------------ 
            //https://www.microsoft.com/typography/OTSPEC/glyf.htm
            //Composite Glyph Description

            //This is the table information needed for composite glyphs (numberOfContours is -1). 
            //A composite glyph starts with two USHORT values (“flags” and “glyphIndex,” i.e. the index of the first contour in this composite glyph); 
            //the data then varies according to “flags”).
            //Type 	Name 	Description
            //USHORT 	flags 	component flag
            //USHORT 	glyphIndex 	glyph index of component
            //VARIABLE 	argument1 	x-offset for component or point number; type depends on bits 0 and 1 in component flags
            //VARIABLE 	argument2 	y-offset for component or point number; type depends on bits 0 and 1 in component flags
            //---------
            //see more at https://fontforge.github.io/assets/old/Composites/index.html
            //---------

            //move to composite glyph position
            reader.BaseStream.Seek(tableOffset + GlyphLocations.Offsets[compositeGlyphIndex], SeekOrigin.Begin);//reset
            //------------------------
            short contoursCount = reader.ReadInt16(); // ignored
            Bounds bounds = Utils.ReadBounds(reader);

            TtfGlyph finalGlyph = null;
            CompositeGlyphFlags flags;

            do
            {
                flags = (CompositeGlyphFlags)reader.ReadUInt16();
                ushort glyphIndex = reader.ReadUInt16();
                if (createdGlyphs[glyphIndex] == null)
                {
                    // This glyph is not read yet, resolve it first!
                    long storedOffset = reader.BaseStream.Position;
                    TtfGlyph missingGlyph = ReadCompositeGlyph(createdGlyphs, reader, tableOffset, glyphIndex);
                    createdGlyphs[glyphIndex] = missingGlyph;
                    reader.BaseStream.Position = storedOffset;
                }

                TtfGlyph newGlyph = TtfGlyph.Clone(createdGlyphs[glyphIndex]);

                short arg1 = 0;
                short arg2 = 0;
                ushort arg1and2 = 0;

                if (HasFlag(flags, CompositeGlyphFlags.ARG_1_AND_2_ARE_WORDS))
                {
                    arg1 = reader.ReadInt16();
                    arg2 = reader.ReadInt16();
                }
                else
                {
                    arg1and2 = reader.ReadUInt16();
                }
                //-----------------------------------------
                float xscale = 1;
                float scale01 = 0;
                float scale10 = 0;
                float yscale = 1;

                bool useMatrix = false;
                //-----------------------------------------
                bool hasScale = false;
                if (HasFlag(flags, CompositeGlyphFlags.WE_HAVE_A_SCALE))
                {
                    //If the bit WE_HAVE_A_SCALE is set,
                    //the scale value is read in 2.14 format-the value can be between -2 to almost +2.
                    //The glyph will be scaled by this value before grid-fitting. 
                    xscale = yscale = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    hasScale = true;
                }
                else if (HasFlag(flags, CompositeGlyphFlags.WE_HAVE_AN_X_AND_Y_SCALE))
                {
                    xscale = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    yscale = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    hasScale = true;
                }
                else if (HasFlag(flags, CompositeGlyphFlags.WE_HAVE_A_TWO_BY_TWO))
                {

                    //The bit WE_HAVE_A_TWO_BY_TWO allows for linear transformation of the X and Y coordinates by specifying a 2 × 2 matrix.
                    //This could be used for scaling and 90-degree*** rotations of the glyph components, for example.

                    //2x2 matrix

                    //The purpose of USE_MY_METRICS is to force the lsb and rsb to take on a desired value.
                    //For example, an i-circumflex (U+00EF) is often composed of the circumflex and a dotless-i. 
                    //In order to force the composite to have the same metrics as the dotless-i,
                    //set USE_MY_METRICS for the dotless-i component of the composite. 
                    //Without this bit, the rsb and lsb would be calculated from the hmtx entry for the composite 
                    //(or would need to be explicitly set with TrueType instructions).

                    //Note that the behavior of the USE_MY_METRICS operation is undefined for rotated composite components. 
                    useMatrix = true;
                    hasScale = true;
                    xscale = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    scale01 = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    scale10 = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */
                    yscale = ((float)reader.ReadInt16()) / (1 << 14); /* Format 2.14 */

                    if (HasFlag(flags, CompositeGlyphFlags.UNSCALED_COMPONENT_OFFSET))
                    {


                    }
                    else
                    {


                    }
                    if (HasFlag(flags, CompositeGlyphFlags.USE_MY_METRICS))
                    {

                    }
                }

                //--------------------------------------------------------------------
                if (HasFlag(flags, CompositeGlyphFlags.ARGS_ARE_XY_VALUES))
                {
                    //Argument1 and argument2 can be either x and y offsets to be added to the glyph or two point numbers.  
                    //x and y offsets to be added to the glyph
                    //When arguments 1 and 2 are an x and a y offset instead of points and the bit ROUND_XY_TO_GRID is set to 1,
                    //the values are rounded to those of the closest grid lines before they are added to the glyph.
                    //X and Y offsets are described in FUnits. 

                    if (useMatrix)
                    {
                        //use this matrix  
                        TtfGlyph.TransformNormalWith2x2Matrix(newGlyph, xscale, scale01, scale10, yscale);
                        TtfGlyph.OffsetXY(newGlyph, (short)(arg1), arg2);
                    }
                    else
                    {
                        if (hasScale)
                        {
                            if (xscale == 1.0 && yscale == 1.0)
                            {

                            }
                            else
                            {
                                TtfGlyph.TransformNormalWith2x2Matrix(newGlyph, xscale, 0, 0, yscale);
                            }
                            TtfGlyph.OffsetXY(newGlyph, arg1, arg2);
                        }
                        else
                        {
                            if (HasFlag(flags, CompositeGlyphFlags.ROUND_XY_TO_GRID))
                            {
                                //TODO: implement round xy to grid***
                                //----------------------------
                            }
                            //just offset***
                            TtfGlyph.OffsetXY(newGlyph, arg1, arg2);
                        }
                    }


                }
                else
                {
                    //two point numbers. 
                    //the first point number indicates the point that is to be matched to the new glyph. 
                    //The second number indicates the new glyph's “matched” point. 
                    //Once a glyph is added,its point numbers begin directly after the last glyphs (endpoint of first glyph + 1)

                }

                //
                if (finalGlyph == null)
                {
                    finalGlyph = newGlyph;
                }
                else
                {
                    //merge 
                    TtfGlyph.AppendGlyph(finalGlyph, newGlyph);
                }

            } while (HasFlag(flags, CompositeGlyphFlags.MORE_COMPONENTS));
            //
            if (HasFlag(flags, CompositeGlyphFlags.WE_HAVE_INSTRUCTIONS))
            {
                ushort numInstr = reader.ReadUInt16();
                byte[] insts = reader.ReadBytes(numInstr);
                finalGlyph.GlyphInstructions = insts;
            }
            //F2DOT14 	16-bit signed fixed number with the low 14 bits of fraction (2.14).
            //Transformation Option
            //
            //The C pseudo-code fragment below shows how the composite glyph information is stored and parsed; definitions for “flags” bits follow this fragment:
            //  do {
            //    USHORT flags;
            //    USHORT glyphIndex;
            //    if ( flags & ARG_1_AND_2_ARE_WORDS) {
            //    (SHORT or FWord) argument1;
            //    (SHORT or FWord) argument2;
            //    } else {
            //        USHORT arg1and2; /* (arg1 << 8) | arg2 */
            //    }
            //    if ( flags & WE_HAVE_A_SCALE ) {
            //        F2Dot14  scale;    /* Format 2.14 */
            //    } else if ( flags & WE_HAVE_AN_X_AND_Y_SCALE ) {
            //        F2Dot14  xscale;    /* Format 2.14 */
            //        F2Dot14  yscale;    /* Format 2.14 */
            //    } else if ( flags & WE_HAVE_A_TWO_BY_TWO ) {
            //        F2Dot14  xscale;    /* Format 2.14 */
            //        F2Dot14  scale01;   /* Format 2.14 */
            //        F2Dot14  scale10;   /* Format 2.14 */
            //        F2Dot14  yscale;    /* Format 2.14 */
            //    }
            //} while ( flags & MORE_COMPONENTS ) 
            //if (flags & WE_HAVE_INSTR){
            //    USHORT numInstr
            //    BYTE instr[numInstr]
            //------------------------------------------------------------ 


            return finalGlyph ?? TtfGlyph.Empty;
        }
    }
}
