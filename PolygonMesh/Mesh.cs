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
		public List<Vector3Float> FaceNormals
		{
			get
			{
				if(_faceNormals.Count != Faces.Count)
				{
					// calculate them from the faces
					_faceNormals = new List<Vector3Float>(Faces.Count);
					for(int i=0; i<Faces.Count; i++)
					{
						var position0 = Vertices[Faces[i].v0];
						var position1 = Vertices[Faces[i].v1];
						var position2 = Vertices[Faces[i].v2];
						var faceEdge1Minus0 = position1 - position0;
						var face2Minus0 = position2 - position0;
						_faceNormals.Add(faceEdge1Minus0.Cross(face2Minus0).GetNormal());
					}
				}
				return _faceNormals;
			}
			set
			{
				_faceNormals = value;
			}
		}

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
		/// Iitialize with a 3xN vertex array and a 3xM vertex index array
		/// </summary>
		/// <param name="v">a 3xN array of doubles representing vertices</param>
		/// <param name="f">a 3xM array of ints represeting face vertex indexes</param>
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
				this.Faces.Add((f[faceIndex + 0],
					f[faceIndex + 1],
					f[faceIndex + 2]));
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

		public long GetLongHashCode()
		{
			unchecked
			{
				long hash = 19;

				hash = hash * 31 + Vertices.Count;
				hash = hash * 31 + Faces.Count;

				int vertexStep = Math.Max(1, Vertices.Count / 16);
				for (int i = 0; i < Vertices.Count; i += vertexStep)
				{
					var vertex = Vertices[i];
					hash = hash * 31 + vertex.GetLongHashCode();
				}

				// also need the direction of vertices for face edges
				int faceStep = Math.Max(1, Faces.Count / 16);
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
				MarkAsChanged();
			}
		}

		public void CalculateNormals()
		{
			// TODO: add a property bag of normals for the faces
			throw new NotImplementedException();
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
		private List<Vector3Float> _faceNormals = new List<Vector3Float>();

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
				this.Faces.Add((firstVertex, firstVertex + i + 1, firstVertex + i + 2));
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
	}
}