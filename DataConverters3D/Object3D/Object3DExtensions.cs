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

using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.PolygonMesh
{
	public static class Object3DExtensions
	{
		internal static void LoadMeshLinks(this IObject3D tempScene, Dictionary<string, IObject3D> itemCache, ReportProgressRatio progress)
		{
			var itemsToLoad = (from object3D in tempScene.Descendants()
							   where !string.IsNullOrEmpty(object3D.MeshPath) &&
									 File.Exists(object3D.MeshPath)
							   select object3D).ToList();

			foreach (IObject3D object3D in itemsToLoad)
			{
				object3D.Load(itemCache, progress);
			}
		}

		public static List<MeshGroup> ToMeshGroupList(this IObject3D item) => new List<MeshGroup> { item.Flatten() };

		public static void Load(this IObject3D item, Dictionary<string, IObject3D> itemCache, ReportProgressRatio progress)
		{
			var loadedItem = Object3D.Load(item.MeshPath, itemCache, progress);

			// TODO: Consider refactoring progress reporting to use an instance with state and the original delegate reference to allow anyone along the chain
			// to determine if continueProcessing has been set to false and allow for more clear aborting (rather than checking for null as we have to do below) 
			//
			// During startup we reload the main control multiple times. When the timing is right, reportProgress0to100 may set continueProcessing 
			// on the reporter to false and MeshFileIo.Load will return null. In those cases, we need to exit rather than continue processing
			if (loadedItem != null)
			{
				item.Mesh = loadedItem.Mesh;
				item.ItemType = loadedItem.ItemType;
				item.Children = loadedItem.Children;
			}
		}

		public static AxisAlignedBoundingBox GetUnionedAxisAlignedBoundingBox(this List<IObject3D> items)
		{
			// first find the bounds of what is already here.
			AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty;
			foreach (var object3D in items)
			{
				totalBounds = AxisAlignedBoundingBox.Union(totalBounds, object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
			}

			return totalBounds;
		}

		public static IEnumerable<IObject3D> Descendants(this IObject3D root)
		{
			var nodes = new Stack<IObject3D>(new[] { root });
			while (nodes.Any())
			{
				IObject3D node = nodes.Pop();
				yield return node;
				foreach (var n in node.Children) nodes.Push(n);
			}
		}

		public static IPrimitive CreateTraceData(this Mesh mesh)
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

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, 0);
		}

		public static void CollapseInto(this IObject3D objectToCollapse, List<IObject3D> collapseInto, Object3DTypes typeFilter = Object3DTypes.SelectionGroup, int depth = int.MaxValue)
		{
			if (objectToCollapse != null && objectToCollapse.ItemType == typeFilter)
			{
				collapseInto.Remove(objectToCollapse);

				// Move each child from objectToRemove into the scene, applying the parent transform to each
				foreach (var child in objectToCollapse.Children)
				{
					child.Matrix *= objectToCollapse.Matrix;

					if (child.ItemType == Object3DTypes.SelectionGroup && depth > 0)
					{
						child.CollapseInto(collapseInto, typeFilter, depth - 1);
					}
					else
					{
						collapseInto.Add(child);
					}
				}
			}
		}
	}
}