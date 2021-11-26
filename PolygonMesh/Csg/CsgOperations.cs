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
using System.Collections.Generic;
using System;
using System.Threading;

namespace MatterHackers.PolygonMesh.Csg
{
	// Public interface implementation
	public static class CsgOperations
	{
		public static Mesh Union(Mesh a, Mesh b)
		{
			return Union(a, b, null, CancellationToken.None);
		}

		public static Mesh Union(Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken)
		{
			if (a.Faces.Count == 0)
			{
				return b;
			}
			if (b.Faces.Count == 0)
			{
				return a;
			}

			return BooleanProcessing.Do(a,
				Matrix4X4.Identity,
				// other mesh
				b,
				Matrix4X4.Identity,
				// operation type
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				// reporting
				null,
				1,
				0,
				null,
				cancellationToken);
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

			return BooleanProcessing.Do(a,
				Matrix4X4.Identity,
				// other mesh
				b,
				Matrix4X4.Identity,
				// operation type
				CsgModes.Subtract,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				// reporting
				null,
				1,
				0,
				null,
				cancellationToken);
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

			return BooleanProcessing.Do(a,
				Matrix4X4.Identity,
				// other mesh
				b,
				Matrix4X4.Identity,
				// operation type
				CsgModes.Intersect,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				// reporting
				null,
				1,
				0,
				null,
				cancellationToken);
		}

		public static (Mesh subtract, Mesh intersect) IntersectAndSubtract(this Mesh a, Mesh b)
		{
			return IntersectAndSubtract(a, b, null, CancellationToken.None);
		}

		public static (Mesh subtract, Mesh intersect) IntersectAndSubtract(Mesh a, Mesh b, Action<string, double> reporter, CancellationToken cancellationToken)
		{
			var subtract = BooleanProcessing.Do(a,
				Matrix4X4.Identity,
				// other mesh
				b,
				Matrix4X4.Identity,
				// operation type
				CsgModes.Subtract,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				// reporting
				null,
				1,
				0,
				null,
				cancellationToken);

			var intersect = BooleanProcessing.Do(a,
				Matrix4X4.Identity,
				// other mesh
				b,
				Matrix4X4.Identity,
				// operation type
				CsgModes.Intersect,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				// reporting
				null,
				1,
				0,
				null,
				cancellationToken);

			return (subtract, intersect);
		}
	}
}