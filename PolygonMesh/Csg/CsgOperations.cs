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

using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.PolygonMesh.Csg
{
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
}