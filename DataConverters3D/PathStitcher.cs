/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	using Polygon = List<ClipperLib.IntPoint>;
	using Polygons = List<List<ClipperLib.IntPoint>>;

	public static class PathStitcher
	{
		public static Mesh Stitch(Polygons bottomLoop, double bottomHeight, Polygons topLoop, double topHeight, double scaling = 1000)
		{
			// only a bottom
			if (bottomLoop?.Count > 0
				&& (topLoop == null || topLoop.Count == 0))
			{
				// if there is no top than we need to create a top
				return CreateTop(bottomLoop, bottomHeight, scaling);
			}

			// only a top
			if ((bottomLoop == null || bottomLoop.Count == 0)
				&& topLoop.Count > 0)
			{
				// if there is no bottom than we need to create  bottom
				return CreateBottom(topLoop, topHeight, scaling);
			}

			// simple bottom and top
			if (bottomLoop.Count == 1
				&& topLoop.Count == 1
				&& bottomLoop[0].Count == topLoop[0].Count)
			{
				var mesh = CreateSimpleWall(bottomLoop[0], bottomHeight * 1000, topLoop[0], topHeight * 1000);
				mesh.Transform(Matrix4X4.CreateScale(1 / scaling));
				return mesh;
			}

			var all = new Polygons();
			all.AddRange(bottomLoop);
			all.AddRange(topLoop);
			all = all.GetCorrectedWinding();

			var bevelLoop = all.CreateVertexStorage().TriangulateFaces();

			for (var i = 0; i < bevelLoop.Vertices.Count; i++)
			{
				bevelLoop.Vertices[i] = bevelLoop.Vertices[i] + new Vector3Float(0, 0, 16);
			}

			return bevelLoop;
		}

		private static Mesh CreateTop(Polygons path, double topHeight, double scaling)
		{
			return path.CreateVertexStorage(scaling).TriangulateFaces(zHeight: topHeight);
		}

		private static Mesh CreateBottom(Polygons path, double bottomHeight, double scaling)
		{
			var mesh = path.CreateVertexStorage(scaling).TriangulateFaces(zHeight: bottomHeight);
			mesh.ReverseFaces();
			return mesh;
		}

		private static Mesh CreateSimpleWall(Polygon bottomLoop, double bottomHeight, Polygon topLoop, double topHeight)
		{
			var mesh = new Mesh();
			for (int i=0; i<bottomLoop.Count; i++)
			{
				var next = (i + 1) % bottomLoop.Count;
				mesh.CreateFace(new Vector3[]
				{
					new Vector3(bottomLoop[i].X, bottomLoop[i].Y, bottomHeight),
					new Vector3(bottomLoop[next].X, bottomLoop[next].Y, bottomHeight),
					new Vector3(topLoop[i].X, topLoop[i].Y, topHeight),
				});
				mesh.CreateFace(new Vector3[]
				{
					new Vector3(bottomLoop[next].X, bottomLoop[next].Y, bottomHeight),
					new Vector3(topLoop[next].X, topLoop[next].Y, topHeight),
					new Vector3(topLoop[i].X, topLoop[i].Y, topHeight),
				});
			}

			return mesh;
		}
	}
}
