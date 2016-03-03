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
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.DataConverters3D
{
	public class Object3D : IObject3D
	{
		public string ActiveEditor { get; set; }
		public List<IObject3D> Children { get; set; } = new List<IObject3D>();
		public PlatingData ExtraData { get; } = new PlatingData();

		public MeshGroup Flatten()
		{
			return Flatten(this, new MeshGroup(), Matrix4X4.Identity);
		}

		private MeshGroup Flatten(IObject3D item, MeshGroup meshGroup, Matrix4X4 totalTransform, ReportProgressRatio progress = null)
		{
			totalTransform *= item.Matrix;

			if (item.Mesh  != null)
			{
				var mesh = Mesh.Copy(item.Mesh, progress);
				mesh.Transform(totalTransform);
				meshGroup.Meshes.Add(mesh);
			}

			foreach(IObject3D child in item.Children.Where(child => child.Visible))
			{
				Flatten(child, meshGroup, totalTransform, progress);
			}

			return meshGroup;
		}

		public RGBA_Bytes Color { get; set; }

		[JsonIgnore]
		public bool HasChildren => Children.Count > 0;

		public Object3DTypes ItemType { get; set; } = Object3DTypes.Model;

		public Matrix4X4 Matrix { get; set; } = Matrix4X4.Identity;

		[JsonIgnore]
		public Mesh Mesh { get; set; }

		public string MeshPath { get; set; }

		public string Name { get; set; }

		public bool PersistNode { get; set; } = true;

		public bool Visible { get; set; } = true;

		public static IObject3D Load(string meshPath, Dictionary<string, IObject3D> itemCache = null, ReportProgressRatio progress = null)
		{
			if(string.IsNullOrEmpty(meshPath) || !File.Exists(meshPath))
			{
				return null;
			}

			if(itemCache == null)
			{
				itemCache = new Dictionary<string, IObject3D>();
			}

			IObject3D loadedItem;

			// Try to pull the item from cache
			if (itemCache == null || !itemCache.TryGetValue(meshPath, out loadedItem) || loadedItem == null)
			{
				// Otherwise, load it up
				bool isMcxFile = Path.GetExtension(meshPath) == ".mcx";
				if (isMcxFile)
				{
					// Load the meta file and convert MeshPath links into objects
					loadedItem = JsonConvert.DeserializeObject<Object3D>(File.ReadAllText(meshPath));
					loadedItem.LoadMeshLinks(itemCache, progress);
				}
				else
				{
					loadedItem = MeshFileIo.Load(meshPath, progress);
				}

				if (itemCache != null && !isMcxFile)
				{
					itemCache[meshPath] = loadedItem;
				}
			}
			else
			{
				// TODO: Clone might be unnecessary... What about just invalidating the TraceData!!!!!
				loadedItem = loadedItem?.Clone();
			}

			return loadedItem;
		}

		// TODO - first attempt at deep clone
		public IObject3D Clone()
		{
			return new Object3D()
			{
				ItemType = this.ItemType,
				Mesh = this.Mesh,
				Color = this.Color,
				ActiveEditor = this.ActiveEditor,
				MeshPath = this.MeshPath,
				Children = new List<IObject3D>(this.Children.Select(child => child.Clone())),
				Matrix = this.Matrix,
				traceData = this.traceData
			};
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			var totalTransorm = this.Matrix * matrix;

			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			bool meshIsEmpty = this.Mesh == null;
			AxisAlignedBoundingBox totalBounds = meshIsEmpty ? AxisAlignedBoundingBox.Empty : this.Mesh.GetAxisAlignedBoundingBox(totalTransorm);

			// Add the bounds of each child object
			foreach (IObject3D child in Children)
			{
				totalBounds += child.GetAxisAlignedBoundingBox(totalTransorm);
			}

			return totalBounds;
		}

		private IPrimitive traceData;

		// Cache busting on child nodes
		private int tracedChildren = int.MinValue;

		public IPrimitive TraceData()
		{
			// Cache busting on child nodes
			int hashCode = GetChildrenHashCode();

			if (traceData == null || tracedChildren != hashCode)
			{
				// Get the trace data for the local mesh
				List<IPrimitive> traceables = (Mesh == null) ? new List<IPrimitive>() : new List<IPrimitive> { Mesh.CreateTraceData() };

				// Get the trace data for all children
				foreach (Object3D child in Children)
				{
					traceables.Add(child.TraceData());
				}

				// Wrap with a BVH
				traceData = BoundingVolumeHierarchy.CreateNewHierachy(traceables, 0);

				tracedChildren = hashCode;
			}

			// Wrap with the local transform
			return new Transform(traceData, Matrix);
		}

		public IEnumerable<MeshAndTransform> VisibleMeshes(Matrix4X4 transform)
		{
			Matrix4X4 totalTransform = this.Matrix * transform;

			foreach (var child in Children)
			{
				foreach (var meshTransform in child.VisibleMeshes(totalTransform))
				{
					yield return meshTransform;
				}
			}

			if (this.Mesh != null)
			{
				yield return new MeshAndTransform(this.Mesh, totalTransform);
			}
		}

		// Hashcode for lists as proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		public int GetChildrenHashCode()
		{
			unchecked
			{
				int hash = 19;

				//if (Mesh != null)
				//{
				//	hash = hash * 31 + Mesh.GetHashCode();
				//}

				foreach (var child in Children)
				{
					hash = hash * 31 + child.GetHashCode();
					hash = hash * 31 + child.Matrix.GetHashCode();
				}

				return hash;
			}
		}
	}
}