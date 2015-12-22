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
		public static bool IsIntersectedBy(this Face face, Vector3 end0, Vector3 end1, out Vector3 intersectionPosition)
		{
			intersectionPosition = Vector3.Zero;
			return false;
		}

		public static bool MeshEdgeIsIntersectedAt(this Face face, Vector3 intersectionPosition, out MeshEdge intersectedEdge)
		{
			intersectedEdge = null;
			return false;
		}
    }

	public class CsgAcceleratedMesh
	{
		private Mesh mesh;
		int internalIntegerScale = 1000;

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

		public Mesh Mesh { get; internal set; }

		public void SplitOnAllEdgeIntersections(CsgAcceleratedMesh meshWidthEdges)
		{
			foreach (var meshEdge in meshWidthEdges.mesh.MeshEdges)
			{
				// Check the mesh edge bounds agains all polygons. If there is an intersection
				// subdivide the mesh edge and if the face that is hit. If hit face on an edge only split the edge.
				Vector3 end0 = meshEdge.VertexOnEnd[0].Position;
				Vector3 end1 = meshEdge.VertexOnEnd[1].Position;
				AxisAlignedBoundingBox edgeBounds = new AxisAlignedBoundingBox(Vector3.ComponentMin(end0, end1), Vector3.ComponentMax(end0, end1));

				foreach(Face face in GetFacesTouching(edgeBounds))
				{
					Vector3 intersectionPosition;
					// intersect the face with the edge
					if(face.IsIntersectedBy(end0, end1, out intersectionPosition))
					{
						Vertex vertexCreatedDuringSplit;
						MeshEdge meshEdgeCreatedDuringSplit;
						// split the ray at intersectionPosition
						Mesh.SplitMeshEdge(meshEdge, out vertexCreatedDuringSplit, out meshEdgeCreatedDuringSplit);
						// split the face at intersectionPosition
						SplitFaceAtPosition(face, intersectionPosition);
					}
				}
			}
		}

		private void SplitFaceAtPosition(Face face, Vector3 intersectionPosition)
		{
			MeshEdge intersectedEdge;
			if(face.MeshEdgeIsIntersectedAt(intersectionPosition, out intersectedEdge))
			{
				// split the edge
			}
			else
			{
				//    ^           ^
				//   / \         /|\
				//  /   \   =   / . \
				// /_____\     /_/_\_\  // imagine the bottom lines are connected to the end points
				// remove the face
				// add the Vertex to the mesh
				// add the three new faces to the mesh
			}
		}

		private IEnumerable<Face> GetFacesTouching(AxisAlignedBoundingBox edgeBounds)
		{
			// TODO: make this only get the right faces
			foreach(var face in Mesh.Faces)
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