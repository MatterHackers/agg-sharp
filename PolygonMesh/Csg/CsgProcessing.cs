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
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Csg
{
	public static class FaceHelper
	{
		public enum IntersectionType { None, Vertex, MeshEdge, Face }
		public static IntersectionType Intersection(this Face face, Ray ray, out Vector3 intersectionPosition)
		{
			Plane facePlane = new Plane(face.normal, face.firstFaceEdge.firstVertex.Position);
			double distanceToHit;
			bool hitFrontOfPlane;
			if (facePlane.RayHitPlane(ray, out distanceToHit, out hitFrontOfPlane))
			{
				intersectionPosition = ray.origin + ray.directionNormal * distanceToHit;
				return IntersectionType.Face;
			}
			intersectionPosition = Vector3.PositiveInfinity;
			return IntersectionType.None;
		}

		public static MeshEdge GetIntersectedMeshEdge(this Face face, Vector3 intersectionPosition)
		{
			MeshEdge intersectedEdge = null;
			return intersectedEdge;
		}
    }

	public class CsgAcceleratedMesh
	{
		int internalIntegerScale = 100;

		public CsgAcceleratedMesh(Mesh source)
		{
			mesh = Mesh.Copy(source);
			mesh.Triangulate();

			//ScaleAndMakeInteger(mesh, internalIntegerScale);
			//mesh.MergeVertices(); // now that it is integer remove degenerate faces
		}

		public static void ScaleAndMakeInteger(Mesh mesh, int scale)
		{
			for(int i=0; i<mesh.Vertices.Count; i++)
			{
				Vector3 intPosition = mesh.Vertices[i].Position;
				intPosition.x = (int)(mesh.Vertices[i].Position.x * scale + .5);
				intPosition.y = (int)(mesh.Vertices[i].Position.x * scale + .5);
				intPosition.z = (int)(mesh.Vertices[i].Position.x * scale + .5);
				mesh.Vertices[i].Position = intPosition;
            }
		}

		public Mesh mesh { get; internal set; }

		public void SplitOnAllEdgeIntersections(CsgAcceleratedMesh meshWidthEdges)
		{
			AxisAlignedBoundingBox boundsForFaces = this.mesh.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox boundsForEdges = meshWidthEdges.mesh.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox faceEdgeBoundsIntersection = AxisAlignedBoundingBox.Intersection(boundsForEdges, boundsForFaces);

			foreach (var meshEdge in meshWidthEdges.GetMeshEdgesTouching(faceEdgeBoundsIntersection))
			{
				// Check the mesh edge bounds agains all polygons. If there is an intersection
				// subdivide the mesh edge and if the face that is hit. If hit face on an edge only split the edge.
				Vector3 end0 = meshEdge.VertexOnEnd[0].Position;
				Vector3 end1 = meshEdge.VertexOnEnd[1].Position;
				Ray ray = new Ray(end0, (end1 - end0).GetNormal());
				AxisAlignedBoundingBox edgeBounds = new AxisAlignedBoundingBox(Vector3.ComponentMin(end0, end1), Vector3.ComponentMax(end0, end1));

				foreach (Face face in GetFacesTouching(edgeBounds))
				{
					Vector3 intersectionPosition;
					// intersect the face with the edge
					switch (face.Intersection(ray, out intersectionPosition))
					{
						case FaceHelper.IntersectionType.Vertex:
							break;

						case FaceHelper.IntersectionType.MeshEdge:
							{
								SplitMeshEdge(meshEdge, intersectionPosition);
								// split the face at intersectionPosition
								SplitMeshEdgeAtPosition(face, intersectionPosition);
							}
							break;

						case FaceHelper.IntersectionType.Face:
							{
								SplitMeshEdge(meshEdge, intersectionPosition);
								// split the face at intersectionPosition
								SplitFaceAtPosition(face, intersectionPosition);
							}
							break;
					}
				}
			}
		}

		private IEnumerable<MeshEdge> GetMeshEdgesTouching(AxisAlignedBoundingBox faceEdgeBoundsIntersection)
		{
			// TODO: make this only get the right mesh edges
			foreach (var meshEdge in mesh.MeshEdges)
			{
				yield return meshEdge;
			}
		}

		private void SplitMeshEdge(MeshEdge meshEdge, Vector3 intersectionPosition)
		{
			Vertex vertexCreatedDuringSplit;
			MeshEdge meshEdgeCreatedDuringSplit;
			// split the ray at intersectionPosition
			mesh.SplitMeshEdge(meshEdge, out vertexCreatedDuringSplit, out meshEdgeCreatedDuringSplit);
			vertexCreatedDuringSplit.Position = intersectionPosition;
		}

		private void SplitMeshEdgeAtPosition(Face face, Vector3 intersectionPosition)
		{
			MeshEdge intersectedEdge = face.GetIntersectedMeshEdge(intersectionPosition);
			// split the edge
			throw new NotImplementedException();
		}

		private void SplitFaceAtPosition(Face face, Vector3 intersectionPosition)
		{
			//    ^           ^
			//   / \         /|\
			//  /   \   =   / . \
			// /_____\     /_/_\_\  // imagine the bottom lines are connected to the end points

			// get the center and all the vertices this face was connected to
			List<Vertex> faceVertices = new List<Vertex>();
			foreach(var vertex in face.Vertices())
			{
				faceVertices.Add(vertex);
			}

			// remove the face
			mesh.DeleteFace(face);

			// add the Vertex to the mesh
			Vertex centerVertex = mesh.CreateVertex(intersectionPosition);

			// put in the new faces
			for(int i=0; i<faceVertices.Count; i++)
			{
				mesh.CreateFace(new Vertex[] { centerVertex, faceVertices[i], faceVertices[(i+1) % faceVertices.Count] });
			}
		}

		private IEnumerable<Face> GetFacesTouching(AxisAlignedBoundingBox edgeBounds)
		{
			// TODO: make this only get the right faces
			foreach(var face in mesh.Faces)
			{
				yield return face;
			}
		}

		internal void FlipFaces()
		{
			throw new NotImplementedException();
		}

		internal void MarkInternalVertices(CsgAcceleratedMesh b)
		{
			throw new NotImplementedException();
		}

		internal void MergeWith(CsgAcceleratedMesh b)
		{
			throw new NotImplementedException();
		}

		internal void RemoveAllExternalEdges()
		{
			throw new NotImplementedException();
		}

		internal void RemoveAllInternalEdges()
		{
			throw new NotImplementedException();
		}
	}
}