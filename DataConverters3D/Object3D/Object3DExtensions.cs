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
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.PolygonMesh
{
	public static class Object3DExtensions
	{
		public static bool LoadMeshLinks(this IObject3D tempScene, Dictionary<string, List<MeshGroup>> cachedMeshes, ReportProgressRatio progress)
		{
			var itemsToLoad = (from object3D in tempScene.Descendants()
							   where !string.IsNullOrEmpty(object3D.MeshPath) &&
									 File.Exists(object3D.MeshPath)
							   select object3D).ToList();

			int itemCount = itemsToLoad.Count;

			List<MeshGroup> loadedMeshGroups;

			foreach (IObject3D object3D in itemsToLoad)
			{
				// TODO: Make cacheKey based on a SHA1 hash of the mesh data so that duplicate content, remote or local, can pull from the same file. This
				// is especially true for dynamic content in the scene that comes from generators or is duplicated. If that data is hashed, we'll save only
				// a single mesh file rather than n number of duplicates
				//
				// Pull from cache or load
				if (!cachedMeshes.TryGetValue(object3D.MeshPath, out loadedMeshGroups))
				{
					// TODO: calc the actual progress
					loadedMeshGroups = MeshFileIo.Load(object3D.MeshPath, progress);
					cachedMeshes[object3D.MeshPath] = loadedMeshGroups;
				}

				// During startup we reload the main control multiple times. When this occurs, sometimes the reportProgress0to100 will set
				// continueProcessing to false and MeshFileIo.LoadAsync will return null. In those cases, we need to exit rather than process the loaded MeshGroup
				if (loadedMeshGroups == null)
				{
					return false;
				}
				else if (loadedMeshGroups == null)
				{
					// TODO: Someday handle load errors by placing something in the scene that notes the lack of a source file and guides the user to fix
					// Load error for file, skip
					continue;
				}

				if (loadedMeshGroups.Count == 1)
				{
					object3D.Mesh = loadedMeshGroups?.First().Meshes?.First();
					object3D.ItemType = Object3DTypes.Model;
				}
				else
				{
					foreach (var meshGroup in loadedMeshGroups)
					{
						foreach (var mesh in meshGroup.Meshes)
						{
							object3D.Children.Add(new Object3D()
							{
								ItemType = Object3DTypes.Model,
								Mesh = mesh,
								PersistNode = false
							});
						}
					}
				}
			}

			return true;
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