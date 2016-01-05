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
// Class FontSVG.cs
//
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace AGG
{
	public class SVGParser
	{
        public static IVertexSource PathStorageFromD(String DFromSVGFile, double xOffset, double yOffset)
        {
            PathStorage path = new PathStorage();
            string[] splitOnSpace = DFromSVGFile.Split(' ');
            string[] splitOnComma;
            double xc1, yc1, xc2, yc2, x, y;
            for (int i = 0; i < splitOnSpace.Length; i++)
            {
                switch (splitOnSpace[i++])
                {
                    case "M":
                        {
                            splitOnComma = splitOnSpace[i].Split(',');
                            double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
                            double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
                            path.MoveTo(x, y + yOffset);
                        }
                        break;

                    case "L":
                        {
                            splitOnComma = splitOnSpace[i].Split(',');
                            double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
                            double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
                            path.LineTo(x, y + yOffset);
                        }
                        break;

                    case "C":
                        {
                            splitOnComma = splitOnSpace[i++].Split(',');
                            double.TryParse(splitOnComma[0], NumberStyles.Number, null, out xc1);
                            double.TryParse(splitOnComma[1], NumberStyles.Number, null, out yc1);

                            splitOnComma = splitOnSpace[i++].Split(',');
                            double.TryParse(splitOnComma[0], NumberStyles.Number, null, out xc2);
                            double.TryParse(splitOnComma[1], NumberStyles.Number, null, out yc2);

                            splitOnComma = splitOnSpace[i].Split(',');
                            double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
                            double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
                            path.curve4(xc1, yc1 + yOffset, xc2, yc2 + yOffset, x, y + yOffset);
                        }
                        break;

                    case "z":
                        if (i < splitOnSpace.Length)
                        {
                            throw new Exception();
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            path.arrange_orientations_all_paths(AGG.Path.FlagsAndCommand.FlagCW);
            VertexSourceApplyTransform flipped = new VertexSourceApplyTransform(path, Affine.NewScaling(1, -1));
            return flipped;
        }
	}
	
	public class Glyph : IVertexSource
	{
		PathStorage glyphData;
		
        public void rewind(int pathId)
		{
			glyphData.rewind(pathId);
		}
		
        public Path.FlagsAndCommand vertex(out double x, out double y)
		{
			return glyphData.vertex(out x, out y);
		}
	}
}

