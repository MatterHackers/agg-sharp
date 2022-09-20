/*
Copyright (c) 2021, Lars Brubaker
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

using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
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
				&& topLoop?.Count > 0)
			{
				// if there is no bottom than we need to create  bottom
				return CreateBottom(topLoop, topHeight, scaling);
			}

			// simple bottom and top
			if (bottomLoop.Count == 1
				&& topLoop.Count == 1)
			{
				Mesh mesh = null;
				if (bottomLoop[0].Count == topLoop[0].Count)
				{
					mesh = CreateSimpleWall(bottomLoop[0], bottomHeight * 1000, topLoop[0], topHeight * 1000);
				}
				else
				{
					mesh = Stitch2SingleWalls(bottomLoop[0], bottomHeight * 1000, topLoop[0], topHeight * 1000);
				}

				mesh.Transform(Matrix4X4.CreateScale(1 / scaling));
				return mesh;
			}

			var all = new Polygons();
			if (bottomLoop != null)
			{
				all.AddRange(bottomLoop);
			}
			if (topLoop != null)
			{
				all.AddRange(topLoop);
			}
			all = all.GetCorrectedWinding();

			var bevelLoop = all.CreateVertexStorage().TriangulateFaces();

			for (var i = 0; i < bevelLoop.Vertices.Count; i++)
			{
				bevelLoop.Vertices[i] = bevelLoop.Vertices[i] + new Vector3Float(0, 0, bottomHeight);
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

		private static (int bs, int ts) BestStart(Polygon bottomLoop, Polygon topLoop)
		{
			var bestDistance = double.MaxValue;
			var bestBs = 0;
			var bestTs = 0;
			for (var bs = 0; bs < bottomLoop.Count; bs++)
			{
				for (var ts = 0; ts < topLoop.Count; ts++)
				{
					var distance = (bottomLoop[bs] - topLoop[ts]).LengthSquared();
					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestBs = bs;
						bestTs = ts;
					}
				}
			}

			return (bestBs, bestTs);
		}

		private static Mesh Stitch2SingleWalls(Polygon bottomLoop, double bottomHeight, Polygon topLoop, double topHeight)
		{
			var mesh = new Mesh();

			var (bs, ts) = BestStart(bottomLoop, topLoop);

			var bc = bs;
			var tc = ts;
			var loopedB = false;
			var loopedT = false;
			do
			{
				var b1 = bc;
				var b2 = (bc + 1) % bottomLoop.Count;
				var t1 = tc;
				var t2 = (tc + 1) % topLoop.Count;

				var b1b2 = (bottomLoop[b1] - bottomLoop[b2]).LengthSquared();
				var t1t2 = (topLoop[t1] - topLoop[t2]).LengthSquared();

				if ((b1b2 < t1t2 && !loopedB)
					|| loopedT)
				{
					mesh.CreateFace(new Vector3[]
					{
						new Vector3(bottomLoop[b1].X, bottomLoop[b1].Y, bottomHeight),
						new Vector3(bottomLoop[b2].X, bottomLoop[b2].Y, bottomHeight),
						new Vector3(topLoop[tc].X, topLoop[tc].Y, topHeight)
					});

					bc = b2;
					loopedB = bc == bs;
				}
				else
				{
					mesh.CreateFace(new Vector3[]
					{
						new Vector3(bottomLoop[bc].X, bottomLoop[bc].Y, bottomHeight),
						new Vector3(topLoop[t1].X, topLoop[t1].Y, topHeight),
						new Vector3(topLoop[t2].X, topLoop[t2].Y, topHeight)
					});

					tc = t2;
					loopedT = tc == ts;
				}
			} while (bc != bs || tc != ts);


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
