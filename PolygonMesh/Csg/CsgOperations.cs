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
		public static Solid SolidFromMesh(Mesh mesh)
		{
			var solid = new Solid();

			solid.setData(mesh.Vertices.ToArray(), mesh.Faces.ToIntArray());

			return solid;
		}

		public static Mesh MeshFromSolid(Solid solid)
		{
			Mesh model = new Mesh();
			var vertices = new List<Vector3>();
			var indices = solid.getIndices();
			var solidVertices = solid.getVertices();
			for (int vertexIndex = 0; vertexIndex < indices.Length; vertexIndex++)
			{
				var position = solidVertices[indices[vertexIndex]];
				vertices.Add(new Vector3(position.X, position.Y, position.Z));

				if (vertices.Count > 2)
				{
					model.CreateFace(vertices.ToArray());
					vertices.Clear();
				}
			}

			model.CleanAndMerge();

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

		public static Mesh Subtract(this Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken)
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
			}, cancellationToken);

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

		public static (Mesh subtract, Mesh intersect) IntersectAndSubtract(this Mesh a, Mesh b)
		{
			return IntersectAndSubtract(a, b, null, CancellationToken.None);
		}

		public static (Mesh subtract, Mesh intersect) IntersectAndSubtract(Mesh recieveSubtraction, Mesh recieveIntersection, Action<string, double> reporter, CancellationToken cancellationToken)
		{
			if (recieveSubtraction.Faces.Count == 0)
			{
				return (recieveSubtraction, recieveIntersection);
			}
			if (recieveIntersection.Faces.Count == 0)
			{
				return (recieveSubtraction, recieveIntersection);
			}
			reporter?.Invoke("Mesh to Solid A", 0);
			var A = SolidFromMesh(recieveSubtraction);
			reporter?.Invoke("Mesh to Solid B", .2);
			var B = SolidFromMesh(recieveIntersection);

			reporter?.Invoke("BooleanModeller", .4);
			var modeller = new BooleanModeller(A, B, (status, progress0To1) =>
			{
				reporter?.Invoke(status, .4 + progress0To1 * .2);
			}, cancellationToken);
			reporter?.Invoke("Intersection", .6);
			var intersection = modeller.GetIntersection();
			reporter?.Invoke("Difference", .6);
			var difference = modeller.GetDifference();

			reporter?.Invoke("Solid to Mesh", .8);
			var results = (MeshFromSolid(difference), MeshFromSolid(intersection));
			reporter?.Invoke("Solid to Mesh", 1);
			return results;
		}
	}
}