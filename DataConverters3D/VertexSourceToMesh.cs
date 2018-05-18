﻿/*
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
using System.Threading;
using ClipperLib;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	using Polygons = List<List<IntPoint>>;
	public static class VertexSourceToMesh
	{
		public static Mesh TriangulateFaces(IVertexSource vertexSource)
		{
			CachedTesselator teselatedSource = new CachedTesselator();
			return TriangulateFaces(vertexSource, teselatedSource);
		}

		private static Mesh TriangulateFaces(IVertexSource vertexSource, CachedTesselator teselatedSource)
		{
			VertexSourceToTesselator.SendShapeToTesselator(teselatedSource, vertexSource);

			Mesh extrudedVertexSource = new Mesh();

			int numIndicies = teselatedSource.IndicesCache.Count;

			// build the top first so it will render first when we are translucent
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				IVertex topVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, 0));
				IVertex topVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, 0));
				IVertex topVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, 0));

				extrudedVertexSource.CreateFace(new IVertex[] { topVertex0, topVertex1, topVertex2 });
			}

			return extrudedVertexSource;
		}

		private readonly static double EqualityTolerance = 1e-5f;

		public static Mesh Revolve(IVertexSource source, int angleSteps = 30, double angleStart = 0, double angleEnd = MathHelper.Tau)
		{
			angleSteps = Math.Max(angleSteps, 3);
			angleStart = MathHelper.Range0ToTau(angleStart);
			angleEnd = MathHelper.Range0ToTau(angleEnd);
			// convert to clipper polygons and scale so we can ensure good shapes
			Polygons polygons = VertexSourceToClipperPolygons.CreatePolygons(source);
			// ensure good winding and consistent shapes
			// clip against x=0 left and right
			// mirror left material across the origin
			// union mirrored left with right material
			// convert the data back to PathStorage
			VertexStorage cleanedPath = VertexSourceToClipperPolygons.CreateVertexStorage(polygons);

			Mesh mesh = new Mesh();

			var hasStartAndEndFaces = angleStart > 0.000001;
			hasStartAndEndFaces |= angleEnd < MathHelper.Tau - 0.000001;
			// check if we need to make closing faces
			if (hasStartAndEndFaces)
			{
				// make a face for the start
				CachedTesselator teselatedSource = new CachedTesselator();
				Mesh extrudedVertexSource = TriangulateFaces(source, teselatedSource);
				extrudedVertexSource.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
				extrudedVertexSource.Transform(Matrix4X4.CreateRotationZ(angleStart));
				mesh.CopyFaces(extrudedVertexSource);
			}

			// make the outside shell
			double angleDelta = (angleEnd - angleStart) / angleSteps;
			double currentAngle = angleStart;
			if(!hasStartAndEndFaces)
			{
				angleSteps--;
			}

			for (int i=0; i < angleSteps; i++)
			{
				AddRevolveStrip(cleanedPath, mesh, currentAngle, currentAngle + angleDelta);
				currentAngle += angleDelta;
			}

			if (!hasStartAndEndFaces)
			{
				if (((angleEnd - angleStart) < .0000001
					|| (angleEnd - MathHelper.Tau - angleStart) < .0000001)
					&& (angleEnd - currentAngle) > .0000001)
				{
					// make sure we close the shape exactly
					AddRevolveStrip(cleanedPath, mesh, currentAngle, angleStart);
				}
			}
			else // add the end face
			{
				// make a face for the end
				CachedTesselator teselatedSource = new CachedTesselator();
				Mesh extrudedVertexSource = TriangulateFaces(source, teselatedSource);
				extrudedVertexSource.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
				extrudedVertexSource.Transform(Matrix4X4.CreateRotationZ(currentAngle));
				extrudedVertexSource.ReverseFaceEdges();
				mesh.CopyFaces(extrudedVertexSource);
			}

			// return the completed mesh
			return mesh;
		}

		static void AddRevolveStrip(IVertexSource vertexSource, Mesh mesh, double startAngle, double endAngle)
		{
			CreateOption createOption = CreateOption.CreateNew;
			SortOption sortOption = SortOption.WillSortLater;

			Vector3 lastPosition = Vector3.Zero;

			foreach (var vertexData in vertexSource.Vertices())
			{
				if (vertexData.IsStop)
				{
					break;
				}
				if (vertexData.IsMoveTo)
				{
					lastPosition = new Vector3(vertexData.position.X, 0, vertexData.position.Y);
				}

				if (vertexData.IsLineTo)
				{
					Vector3 currentPosition = new Vector3(vertexData.position.X, 0, vertexData.position.Y);

					IVertex lastStart = mesh.CreateVertex(Vector3.Transform(lastPosition, Matrix4X4.CreateRotationZ(startAngle)), createOption, sortOption);
					IVertex lastEnd = mesh.CreateVertex(Vector3.Transform(lastPosition, Matrix4X4.CreateRotationZ(endAngle)), createOption, sortOption);

					IVertex currentStart = mesh.CreateVertex(Vector3.Transform(currentPosition, Matrix4X4.CreateRotationZ(startAngle)), createOption, sortOption);
					IVertex currentEnd = mesh.CreateVertex(Vector3.Transform(currentPosition, Matrix4X4.CreateRotationZ(endAngle)), createOption, sortOption);

					mesh.CreateFace(new IVertex[] { lastStart, lastEnd, currentEnd, currentStart }, createOption);

					lastPosition = currentPosition;
				}
			}
		}

		public static Mesh Extrude(IVertexSource vertexSource, double zHeight)
		{
			CachedTesselator teselatedSource = new CachedTesselator();
			Mesh extrudedVertexSource = TriangulateFaces(vertexSource, teselatedSource);
			int numIndicies = teselatedSource.IndicesCache.Count;

			extrudedVertexSource.Translate(new Vector3(0, 0, zHeight));

			// then the outside edge
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				IVertex bottomVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, 0));
				IVertex bottomVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, 0));
				IVertex bottomVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, 0));

				IVertex topVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, zHeight));
				IVertex topVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, zHeight));
				IVertex topVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, zHeight));

				if (teselatedSource.IndicesCache[i + 0].IsEdge)
				{
					extrudedVertexSource.CreateFace(new IVertex[] { bottomVertex0, bottomVertex1, topVertex1, topVertex0 });
				}

				if (teselatedSource.IndicesCache[i + 1].IsEdge)
				{
					extrudedVertexSource.CreateFace(new IVertex[] { bottomVertex1, bottomVertex2, topVertex2, topVertex1 });
				}

				if (teselatedSource.IndicesCache[i + 2].IsEdge)
				{
					extrudedVertexSource.CreateFace(new IVertex[] { bottomVertex2, bottomVertex0, topVertex0, topVertex2 });
				}
			}

			// then the bottom
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 0].Index].Position;
				Vector2 v1 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 1].Index].Position;
				Vector2 v2 = teselatedSource.VerticesCache[teselatedSource.IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				IVertex bottomVertex0 = extrudedVertexSource.CreateVertex(new Vector3(v0, 0));
				IVertex bottomVertex1 = extrudedVertexSource.CreateVertex(new Vector3(v1, 0));
				IVertex bottomVertex2 = extrudedVertexSource.CreateVertex(new Vector3(v2, 0));

				extrudedVertexSource.CreateFace(new IVertex[] { bottomVertex2, bottomVertex1, bottomVertex0 });
			}

			return extrudedVertexSource;
		}
	}
}
