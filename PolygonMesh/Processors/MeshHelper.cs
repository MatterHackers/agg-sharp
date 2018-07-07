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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public static class MeshHelper
	{
		public static Mesh CreatePlane(double xScale = 1, double yScale = 1)
		{
			return CreatePlane(new Vector2(xScale, yScale));
		}

		public static Mesh CreatePlane(Vector2 scaleIn)
		{
			Vector3 scale = new Vector3(scaleIn * .5); // the plane is -1 to 1 and we want it to be -.5 to .5 so it is a unit cube.
			Mesh plane = new Mesh();
			IVertex[] verts = new Vertex[8];
			verts[0] = plane.CreateVertex(new Vector3(-1, -1, 0) * scale);
			verts[1] = plane.CreateVertex(new Vector3(1, -1, 0) * scale);
			verts[2] = plane.CreateVertex(new Vector3(1, 1, 0) * scale);
			verts[3] = plane.CreateVertex(new Vector3(-1, 1, 0) * scale);

			// front
			plane.CreateFace(new IVertex[] { verts[0], verts[1], verts[2], verts[3] });

			return plane;
		}

		public static Matrix4X4 GetMaxFaceProjection(Face face, ImageBuffer textureToUse, Matrix4X4? initialTransform = null)
		{
			// If not set than make it identity
			var firstTransform = initialTransform == null ? Matrix4X4.Identity : (Matrix4X4)initialTransform;

			var textureCoordinateMapping = Matrix4X4.CreateRotation(new Quaternion(face.Normal, Vector3.UnitZ));

			var bounds = RectangleDouble.ZeroIntersection;
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				var edgeStartPosition = faceEdge.FirstVertex.Position;
				var textureUv = Vector3.Transform(edgeStartPosition, textureCoordinateMapping);
				bounds.ExpandToInclude(new Vector2(textureUv));
			}
			var centering = Matrix4X4.CreateTranslation(new Vector3(-bounds.Left, -bounds.Bottom, 0));
			var scaling = Matrix4X4.CreateScale(new Vector3(1 / bounds.Width, 1 / bounds.Height, 1));

			return textureCoordinateMapping * firstTransform * centering * scaling;
		}

		public static void PlaceTextureOnFace(this Face face, ImageBuffer textureToUse)
		{
			// planer project along the normal of this face
			PlaceTextureOnFace(face, textureToUse, GetMaxFaceProjection(face, textureToUse));
		}

		public static void PlaceTextureOnFace(this Face face, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			face.SetTexture(0, textureToUse);
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				Vector3 edgeStartPosition = faceEdge.FirstVertex.Position;
				Vector3 textureUv = Vector3.Transform(edgeStartPosition, textureCoordinateMapping);
				faceEdge.SetUv(0, new Vector2(textureUv));
			}
			face.ContainingMesh.MarkAsChanged();
		}

		public static void CopyFaces(this Mesh copyTo, Mesh copyFrom)
		{
			foreach (Face face in copyFrom.Faces)
			{
				List<IVertex> faceVertices = new List<IVertex>();
				foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
				{
					// we allow duplicates (the true) to make sure we are not changing the loaded models accuracy.
					IVertex newVertex = copyTo.CreateVertex(faceEdgeToAdd.FirstVertex.Position, CreateOption.CreateNew, SortOption.WillSortLater);
					faceVertices.Add(newVertex);
				}

				// we allow duplicates (the true) to make sure we are not changing the loaded models accuracy.
				copyTo.CreateFace(faceVertices.ToArray(), CreateOption.CreateNew);
			}
		}

		public static void RemoveTexture(this Mesh mesh, ImageBuffer texture, int index)
		{
			foreach (var face in mesh.Faces)
			{
				face.RemoveTexture(texture, index);
			}

			mesh.MarkAsChanged();
		}

		public static void RemoveTexture(this Face face, ImageBuffer texture, int index)
		{
			face.ContainingMesh.FaceTexture.Remove((face, index));
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				face.ContainingMesh.TextureUV.Remove((faceEdge, index));
			}
		}

		/// <summary>
		/// Get the planes for all the faces in a mesh. You can use the Normal on the face
		/// and the distanceFromOrigin to have the plane of the face.
		/// </summary>
		/// <param name="mesh"></param>
		/// <returns></returns>
		public static IEnumerable<(Face face, double distanceFromOrigin)> FacePlanes(this Mesh mesh)
		{
			foreach(var face in mesh.Faces)
			{
				yield return (face, Vector3.Dot(face.Normal, face.firstFaceEdge.FirstVertex.Position));
			}
		}

		public static IEnumerable<(Face face, double distanceFromOrign)> GetPlanerFaces(this Mesh mesh, Vector3 normal, double distanceFromOrigin, double normalTolerance = 0, double distanceTolerance = 0)
		{
			var normalToleranceSquared = normalTolerance * normalTolerance;
			foreach (var facePlane in mesh.FacePlanes())
			{
				if (Math.Abs(facePlane.distanceFromOrigin - distanceFromOrigin) <= distanceTolerance
					&& (facePlane.face.Normal - normal).LengthSquared <= normalToleranceSquared)
				{
					yield return facePlane;
				}
			}
		}

		public static void PlaceTexture(this Mesh mesh, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			foreach (var face in mesh.Faces)
			{
				face.PlaceTextureOnFace(textureToUse, textureCoordinateMapping);
			}

			mesh.MarkAsChanged();
		}

		public static Mesh TexturedPlane(ImageBuffer textureToUse, double xScale = 1, double yScale = 1)
		{
			Mesh texturedPlane = MeshHelper.CreatePlane(xScale, yScale);
			{
				Face face = texturedPlane.Faces[0];
				PlaceTextureOnFace(face, textureToUse);
			}

			return texturedPlane;
		}

		/// <summary>
		/// For every T Junction add a vertex to the mesh edge that needs one.
		/// </summary>
		/// <param name="mesh"></param>
		public static void RepairTJunctions(this Mesh mesh)
		{
			var nonManifoldEdges = mesh.GetNonManifoldEdges();

			foreach(MeshEdge edge in nonManifoldEdges)
			{
				IVertex start = edge.VertexOnEnd[0];
				IVertex end = edge.VertexOnEnd[1];
				Vector3 normal = (end.Position - start.Position).GetNormal();

				// Get all the vertices that lay on this edge
				foreach (var vertex in mesh.Vertices)
				{
					// test if it falls on the edge
					// split the edge at them
					IVertex createdVertex;
					MeshEdge createdMeshEdge;
					mesh.SplitMeshEdge(edge, out createdVertex, out createdMeshEdge);
					createdVertex.Position = vertex.Position;
					createdVertex.Normal = vertex.Normal;
					mesh.MergeVertices(vertex, createdVertex);
				}
			}

			throw new NotImplementedException();

			// and merge the mesh edges that are now manifold
			//mesh.MergeMeshEdges(CancellationToken.None);
		}

		public static bool IsManifold(this Mesh mesh)
		{
			var nonManifoldEdges = mesh.GetNonManifoldEdges();

			if(nonManifoldEdges.Count == 0)
			{
				return true;
			}

			// Every non-manifold edge must have matching non-manifold edge(s) that it lines up with.
			// If this is true the model is still functionally manifold.

			return false;
		}
	}
}