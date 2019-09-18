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
		// public List<Vector3> Vertices { get; set; } = new List<Vector3>();
		public List<Vector3Float> Vertices { get; set; } = new List<Vector3Float>();

		public FaceList Faces { get; set; } = new FaceList();

		/// <summary>
		/// Gets or sets lookup by face index into the UVs and image for a face.
		/// </summary>
		public Dictionary<int, FaceTextureData> FaceTextures { get; set; } = new Dictionary<int, FaceTextureData>();

		private static object nextIdLocker = new object();

		public BspNode FaceBspTree { get; set; } = null;

		public AxisAlignedBoundingBox cachedAABB = null;

		private TransformedAabbCache transformedAabbCache = new TransformedAabbCache();

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
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// with a 3xN vertex array and a 3xM vertex index array.
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

		private ulong _longHashBeforeClean = 0;

		public ulong LongHashBeforeClean
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
			{
				return false;
			}

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
			for (int i = 0; i < Faces.Count; i++)
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
				if (positionToIndex.TryGetValue((position.X, position.Y, position.Z), out int index))
				{
					return index;
				}

				var count = newVertices.Count;
				positionToIndex.Add((position.X, position.Y, position.Z), count);
				newVertices.Add(position);
				return count;
			}

			foreach (var face in Faces)
			{
				int iv0 = GetIndex(Vertices[face.v0]);
				int iv1 = GetIndex(Vertices[face.v1]);
				int iv2 = GetIndex(Vertices[face.v2]);
				if (iv0 != iv1 && iv1 != iv2 && iv2 != iv0)
				{
					newFaces.Add(iv0, iv1, iv2, newVertices);
				}
			}

			this.Faces = newFaces;
			this.Vertices = newVertices;
		}

		public void MergeVertices(double treatAsSameDistance)
		{
			if (Vertices.Count < 2)
			{
				return;
			}

			var sameDistance = new Vector3Float(treatAsSameDistance, treatAsSameDistance, treatAsSameDistance);
			var tinyDistance = new Vector3Float(.001, .001, .001);
			// build a bvh tree of all the vertices
			var bvhTree = BvhTree<int>.CreateNewHierachy(this.Vertices
				.Select((v, i) => new BvhTreeItemData<int>(i, new AxisAlignedBoundingBox(v - tinyDistance, v + tinyDistance))).ToList());

			var newVertices = new List<Vector3Float>(Vertices.Count);
			var vertexIndexRemaping = Enumerable.Range(0, Vertices.Count).Select(i => -1).ToList();
			var searchResults = new List<int>();
			// build up the list of index mapping
			for (int i = 0; i < Vertices.Count; i++)
			{
				// first check if we have already found this vertex
				if (vertexIndexRemaping[i] == -1)
				{
					var vertex = Vertices[i];
					// remember the new index
					var newIndex = newVertices.Count;
					// add it to the vertices we will end up with
					newVertices.Add(vertex);
					// clear for new search
					searchResults.Clear();
					// find everything close
					bvhTree.SearchBounds(new AxisAlignedBoundingBox(vertex - sameDistance, vertex + sameDistance), searchResults);
					// map them to this new vertex
					foreach (var result in searchResults)
					{
						// this vertex has not been mapped
						if (vertexIndexRemaping[result] == -1)
						{
							vertexIndexRemaping[result] = newIndex;
						}
					}
				}
			}

			// now make a new face list with the merge vertices
			int GetIndex(int originalIndex)
			{
				return vertexIndexRemaping[originalIndex];
			}

			var newFaces = new FaceList();
			foreach (var face in Faces)
			{
				int iv0 = GetIndex(face.v0);
				int iv1 = GetIndex(face.v1);
				int iv2 = GetIndex(face.v2);
				if (iv0 != iv1 && iv1 != iv2 && iv2 != iv0)
				{
					newFaces.Add(iv0, iv1, iv2, newVertices);
				}
			}

			this.Faces = newFaces;
			this.Vertices = newVertices;
		}

		/// <summary>
		/// Split the given face on the given plane. Remove the original face
		/// and add as many new faces as required for the split.
		/// </summary>
		/// <param name="faceIndex">The index of the face to split.</param>
		/// <param name="plane">The plane to split the face on. The face will not be split
		/// if it is not intersected by this plane.</param>
		/// <param name="onPlaneDistance">If a given edge of the face has a vertex that is within
		/// this distance of the plane, the edge will not be split.</param>
		/// <returns>Returns if the edge was actually split.</returns>
		public bool SplitFace(int faceIndex, Plane plane, double onPlaneDistance = .001)
		{
			var newVertices = new List<Vector3Float>();
			var newFaces = new List<Face>();
			if (Faces[faceIndex].Split(this.Vertices, plane, newFaces, newVertices, onPlaneDistance))
			{
				var vertexCount = Vertices.Count;
				// remove the face index
				Faces.RemoveAt(faceIndex);
				// add the new vertices
				Vertices.AddRange(newVertices);
				// add the new faces (have to make the vertex indices to the new vertices
				foreach (var newFace in newFaces)
				{
					Face faceNewIndices = newFace;
					faceNewIndices.v0 += vertexCount;
					faceNewIndices.v1 += vertexCount;
					faceNewIndices.v2 += vertexCount;
					Faces.Add(faceNewIndices);
				}

				CleanAndMerge();

				return true;
			}

			return false;
		}

		public class SplitData
		{
			public Face Face { get; }

			public double[] Dist { get; }

			public SplitData(Face face, double[] dist)
			{
				this.Face = face;
				this.Dist = dist;
			}
		}

		public bool Split(Plane plane, double onPlaneDistance = .001, Func<SplitData, bool> clipFace = null)
		{
			var newVertices = new List<Vector3Float>();
			var newFaces = new List<Face>();
			var facesToRemove = new HashSet<int>();

			for (int i = 0; i < Faces.Count; i++)
			{
				var face = Faces[i];

				if (face.Split(this.Vertices, plane, newFaces, newVertices, onPlaneDistance, clipFace))
				{
					// record the face for removal
					facesToRemove.Add(i);
				}
			}

			// make a new list of all the faces we are keeping
			var keptFaces = new List<Face>();
			for (int i = 0; i < Faces.Count; i++)
			{
				if (!facesToRemove.Contains(i))
				{
					keptFaces.Add(Faces[i]);
				}
			}

			var vertexCount = Vertices.Count;

			// add the new vertices
			Vertices.AddRange(newVertices);

			// add the new faces (have to make the vertex indices to the new vertices
			foreach (var newFace in newFaces)
			{
				Face faceNewIndices = newFace;
				faceNewIndices.v0 += vertexCount;
				faceNewIndices.v1 += vertexCount;
				faceNewIndices.v2 += vertexCount;
				keptFaces.Add(faceNewIndices);
			}

			Faces = new FaceList(keptFaces);

			CleanAndMerge();

			return true;
		}

		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			unchecked
			{
				hash = Vertices.Count.GetLongHashCode(hash);
				hash = Faces.Count.GetLongHashCode(hash);

				// we want to at most consider 100000 vertices
				int vertexStep = Math.Max(1, Vertices.Count / 1000);
				for (int i = 0; i < Vertices.Count; i += vertexStep)
				{
					var vertex = Vertices[i];
					hash = vertex.GetLongHashCode(hash);
				}

				// we want to at most consider 100000 faces
				int faceStep = Math.Max(1, Faces.Count / 10000);
				for (int i = 0; i < Faces.Count; i += faceStep)
				{
					var face = Faces[i];
					hash = face.v0.GetLongHashCode(hash);
					hash = face.v1.GetLongHashCode(hash);
					hash = face.v2.GetLongHashCode(hash);
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
				for (int i = 0; i < Faces.Count; i++)
				{
					Faces[i] = new Face(Faces[i].v0, Faces[i].v1, Faces[i].v2, Faces[i].normal.TransformNormal(matrix));
				}

				MarkAsChanged();
			}
		}

		public void CalculateNormals()
		{
			foreach (var face in Faces)
			{
				face.CalculateNormal(Vertices);
			}

			MarkAsChanged();
		}

		public void Translate(Vector3 offset)
		{
			if (offset != Vector3.Zero)
			{
				Vertices.Transform(Matrix4X4.CreateTranslation(offset));

				MarkAsChanged();
			}
		}

		// private static Dictionary<object, int> Ids = new Dictionary<object, int>(ReferenceEqualityComparer.Default);
		private static int nextId = 0;

		public static int GetID()
		{
			lock (nextIdLocker)
			{
				return nextId++;
			}
		}

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
	}

	public static class FaceExtensionMethods
	{
		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(this Face face, Mesh mesh)
		{
			var bounds = AxisAlignedBoundingBox.Empty();
			bounds.ExpandToInclude(mesh.Vertices[face.v0]);
			bounds.ExpandToInclude(mesh.Vertices[face.v1]);
			bounds.ExpandToInclude(mesh.Vertices[face.v2]);
			return bounds;
		}

		/// <summary>
		/// Split the face at the given plane.
		/// </summary>
		/// <param name="face">The face to split.</param>
		/// <param name="faceVertices">The list containing the vertices for the face.</param>
		/// <param name="plane">The plane to split at.</param>
		/// <param name="newFaces">The new faces created will be added to this list, not the mesh.</param>
		/// <param name="newVertices">The new vertices will be added to this list, not the mesh.</param>
		/// <param name="onPlaneDistance">Treat any distance less than this as not crossing the plane.</param>
		/// <param name="clipFace">An optional function that can be called to check if the given
		/// face should be clipped.</param>
		/// <returns>True if the face crosses the plane else false.</returns>
		public static bool Split(this Face face, List<Vector3Float> faceVertices, Plane plane, List<Face> newFaces, List<Vector3Float> newVertices, double onPlaneDistance, Func<Mesh.SplitData, bool> clipFace = null)
		{
			var v = new Vector3Float[]
			{
				faceVertices[face.v0],
				faceVertices[face.v1],
				faceVertices[face.v2]
			};

			// get the distance from the crossing plane
			var dist = v.Select(a => plane.GetDistanceFromPlane(a)).ToArray();

			// bool if each point is clipped
			var clipPoint = dist.Select(a => Math.Abs(a) > onPlaneDistance).ToArray();

			// bool if there is a clip on a line segment (between points)
			var clipSegment = clipPoint.Select((a, i) =>
			{
				var nextI = (i + 1) % 3;
				// if both points are clipped and they are on opposite sides of the clip plane
				return clipPoint[i] && clipPoint[nextI] && ((dist[i] < 0 && dist[nextI] > 0) || (dist[i] > 0 && dist[nextI] < 0));
			}).ToArray();

			// the number of segments that need to be clipped
			var segmentsClipped = clipSegment[0] ? 1 : 0;
			segmentsClipped += clipSegment[1] ? 1 : 0;
			segmentsClipped += clipSegment[2] ? 1 : 0;

			void ClipEdge(int vi0)
			{
				var vi1 = (vi0 + 1) % 3;
				var vi2 = (vi0 + 2) % 3;
				var totalDistance = Math.Abs(dist[vi0]) + Math.Abs(dist[vi1]);
				var ratioTodist0 = Math.Abs(dist[vi0]) / totalDistance;
				var newPoint = v[vi0] + (v[vi1] - v[vi0]) * ratioTodist0;
				// add the new vertex
				newVertices.Add(newPoint);
			}

			switch (segmentsClipped)
			{
				// if 2 sides are clipped we will add 2 new vertices and 3 polygons
				case 2:
					if (clipFace?.Invoke(new Mesh.SplitData(face, dist)) != false)
					{
						// find the side we are not going to clip
						int vi0 = clipSegment[0] && clipSegment[1] ? 2
							: clipSegment[0] && clipSegment[2] ? 1 : 0;
						var vi1 = (vi0 + 1) % 3;
						var vi2 = (vi0 + 2) % 3;
						// get the current count
						var vertexStart = newVertices.Count;
						// add the existing vertices
						newVertices.Add(v[vi0]);
						newVertices.Add(v[vi1]);
						newVertices.Add(v[vi2]);
						// clip the edges, will add the new points
						ClipEdge(vi1);
						ClipEdge(vi2);
						// add the new faces
						newFaces.Add(new Face(vertexStart, vertexStart + 1, vertexStart + 3, newVertices));
						newFaces.Add(new Face(vertexStart, vertexStart + 3, vertexStart + 4, newVertices));
						newFaces.Add(new Face(vertexStart + 3, vertexStart + 2, vertexStart + 4, newVertices));
						return true;
					}

					break;

				// if 1 side is clipped we will add 1 new vertex and 2 polygons
				case 1:
					{
						// find the side we are going to clip
						int vi0 = clipSegment[0] ? 0 : clipSegment[1] ? 1 : 2;
						var vi1 = (vi0 + 1) % 3;
						var vi2 = (vi0 + 2) % 3;
						// get the current count
						var vertexStart = newVertices.Count;
						// add the existing vertices
						newVertices.Add(v[vi0]);
						newVertices.Add(v[vi1]);
						newVertices.Add(v[vi2]);
						// clip the edge, will add the new point
						ClipEdge(vi0);
						// add the new faces
						newFaces.Add(new Face(vertexStart, vertexStart + 3, vertexStart + 2, newVertices));
						newFaces.Add(new Face(vertexStart + 3, vertexStart + 1, vertexStart + 2, newVertices));
					}

					return true;
			}

			return false;
		}

		public static double GetArea(this Face face, Mesh mesh)
		{
			// area = (a * c * sen(B))/2
			var p0 = mesh.Vertices[face.v0];
			var p1 = mesh.Vertices[face.v1];
			var p2 = mesh.Vertices[face.v2];
			var xy = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
			var xz = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);

			double a = (p0 - p1).Length;
			double c = (p0 - p2).Length;
			double b = Vector3.CalculateAngle(xy, xz);

			return (a * c * Math.Sin(b)) / 2d;
		}
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
			return MeshEdge.CreateMeshEdgeList(mesh);
		}

		public static IEnumerable<int> GetCoplanerFaces(this Mesh mesh, int faceIndex)
		{
			var plane = mesh.GetPlane(faceIndex);

			return mesh.GetCoplanerFaces(plane);
		}

		public static double GetSurfaceArea(this Mesh mesh, int faceIndex)
		{
			var face = mesh.Faces[faceIndex];
			var verts = mesh.Vertices;
			var a = (verts[face.v0] - verts[face.v1]).Length;
			var b = (verts[face.v1] - verts[face.v2]).Length;
			var c = (verts[face.v2] - verts[face.v0]).Length;
			var p = 0.5 * (a + b + c);
			return Math.Sqrt(p * (p - a) * (p - b) * (p - c));
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

		/// <summary>
		/// Split a mesh at a series of coplanar planes.
		/// </summary>
		/// <param name="mesh">The mesh to split faces on.</param>
		/// <param name="planeNormal">The plane normal of the planes to split on.</param>
		/// <param name="distancesFromOrigin">The series of coplanar planes to split the mash at.</param>
		/// <param name="onPlaneDistance">Any mesh edge that has a vertex at this distance or less from a cut plane
		/// should not be cut by that plane.</param>
		public static void SplitOnPlanes(this Mesh mesh, Vector3 planeNormal, List<double> distancesFromOrigin, double onPlaneDistance)
		{
			for (int i = 0; i < distancesFromOrigin.Count; i++)
			{
				mesh.Split(new Plane(planeNormal, distancesFromOrigin[i]), onPlaneDistance, (clipData) =>
				{
					// if two distances are less than 0
					if ((clipData.Dist[0] < 0 && clipData.Dist[1] < 0)
						|| (clipData.Dist[1] < 0 && clipData.Dist[2] < 0)
						|| (clipData.Dist[2] < 0 && clipData.Dist[0] < 0))
					{
						return true;
					}

					return false;
				});
			}

			for (int i = distancesFromOrigin.Count - 1; i >= 0; i--)
			{
				mesh.Split(new Plane(planeNormal, distancesFromOrigin[i]), .1);
			}

			return;
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
			if (faces.Any())
			{
				mesh.PlaceTextureOnFaces(faces, textureToUse, mesh.GetMaxPlaneProjection(faces, textureToUse));
			}
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

			mesh.FaceTextures[face] = new FaceTextureData(textureToUse, uvs[0], uvs[1], uvs[2]);

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
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				mesh.RemoveTexture(i, texture, index);
			}

			mesh.MarkAsChanged();
		}

		public static void RemoveTexture(this Mesh mesh, int faceIndex, ImageBuffer texture, int index)
		{
			mesh.FaceTextures.Remove(faceIndex);
		}

		public static void PlaceTexture(this Mesh mesh, ImageBuffer textureToUse, Matrix4X4 textureCoordinateMapping)
		{
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				mesh.PlaceTextureOnFace(i, textureToUse, textureCoordinateMapping);
			}

			mesh.MarkAsChanged();
		}

		public static Mesh TexturedPlane(ImageBuffer textureToUse, double xScale = 1, double yScale = 1)
		{
			throw new NotImplementedException();
			// Mesh texturedPlane = MeshHelper.CreatePlane(xScale, yScale);
			// {
			// Face face = texturedPlane.Faces[0];
			// PlaceTextureOnFace(face, textureToUse);
			// }

			// return texturedPlane;
		}

		/// <summary>
		/// For every T Junction add a vertex to the mesh edge that needs one.
		/// </summary>
		/// <param name="mesh">The mesh to repair.</param>
		public static void RepairTJunctions(this Mesh mesh)
		{
			throw new NotImplementedException();
			// var nonManifoldEdges = mesh.GetNonManifoldEdges();

			// foreach(MeshEdge edge in nonManifoldEdges)
			// {
			// IVertex start = edge.VertexOnEnd[0];
			// IVertex end = edge.VertexOnEnd[1];
			// Vector3 normal = (end.Position - start.Position).GetNormal();

			// // Get all the vertices that lay on this edge
			// foreach (var vertex in mesh.Vertices)
			// {
			// // test if it falls on the edge
			// // split the edge at them
			// IVertex createdVertex;
			// MeshEdge createdMeshEdge;
			// mesh.SplitMeshEdge(edge, out createdVertex, out createdMeshEdge);
			// createdVertex.Position = vertex.Position;
			// createdVertex.Normal = vertex.Normal;
			// mesh.MergeVertices(vertex, createdVertex);
			// }
			// }

			// throw new NotImplementedException();

			// and merge the mesh edges that are now manifold
			// mesh.MergeMeshEdges(CancellationToken.None);
		}

		public static bool IsManifold(this Mesh mesh)
		{
			throw new NotImplementedException();
			// var nonManifoldEdges = mesh.GetNonManifoldEdges();

			// if(nonManifoldEdges.Count == 0)
			// {
			// return true;
			// }

			// Every non-manifold edge must have matching non-manifold edge(s) that it lines up with.
			// If this is true the model is still functionally manifold.

			return false;
		}
	}
}