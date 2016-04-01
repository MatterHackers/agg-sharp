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
	public class Object3D : IObject3D
	{
		public List<IObject3D> Children { get; set; } = new List<IObject3D>();
		public PlatingData ExtraData { get; } = new PlatingData();

		public bool HasChildren => Children.Count > 0;

		public Object3DTypes ItemType { get; set; } = Object3DTypes.Model;

		public Matrix4X4 Matrix { get; set; } = Matrix4X4.Identity;

		[JsonIgnore]
		public MeshGroup MeshGroup { get; set; }

		public string MeshPath { get; set; }

		public bool PersistNode { get; set; } = true;

		[JsonIgnore]
		public object SourceNode { get; set; }

		public bool Visible { get; set; }
		
		public static IObject3D Load(string meshPathAndFileName, Dictionary<string, List<MeshGroup>> cachedMeshes, ReportProgressRatio progress)
		{
			string extension = Path.GetExtension(meshPathAndFileName);

			if (extension == ".mcx")
			{
				// Load the meta file
				IObject3D loadedItem = JsonConvert.DeserializeObject<Object3D>(File.ReadAllText(meshPathAndFileName));

				// Load all mesh links in the file definition
				loadedItem.LoadMeshLinks(cachedMeshes, progress);

				return loadedItem;
			}
			else
			{
				List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(meshPathAndFileName, progress);

				// During startup we load and reload the main control multiple times. When this occurs, sometimes the reportProgress0to100 will set
				// continueProcessing to false and MeshFileIo.LoadAsync will return null. In those cases, we need to exit rather than process the loaded MeshGroup
				if (loadedMeshGroups == null)
				{
					return null;
				}

				IObject3D loadedItem = new Object3D()
				{
					MeshGroup = new MeshGroup(),
					ItemType = Object3DTypes.Group
				};

				foreach (var meshGroup in loadedMeshGroups)
				{
					loadedItem.Children.Add(new Object3D() { MeshGroup = meshGroup });
				}

				return loadedItem;
			}
		}

		// TODO - first attempt at deep clone
		public IObject3D Clone()
		{
			return new Object3D()
			{
				ItemType = this.ItemType,
				MeshGroup = this.MeshGroup,
				Children = new List<IObject3D>(this.Children.Select(child => child.Clone())),
				Matrix = this.Matrix,
				traceData = this.traceData
			};
		}

		public double DistanceToHit(Ray ray, ref IntersectInfo info)
		{
			IntersectInfo infoMesh = TraceData().GetClosestIntersection(ray);
			if (infoMesh != null)
			{
				info = infoMesh;
				return info.distanceToHit;
			}

			return double.PositiveInfinity;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			bool meshIsEmpty = this.MeshGroup == null || this.MeshGroup.Meshes.Count == 0;
			AxisAlignedBoundingBox totalBounds = meshIsEmpty ? AxisAlignedBoundingBox.Empty : this.MeshGroup.GetAxisAlignedBoundingBox(this.Matrix);

			// Add the bounds of each child object
			foreach (IObject3D child in Children)
			{
				totalBounds += child.GetAxisAlignedBoundingBox(this.Matrix);
			}

			return totalBounds;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			var totalTransorm = this.Matrix * matrix;

			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			bool meshIsEmpty = this.MeshGroup == null || this.MeshGroup.Meshes.Count == 0;
			AxisAlignedBoundingBox totalBounds = meshIsEmpty ? AxisAlignedBoundingBox.Empty : this.MeshGroup.GetAxisAlignedBoundingBox(totalTransorm);

			// Add the bounds of each child object
			foreach (IObject3D child in Children)
			{
				totalBounds += child.GetAxisAlignedBoundingBox(totalTransorm);
			}

			return totalBounds;
		}

		private IPrimitive traceData;

		public IPrimitive TraceData()
		{
			if (traceData == null)
			{
				// Get the trace data for the local mesh
				List<IPrimitive> traceables = (MeshGroup == null) ? new List<IPrimitive>() : MeshGroup.Meshes.Select(mesh => mesh.CreateTraceData()).ToList();

				// Get the trace data for all children
				foreach (Object3D child in Children)
				{
					traceables.Add(child.TraceData());
				}

				// Wrap with a BVH
				traceData = BoundingVolumeHierarchy.CreateNewHierachy(traceables, 0);
			}

			// Wrap with the local transform
			return new Transform(traceData, Matrix);
		}
	}
}