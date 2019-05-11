/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.DataConverters2D
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class VertexSourceToClipperPolygons
	{
		public static VertexStorage CreateVertexStorage(this Polygons polygons, double scaling = 1000)
		{
			var output = new VertexStorage();

			foreach (Polygon polygon in polygons)
			{
				bool first = true;

				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X / scaling, point.Y / scaling, ShapePath.FlagsAndCommand.MoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X / scaling, point.Y / scaling, ShapePath.FlagsAndCommand.LineTo);
					}
				}

				output.ClosePolygon();
			}

			return output;
		}

		public static Polygons CreatePolygons(this IVertexSource sourcePath, double scaling = 1000)
		{
			var allPolys = new Polygons();
			Polygon currentPoly = null;

			var last = default(VertexData);
			var first = default(VertexData);

			bool addedFirst = false;

			foreach (VertexData vertexData in sourcePath.Vertices())
			{
				if (vertexData.IsLineTo)
				{
					if (!addedFirst)
					{
						currentPoly.Add(new IntPoint(last.position.X * scaling, last.position.Y * scaling));
						addedFirst = true;
						first = last;
					}

					currentPoly.Add(new IntPoint(vertexData.position.X * scaling, vertexData.position.Y * scaling));
					last = vertexData;
				}
				else
				{
					addedFirst = false;
					currentPoly = new Polygon();
					allPolys.Add(currentPoly);

					if (vertexData.IsMoveTo)
					{
						last = vertexData;
					}
					else
					{
						last = first;
					}
				}
			}

			return allPolys;
		}
	}
}