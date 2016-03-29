/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.PolygonMesh
{
	public enum Object3DTypes { Model, Group, SelectionGroup, GenericObject };

	public interface IObject3D
	{
		List<IObject3D> Children { get; set; }
		PlatingData ExtraData { get; }
		bool HasChildren { get; }
		Object3DTypes ItemType { get; set; }
		Matrix4X4 Matrix { get; set; }
		MeshGroup MeshGroup { get; set; }
		object SourceNode { get; set; } // HACK: For time's sake, stuffing this in for easy persistence
		bool Visible { get; set; }

		IObject3D Clone();

		void CreateTraceables();

		double DistanceToHit(Ray ray, ref IntersectInfo info);

		AxisAlignedBoundingBox GetAxisAlignedBoundingBox();

		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 offet);
	}

	public static class Object3DExtensions
	{
		public static AxisAlignedBoundingBox GetUnionedAxisAlignedBoundingBox(this List<IObject3D> items)
		{
			// first find the bounds of what is already here.
			AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty;
			foreach (var object3D in items)
			{
				totalBounds = AxisAlignedBoundingBox.Union(totalBounds, object3D.GetAxisAlignedBoundingBox());
			}

			return totalBounds;
		}
	}

	public class Object3D : IObject3D
	{
		public List<IObject3D> Children { get; set; } = new List<IObject3D>();
		public PlatingData ExtraData { get; } = new PlatingData();
		public bool HasChildren => Children.Count > 0;

		public Object3DTypes ItemType { get; set; } = Object3DTypes.Model;

		public Matrix4X4 Matrix { get; set; } = Matrix4X4.Identity;
		public MeshGroup MeshGroup { get; set; }
		public object SourceNode { get; set; }

		public bool Visible { get; set; }

		public static IPrimitive CreateTraceDataForMesh(Mesh mesh)
		{
			List<IPrimitive> allPolys = AddTraceDataForMesh(mesh);

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys);
		}

		// TODO - first attempt at deep clone
		public IObject3D Clone()
		{
			return new Object3D()
			{
				ItemType = this.ItemType,
				MeshGroup = this.MeshGroup,
				Children = new List<IObject3D>(this.Children.Select(child => child.Clone())),
				Matrix = this.Matrix
			};
		}

		/// <summary>
		/// Initializes trace data, stored in a transformed BVH, for this items mesh and all its children.
		/// </summary>
		public void CreateTraceables()
		{
			this.ExtraData.MeshTraceables = new List<IPrimitive> { createTraceables() };
		}

		public double DistanceToHit(Ray ray, ref IntersectInfo info)
		{
			var meshTraceables = this.ExtraData.MeshTraceables;

			IntersectInfo infoMesh = createTraceables().GetClosestIntersection(ray);
			if (infoMesh != null)
			{
				info = infoMesh;
				return info.distanceToHit;
			}

			return double.PositiveInfinity;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			var totalTransorm = this.Matrix * matrix;

			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			bool meshIsEmpty = this.MeshGroup == null || this.MeshGroup.Meshes.Count == 0;
			AxisAlignedBoundingBox totalBounds = meshIsEmpty ? AxisAlignedBoundingBox.Empty : this.MeshGroup.GetAxisAlignedBoundingBox(totalTransorm);

			// Add the bounds of each child object
			foreach (IObject3D object3D in Children)
			{
				totalBounds += object3D.GetAxisAlignedBoundingBox(totalTransorm);
			}

			return totalBounds;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			bool meshIsEmpty = this.MeshGroup == null || this.MeshGroup.Meshes.Count == 0;
			AxisAlignedBoundingBox totalBounds = meshIsEmpty ? AxisAlignedBoundingBox.Empty : this.MeshGroup.GetAxisAlignedBoundingBox(this.Matrix);

			// Add the bounds of each child object
			foreach (IObject3D object3D in Children)
			{
				totalBounds += object3D.GetAxisAlignedBoundingBox(this.Matrix);
			}

			return totalBounds;
		}

		private static List<IPrimitive> AddTraceDataForMesh(Mesh mesh)
		{
			List<IPrimitive> allPolys = new List<IPrimitive>();
			List<Vector3> positions = new List<Vector3>();

			foreach (Face face in mesh.Faces)
			{
				positions.Clear();
				foreach (Vertex vertex in face.Vertices())
				{
					positions.Add(vertex.Position);
				}

				// We should use the tessellator for this if it is greater than 3.
				Vector3 next = positions[1];
				for (int positionIndex = 2; positionIndex < positions.Count; positionIndex++)
				{
					TriangleShape triangel = new TriangleShape(positions[0], next, positions[positionIndex], null);
					allPolys.Add(triangel);
					next = positions[positionIndex];
				}
			}

			return allPolys;
		}

		private IPrimitive createTraceables()
		{
			// Get the trace data for the local mesh
			List<IPrimitive> meshTraceables = (MeshGroup == null) ? new List<IPrimitive>() : MeshGroup.Meshes.SelectMany(meshGroup => AddTraceDataForMesh(meshGroup)).ToList();

			// Get the trace data for all children
			foreach (Object3D child in Children)
			{
				meshTraceables.Add(child.createTraceables());
			}

			// Wrap with transform and BVH
			return new Transform(BoundingVolumeHierarchy.CreateNewHierachy(meshTraceables, 0), Matrix);
		}
	}

	public class PlatingData
	{
		public Vector3 CurrentScale = new Vector3(1, 1, 1);
		public List<IPrimitive> MeshTraceables = new List<IPrimitive>();
		public Vector2 Spacing;
	}
}