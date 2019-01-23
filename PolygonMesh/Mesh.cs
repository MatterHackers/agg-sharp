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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public class FaceTextureData
	{
		public ImageBuffer image;
		public Vector2Float uv0;
		public Vector2Float uv1;
		public Vector2Float uv2;

		public FaceTextureData(ImageBuffer textureToUse, Vector2Float vector2Float1, Vector2Float vector2Float2, Vector2Float vector2Float3)
		{
			this.image = textureToUse;
			this.uv0 = vector2Float1;
			this.uv1 = vector2Float2;
			this.uv2 = vector2Float3;
		}
	}

	public class Mesh
	{
		//public List<Vector3> Vertices { get; set; } = new List<Vector3>();
		public List<Vector3Float> Vertices { get; set; } = new List<Vector3Float>();
		public FaceList Faces { get; set; } = new FaceList();

		/// <summary>
		/// lookup by face index into the UVs and image for a face
		/// </summary>
		public Dictionary<int, FaceTextureData> FaceTextures { get; set; } = new Dictionary<int, FaceTextureData>();

		private static object nextIdLocker = new object();
		public BspNode FaceBspTree { get; set; } = null;
		public AxisAlignedBoundingBox cachedAABB = null;

		TransformedAabbCache transformedAabbCache = new TransformedAabbCache();

		public Dictionary<string, object> PropertyBag = new Dictionary<string, object>();

		public Mesh()
		{
		}

		public Mesh(List<Vector3> v, FaceList f)
		{
			Vertices.Clear();
			Vertices.AddRange(v);

			Faces.Clear();
			Faces.AddRange(f);
		}

		public Mesh(List<Vector3Float> v, FaceList f)
		{
			Vertices.Clear();
			Vertices.AddRange(v);

			Faces.Clear();
			Faces.AddRange(f);
		}

		/// <summary>
		/// Initialize with a 3xN vertex array and a 3xM vertex index array
		/// </summary>
		/// <param name="v">a 3xN array of doubles representing vertices</param>
		/// <param name="f">a 3xM array of ints representing face vertex indexes</param>
		public Mesh(double[] v, int[] f)
		{
			for (int vertexIndex = 0; vertexIndex < v.Length - 2; vertexIndex += 3)
			{
				this.Vertices.Add(new Vector3(v[vertexIndex + 0],
					v[vertexIndex + 1],
					v[vertexIndex + 2]));
			}

			for (int faceIndex = 0; faceIndex < f.Length - 2; faceIndex += 3)
			{
				this.Faces.Add(f[faceIndex + 0],
					f[faceIndex + 1],
					f[faceIndex + 2],
					this.Vertices);
			}
		}

		public event EventHandler Changed;

		public int ChangedCount { get; private set; } = 0;

		long _longHashBeforeClean = 0;
		public long LongHashBeforeClean
		{
			get
			{
				if (_longHashBeforeClean == 0)
				{
					_longHashBeforeClean = GetLongHashCode();
				}

				return _longHashBeforeClean;
			}
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Mesh))
				return false;

			return this.Equals((Mesh)obj);
		}

		public override string ToString()
		{
			return $"Faces = {Faces.Count}, Vertices = {Vertices.Count}";
		}

		public bool Equals(Mesh other)
		{
			if (this.Vertices.Count == other.Vertices.Count
				&& this.Faces.Count == other.Faces.Count)
			{
				for (int i = 0; i < Vertices.Count; i++)
				{
					if (Vertices[i] != other.Vertices[i])
					{
						return false;
					}
				}

				for (int i = 0; i < Faces.Count; i++)
				{
					if (Faces[i].v0 != other.Faces[i].v0
						|| Faces[i].v1 != other.Faces[i].v1
						|| Faces[i].v2 != other.Faces[i].v2)
					{
						return false;
					}
				}

				return true;
			}

			return false;
		}

		public void ReverseFaces()
		{
			for(int i=0; i<Faces.Count; i++)
			{
				ReverseFace(i);
			}
			MarkAsChanged();
		}

		public void ReverseFace(int faceIndex)
		{
			var hold = Faces[faceIndex];
			Faces[faceIndex] = new Face(hold.v0, hold.v2, hold.v1, hold.normal);
		}

		public void CleanAndMerge()
		{
			var newVertices = new List<Vector3Float>();
			var newFaces = new FaceList();

			var positionToIndex = new Dictionary<(float, float, float), int>();
			int GetIndex(Vector3Float position)
			{
				int index;
				if (positionToIndex.TryGetValue((position.X, position.Y, position.Z), out index))
				{
					return index;
				}
				var count = newVertices.Count;
				positionToIndex.Add((position.X, position.Y, position.Z), count);
				newVertices.Add(position);
				return count;
			}

			foreach(var face in Faces)
			{
				int iv0 = GetIndex(Vertices[face.v0]);
				int iv1 = GetIndex(Vertices[face.v1]);
				int iv2 = GetIndex(Vertices[face.v2]);
				newFaces.Add(iv0, iv1, iv2, newVertices);
			}

			this.Faces = newFaces;
			this.Vertices = newVertices;
		}

		public static long RotateLeft(long value, int count)
		{
			ulong val = (ulong)value;
			return (long)((val << count) | (val >> (64 - count)));
		}

		public long GetLongHashCode()
		{
			unchecked
			{
				long hash = 19;

				hash = hash * 31 + Vertices.Count;
				hash = hash * 31 + Faces.Count;

				// we want to at most consider 100000 vertecies
				int vertexStep = Math.Max(1, Vertices.Count / 10000);
				for (int i = 0; i < Vertices.Count; i += vertexStep)
				{
					var vertex = Vertices[i];
					hash ^= RotateLeft(vertex.GetLongHashCode(), 13);
				}

				// we want to at most consider 100000 faces
				int faceStep = Math.Max(1, Faces.Count / 10000);
				for (int i = 0; i < Faces.Count; i += faceStep)
				{
					var face = Faces[i];
					hash = hash * 31 + face.v0;
					hash = hash * 31 + face.v1;
					hash = hash * 31 + face.v2;
				}

				return hash;
			}
		}

		public void MarkAsChanged()
		{
			// mark this unchecked as we don't want to throw an exception if this rolls over.
			unchecked
			{
				transformedAabbCache.Changed();
				cachedAABB = null;
				ChangedCount++;
				Changed?.Invoke(this, null);
			}
		}

		public void Transform(Matrix4X4 matrix)
		{
			if (matrix != Matrix4X4.Identity)
			{
				Vertices.Transform(matrix);
				for(int i=0; i<Faces.Count; i++)
				{
					Faces[i] = new Face(Faces[i].v0, Faces[i].v1, Faces[i].v2, Faces[i].normal.TransformNormal(matrix));
				}
				MarkAsChanged();
			}
		}

		public void CalculateNormals()
		{
			for (int i = 0; i < Faces.Count; i++)
			{
				Faces[i].CalculateNormal(Vertices);
			}
		}

		public void Translate(Vector3 offset)
		{
			if (offset != Vector3.Zero)
			{
				Vertices.Transform(Matrix4X4.CreateTranslation(offset));

				MarkAsChanged();
			}
		}

		#region meshIDs
		//private static Dictionary<object, int> Ids = new Dictionary<object, int>(ReferenceEqualityComparer.Default);
		private static int nextId = 0;

		public static int GetID()
		{
			lock (nextIdLocker)
			{
				return nextId++;
			}
		}
		#endregion

		#region Public Members

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (Vertices.Count == 0)
			{
				return new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero);
			}

			if (cachedAABB == null)
			{
				cachedAABB = Vertices.Bounds();
			}

			return cachedAABB;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 transform)
		{
			return transformedAabbCache.GetAxisAlignedBoundingBox(this, transform);
		}

		public override int GetHashCode()
		{
			return (int)GetLongHashCode();
		}

		public void CreateFace(IEnumerable<Vector3> positionsIn)
		{
			var positions = positionsIn.Distinct();
			int firstVertex = this.Vertices.Count;
			// we don't have to iterate the positions twice if we count them as we add them
			int addedPositions = 0;
			foreach (var p in positions)
			{
				this.Vertices.Add(p);
				addedPositions++;
			}

			for (int i = 0; i < addedPositions - 2; i++)
			{
				this.Faces.Add(firstVertex, firstVertex + i + 1, firstVertex + i + 2, this.Vertices);
			}
		}

		public void CreateFace(IEnumerable<Vector3Float> positionsIn)
		{
			var positions = positionsIn.Distinct();
			int firstVertex = this.Vertices.Count;
			// we don't have to iterate the positions twice if we count them as we add them
			int addedPositions = 0;
			foreach (var p in positions)
			{
				this.Vertices.Add(p);
				addedPositions++;
			}

			for (int i = 0; i < addedPositions - 2; i++)
			{
				this.Faces.Add(firstVertex, firstVertex + i + 1, firstVertex + i + 2, this.Vertices);
			}
		}

		#endregion Public Members
	}

	public static class MeshExtensionMethods
	{
		public static Mesh Copy(this Mesh meshToCopyIn, CancellationToken cancellationToken, Action<double, string> progress = null, bool allowFastCopy = true)
		{
			return new Mesh(meshToCopyIn.Vertices, meshToCopyIn.Faces);
		}
		public static Plane GetPlane(this Mesh mesh, int faceIndex)
		{
			var face = mesh.Faces[faceIndex];
			var verts = mesh.Vertices;
			return new Plane(verts[face.v0], verts[face.v1], verts[face.v2]);
		}

		public static IEnumerable<int> GetCoplanerFaces(this Mesh mesh, Plane plane)
		{
			double normalTolerance = .001;
			double distanceTolerance = .001;

			// TODO: check if the mesh has a face acceleration structure on it (if so use it)
			var normalToleranceSquared = normalTolerance * normalTolerance;
			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				var face = mesh.Faces[faceIndex];
				var faceNormal = mesh.Faces[faceIndex].normal;
				var distanceFromOrigin = faceNormal.Dot(mesh.Vertices[face.v0]);

				if (Math.Abs(plane.DistanceFromOrigin - distanceFromOrigin) <= distanceTolerance
					&& (plane.Normal - new Vector3(faceNormal)).LengthSquared <= normalToleranceSquared)
				{
					yield return faceIndex;
				}
			}
		}

		public static IReadOnlyList<VertexFaceList> NewVertexFaceLists(this Mesh mesh)
		{
			return VertexFaceList.CreateVertexFaceList(mesh);
		}

		public static IReadOnlyList<MeshEdge> NewMeshEdges(this Mesh mesh)
		{
			return MeshEdge.CreateMeshEdgeList(mesh, VertexFaceList.CreateVertexFaceList(mesh));
		}

		public static IEnumerable<int> GetCoplanerFaces(this Mesh mesh, int faceIndex)
		{
			var plane = mesh.GetPlane(faceIndex);

			return mesh.GetCoplanerFaces(plane);
		}

		public static Matrix4X4 GetMaxPlaneProjection(this Mesh mesh, IEnumerable<int> faces, ImageBuffer textureToUse, Matrix4X4? initialTransform = null)
		{
			// If not set than make it identity
			var firstTransform = initialTransform == null ? Matrix4X4.Identity : (Matrix4X4)initialTransform;

			var textureCoordinateMapping = Matrix4X4.CreateRotation(new Quaternion(mesh.Faces[faces.First()].normal.AsVector3(), Vector3.UnitZ));

			var bounds = RectangleDouble.ZeroIntersection;

			foreach (var face in faces)
			{
				foreach (int vertexIndex in new int[] { mesh.Faces[face].v0, mesh.Faces[face].v1, mesh.Faces[face].v2 })
				{
					var edgeStartPosition = mesh.Vertices[vertexIndex];
					var textureUv = edgeStartPosition.Transform(textureCoordinateMapping);
					bounds.ExpandToInclude(new Vector2(textureUv));
				}
			}

			var centering = Matrix4X4.CreateTranslation(new Vector3(-bounds.Left, -bounds.Bottom, 0));
			var scaling = Matrix4X4.CreateScale(new Vector3(1 / bounds.Width, 1 / bounds.Height, 1));

			return textureCoordinateMapping * firstTransform * centering * scaling;
		}

		public static Matrix4X4 GetMaxPlaneProjection(this Mesh mesh, int face, ImageBuffer textureToUse, Matrix4X4? initialTransform = null)
		{
			// If not set than make it identity
			var firstTransform = initialTransform == null ? Matrix4X4.Identity : (Matrix4X4)initialTransform;

			var textureCoordinateMapping = Matrix4X4.CreateRotation(new Quaternion(mesh.Faces[face].normal.AsVector3(), Vector3.UnitZ));

			var bounds = RectangleDouble.ZeroIntersection;

			foreach (int vertexIndex in new int[] { mesh.Faces[face].v0, mesh.Faces[face].v1, mesh.Faces[face].v2 })
			{
				var edgeStartPosition = mesh.Vertices[vertexIndex];
				var textureUv = edgeStartPosition.Transform(textureCoordinateMapping);
				bounds.ExpandToInclude(new Vector2(textureUv));
			}

			var centering = Matrix4X4.CreateTranslation(new Vector3(-bounds.Left, -bounds.Bottom, 0));
			var scaling = Matrix4X4.CreateScale(new Vector3(1 / bounds.Width, 1 / bounds.Height, 1));

			return textureCoordinateMapping * firstTransform * centering * scaling;
		}

		public static void PlaceTextureOnFaces(this Mesh mesh, int face, ImageBuffer textureToUse)
		{
			//// planer project along the normal of this face
			var faces = mesh.GetCoplanerFaces(face);
			mesh.PlaceTextureOnFaces(faces, textureToUse, mesh.GetMaxPlaneProjection(faces, textureToUse));
		}

		public static void PlaceTextureOnFace(this Mesh mesh, int face, ImageBuffer textureToUse)
		{
			//// planer project along the normal of this face
			mesh.PlaceTextureOnFace(face, textureToUse, mesh.GetMaxPlaneProjection(face, textureToUse));
		}

		public static void PlaceTextureOnFace(this Mesh mesh, int face, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			var uvs = new Vector2Float[3];
			int uvIndex = 0;
			foreach (int vertexIndex in new int[] { mesh.Faces[face].v0, mesh.Faces[face].v1, mesh.Faces[face].v2 })
			{
				var edgeStartPosition = mesh.Vertices[vertexIndex];
				var textureUv = edgeStartPosition.Transform(textureCoordinateMapping);
				uvs[uvIndex++] = new Vector2Float(textureUv);
			}

			mesh.FaceTextures.Add(face, new FaceTextureData(textureToUse, uvs[0], uvs[1], uvs[2]));

			mesh.MarkAsChanged();
		}

		public static void PlaceTextureOnFaces(this Mesh mesh, IEnumerable<int> faces, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			var uvs = new Vector2Float[3];
			foreach (var face in faces)
			{
				int uvIndex = 0;
				foreach (int vertexIndex in new int[] { mesh.Faces[face].v0, mesh.Faces[face].v1, mesh.Faces[face].v2 })
				{
					var edgeStartPosition = mesh.Vertices[vertexIndex];
					var textureUv = edgeStartPosition.Transform(textureCoordinateMapping);
					uvs[uvIndex++] = new Vector2Float(textureUv);
				}

				mesh.FaceTextures.Add(face, new FaceTextureData(textureToUse, uvs[0], uvs[1], uvs[2]));
			}
			mesh.MarkAsChanged();
		}

		public static void CopyFaces(this Mesh copyTo, Mesh copyFrom)
		{
			int vStart = copyTo.Vertices.Count;
			// add all the vertices
			for (int i = 0; i < copyFrom.Vertices.Count; i++)
			{
				copyTo.Vertices.Add(copyFrom.Vertices[i]);
			}

			// add all the faces
			for (int i = 0; i < copyFrom.Faces.Count; i++)
			{
				var face = copyFrom.Faces[i];
				copyTo.Faces.Add(face.v0 + vStart, face.v1 + vStart, face.v2 + vStart, face.normal);
			}
		}

		public static void RemoveTexture(this Mesh mesh, ImageBuffer texture, int index)
		{
			throw new NotImplementedException();
			//foreach (var face in mesh.Faces)
			//{
			//	face.RemoveTexture(texture, index);
			//}

			//mesh.MarkAsChanged();
		}

		public static void RemoveTexture(this Mesh mesh, int face, ImageBuffer texture, int index)
		{
			throw new NotImplementedException();
			//face.ContainingMesh.FaceTexture.Remove((face, index));
			//foreach (FaceEdge faceEdge in face.FaceEdges())
			//{
			//	face.ContainingMesh.TextureUV.Remove((faceEdge, index));
			//}
		}

		public static void PlaceTexture(this Mesh mesh, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			throw new NotImplementedException();
			//foreach (var face in mesh.Faces)
			//{
			//	face.PlaceTextureOnFace(textureToUse, textureCoordinateMapping);
			//}

			//mesh.MarkAsChanged();
		}

		public static Mesh TexturedPlane(ImageBuffer textureToUse, double xScale = 1, double yScale = 1)
		{
			throw new NotImplementedException();
			//Mesh texturedPlane = MeshHelper.CreatePlane(xScale, yScale);
			//{
			//	Face face = texturedPlane.Faces[0];
			//	PlaceTextureOnFace(face, textureToUse);
			//}

			//return texturedPlane;
		}

		/// <summary>
		/// For every T Junction add a vertex to the mesh edge that needs one.
		/// </summary>
		/// <param name="mesh"></param>
		public static void RepairTJunctions(this Mesh mesh)
		{
			throw new NotImplementedException();
			//var nonManifoldEdges = mesh.GetNonManifoldEdges();

			//foreach(MeshEdge edge in nonManifoldEdges)
			//{
			//	IVertex start = edge.VertexOnEnd[0];
			//	IVertex end = edge.VertexOnEnd[1];
			//	Vector3 normal = (end.Position - start.Position).GetNormal();

			//	// Get all the vertices that lay on this edge
			//	foreach (var vertex in mesh.Vertices)
			//	{
			//		// test if it falls on the edge
			//		// split the edge at them
			//		IVertex createdVertex;
			//		MeshEdge createdMeshEdge;
			//		mesh.SplitMeshEdge(edge, out createdVertex, out createdMeshEdge);
			//		createdVertex.Position = vertex.Position;
			//		createdVertex.Normal = vertex.Normal;
			//		mesh.MergeVertices(vertex, createdVertex);
			//	}
			//}

			//throw new NotImplementedException();

			// and merge the mesh edges that are now manifold
			//mesh.MergeMeshEdges(CancellationToken.None);
		}

		public static bool IsManifold(this Mesh mesh)
		{
			throw new NotImplementedException();
			//var nonManifoldEdges = mesh.GetNonManifoldEdges();

			//if(nonManifoldEdges.Count == 0)
			//{
			//	return true;
			//}

			// Every non-manifold edge must have matching non-manifold edge(s) that it lines up with.
			// If this is true the model is still functionally manifold.

			return false;
		}
	}
}