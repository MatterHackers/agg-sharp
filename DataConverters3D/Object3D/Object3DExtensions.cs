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
					object3D.MeshGroup = loadedMeshGroups.First();
					object3D.ItemType = Object3DTypes.Model;
				}
				else
				{
					foreach (var meshGroup in loadedMeshGroups)
					{
						object3D.Children.Add(new Object3D()
						{
							ItemType = Object3DTypes.Model,
							MeshGroup = meshGroup,
							PersistNode = false
						});
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
				totalBounds = AxisAlignedBoundingBox.Union(totalBounds, object3D.GetAxisAlignedBoundingBox());
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
	}
}