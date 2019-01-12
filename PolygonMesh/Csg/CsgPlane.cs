using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Csg
{
	// Represents a plane in 3D space.
	public struct CsgPlane
	{
		public Vector3 normal;
		public double w;

		public CsgPlane(Vector3 a, Vector3 b, Vector3 c)
		{
			this.normal = (Vector3Ex.Cross(b - a, c - a)).GetNormal();
			this.w = Vector3Ex.Dot(this.normal, a);
		}

		public bool ok()
		{
			return this.normal.Length > 0.0;
		}

		public void flip()
		{
			this.normal = -this.normal;
			this.w *= -1.0;
		}

		[Flags]
		public enum PolyType
		{
			COPLANAR = 0,
			FRONT = 1,
			BACK = 2,
			SPANNING = 3
		};

		// Split `polygon` by this plane if needed, then put the polygon or polygon
		// fragments in the appropriate lists. Coplanar polygons go into either
		// `coplanarFront` or `coplanarBack` depending on their orientation with
		// respect to this plane. Polygons in front or in back of this plane go into
		// either `front` or `back`.
		public void SplitPolygon(CsgPolygon polygon, List<CsgPolygon> coplanarFront, List<CsgPolygon> coplanarBack, List<CsgPolygon> front, List<CsgPolygon> back)
		{
			throw new NotImplementedException();
			//double splitTolerance = 0.00001;
			//// Classify each point as well as the entire polygon into one of the above
			//// four classes.
			//PolyType polygonType = PolyType.COPLANAR;
			//List<PolyType> types = new List<PolyType>();

			//for (int i = 0; i < polygon.vertices.Count; i++)
			//{
			//	double t = Vector3Ex.Dot(this.normal, polygon.vertices[i].Position) - this.w;
			//	PolyType type = (t < -splitTolerance) ? PolyType.BACK : ((t > splitTolerance) ? PolyType.FRONT : PolyType.COPLANAR);
			//	polygonType |= type;
			//	types.Add(type);
			//}

			//// Put the polygon in the correct list, splitting it when necessary.
			//switch (polygonType)
			//{
			//	case PolyType.COPLANAR:
			//		{
			//			if (Vector3Ex.Dot(this.normal, polygon.plane.normal) > 0)
			//			{
			//				coplanarFront.Add(polygon);
			//			}
			//			else
			//			{
			//				coplanarBack.Add(polygon);
			//			}
			//			break;
			//		}
			//	case PolyType.FRONT:
			//		{
			//			front.Add(polygon);
			//			break;
			//		}
			//	case PolyType.BACK:
			//		{
			//			back.Add(polygon);
			//			break;
			//		}
			//	case PolyType.SPANNING:
			//		{
			//			List<IVertex> frontVertices = new List<IVertex>();
			//			List<IVertex> backVertices = new List<IVertex>();
			//			for (int firstVertexIndex = 0; firstVertexIndex < polygon.vertices.Count; firstVertexIndex++)
			//			{
			//				int nextVertexIndex = (firstVertexIndex + 1) % polygon.vertices.Count;
			//				PolyType firstPolyType = types[firstVertexIndex];
			//				PolyType nextPolyType = types[nextVertexIndex];
			//				IVertex firstVertex = polygon.vertices[firstVertexIndex];
			//				IVertex nextVertex = polygon.vertices[nextVertexIndex];
			//				if (firstPolyType != PolyType.BACK)
			//				{
			//					frontVertices.Add(firstVertex);
			//				}

			//				if (firstPolyType != PolyType.FRONT)
			//				{
			//					backVertices.Add(firstVertex);
			//				}

			//				if ((firstPolyType | nextPolyType) == PolyType.SPANNING)
			//				{
			//					double planDotFirstVertex = Vector3Ex.Dot(this.normal, firstVertex.Position);
			//					double firstDistToPlane = this.w - planDotFirstVertex;
			//					Vector3 deltaFromFirstToNext = nextVertex.Position - firstVertex.Position;
			//					double t = firstDistToPlane / Vector3Ex.Dot(this.normal, deltaFromFirstToNext);
			//					IVertex newVertex = firstVertex.CreateInterpolated(nextVertex, t);
			//					frontVertices.Add(newVertex);
			//					backVertices.Add(newVertex);
			//				}
			//			}

			//			if (frontVertices.Count >= 3)
			//			{
			//				front.Add(new CsgPolygon(frontVertices));
			//			}

			//			if (backVertices.Count >= 3)
			//			{
			//				back.Add(new CsgPolygon(backVertices));
			//			}
			//		}
			//		break;

			//	default:
			//		throw new NotImplementedException();
			//}
		}
	}
}