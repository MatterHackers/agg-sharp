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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.SvgTools
{
    public static class SvgParser
    {
        private static HashSet<char> validNumberStartingCharacters = new HashSet<char> { '-', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        private static HashSet<char> validSkipCharacters = new HashSet<char> { ' ', ',' };

        public static List<ColoredVertexSource> Parse(string filePath, bool flipY)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Parse(stream, flipY);
            }
        }

        public static List<ColoredVertexSource> Parse(Stream stream, bool flipY)
        {
            var svgDocument = new HtmlDocument();
            svgDocument.Load(stream);

            // get the viewBox
            var viewBox = svgDocument.DocumentNode.SelectSingleNode("//svg").Attributes["viewBox"].Value;

            // if we want to parse size at some point
            if (!string.IsNullOrEmpty(viewBox))
            {
                var segments = viewBox.Split(' ');
                int.TryParse(segments[2], out int width);
                int.TryParse(segments[3], out int height);
            }

            var items = new List<ColoredVertexSource>();

            VertexStorage FlipIfRequired(VertexStorage source)
            {
                if (flipY)
                {
                    var flip = Affine.NewScaling(1, -1);
                    return new VertexStorage(new VertexSourceApplyTransform(source, flip));
                }

                return source;
            }

            // process all the paths and polygons
            if (svgDocument.DocumentNode.Descendants("path").Any())
            {
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

                    items.Add(new ColoredVertexSource(FlipIfRequired(vertexStorage), fillColor));
                }
            }

            var fastSimpleNumbers = true;
            if (svgDocument.DocumentNode.Descendants("polygon").Any())
            {
                foreach (var polgonNode in svgDocument.DocumentNode.SelectNodes("//polygon"))
                {
                    var pointsString = polgonNode.Attributes["points"].Value;
                    var vertexStorage = new VertexStorage();
                    var parseIndex = 0;
                    bool first = true;
                    do
                    {
                        var x = Util.ParseDouble(pointsString, ref parseIndex, fastSimpleNumbers);
                        var y = Util.ParseDouble(pointsString, ref parseIndex, fastSimpleNumbers);
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

                    items.Add(new ColoredVertexSource(FlipIfRequired(vertexStorage), Color.Black));
                }
            }

            return items;
        }

        public static string SvgDString(this IVertexSource vertexSource)
        {
            var dstring = new StringBuilder();
            var curveIndex = 0;
            var lastCommand = FlagsAndCommand.Stop;
            foreach (var vertexData in vertexSource.Vertices())
            {
                if (lastCommand != vertexData.Command)
                {
                    curveIndex = 0;
                }

                switch (vertexData.Command)
                {
                    case FlagsAndCommand.MoveTo:
                        {
                            dstring.Append($"M {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                            break;
                        }

                    case FlagsAndCommand.LineTo:
                        {
                            dstring.Append($"L {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                            break;
                        }

                    case FlagsAndCommand.Stop:
                        break;

                    case FlagsAndCommand.FlagClose:
                        {
                            dstring.Append($"Z");
                            break;
                        }

                    case FlagsAndCommand.Curve3:
                        {
                            switch (curveIndex)
                            {
                                case 0:
                                    {
                                        dstring.Append($"Q {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                                        curveIndex++;
                                    }
                                    break;

                                case 1:
                                    {
                                        dstring.Append($" {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                                        curveIndex = 0;
                                    }
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;

                    case FlagsAndCommand.Curve4:
                        {
                            switch (curveIndex)
                            {
                                case 0:
                                    {
                                        dstring.Append($"C {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                                        curveIndex++;
                                    }
                                    break;

                                case 1:
                                    {
                                        dstring.Append($" {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                                        curveIndex++;
                                    }
                                    break;

                                case 2:
                                    {
                                        dstring.Append($" {vertexData.Position.X:0.###} {vertexData.Position.Y:0.###}");
                                        curveIndex = 0;
                                    }
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }

                        }
                        break;

                    default:
                        if (vertexData.IsClose)
                        {
                            dstring.Append($"Z");
                        }
                        break;
                }

                lastCommand = vertexData.Command;
            }

            var newString = dstring.ToString();

            return newString;
        }

        public static void ParseSvgDString(this VertexStorage vertexStorage, string dString)
        {
            vertexStorage.Clear();

            var fastSimpleNumbers = dString.IndexOf('e') == -1;
            var parseIndex = 0;
            var lastXY = new Vector2();
            var curXY = new Vector2();

            var secondControlPoint = new Vector2();
            var polygonStart = new Vector2();
            

            while (parseIndex < dString.Length)
            {
                var command = dString[parseIndex];
                switch (command)
                {
                    case 'a': // relative arc
                    case 'A': // absolute arc
                        {
                            parseIndex++;
                            do
                            {
                                Vector2 radii;
                                radii.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                radii.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var angle = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var largeArcFlag = (int)Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                var sweepFlag = (int)Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);

                                if (command == 'a')
                                {
                                    curXY += lastXY;
                                }

                                AddArcToPath(vertexStorage,
                                    lastXY,
                                    radii,
                                    angle,
                                    largeArcFlag,
                                    sweepFlag,
                                    curXY);

                                lastXY = curXY;

                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;

                    case 'c': // curve to relative
                    case 'C': // curve to absolute
                        {
                            parseIndex++;

                            do
                            {
                                Vector2 controlPoint1;
                                controlPoint1.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                controlPoint1.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                secondControlPoint.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                secondControlPoint.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                if (command == 'c')
                                {
                                    controlPoint1 += lastXY;
                                    secondControlPoint += lastXY;
                                    curXY += lastXY;
                                }

                                vertexStorage.Curve4(controlPoint1.X, controlPoint1.Y, secondControlPoint.X, secondControlPoint.Y, curXY.X, curXY.Y);

                                lastXY = curXY;

                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;

                    case 's': // shorthand/smooth curveto relative
                    case 'S': // shorthand/smooth curveto absolute
                        {
                            parseIndex++;

                            do
                            {
                                Vector2 controlPoint = lastXY;

                                if (vertexStorage[vertexStorage.Count - 1].Command == FlagsAndCommand.Curve4)
                                {
                                    controlPoint = Reflect(secondControlPoint, lastXY);
                                }
                                else
                                {
                                    controlPoint = curXY;
                                }

                                secondControlPoint.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                secondControlPoint.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                                if (command == 's')
                                {
                                    secondControlPoint += lastXY;
                                    curXY += lastXY;
                                }

                                vertexStorage.Curve4(controlPoint.X, controlPoint.Y, secondControlPoint.X, secondControlPoint.Y, curXY.X, curXY.Y);

                                lastXY = curXY;
                                // if the next element is another coordinate than we just continue to add more curves.
                            } while (NextElementIsANumber(dString, parseIndex));
                        }
                        break;

                    case 'h': // horizontal line to relative
                    case 'H': // horizontal line to absolute
                        parseIndex++;
                        do
                        {
                            curXY.Y = lastXY.Y;
                            curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'h')
                            {
                                curXY.X += lastXY.X;
                            }

                            vertexStorage.HorizontalLineTo(curXY.X);

                            lastXY = curXY;
                        } while (NextElementIsANumber(dString, parseIndex));
                        break;

                    case 'l': // line to relative
                    case 'L': // line to absolute
                        parseIndex++;
                        do
                        {
                            curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
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
                            curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'm')
                            {
                                curXY += lastXY;
                            }

                            vertexStorage.MoveTo(curXY.X, curXY.Y);
                            polygonStart = curXY;
                            lastXY = curXY;
                        } while (NextElementIsANumber(dString, parseIndex));
                        break;

                    case 'q': // quadratic Bézier curveto relative
                    case 'Q': // quadratic Bézier curveto absolute
                        parseIndex++;
                        do
                        {
                            Vector2 controlPoint;
                            controlPoint.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            controlPoint.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 'q')
                            {
                                controlPoint += lastXY;
                                curXY += lastXY;
                            }

                            vertexStorage.Curve3(controlPoint.X, controlPoint.Y, curXY.X, curXY.Y);
                        } while (NextElementIsANumber(dString, parseIndex)) ;
                        lastXY = curXY;
                        break;

                    case 't': // Shorthand/smooth quadratic Bézier curveto relative
                    case 'T': // Shorthand/smooth quadratic Bézier curveto absolute
                        parseIndex++;
                        do
                        {
                            curXY.X = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                            if (command == 't')
                            {
                                curXY += lastXY;
                            }

                            vertexStorage.Curve3(curXY.X, curXY.Y);
                        } while (NextElementIsANumber(dString, parseIndex));
                        lastXY = curXY;
                        break;

                    case 'v': // vertical line to relative
                    case 'V': // vertical line to absolute
                        parseIndex++;
                        curXY.X = lastXY.X;
                        curXY.Y = Util.ParseDouble(dString, ref parseIndex, fastSimpleNumbers);
                        if (command == 'v')
                        {
                            curXY.Y += lastXY.Y;
                        }

                        vertexStorage.VerticalLineTo(curXY.Y);
                        lastXY = curXY;
                        break;

                    case 'z': // close path
                    case 'Z': // close path
                        parseIndex++;
                        vertexStorage.ClosePolygon();
                        lastXY = polygonStart;
                        break;

                    case ' ':
                    case '\n': // some white space we need to skip
                    case '\r':
                        parseIndex++;
                        break;

                    default:
                        throw new NotImplementedException("unrecognized d command '" + command + "'.");
                }
            }
        }

        private static double CalculateVectorAngle(double ux, double uy, double vx, double vy)
        {
            var aTanU = Math.Atan2(uy, ux);
            var aTanV = Math.Atan2(vy, vx);

            if (aTanV >= aTanU)
            {
                return aTanV - aTanU;
            }

            return MathHelper.Tau - (aTanU - aTanV);
        }

        public static void AddArcToPath(VertexStorage vertexStorage, Vector2 start, Vector2 radii, double angleDegrees, int size, int sweep, Vector2 end)
        {
            if (start == end)
            {
                return;
            }

            if (radii.X == 0 && radii.Y == 0)
            {
                vertexStorage.LineTo(end);
                return;
            }

            var sinAngle = Math.Sin(MathHelper.DegreesToRadians(angleDegrees));
            var cosAngle = Math.Cos(MathHelper.DegreesToRadians(angleDegrees));

            var center = (start - end) / 2.0;
            var centerAngle = new Vector2(cosAngle * center.X + sinAngle * center.Y, -sinAngle * center.X + cosAngle * center.Y);

            var numerator = radii.X * radii.X * radii.Y * radii.Y - radii.X * radii.X * centerAngle.Y * centerAngle.Y - radii.Y * radii.Y * centerAngle.X * centerAngle.X;

            double root;
            if (numerator < 0.0)
            {
                var s = Math.Sqrt(1.0 - numerator / (radii.X * radii.X * radii.Y * radii.Y));

                radii *= s;
                root = 0.0;
            }
            else
            {
                root = ((size == 1 && sweep == 1)
                    || (size == 0 && sweep == 0) ? -1.0 : 1.0) * Math.Sqrt(numerator / (radii.X * radii.X * centerAngle.Y * centerAngle.Y + radii.Y * radii.Y * centerAngle.X * centerAngle.X));
            }

            var cxdash = root * radii.X * centerAngle.Y / radii.Y;
            var cydash = -root * radii.Y * centerAngle.X / radii.X;

            var cx = cosAngle * cxdash - sinAngle * cydash + (start.X + end.X) / 2.0;
            var cy = sinAngle * cxdash + cosAngle * cydash + (start.Y + end.Y) / 2.0;

            var vectorAngle1 = CalculateVectorAngle(1.0, 0.0, (centerAngle.X - cxdash) / radii.X, (centerAngle.Y - cydash) / radii.Y);
            var vectorAngleD = CalculateVectorAngle((centerAngle.X - cxdash) / radii.X, (centerAngle.Y - cydash) / radii.Y, (-centerAngle.X - cxdash) / radii.X, (-centerAngle.Y - cydash) / radii.Y);

            if (sweep == 0 && vectorAngleD > 0)
            {
                vectorAngleD -= 2.0 * Math.PI;
            }
            else if (sweep == 1 && vectorAngleD < 0)
            {
                vectorAngleD += 2.0 * Math.PI;
            }

            var segments = (int)Math.Ceiling((double)Math.Abs(vectorAngleD / (Math.PI / 2.0)));
            var delta = vectorAngleD / segments;
            var t = 8.0 / 3.0 * Math.Sin(delta / 4.0) * Math.Sin(delta / 4.0) / Math.Sin(delta / 2.0);

            for (var i = 0; i < segments; ++i)
            {
                var cosTheta1 = Math.Cos(vectorAngle1);
                var sinTheta1 = Math.Sin(vectorAngle1);
                var theta2 = vectorAngle1 + delta;
                var cosTheta2 = Math.Cos(theta2);
                var sinTheta2 = Math.Sin(theta2);

                var endpoint = new Vector2(cosAngle * radii.X * cosTheta2 - sinAngle * radii.Y * sinTheta2 + cx,
                                           sinAngle * radii.X * cosTheta2 + cosAngle * radii.Y * sinTheta2 + cy);

                var dx1 = t * (-cosAngle * radii.X * sinTheta1 - sinAngle * radii.Y * cosTheta1);
                var dy1 = t * (-sinAngle * radii.X * sinTheta1 + cosAngle * radii.Y * cosTheta1);

                var dxe = t * (cosAngle * radii.X * sinTheta2 + sinAngle * radii.Y * cosTheta2);
                var dye = t * (sinAngle * radii.X * sinTheta2 - cosAngle * radii.Y * cosTheta2);

                vertexStorage.Curve4(start.X + dx1, start.Y + dy1, endpoint.X + dxe, endpoint.Y + dye, endpoint.X, endpoint.Y);

                vectorAngle1 = theta2;
                start = endpoint;
            }
        }

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

        public static void RenderDebug(this IVertexSource vertexSource, Graphics2D graphics)
        {
            IEnumerator<VertexData> vertexDataEnumerator = vertexSource.Vertices().GetEnumerator();
            VertexData lastPosition = new VertexData();
            while (vertexDataEnumerator.MoveNext())
            {
                VertexData vertexData = vertexDataEnumerator.Current;

                switch (vertexData.Command)
                {
                    case FlagsAndCommand.Stop:
                        break;
                        
                    case FlagsAndCommand.MoveTo:
                        break;
                        
                    case FlagsAndCommand.LineTo:
                        break;
                        
                    case FlagsAndCommand.Curve3:
                        break;
                        
                    case FlagsAndCommand.Curve4:
                        {
                            vertexDataEnumerator.MoveNext();
                            var vertexDataControl2 = vertexDataEnumerator.Current;
                            vertexDataEnumerator.MoveNext();
                            var vertexDataEnd = vertexDataEnumerator.Current;

                            graphics.Line(lastPosition.Position, vertexData.Position, Color.Green);
                            graphics.Line(vertexDataControl2.Position, vertexDataEnd.Position, Color.Green);
                            graphics.Circle(vertexData.Position, 5, Color.Red);
                            graphics.Circle(vertexDataControl2.Position, 5, Color.Red);
                            vertexData = vertexDataEnd;
                        }
                        break;
                        
                    case FlagsAndCommand.EndPoly:
                        break;
                        
                    case FlagsAndCommand.FlagCCW:
                        break;
                        
                    case FlagsAndCommand.FlagCW:
                        break;
                        
                    case FlagsAndCommand.FlagClose:
                        break;
                        
                    case FlagsAndCommand.FlagsMask:
                        break;
                }

                lastPosition = vertexData;
            }
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
    }
}