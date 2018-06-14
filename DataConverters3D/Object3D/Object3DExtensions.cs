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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	public static class Object3DExtensions
	{
		internal static void LoadMeshLinks(this IObject3D tempScene, CancellationToken cancellationToken, Dictionary<string, IObject3D> itemCache, Action<double, string> progress)
		{
			var itemsToLoad = (from object3D in tempScene.DescendantsAndSelf()
							   where !string.IsNullOrEmpty(object3D.MeshPath)
							   select object3D).ToList();

			foreach (IObject3D object3D in itemsToLoad)
			{
				object3D.LoadLinkedMesh(itemCache, cancellationToken, progress);
			}
		}

		public static int Depth(this IObject3D item)
		{
			return item.Parents<IObject3D>().Count();
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void DebugDepth(this IObject3D item, string extra = "")
		{
			Debug.WriteLine(new String(' ', item.Depth()) + $"({item.Depth()}) {item.GetType().Name} " + extra);
		}

		private static void LoadLinkedMesh(this IObject3D item, Dictionary<string, IObject3D> itemCache, CancellationToken cancellationToken, Action<double, string> progress)
		{
			// Natural path
			string filePath = item.MeshPath;

			// If relative/asset file name
			if (Path.GetDirectoryName(filePath) == "")
			{
				string sha1PlusExtension = filePath;

				filePath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

				// If the asset is not in the local cache folder, acquire it
				if (!File.Exists(filePath))
				{
					// *************************************************************************
					// TODO: Fix invalid use of Wait()
					// *************************************************************************
					// Prime cache
					AssetObject3D.AssetManager.AcquireAsset(sha1PlusExtension, cancellationToken, progress).Wait();
				}
			}

			var loadedItem = Object3D.Load(filePath, cancellationToken, itemCache, progress);

			// TODO: Consider refactoring progress reporting to use an instance with state and the original delegate reference to allow anyone along the chain
			// to determine if continueProcessing has been set to false and allow for more clear aborting (rather than checking for null as we have to do below)
			//
			// During startup we reload the main control multiple times. When the timing is right, reportProgress0to100 may set continueProcessing
			// on the reporter to false and MeshFileIo.Load will return null. In those cases, we need to exit rather than continue processing
			if (loadedItem != null)
			{
				item.SetMeshDirect(loadedItem.Mesh);

				// TODO: When loading mesh links, if a node has children and a mesh (MeshWrapers for example)
				// then we load the mesh and blow away the children in the assignment below. The new conditional
				// assignment accounts for that case but may need more consideration
				if (string.Equals(Path.GetExtension(filePath), ".mcx", StringComparison.OrdinalIgnoreCase))
				{
					item.Children = loadedItem.Children;
				}
			}
		}

		public static void SaveTo(this IObject3D sourceItem, Stream outputStream, Action<double, string> progress = null)
		{
			sourceItem.PersistAssets(progress);

			var streamWriter = new StreamWriter(outputStream);
			streamWriter.Write(sourceItem.ToJson());
			streamWriter.Flush();
		}

		public static void Fit(this WorldView world, IObject3D itemToRender, RectangleDouble goalBounds)
		{
			world.Fit(itemToRender, goalBounds, Matrix4X4.Identity);
		}

		public static void Fit(this WorldView world, IObject3D itemToRender, RectangleDouble goalBounds, Matrix4X4 offset)
		{
			AxisAlignedBoundingBox meshBounds = itemToRender.GetAxisAlignedBoundingBox(offset);

			bool done = false;
			double scaleFraction = .1;
			// make the target size a portion of the total size
			goalBounds.Inflate(-goalBounds.Width * .1);

			int rescaleAttempts = 0;
			while (!done && rescaleAttempts++ < 500)
			{
				RectangleDouble partScreenBounds = GetScreenBounds(meshBounds, world);

				if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
				{
					world.Scale *= (1 + scaleFraction);
					partScreenBounds = GetScreenBounds(meshBounds, world);

					// If it crossed over the goal reduct the amount we are adjusting by.
					if (NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						scaleFraction /= 2;
					}
				}
				else
				{
					world.Scale *= (1 - scaleFraction);
					partScreenBounds = GetScreenBounds(meshBounds, world);

					// If it crossed over the goal reduct the amount we are adjusting by.
					if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
					{
						scaleFraction /= 2;
						if (scaleFraction < .001)
						{
							done = true;
						}
					}
				}
			}
		}

		private static bool NeedsToBeSmaller(RectangleDouble partScreenBounds, RectangleDouble goalBounds)
		{
			if (partScreenBounds.Bottom < goalBounds.Bottom
				|| partScreenBounds.Top > goalBounds.Top
				|| partScreenBounds.Left < goalBounds.Left
				|| partScreenBounds.Right > goalBounds.Right)
			{
				return true;
			}

			return false;
		}

		public static RectangleDouble GetScreenBounds(AxisAlignedBoundingBox meshBounds, WorldView world)
		{
			RectangleDouble screenBounds = RectangleDouble.ZeroIntersection;

			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.minXYZ.X, meshBounds.minXYZ.Y, meshBounds.minXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.maxXYZ.X, meshBounds.minXYZ.Y, meshBounds.minXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.maxXYZ.X, meshBounds.maxXYZ.Y, meshBounds.minXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.minXYZ.X, meshBounds.maxXYZ.Y, meshBounds.minXYZ.Z)));

			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.minXYZ.X, meshBounds.minXYZ.Y, meshBounds.maxXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.maxXYZ.X, meshBounds.minXYZ.Y, meshBounds.maxXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.maxXYZ.X, meshBounds.maxXYZ.Y, meshBounds.maxXYZ.Z)));
			screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.minXYZ.X, meshBounds.maxXYZ.Y, meshBounds.maxXYZ.Z)));
			return screenBounds;
		}

		public static async void PersistAssets(this IObject3D sourceItem, Action<double, string> progress = null, bool publishAssets=false)
		{
			// Must use DescendantsAndSelf so that leaf nodes save their meshes
			var persistableItems = from object3D in sourceItem.DescendantsAndSelf()
										 where object3D.WorldPersistable() &&
												(((object3D.MeshPath == null || publishAssets) && object3D.Mesh != null)
												|| (object3D is IAssetObject && publishAssets))
										 select object3D;

			Directory.CreateDirectory(Object3D.AssetsPath);

			var assetFiles = new Dictionary<int, string>();

			try
			{
				// Write unsaved content to disk
				foreach (IObject3D item in persistableItems)
				{
					// If publishAssets is specified, persist any unsaved IAssetObject items to disk
					if (item is IAssetObject assetObject && publishAssets)
					{
						await AssetObject3D.AssetManager.StoreAsset(assetObject, publishAssets, CancellationToken.None, progress);

						if (string.IsNullOrWhiteSpace(item.MeshPath))
						{
							continue;
						}
					}

					// Calculate the fast mesh hash
					int hashCode = (int)item.Mesh.GetLongHashCode();

					// Index into dictionary using fast hash
					if (!assetFiles.TryGetValue(hashCode, out string assetPath))
					{
						// Store and update cache if missing
						await AssetObject3D.AssetManager.StoreMesh(item, publishAssets, CancellationToken.None, progress);
						assetFiles.Add(hashCode, item.MeshPath);
					}
					else
					{
						// If the Mesh is in the assetFile cache, set .MeshPath to the existing asset name
						item.MeshPath = assetPath;
					}
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		public static AxisAlignedBoundingBox GetUnionedAxisAlignedBoundingBox(this IEnumerable<IObject3D> items)
		{
			// first find the bounds of what is already here.
			AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty;
			foreach (var object3D in items)
			{
				totalBounds = AxisAlignedBoundingBox.Union(totalBounds, object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
			}

			return totalBounds;
		}

		public static Matrix4X4 WorldMatrix(this IObject3D child, IObject3D rootOverride = null)
		{
			var matrix = Matrix4X4.Identity;
			foreach (var item in child.AncestorsAndSelf())
			{
				matrix *= item.Matrix;
				if (item == rootOverride)
				{
					break;
				}
			}

			return matrix;
		}

		public static IEnumerable<IObject3D> Ancestors(this IObject3D child)
		{
			var parent = child.Parent;
			while (parent != null)
			{
				yield return parent;
				parent = parent.Parent;
			}
		}

		public static IEnumerable<IObject3D> AncestorsAndSelf(this IObject3D child)
		{
			yield return child;
			var parent = child.Parent;
			while (parent != null)
			{
				yield return parent;
				parent = parent.Parent;
			}
		}

		public static Color WorldColor(this IObject3D child, IObject3D rootOverride = null)
		{
			var lastColorFound = Color.White;
			foreach(var item in child.AncestorsAndSelf())
			{
				// if we find a color it overrides our current color so set it
				if (item.Color.Alpha0To255 != 0)
				{
					lastColorFound = item.Color;
				}

				// If the root override has been matched, break and return latest
				if (item == rootOverride)
				{
					break;
				}
			}

			return lastColorFound;
		}

		public static bool WorldPersistable(this IObject3D child, IObject3D rootOverride = null)
		{
			foreach (var item in child.AncestorsAndSelf())
			{
				// if we find a color it overrides our current color so set it
				if (!item.Persistable)
				{
					return false;
				}

				// If the root override has been matched, break and return latest
				if (item == rootOverride)
				{
					break;
				}
			}

			return true;
		}

		public static PrintOutputTypes WorldOutputType(this IObject3D child, IObject3D rootOverride = null)
		{
			var lastOutputTypeFound = PrintOutputTypes.Default;
			foreach (var item in child.AncestorsAndSelf())
			{
				if (item.OutputType != PrintOutputTypes.Default)
				{
					// use collection as the color for all recursive children
					lastOutputTypeFound = item.OutputType;
				}

				// If the root override has been matched, break and return latest
				if (item == rootOverride)
				{
					break;
				}
			}

			return lastOutputTypeFound;
		}

		public static int WorldMaterialIndex(this IObject3D child, IObject3D rootOverride = null)
		{
			var lastMaterialIndexFound = -1;
			foreach (var item in child.AncestorsAndSelf())
			{
				if (item.MaterialIndex != -1)
				{
					// use collection as the color for all recursive children
					lastMaterialIndexFound = item.MaterialIndex;
				}

				// If the root override has been matched, break and return latest
				if (item == rootOverride)
				{
					break;
				}
			}

			// If we don't find a color (-1) return 0
			return lastMaterialIndexFound;
		}

		public static void SuspendAll(this IObject3D parent)
		{
			foreach (var item in parent.DescendantsAndSelf())
			{
				item.SuspendRebuild();
			}
		}

		public static void ResumeAll(this IObject3D parent)
		{
			foreach (var item in parent.DescendantsAndSelf())
			{
				item.ResumeRebuild();
			}
		}

		public static IEnumerable<IObject3D> DescendantsAndSelf(this IObject3D root)
		{
			var items = new Stack<IObject3D>(new[] { root });
			while (items.Count > 0)
			{
				IObject3D item = items.Pop();

				yield return item;
				foreach (var n in item.Children)
				{
					items.Push(n);
				}
			}
		}

		/// <summary>
		/// Returns all ancestors of the current IObject3D matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="item">The context item</param>
		/// <returns>The matching ancestor item</returns>
		public static IEnumerable<T> Parents<T>(this IObject3D item) where T : IObject3D
		{
			IObject3D context = item.Parent;
			while (context != null)
			{
				if (context is T)
				{
					yield return (T)context;
				}

				context = context.Parent;
			}
		}

		public static IEnumerable<IObject3D> Descendants(this IObject3D root)
		{
			return (IEnumerable<IObject3D>)Descendants<IObject3D>(root);
		}

		public static IEnumerable<T> Descendants<T>(this IObject3D root) where T : IObject3D
		{
			var items = new Stack<IObject3D>();

			foreach (var n in root.Children)
			{
				items.Push(n);
			}

			while (items.Count > 0)
			{
				IObject3D item = items.Pop();

				if (item is T asType)
				{
					yield return asType;
				}
				foreach (var n in item.Children)
				{
					items.Push(n);
				}
			}
		}

		public static void Rotate(this IObject3D item, Vector3 origin, Vector3 axis, double angle)
		{
			// move object relative to rotation
			item.Matrix *= Matrix4X4.CreateTranslation(-origin);
			// rotate it
			item.Matrix *= Matrix4X4.CreateRotation(axis, angle);
			// move it back
			item.Matrix *= Matrix4X4.CreateTranslation(origin);
		}

		public static IPrimitive CreateTraceData(this Mesh mesh, int maxRecursion = int.MaxValue)
		{
			List<IPrimitive> allPolys = new List<IPrimitive>();
			List<Vector3> positions = new List<Vector3>();
			List<Vector2> uvs = new List<Vector2>();

			foreach (Face face in mesh.Faces)
			{
				positions.Clear();
				bool hasTexture = false;

				foreach (FaceEdge faceEdge in face.FaceEdges())
				{
					if (mesh.TextureUV.ContainsKey((faceEdge, 0)))
					{
						uvs.Add(faceEdge.GetUv(0));
						hasTexture = true;
					}
					positions.Add(faceEdge.FirstVertex.Position);
				}

				// We should use the tessellator for this if it is greater than 3.
				Vector3 next = positions[1];
				Vector2 nextuv = hasTexture ? uvs[1] : Vector2.Zero;
				for (int positionIndex = 2; positionIndex < positions.Count; positionIndex++)
				{
					TriangleShape triangel;
					if (hasTexture)
					{
						triangel = new TriangleShapeUv(positions[0], next, positions[positionIndex],
							uvs[0], nextuv, uvs[positionIndex], null);
					}
					else
					{
						triangel = new TriangleShape(positions[0], next, positions[positionIndex], null);
					}
					allPolys.Add(triangel);
					next = positions[positionIndex];
				}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, maxRecursion);
		}

		/*
		public class SelectionChangeCommand : IUndoRedoCommand
		{
			public Color Color{ get; set; }
			public int MaterialIndex { get; set; }
			PrintOutputTypes PrintOutputTypes { get; set; }
			Matrix4X4 Matrix { get; set; }

			public void Do()
			{

			}

			public void Undo()
			{
				throw new NotImplementedException();
			}
		}*/

		/// <summary>
		/// Collapses the source object into the target list (often but not necessarily the scene)
		/// </summary>
		/// <param name="objectToCollapse">The object to collapse</param>
		/// <param name="collapseInto">The target to collapse into</param>
		/// <param name="typeFilter">Type filter</param>
		/// <param name="depth">?</param>
		public static void CollapseInto(this IObject3D objectToCollapse, List<IObject3D> collapseInto, bool filterToSelectionGroup = true, int depth = int.MaxValue)
		{
			if (objectToCollapse != null
				&& objectToCollapse is SelectionGroup == filterToSelectionGroup)
			{
				// Remove the collapsing item from the list
				collapseInto.Remove(objectToCollapse);

				// Move each child from objectToCollapse into the target (often the scene), applying the parent transform to each
				foreach (var child in objectToCollapse.Children)
				{
					if (objectToCollapse.Color != Color.Transparent)
					{
						child.Color = objectToCollapse.Color;
					}

					if (objectToCollapse.MaterialIndex != -1)
					{
						child.MaterialIndex = objectToCollapse.MaterialIndex;
					}

					if (objectToCollapse.OutputType != PrintOutputTypes.Default)
					{
						child.OutputType = objectToCollapse.OutputType;
					}

					child.Matrix *= objectToCollapse.Matrix;

					if (child is SelectionGroup && depth > 0)
					{
						child.CollapseInto(collapseInto, filterToSelectionGroup, depth - 1);
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