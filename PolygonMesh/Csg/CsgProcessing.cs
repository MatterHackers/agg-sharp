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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
	public static class FaceHelper
	{
		public enum IntersectionType { None, Vertex, MeshEdge, Face }

		//public static IntersectionType Intersection(this Face face, Ray ray, out Vector3 intersectionPosition)
		//{
		//	Plane facePlane = new Plane(face.Normal, face.firstFaceEdge.FirstVertex.Position);
		//	double distanceToHit;
		//	bool hitFrontOfPlane;
		//	if (facePlane.RayHitPlane(ray, out distanceToHit, out hitFrontOfPlane))
		//	{
		//		intersectionPosition = ray.origin + ray.directionNormal * distanceToHit;
		//		return IntersectionType.Face;
		//	}
		//	intersectionPosition = Vector3.PositiveInfinity;
		//	return IntersectionType.None;
		//}

		public static void SplitFaces(this Mesh meshToSplit, Mesh meshToConsider)
		{
			throw new NotImplementedException();
			/*
			AxisAlignedBoundingBox boundsForFaces = meshToSplit.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox boundsForEdges = meshToConsider.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox faceEdgeBoundsIntersection = AxisAlignedBoundingBox.Intersection(boundsForEdges, boundsForFaces);

			foreach (Face thisFace in meshToSplit.Faces.ToArray())// GetFacesTouching(faceEdgeBoundsIntersection).ToArray())
			{
				foreach (Face compareFace in meshToConsider.Faces)//.GetFacesTouching(thisFace.GetAxisAlignedBoundingBox()))
				{
					//distance from the face1 vertices to the face2 plane

					//distances signs from the face1 vertices to the face2 plane

					//if all the signs are zero, the planes are coplanar
					//if all the signs are positive or negative, the planes do not intersect
					//if the signs are not equal...
					bool signsAreSame = true;
					if (!signsAreSame)
					{
						//distance from the face2 vertices to the face1 plane

						//distances signs from the face2 vertices to the face1 plane

						//if the signs are not equal...
						if(!signsAreSame)
						{
							// split all intersecting mesh edges
							// get all splits and sort them along the line
							// split the face at every pair of edge splits
						}
					}
				}
			}
			*/
		}

		public static void FlipFaces(this Mesh a)
		{
			throw new NotImplementedException();
		}

		public static void MarkInternalVertices(this Mesh a, Mesh b)
		{
			throw new NotImplementedException();
		}

		public static void MergeWith(this Mesh a, Mesh b)
		{
			throw new NotImplementedException();
		}

		public static void RemoveAllExternalEdges(this Mesh a)
		{
			throw new NotImplementedException();
		}

		public static void RemoveAllInternalEdges(this Mesh a)
		{
			throw new NotImplementedException();
		}

		//public static IEnumerable<Face> GetFacesTouching(this Mesh a, AxisAlignedBoundingBox edgeBounds)
		//{
		//	// TODO: make this only get the right faces
		//	foreach (var face in a.Faces)
		//	{
		//		yield return face;
		//	}
		//}

		//public static IEnumerable<MeshEdge> GetMeshEdgesTouching(this Mesh a, AxisAlignedBoundingBox faceEdgeBoundsIntersection)
		//{
		//	// TODO: make this only get the right mesh edges
		//	foreach (var meshEdge in a.MeshEdges)
		//	{
		//		yield return meshEdge;
		//	}
		//}
	}
}