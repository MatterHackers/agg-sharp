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
using System.Collections.Generic;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace AGG
{
    public class SvgParser
    {
        public List<ColoredVertexSource> Items { get; set; }  = new List<ColoredVertexSource>();

        public SvgParser(string filePath, double scale, int width = -1, int height = -1)
            : this(File.OpenRead(filePath), scale, width, height)
        {
        }

        public SvgParser(Stream stream, double scale, int width = -1, int height = -1)
        {
            var svgDocument = new HtmlDocument();
            svgDocument.Load(stream);

            // get the viewBox
            var viewBox = svgDocument.DocumentNode.SelectSingleNode("//svg").Attributes["viewBox"].Value;

            this.Scale = scale;

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

            var currentPath = new VertexStorage();
            // process all the paths and polygons
            foreach (var pathNode in svgDocument.DocumentNode.SelectNodes("//path"))
            {
                var pathDString = pathNode.Attributes["d"].Value;
                var vertexStorage = new VertexStorage(pathDString);

                currentPath = new VertexStorage(currentPath.CombineWith(vertexStorage));
            }
            Items.Add(new ColoredVertexSource() { VertexSource = currentPath, Color = Color.Black });

            stream.Dispose();
        }

        public double Scale { get; set; } = 0.7;

        public class ColoredVertexSource
        {
            public Color Color { get; set; }
            public IVertexSource VertexSource { get; set; }
        }
    }
}