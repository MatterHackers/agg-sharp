// Original CSG.JS library by Evan Wallace (http://madebyevan.com), under the MIT license.
// GitHub: https://github.com/evanw/csg.js/
//
// C++ port by Tomasz Dabrowski (http://28byteslater.com), under the MIT license.
// GitHub: https://github.com/dabroz/csgjs-cpp/
// C# port by Lars Brubaker
//
// Constructive Solid Geometry (CSG) is a modeling technique that uses Boolean
// operations like union and intersection to combine 3D solids. This library
// implements CSG operations on meshes elegantly and concisely using BSP trees,
// and is meant to serve as an easily understandable implementation of the
// algorithm. All edge cases involving overlapping coplanar polygons in both
// solids are correctly handled.
//

using MatterHackers.VectorMath;
using Net3dBool;
//using Net3dBool;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.PolygonMesh.Csg
{
#if false
	public static class CsgOperations
	{
		// Return a new CSG solid representing space in either this solid or in the
		// solid `csg`. Neither this solid nor the solid `csg` are modified.
		//
		//     +-------+            +-------+
		//     |       |            |       |
		//     |   A   |            |       |
		//     |    +--+----+   =   |       +----+
		//     +----+--+    |       +----+       |
		//          |   B   |            |       |
		//          |       |            |       |
		//          +-------+            +-------+
		//
		public static Mesh Union(Mesh a1, Mesh b1)
		{
			CsgAcceleratedMesh a = new CsgAcceleratedMesh(a1);
			CsgAcceleratedMesh b = new CsgAcceleratedMesh(b1);
			a.SplitOnAllEdgeIntersections(b);
			b.SplitOnAllEdgeIntersections(a);

			a.MarkInternalVertices(b);
			b.MarkInternalVertices(a);

			a.RemoveAllInternalEdges();
			b.RemoveAllInternalEdges();

			a.MergeWith(b);

			return a.mesh;
		}

		// Return a new CSG solid representing space in this solid but not in the
		// solid `csg`. Neither this solid nor the solid `csg` are modified.
		//
		//     A.subtract(B)
		//
		//     +-------+            +-------+
		//     |       |            |       |
		//     |   A   |            |       |
		//     |    +--+----+   =   |    +--+
		//     +----+--+    |       +----+
		//          |   B   |
		//          |       |
		//          +-------+
		//
		public static Mesh Subtract(Mesh a1, Mesh b1)
		{
			CsgAcceleratedMesh a = new CsgAcceleratedMesh(a1);
			CsgAcceleratedMesh b = new CsgAcceleratedMesh(b1);
			a.SplitOnAllEdgeIntersections(b);
			a.MarkInternalVertices(b);

			b.SplitOnAllEdgeIntersections(a);
			b.MarkInternalVertices(a);

			a.RemoveAllInternalEdges();
			b.RemoveAllExternalEdges();
			b.FlipFaces();

			a.MergeWith(b);

			return a.mesh;
		}

		// Return a new CSG solid representing space both this solid and in the
		// solid `csg`. Neither this solid nor the solid `csg` are modified.
		//
		//     A.intersect(B)
		//
		//     +-------+
		//     |       |
		//     |   A   |
		//     |    +--+----+   =   +--+
		//     +----+--+    |       +--+
		//          |   B   |
		//          |       |
		//          +-------+
		//
		public static Mesh Intersect(Mesh a, Mesh b)
		{
			return null;//return PerformOperation(a, b, CsgNode.Intersect);
		}
	}
#else
#if false
	// Public interface implementation
	public static class CsgOperations
	{
		public static Solid SolidFromMesh(Mesh model)
		{
			var solid = new Solid();
			List<Vector3> vertices = new List<Vector3>();
			List<int> indices = new List<int>();

			int nextIndex = 0;
			foreach (Face face in model.Faces)
			{
				List<Vertex> triangle = new List<Vertex>();
				VectorMath.Vector3 first = VectorMath.Vector3.Zero;
				VectorMath.Vector3 last = VectorMath.Vector3.Zero;
				bool isFirst = true;
				int count = 0;
				foreach (FaceEdge faceEdge in face.FaceEdges())
				{
					VectorMath.Vector3 position = faceEdge.firstVertex.Position;
					if(isFirst)
					{
						first = position;
						isFirst = false;
					}
					if (count < 3)
					{
						vertices.Add(new Vector3(position.x, position.y, position.z));
						indices.Add(nextIndex++);
					}
					else // add an entire new polygon
					{
						vertices.Add(new Vector3(first.x, first.y, first.z));
						indices.Add(nextIndex++);
						vertices.Add(new Vector3(last.x, last.y, last.z));
						indices.Add(nextIndex++);
						vertices.Add(new Vector3(position.x, position.y, position.z));
						indices.Add(nextIndex++);
					}
					count++;
					last = position;
				}
			}

			solid.setData(vertices.ToArray(), indices.ToArray());

			return solid;
		}

		public static Mesh MeshFromSolid(Solid solid)
		{
			Mesh model = new Mesh();
			List<Vertex> vertices = new List<Vertex>();
			var indices = solid.getIndices();
			var solidVertices = solid.getVertices();
			for (int vertexIndex = 0; vertexIndex < indices.Length; vertexIndex++)
			{
				var position = solidVertices[indices[vertexIndex]];
				vertices.Add(model.CreateVertex(position.x, position.y, position.z));

				if (vertices.Count > 2)
				{
					model.CreateFace(vertices.ToArray());
					vertices.Clear();
				}
			}

			return model;
		}

		public static Mesh Union(Mesh a, Mesh b)
		{
			if(a.Faces.Count == 0)
			{
				return b;
			}
			if(b.Faces.Count == 0)
			{
				return a;
			}
			var A = SolidFromMesh(a);
			var B = SolidFromMesh(b);

			var modeller = new BooleanModeller(A, B);
			var result = modeller.GetUnion();

			return MeshFromSolid(result);
		}

		public static Mesh Subtract(Mesh a, Mesh b)
		{
			if (a.Faces.Count == 0)
			{
				return b;
			}
			if (b.Faces.Count == 0)
			{
				return a;
			}
			var A = SolidFromMesh(a);
			var B = SolidFromMesh(b);

			var modeller = new BooleanModeller(A, B);
			var result = modeller.GetDifference();

			return MeshFromSolid(result);
		}

		public static Mesh Intersect(Mesh a, Mesh b)
		{
			if (a.Faces.Count == 0)
			{
				return b;
			}
			if (b.Faces.Count == 0)
			{
				return a;
			}
			var A = SolidFromMesh(a);
			var B = SolidFromMesh(b);

			var modeller = new BooleanModeller(A, B);
			var result = modeller.GetIntersection();

			return MeshFromSolid(result);
		}
	}
#else
	public delegate CsgNode CsgFunctionHandler(CsgNode a, CsgNode b);

	// Public interface implementation
	public static class CsgOperations
	{
		public static List<CsgPolygon> PolygonsFromMesh(Mesh model)
		{
			List<CsgPolygon> list = new List<CsgPolygon>();

			foreach (Face face in model.Faces)
			{
				List<Vertex> triangle = new List<Vertex>();
				foreach (FaceEdge faceEdge in face.FaceEdges())
				{
					Vertex v = new Vertex(faceEdge.firstVertex.Position);
					v.Normal = faceEdge.firstVertex.Normal;
					triangle.Add(v);
				}

				// TODO: make sure this polygon is convex
				list.Add(new CsgPolygon(triangle));
			}

			return list;
		}

		public static Mesh MeshFromPolygons(List<CsgPolygon> polygons)
		{
			Mesh model = new Mesh();
			HashSet<PolygonMesh.Vertex> vertices = new HashSet<PolygonMesh.Vertex>();
			for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
			{
				CsgPolygon poly = polygons[polygonIndex];
				vertices.Clear();

				for (int vertexIndex = 0; vertexIndex < poly.vertices.Count; vertexIndex++)
				{
					vertices.Add(model.CreateVertex(poly.vertices[vertexIndex].Position));
				}

				if (vertices.Count > 2)
				{
					model.CreateFace(vertices.ToArray());
				}
			}

			return model;
		}

		public static Mesh Union(Mesh a, Mesh b)
		{
			return PerformOperation(a, b, CsgNode.Union);
		}

		public static Mesh Subtract(Mesh a, Mesh b)
		{
			return PerformOperation(a, b, CsgNode.Subtract);
		}

		public static Mesh Intersect(Mesh a, Mesh b)
		{
			return PerformOperation(a, b, CsgNode.Intersect);
		}

		private static Mesh PerformOperation(Mesh a, Mesh b, CsgFunctionHandler fun)
		{
			CsgNode A = new CsgNode(PolygonsFromMesh(a));
			CsgNode B = new CsgNode(PolygonsFromMesh(b));
			CsgNode AB = fun(A, B);
			List<CsgPolygon> polygons = AB.GetAllPolygons();
			return MeshFromPolygons(polygons);
		}
	}
#endif
#endif
}