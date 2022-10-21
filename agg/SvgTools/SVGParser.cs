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
using HtmlAgilityPack;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.Agg.SvgTools
{
    public record ColoredVertexSource(IVertexSource VertexSource, Color Color);

    public static class SvgParser
    {
        public static List<ColoredVertexSource> Parse(string filePath, double scale, int width = -1, int height = -1)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Parse(stream, scale, width, height);
            }
        }

        public static List<ColoredVertexSource> Parse(Stream stream, double scale, int width = -1, int height = -1)
        {
            var svgDocument = new HtmlDocument();
            svgDocument.Load(stream);

            // get the viewBox
            var viewBox = svgDocument.DocumentNode.SelectSingleNode("//svg").Attributes["viewBox"].Value;

            if (!string.IsNullOrEmpty(viewBox))
            {
                var segments = viewBox.Split(' ');

                if (width == -1)
                {
                    int.TryParse(segments[2], out width);
                }

                if (height == -1)
                {
                    int.TryParse(segments[3], out height);
                }
            }

            var items = new List<ColoredVertexSource>();

            // process all the paths and polygons
            foreach (var pathNode in svgDocument.DocumentNode.SelectNodes("//path"))
            {
                var pathDString = pathNode.Attributes["d"].Value;
                var vertexStorage = new VertexStorage(pathDString);

                // get the fill color
                var fillColor = Color.Black;
                if (pathNode.Attributes["fill"] != null)
                {
                    var fillString = pathNode.Attributes["fill"].Value;
                    if (fillString.StartsWith("#"))
                    {
                        fillColor = new Color(fillString);
                    }
                }

                items.Add(new ColoredVertexSource(vertexStorage, fillColor));
            }

            var fastSimpleNumbers = true;
            foreach (var polgonNode in svgDocument.DocumentNode.SelectNodes("//polygon"))
            {
                var pointsString = polgonNode.Attributes["points"].Value;
                var vertexStorage = new VertexStorage();
                var parseIndex = 0;
                bool first = true;
                do
                {
                    parseIndex++;
                    var x = agg_basics.ParseDouble(pointsString, ref parseIndex, fastSimpleNumbers);
                    var y = agg_basics.ParseDouble(pointsString, ref parseIndex, fastSimpleNumbers);
                    if (first)
                    {
                        vertexStorage.MoveTo(x, y);
                        first = false;
                    }
                    else
                    {
                        vertexStorage.LineTo(x, y);
                    }
                } while (NextElementIsANumber(pointsString, parseIndex));

                vertexStorage.ClosePolygon();

                items.Add(new ColoredVertexSource(vertexStorage, Color.Black));
            }

            return items;
        }

        private static Vector2 Reflect(Vector2 point, Vector2 mirror)
        {
            double x, y, dx, dy;
            dx = Math.Abs(mirror.X - point.X);
            dy = Math.Abs(mirror.Y - point.Y);

            if (mirror.X >= point.X)
            {
                x = mirror.X + dx;
            }
            else
            {
                x = mirror.X - dx;
            }
            if (mirror.Y >= point.Y)
            {
                y = mirror.Y + dy;
            }
            else
            {
                y = mirror.Y - dy;
            }

            return new Vector2(x, y);
        }

        private static HashSet<char> validNumberStartingCharacters = new HashSet<char> { '-', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static HashSet<char> validSkipCharacters = new HashSet<char> { ' ', ',' };
        private static bool NextElementIsANumber(string dString, int parseIndex)
        {
            while (parseIndex < dString.Length
                && validSkipCharacters.Contains(dString[parseIndex]))
            {
                parseIndex++;
            }

            if (parseIndex < dString.Length
                && validNumberStartingCharacters.Contains(dString[parseIndex]))
            {
                return true;
            }

            return false;
        }

        public static void ParseSvgDString(VertexStorage vertexStorage, string dString)
        {
            vertexStorage.Clear();
            
            bool fastSimpleNumbers = dString.IndexOf('e') == -1;
            int parseIndex = 0;
            int polyStartVertexSourceIndex = 0;
            Vector2 polyStart = new Vector2();
            Vector2 lastXY = new Vector2();
            Vector2 curXY = new Vector2();

            bool foundSecondControlPoint = false;
            Vector2 secondControlPoint = new Vector2();

            while (parseIndex < dString.Length)
            {
                Char command = dString[parseIndex];
                switch (command)
                {
                    case 'a': // relative arc
                    case 'A': // absolute arc
                        {
                            do
                            {
                                parseIndex++;
                                Vector2 radii;
                                radii.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                radii.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var angle = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var largeArcFlag = (int)agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var sweepFlag = (int)agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                if (command == 'a')
                                {
                                    curXY += lastXY;
                                }

                                vertexStorage.LineTo(curXY);

                                lastXY = curXY;

                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;


                    case 'c': // curve to relative
                    case 'C': // curve to absolute
                        {
                            do
                            {
                                parseIndex++;
                                Vector2 controlPoint1;
                                controlPoint1.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                controlPoint1.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                foundSecondControlPoint = true;
                                secondControlPoint.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                secondControlPoint.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                if (command == 'c')
                                {
                                    controlPoint1 += lastXY;
                                    secondControlPoint += lastXY;
                                    curXY += lastXY;
                                }

                                vertexStorage.curve4(controlPoint1.X, controlPoint1.Y, secondControlPoint.X, secondControlPoint.Y, curXY.X, curXY.Y);

                                lastXY = curXY;

                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;

                    case 's': // shorthand/smooth curveto relative
                    case 'S': // shorthand/smooth curveto absolute
                        {
                            do
                            {
                                Vector2 controlPoint = lastXY;

                                if (vertexStorage[vertexStorage.Count-1].command == ShapePath.FlagsAndCommand.Curve4)
                                {
                                    controlPoint = Reflect(secondControlPoint, lastXY);
                                }
                                else
                                {
                                    controlPoint = curXY;
                                }

                                parseIndex++;

                                foundSecondControlPoint = true;

                                secondControlPoint.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                secondControlPoint.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                if (command == 's')
                                {
                                    secondControlPoint += lastXY;
                                    curXY += lastXY;
                                }

                                vertexStorage.curve4(controlPoint.X, controlPoint.Y, secondControlPoint.X, secondControlPoint.Y, curXY.X, curXY.Y);

                                lastXY = curXY;
                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;

                    case 'h': // horizontal line to relative
                    case 'H': // horizontal line to absolute
                        parseIndex++;
                        curXY.Y = lastXY.Y;
                        curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                        if (command == 'h')
                        {
                            curXY.X += lastXY.X;
                        }

                        vertexStorage.HorizontalLineTo(curXY.X);
                        break;

                    case 'l': // line to relative
                    case 'L': // line to absolute
                        do
                        {
                            parseIndex++;
                            curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'l')
                            {
                                curXY += lastXY;
                            }

                            vertexStorage.LineTo(curXY.X, curXY.Y);
                            lastXY = curXY;
                        } while (NextElementIsANumber(dString, parseIndex));
                        break;

                    case 'm': // move to relative
                    case 'M': // move to absolute
                        parseIndex++;
                        do
                        {
                            // svg fonts are stored cw and agg expects its shapes to be ccw.  cw shapes are holes.
                            // so we store the position of the start of this polygon so we can flip it when we close it.
                            polyStartVertexSourceIndex = vertexStorage.Count;
                            curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'm')
                            {
                                curXY += lastXY;
                            }

                            vertexStorage.MoveTo(curXY.X, curXY.Y);
                            polyStart = curXY;
                            lastXY = curXY;
                        } while (NextElementIsANumber(dString, parseIndex));
                        break;

                    case 'q': // quadratic Bézier curveto relative
                    case 'Q': // quadratic Bézier curveto absolute
                        {
                            parseIndex++;
                            Vector2 controlPoint;
                            controlPoint.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            controlPoint.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'q')
                            {
                                controlPoint += lastXY;
                                curXY += lastXY;
                            }

                            vertexStorage.curve3(controlPoint.X, controlPoint.Y, curXY.X, curXY.Y);
                        }
                        break;

                    case 't': // Shorthand/smooth quadratic Bézier curveto relative
                    case 'T': // Shorthand/smooth quadratic Bézier curveto absolute
                        parseIndex++;
                        curXY.X = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                        curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                        if (command == 't')
                        {
                            curXY += lastXY;
                        }

                        vertexStorage.curve3(curXY.X, curXY.Y);
                        break;

                    case 'v': // vertical line to relative
                    case 'V': // vertical line to absolute
                        parseIndex++;
                        curXY.X = lastXY.X;
                        curXY.Y = agg_basics.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                        if (command == 'v')
                        {
                            curXY.Y += lastXY.Y;
                        }

                        vertexStorage.VerticalLineTo(curXY.Y);
                        break;

                    case 'z': // close path
                    case 'Z': // close path
                        parseIndex++;
                        curXY = lastXY; // value not used this is to remove an error.
                        vertexStorage.ClosePolygon();
                        // svg fonts are stored cw and agg expects its shapes to be ccw.  cw shapes are holes.
                        // We stored the position of the start of this polygon, now we flip it as we close it.
                        vertexStorage.invert_polygon(polyStartVertexSourceIndex);
                        break;

                    case ' ':
                    case '\n': // some white space we need to skip
                    case '\r':
                        parseIndex++;
                        curXY = lastXY; // value not used this is to remove an error.
                        break;

                    default:
                        throw new NotImplementedException("unrecognized d command '" + command + "'.");
                }

                lastXY = curXY;
            }
        }
    }
}