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
using System;
using System.Threading;
using MatterHackers.Agg;
using System.Text;
using System.IO;

namespace MatterHackers.PolygonMesh.Csg
{
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
				List<IVertex> triangle = new List<IVertex>();
				VectorMath.Vector3 first = VectorMath.Vector3.Zero;
				VectorMath.Vector3 last = VectorMath.Vector3.Zero;
				bool isFirst = true;
				int count = 0;
				foreach (FaceEdge faceEdge in face.FaceEdges())
				{
					VectorMath.Vector3 position = faceEdge.FirstVertex.Position;
					if(isFirst)
					{
						first = position;
						isFirst = false;
					}
					if (count < 3)
					{
						vertices.Add(new Vector3(position.X, position.Y, position.Z));
						indices.Add(nextIndex++);
					}
					else // add an entire new polygon
					{
						vertices.Add(new Vector3(first.X, first.Y, first.Z));
						indices.Add(nextIndex++);
						vertices.Add(new Vector3(last.X, last.Y, last.Z));
						indices.Add(nextIndex++);
						vertices.Add(new Vector3(position.X, position.Y, position.Z));
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
			List<IVertex> vertices = new List<IVertex>();
			var indices = solid.getIndices();
			var solidVertices = solid.getVertices();
			for (int vertexIndex = 0; vertexIndex < indices.Length; vertexIndex++)
			{
				var position = solidVertices[indices[vertexIndex]];
				vertices.Add(model.CreateVertex(position.X, position.Y, position.Z));

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
			return Union(a, b, null, CancellationToken.None);
		}

		public static Mesh Union(Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken)
		{
			if(a.Faces.Count == 0)
			{
				return b;
			}
			if(b.Faces.Count == 0)
			{
				return a;
			}
			reporter?.Invoke("Mesh to Solid A", 0);
			var A = SolidFromMesh(a);
			reporter?.Invoke("Mesh to Solid B", .2);
			var B = SolidFromMesh(b);

			reporter?.Invoke("BooleanModeller", .4);
			var modeller = new BooleanModeller(A, B, (status, progress0To1) =>
			{
				reporter?.Invoke(status, .4 + progress0To1 * .2);
			}, cancellationToken);

			reporter?.Invoke("Union", .6);
			var result = modeller.GetUnion();

			reporter?.Invoke("Solid to Mesh", .8);
			var solidMesh = MeshFromSolid(result);

			reporter?.Invoke("Solid to Mesh", 1);
			return solidMesh;
		}

		/// <summary>
		/// Subtract b from a
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static Mesh Subtract(this Mesh a, Mesh b)
		{
			return Subtract(a, b, null, CancellationToken.None);
		}

		public static Mesh Subtract(this Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken,
			Action<Vector3[]> object1CuttingFaces = null,
			Action<Vector3[]> object1SplitFaces = null,
			Action<Vector3[]> object2CuttingFaces = null,
			Action<Vector3[]> object2SplitFaces = null)
		{
			if (a.Faces.Count == 0)
			{
				return b;
			}

			if (b.Faces.Count == 0)
			{
				return a;
			}

			reporter?.Invoke("Mesh to Solid A", 0);
			var A = SolidFromMesh(a);

			reporter?.Invoke("Mesh to Solid B", .2);
			var B = SolidFromMesh(b);

			reporter?.Invoke("BooleanModeller", .4);
			var modeller = new BooleanModeller(A, B, (status, progress0To1) =>
			{
				reporter?.Invoke(status, .4 + progress0To1 * .2);
			}, cancellationToken)
			{
				Object1CuttingFaces = object1CuttingFaces,
				Object1SplitFaces = object1SplitFaces,
				Object2CuttingFaces = object2CuttingFaces,
				Object2SplitFaces = object2SplitFaces,
			};

			reporter?.Invoke("Difference", .6);
			var result = modeller.GetDifference();

			reporter?.Invoke("Solid to Mesh", .8);
			var solidMesh = MeshFromSolid(result);

			reporter?.Invoke("Solid to Mesh", 1);
			return solidMesh;
		}

		public static Mesh Intersect(Mesh a, Mesh b)
		{
			return Intersect(a, b, null, CancellationToken.None);
		}

		public static Mesh Intersect(Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken)
		{
			if (a.Faces.Count == 0)
			{
				return b;
			}
			if (b.Faces.Count == 0)
			{
				return a;
			}

			reporter?.Invoke("Mesh to Solid A", 0);
			var A = SolidFromMesh(a);

			reporter?.Invoke("Mesh to Solid B", .2);
			var B = SolidFromMesh(b);

			reporter?.Invoke("BooleanModeller", .4);
			var modeller = new BooleanModeller(A, B);
			reporter?.Invoke("Intersection", .6);
			var result = modeller.GetIntersection();

			reporter?.Invoke("Solid to Mesh", 1);
			return MeshFromSolid(result);
		}

		public static (Mesh subtract, Mesh intersect) IntersectAndSubtract(Mesh recieveSubtraction, Mesh recieveIntersection)
		{
			if (recieveSubtraction.Faces.Count == 0)
			{
				return (recieveSubtraction, recieveIntersection);
			}
			if (recieveIntersection.Faces.Count == 0)
			{
				return (recieveSubtraction, recieveIntersection);
			}
			var A = SolidFromMesh(recieveSubtraction);
			var B = SolidFromMesh(recieveIntersection);

			var modeller = new BooleanModeller(A, B);
			var intersection = modeller.GetIntersection();
			var difference = modeller.GetDifference();

			return (MeshFromSolid(difference), MeshFromSolid(intersection));
		}
	}
}