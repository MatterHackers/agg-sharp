using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.DataConverters3D
{
	public class MeshFaceTraceable : IPrimitive
	{
		Face face;
		public MeshFaceTraceable(Face face, MaterialAbstract material)
		{
			this.face = face;
			this.Material = material;
		}

		public RGBA_Floats GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				Vector3Float Position = new Vector3Float(face.Normal);
				Vector3Float vecU = new Vector3Float(Position.y, Position.z, -Position.x);
				Vector3Float vecV = Vector3Float.Cross(vecU, Position);

				double u = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecU);
				double v = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecV);
				return Material.GetColor(u, v);
			}
			else
			{
				return Material.GetColor(0, 0);
			}
		}

		public MaterialAbstract Material { get; set; }

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(subRegion))
			{
				results.Add(this);
				return true;
			}

			return false;
		}

		public bool Contains(IBvhItem itemToCheckFor)
		{
			if (this == itemToCheckFor)
			{
				return true;
			}

			return false;
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			// find the point on the plane
			Vector3[] positions = new Vector3[3];
			int index = 0;
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				positions[index++] = faceEdge.FirstVertex.Position;
				if (index == 3)
				{
					break;
				}
			}
			Plane plane = new Plane(positions[0], positions[1], positions[2]);
			double distanceToHit;
			bool hitFrontOfPlane;
			if (plane.RayHitPlane(ray, out distanceToHit, out hitFrontOfPlane))
			{
				Vector3 polyPlaneIntersection = ray.origin + ray.directionNormal * distanceToHit;
				if (face.PointInPoly(polyPlaneIntersection))
				{
					IntersectInfo info = new IntersectInfo();
					info.closestHitObject = this;
					info.distanceToHit = distanceToHit;
					info.HitPosition = polyPlaneIntersection;
					info.normalAtHit = face.Normal;
					info.hitType = IntersectionType.FrontFace;
					return info;
				}
			}

			return null;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException();
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public double GetSurfaceArea()
		{
			AxisAlignedBoundingBox aabb = GetAxisAlignedBoundingBox();

			double minDimension = Math.Min(aabb.XSize, Math.Min(aabb.YSize, aabb.ZSize));
			if (minDimension == aabb.XSize)
			{
				return aabb.YSize * aabb.ZSize;
			}
			else if (minDimension == aabb.YSize)
			{
				return aabb.XSize * aabb.ZSize;
			}

			return aabb.XSize * aabb.YSize;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return face.GetAxisAlignedBoundingBox();
		}

		public Vector3 GetCenter()
		{
			return face.GetCenter();
		}

		public double GetIntersectCost()
		{
			return 700;
		}
	}
}