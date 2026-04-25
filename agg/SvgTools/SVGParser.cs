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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.SvgTools
{
    public static class SvgParser
    {
        private static HashSet<char> validNumberStartingCharacters = new HashSet<char> { '-', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        private static HashSet<char> validSkipCharacters = new HashSet<char> { ' ', '\t', '\n', '\r', ',' };

        public static List<ColoredVertexSource> Parse(string filePath, bool flipY)
        {
            using var stream = File.OpenRead(filePath);
            return ParseFull(stream, flipY).elements;
        }

        public static List<ColoredVertexSource> Parse(Stream stream, bool flipY)
        {
            return ParseFull(stream, flipY).elements;
        }

        /// <summary>
        /// Renders an SVG file directly to an ImageBuffer at the specified pixel dimensions.
        /// SVG coordinates (top-left origin, Y-down) are transformed to agg-sharp's coordinate
        /// space (bottom-left origin, Y-up). The viewBox is scaled to exactly fill the target size.
        /// </summary>
        public static ImageBuffer ParseAndRender(string filePath, int targetWidth, int targetHeight)
        {
            using var stream = File.OpenRead(filePath);
            var (elements, viewBoxWidth, viewBoxHeight) = ParseFull(stream, flipY: false);
            return RenderToImageBuffer(elements, viewBoxWidth, viewBoxHeight, targetWidth, targetHeight);
        }

        /// <summary>
        /// Rasterizes a list of colored vector elements (from ParseFull) into an ImageBuffer.
        /// Scales from the SVG viewBox coordinate space to the target pixel dimensions.
        /// Uses the same rendering approach as SvgWidget: scale transform + FlipY after rendering.
        /// </summary>
        public static ImageBuffer RenderToImageBuffer(
            List<ColoredVertexSource> elements,
            double viewBoxWidth,
            double viewBoxHeight,
            int targetWidth,
            int targetHeight)
        {
            var image = new ImageBuffer(targetWidth, targetHeight, 32, new BlenderBGRA());
            var graphics = image.NewGraphics2D();
            graphics.Clear(Color.Transparent);

            double scaleX = targetWidth / viewBoxWidth;
            double scaleY = targetHeight / viewBoxHeight;
            graphics.SetTransform(Affine.NewScaling(scaleX, scaleY));

            foreach (var element in elements)
            {
                if (element.FillEvenOdd)
                    graphics.Rasterizer.filling_rule(Util.filling_rule_e.fill_even_odd);
                graphics.Render(element.VertexSource, element.Color);
                if (element.FillEvenOdd)
                    graphics.Rasterizer.filling_rule(Util.filling_rule_e.fill_non_zero);
            }

            // SVG has Y=0 at top (Y increases down); agg-sharp has Y=0 at bottom (Y increases up).
            // FlipY corrects the coordinate inversion — same approach used by SvgWidget.
            image.FlipY();

            return image;
        }

        private static (List<ColoredVertexSource> elements, double viewBoxWidth, double viewBoxHeight) ParseFull(Stream stream, bool flipY)
        {
            var svgDocument = new HtmlDocument();
            svgDocument.Load(stream);

            // Parse viewBox to get the coordinate space dimensions
            double viewBoxWidth = 16;
            double viewBoxHeight = 16;
            var svgNode = svgDocument.DocumentNode.SelectSingleNode("//svg");
            var viewBoxAttr = svgNode?.Attributes["viewBox"];
            if (viewBoxAttr != null && !string.IsNullOrEmpty(viewBoxAttr.Value))
            {
                var segments = viewBoxAttr.Value.Split(' ');
                if (segments.Length >= 4)
                {
                    double.TryParse(segments[2], out viewBoxWidth);
                    double.TryParse(segments[3], out viewBoxHeight);
                }
            }

            var items = new List<ColoredVertexSource>();

            // Accept any IVertexSource so Ellipse and Stroke don't need to be converted first
            IVertexSource FlipIfRequired(IVertexSource source)
            {
                if (flipY)
                {
                    var flip = Affine.NewScaling(1, -1);
                    return new VertexStorage(new VertexSourceApplyTransform(source, flip));
                }

                return source;
            }

            // process all paths
            if (svgDocument.DocumentNode.Descendants("path").Any())
            {
                foreach (var pathNode in svgDocument.DocumentNode.SelectNodes("//path"))
                {
                    var pathDString = pathNode.Attributes["d"].Value;
                    var vertexStorage = new VertexStorage(pathDString);

                    if (HasFill(pathNode))
                    {
                        bool fillEvenOdd = pathNode.Attributes["fill-rule"]?.Value == "evenodd";
                        items.Add(new ColoredVertexSource(FlipIfRequired(vertexStorage), ExtractFillColor(pathNode), fillEvenOdd));
                    }

                    var (strokeColor, strokeWidth) = ExtractStroke(pathNode);
                    if (strokeWidth > 0)
                    {
                        items.Add(new ColoredVertexSource(FlipIfRequired(new Stroke(vertexStorage, strokeWidth)), strokeColor));
                    }
                }
            }

            // process all circles
            if (svgDocument.DocumentNode.Descendants("circle").Any())
            {
                foreach (var circleNode in svgDocument.DocumentNode.SelectNodes("//circle"))
                {
                    double cx = ParseAttrDouble(circleNode, "cx", 0);
                    double cy = ParseAttrDouble(circleNode, "cy", 0);
                    double r = ParseAttrDouble(circleNode, "r", 0);
                    var ellipse = new Ellipse(cx, cy, r, r);

                    if (HasFill(circleNode))
                    {
                        items.Add(new ColoredVertexSource(FlipIfRequired(ellipse), ExtractFillColor(circleNode)));
                    }

                    var (strokeColor, strokeWidth) = ExtractStroke(circleNode);
                    if (strokeWidth > 0)
                    {
                        items.Add(new ColoredVertexSource(FlipIfRequired(new Stroke(ellipse, strokeWidth)), strokeColor));
                    }
                }
            }

            // process all rects
            if (svgDocument.DocumentNode.Descendants("rect").Any())
            {
                foreach (var rectNode in svgDocument.DocumentNode.SelectNodes("//rect"))
                {
                    double rx = ParseAttrDouble(rectNode, "x", 0);
                    double ry = ParseAttrDouble(rectNode, "y", 0);
                    double rw = ParseAttrDouble(rectNode, "width", 0);
                    double rh = ParseAttrDouble(rectNode, "height", 0);

                    var vertexStorage = new VertexStorage();
                    vertexStorage.MoveTo(rx, ry);
                    vertexStorage.LineTo(rx + rw, ry);
                    vertexStorage.LineTo(rx + rw, ry + rh);
                    vertexStorage.LineTo(rx, ry + rh);
                    vertexStorage.ClosePolygon();

                    items.Add(new ColoredVertexSource(FlipIfRequired(vertexStorage), ExtractFillColor(rectNode)));
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

                    items.Add(new ColoredVertexSource(FlipIfRequired(vertexStorage), ExtractFillColor(polgonNode)));
                }
            }

            return (items, viewBoxWidth, viewBoxHeight);
        }

        private static double ParseAttrDouble(HtmlNode node, string attrName, double defaultValue)
        {
            var attr = node.Attributes[attrName];
            if (attr != null && double.TryParse(attr.Value, out double result))
            {
                return result;
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns true if the node should render a fill.
        /// Nodes with explicit fill="none" are not filled. Nodes with no fill attribute but an
        /// explicit stroke are treated as stroke-only (the common pattern for open-path icons).
        /// </summary>
        private static bool HasFill(HtmlNode node)
        {
            var fill = node.Attributes["fill"]?.Value;
            if (fill == null)
            {
                // No explicit fill — if there's a stroke, treat as stroke-only
                return node.Attributes["stroke"] == null;
            }

            return fill != "none" && fill != "transparent";
        }

        /// <summary>
        /// Extracts stroke color and width from an SVG node.
        /// Returns (default, 0) if no usable stroke is present.
        /// </summary>
        private static (Color color, double width) ExtractStroke(HtmlNode node)
        {
            var strokeStr = node.Attributes["stroke"]?.Value;
            if (strokeStr != null && strokeStr != "none" && strokeStr.StartsWith('#'))
            {
                double width = ParseAttrDouble(node, "stroke-width", 1.0);
                return (new Color(strokeStr), width);
            }

            return (default, 0);
        }

        private static Color ExtractFillColor(HtmlNode node)
        {
            if (node.Attributes["fill"] != null)
            {
                var fillString = node.Attributes["fill"].Value;
                if (fillString.StartsWith('#'))
                {
                    return new Color(fillString);
                }
            }
            else if (node.Attributes["style"] != null)
            {
                var styleString = node.Attributes["style"].Value;
                var fillMatch = System.Text.RegularExpressions.Regex.Match(styleString, @"fill:\s*#([0-9A-Fa-f]{6})");
                if (fillMatch.Success)
                {
                    return new Color("#" + fillMatch.Groups[1].Value);
                }
            }

            return Color.Black;
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

                    case 'q': // quadratic B�zier curveto relative
                    case 'Q': // quadratic B�zier curveto absolute
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

                    case 't': // Shorthand/smooth quadratic B�zier curveto relative
                    case 'T': // Shorthand/smooth quadratic B�zier curveto absolute
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
                    case '\t': // tab character
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