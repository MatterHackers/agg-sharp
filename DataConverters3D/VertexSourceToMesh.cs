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

using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.DataConverters3D
{
	public static class VertexSourceToMesh
	{
		public static Mesh TriangulateFaces(this IVertexSource vertexSource,
			CachedTesselator teselatedSource = null,
			Mesh meshToAddTo = null,
			double zHeight = 0)
		{
			return TriangulateFaces(vertexSource.Vertices(), teselatedSource, meshToAddTo, zHeight);
		}

		public static Mesh TriangulateFaces(this IEnumerable<VertexData> vertexSource,
			CachedTesselator teselatedSource = null,
			Mesh meshToAddTo = null,
			double zHeight = 0,
			Matrix4X4? inMatrix = null)
		{
			bool isIdentity = inMatrix == null || inMatrix.Value == Matrix4X4.Identity;
			if (teselatedSource == null)
			{
				teselatedSource = new CachedTesselator();
			}

			VertexSourceToTesselator.SendShapeToTesselator(teselatedSource, vertexSource);

			if (meshToAddTo == null)
			{
				meshToAddTo = new Mesh();
			}

			int numIndicies = teselatedSource.IndicesCache.Count;

			// turn the tessellation output into mesh faces
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				if (!isIdentity)
				{
					var matrix = inMatrix.Value;
					meshToAddTo.CreateFace(new Vector3[] 
					{ 
						new Vector3(v0, zHeight).Transform(matrix),
						new Vector3(v1, zHeight).Transform(matrix), 
						new Vector3(v2, zHeight).Transform(matrix)
					});
				}
				else
				{
					meshToAddTo.CreateFace(new Vector3[] { new Vector3(v0, zHeight), new Vector3(v1, zHeight), new Vector3(v2, zHeight) });
				}
			}

			return meshToAddTo;
		}

		private static double fixCloseAngles(double angle)
		{
			if (Math.Abs(angle) < .001)
			{
				return 0;
			}

			if (angle < MathHelper.Tau + .001
				&& angle > MathHelper.Tau - .001)
			{
				return MathHelper.Tau;
			}

			return angle;
		}

		public static Mesh Revolve(this IVertexSource source,
			int angleSteps = 30,
			double angleStart = 0,
			double angleEnd = MathHelper.Tau,
			bool revolveAroundZ = true)
		{
			angleStart = MathHelper.Range0ToTau(angleStart);
			angleEnd = MathHelper.Range0ToTau(angleEnd);

			// make sure we close 360 shapes
			angleStart = fixCloseAngles(angleStart);
			angleEnd = fixCloseAngles(angleEnd);

			if (angleStart == 0 && angleEnd == MathHelper.Tau)
			{
				angleSteps = Math.Max(angleSteps, 3);
			}
			else
			{
				angleSteps = Math.Max(angleSteps, 1);
			}

			// convert to clipper polygons and scale so we can ensure good shapes
			Polygons polygons = source.CreatePolygons();

			if (polygons.Select(poly => poly.Where(pos => pos.X < 0)).Any())
			{
				// ensure good winding and consistent shapes
				polygons = polygons.GetCorrectedWinding();
				var bounds = polygons.GetBounds();
				bounds.Inflate(10);
				// clip against x=0 left and right
				var leftClip = new Polygon();
				leftClip.Add(new IntPoint(0, bounds.Bottom));
				leftClip.Add(new IntPoint(0, bounds.Top));
				leftClip.Add(new IntPoint(bounds.Left, bounds.Top));
				leftClip.Add(new IntPoint(bounds.Left, bounds.Bottom));
				var rightStuff = polygons.Subtract(leftClip);

				var rightClip = new Polygon();
				rightClip.Add(new IntPoint(0, bounds.Top));
				rightClip.Add(new IntPoint(0, bounds.Bottom));
				rightClip.Add(new IntPoint(bounds.Right, bounds.Bottom));
				rightClip.Add(new IntPoint(bounds.Right, bounds.Top));
				var leftStuff = polygons.Subtract(rightClip);
				// mirror left material across the origin
				var leftAdd = leftStuff.Scale(-1, 1);
				if (leftAdd.Count > 0)
				{
					if (rightStuff.Count > 0)
					{
						polygons = rightStuff.Union(leftAdd);
					}
					else
					{
						polygons = leftAdd;
					}
				}
				else
				{
					// there is nothing on the left
					polygons = rightStuff;
				}
				polygons = polygons.GetCorrectedWinding();
			}

			// convert the data back to PathStorage
			VertexStorage cleanedPath = polygons.CreateVertexStorage();

			var mesh = new Mesh();

			var hasStartAndEndFaces = angleStart > 0.000001;
			hasStartAndEndFaces |= angleEnd < MathHelper.Tau - 0.000001;
			// check if we need to make closing faces
			if (hasStartAndEndFaces)
			{
				// make a face for the start
				Mesh extrudedVertexSource = cleanedPath.TriangulateFaces();
				if (revolveAroundZ)
				{
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationZ(angleStart));
				}
				else
				{
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationY(angleStart));
				}

				mesh.CopyFaces(extrudedVertexSource);
			}

			// make the outside shell
			double angleDelta = (angleEnd - angleStart) / angleSteps;
			double currentAngle = angleStart;
			if (!hasStartAndEndFaces)
			{
				angleSteps--;
			}

			for (int i = 0; i < angleSteps; i++)
			{
				AddRevolveStrip(cleanedPath, mesh, currentAngle, currentAngle + angleDelta, revolveAroundZ);
				currentAngle += angleDelta;
			}

			if (!hasStartAndEndFaces)
			{
				if (((angleEnd - angleStart) < .0000001
					|| (angleEnd - MathHelper.Tau - angleStart) < .0000001)
					&& (angleEnd - currentAngle) > .0000001)
				{
					// make sure we close the shape exactly
					AddRevolveStrip(cleanedPath, mesh, currentAngle, angleStart, revolveAroundZ);
				}
			}
			else // add the end face
			{
				// make a face for the end
				Mesh extrudedVertexSource = cleanedPath.TriangulateFaces();
				if (revolveAroundZ)
				{
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationZ(currentAngle));
				}
				else
				{
					extrudedVertexSource.Transform(Matrix4X4.CreateRotationY(angleStart));
				}

				extrudedVertexSource.ReverseFaces();
				mesh.CopyFaces(extrudedVertexSource);
			}

			mesh.CleanAndMerge();

			// return the completed mesh
			return mesh;
		}

		private static void AddRevolveStrip(IVertexSource vertexSource, Mesh mesh, double startAngle, double endAngle, bool revolveAroundZ)
		{
			Vector3 lastPosition = Vector3.Zero;
			Vector3 firstPosition = Vector3.Zero;

			foreach (var vertexData in vertexSource.Vertices())
			{
				if (vertexData.IsStop)
				{
					break;
				}

				if (vertexData.IsMoveTo)
				{
					firstPosition = new Vector3(vertexData.position.X, 0, vertexData.position.Y);
					if (!revolveAroundZ)
					{
						firstPosition = new Vector3(vertexData.position.X, vertexData.position.Y, 0);
					}

					lastPosition = firstPosition;
				}

				if (vertexData.IsLineTo || vertexData.IsClose)
				{

					var currentPosition = new Vector3(vertexData.position.X, 0, vertexData.position.Y);
					if (!revolveAroundZ)
					{
						currentPosition = new Vector3(vertexData.position.X, vertexData.position.Y, 0);
					}

					if (vertexData.IsClose)
					{
						currentPosition = firstPosition;
					}

					if (currentPosition.X != 0 || lastPosition.X != 0)
					{
						if (revolveAroundZ)
						{
							mesh.CreateFace(new Vector3[]
							{
								Vector3Ex.Transform(currentPosition, Matrix4X4.CreateRotationZ(endAngle)),
								Vector3Ex.Transform(currentPosition, Matrix4X4.CreateRotationZ(startAngle)),
								Vector3Ex.Transform(lastPosition, Matrix4X4.CreateRotationZ(startAngle)),
								Vector3Ex.Transform(lastPosition, Matrix4X4.CreateRotationZ(endAngle)),
							});
						}
						else
						{
							mesh.CreateFace(new Vector3[]
							{
								Vector3Ex.Transform(currentPosition, Matrix4X4.CreateRotationY(endAngle)),
								Vector3Ex.Transform(currentPosition, Matrix4X4.CreateRotationY(startAngle)),
								Vector3Ex.Transform(lastPosition, Matrix4X4.CreateRotationY(startAngle)),
								Vector3Ex.Transform(lastPosition, Matrix4X4.CreateRotationY(endAngle)),
							});
						}
					}

					lastPosition = currentPosition;
				}
			}
		}

		public static Mesh Extrude(this IVertexSource vertexSourceIn,
			double zHeightTop,
			List<(double height, double insetAmount)> bevel = null)
		{
			Polygons bottomPolygons = vertexSourceIn.CreatePolygons();

			// ensure good winding and consistent shapes
			bottomPolygons = bottomPolygons.GetCorrectedWinding();

			var mesh = new Mesh();
			
			if (bevel != null)
			{
				// create the bottom polygon
				var bottom = PathStitcher.Stitch(null, 0, bottomPolygons, 0);
				mesh.CopyFaces(bottom);

				var bottomLoop = bottomPolygons;
				var bottomHeight = 0.0;
				// create all the walls
				var topLoop = bottomPolygons;
				var topHeight = bevel[0].height;

				int i = -1;
				while (i < bevel.Count)
				{
					// add the top polygon
					var walls = PathStitcher.Stitch(bottomLoop, bottomHeight, topLoop, topHeight);
					mesh.CopyFaces(walls);
					bottomLoop = topLoop;
					bottomHeight = topHeight;

					i++;
					if (i < bevel.Count)
					{
						topLoop = bottomPolygons.Offset(bevel[i].insetAmount * 1000);
						if (i == bevel.Count - 1)
						{
							topHeight = zHeightTop;
						}
						else
						{
							topHeight = bevel[i + 1].height;
						}
					}
				}

				// create the top polygon
				var top = PathStitcher.Stitch(topLoop, zHeightTop, null, 0);
				mesh.CopyFaces(top);
				mesh.CleanAndMerge();
				return mesh;
			}

			var bottomTeselatedSource = new CachedTesselator();

			// add the top polygon
			var vertexSourceBottom = bottomPolygons.CreateVertexStorage();
			vertexSourceBottom.TriangulateFaces(bottomTeselatedSource, mesh);
			mesh.Translate(new Vector3(0, 0, zHeightTop));

			int numIndicies = bottomTeselatedSource.IndicesCache.Count;

			// then the outside edge
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				var bottomVertex0 = new Vector3(v0, 0);
				var bottomVertex1 = new Vector3(v1, 0);
				var bottomVertex2 = new Vector3(v2, 0);

				var topVertex0 = new Vector3(v0, zHeightTop);
				var topVertex1 = new Vector3(v1, zHeightTop);
				var topVertex2 = new Vector3(v2, zHeightTop);

				if (bottomTeselatedSource.IndicesCache[i + 0].IsEdge)
				{
					mesh.CreateFace(new Vector3[] { bottomVertex0, bottomVertex1, topVertex1, topVertex0 });
				}

				if (bottomTeselatedSource.IndicesCache[i + 1].IsEdge)
				{
					mesh.CreateFace(new Vector3[] { bottomVertex1, bottomVertex2, topVertex2, topVertex1 });
				}

				if (bottomTeselatedSource.IndicesCache[i + 2].IsEdge)
				{
					mesh.CreateFace(new Vector3[] { bottomVertex2, bottomVertex0, topVertex0, topVertex2 });
				}
			}

			// then the bottom
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = bottomTeselatedSource.VerticesCache[bottomTeselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				mesh.CreateFace(new Vector3[] { new Vector3(v2, 0), new Vector3(v1, 0), new Vector3(v0, 0) });
			}

			mesh.CleanAndMerge();

			return mesh;
		}
	}
}
