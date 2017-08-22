/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.DataConverters3D
{
	public class Object3D : IObject3D
	{
		public static string AssetsPath { get; set; }

		public virtual string ActiveEditor { get; set; }
		public List<IObject3D> Children { get; set; } = new List<IObject3D>();

		public MeshGroup Flatten(Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = null)
		{
			return Flatten(this, new MeshGroup(), Matrix4X4.Identity, meshPrintOutputSettings, this.MaterialIndex, this.OutputType);
		}

		private static MeshGroup Flatten(IObject3D item, MeshGroup meshGroup, Matrix4X4 totalTransform, 
			Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings, 
			int overrideMaterialIndex, PrintOutputTypes printOutputType)
		{
			totalTransform = item.Matrix * totalTransform;

			// if the override is set to a value other than -1 than we need to set every child mesh on down to this extruder / setting
			if (overrideMaterialIndex == -1 
				&& item.MaterialIndex != -1)
			{
				overrideMaterialIndex = item.MaterialIndex;
			}

			if(printOutputType == PrintOutputTypes.Default
				&& item.OutputType != PrintOutputTypes.Default)
			{
				printOutputType = item.OutputType;
			}

			if (item.Mesh != null)
			{
				var mesh = Mesh.Copy(item.Mesh, CancellationToken.None);
				mesh.Transform(totalTransform);
				meshGroup.Meshes.Add(mesh);
				if (meshPrintOutputSettings != null)
				{
					if (!meshPrintOutputSettings.ContainsKey(mesh))
					{
						meshPrintOutputSettings.Add(mesh, new MeshPrintOutputSettings());
					}
					var material = meshPrintOutputSettings[mesh];

					// If we are not setting an extruder we are on the first extruder
					material.ExtruderIndex = overrideMaterialIndex == -1 ? 0 : overrideMaterialIndex;
					material.PrintOutputTypes = printOutputType == PrintOutputTypes.Default ? PrintOutputTypes.Solid : printOutputType;
				}
			}
			else // we only add child meshes if we did not find a mesh at this level
			{
				foreach (IObject3D child in item.Children.Where(child => child.Visible))
				{
					Flatten(child, meshGroup, totalTransform, meshPrintOutputSettings, overrideMaterialIndex, printOutputType);
				}
			}

			return meshGroup;
		}

		public RGBA_Bytes Color { get; set; } = RGBA_Bytes.Transparent;

		public int MaterialIndex { get; set; } = -1;

		[JsonIgnore]
		public bool HasChildren => Children.Count > 0;

		public Object3DTypes ItemType { get; set; } = Object3DTypes.Model;

		public PrintOutputTypes OutputType { get; set; } = PrintOutputTypes.Default;

		public Matrix4X4 Matrix { get; set; } = Matrix4X4.Identity;

		[JsonIgnore]
		public Mesh Mesh { get; set; }

		public void SetAndInvalidateMesh(Mesh mesh)
		{
			this.Mesh = mesh;
			this.MeshPath = null;
		}

		public string MeshPath { get; set; }

		public string Name { get; set; }

		public virtual bool Persistable { get; set; } = true;

		public virtual bool Visible { get; set; } = true;

		public static IObject3D Load(string meshPath, CancellationToken cancellationToken, Dictionary<string, IObject3D> itemCache = null, Action<double, string> progress = null)
		{
			if (string.IsNullOrEmpty(meshPath) || !File.Exists(meshPath))
			{
				return null;
			}

			if (itemCache == null)
			{
				itemCache = new Dictionary<string, IObject3D>();
			}

			IObject3D loadedItem;

			// Try to pull the item from cache
			if (itemCache == null || !itemCache.TryGetValue(meshPath, out loadedItem) || loadedItem == null)
			{
				using (var stream = File.OpenRead(meshPath))
				{
					string extension = Path.GetExtension(meshPath).ToLower();
					
					loadedItem = Load(stream, extension, cancellationToken, itemCache, progress);

					// Cache loaded assets
					if (itemCache != null 
						&& extension != ".mcx"
						&& loadedItem != null)
					{
						itemCache[meshPath] = loadedItem;
					}
				}
			}
			else
			{
				// TODO: Clone seems unnecessary... Review driving requirements
				loadedItem = loadedItem?.Clone();
			}

			return loadedItem;
		}

		public static IObject3D Load(Stream stream, string extension, CancellationToken cancellationToken, Dictionary<string, IObject3D> itemCache = null, Action<double, string> progress = null)
		{
			IObject3D loadedItem = null;

			bool isMcxFile = extension.ToLower() == ".mcx";
			if (isMcxFile)
			{
				string json = new StreamReader(stream).ReadToEnd();

				// Load the meta file and convert MeshPath links into objects
				loadedItem = JsonConvert.DeserializeObject<Object3D>(json);
				loadedItem.LoadMeshLinks(cancellationToken, itemCache, progress);
			}
			else
			{
				loadedItem = MeshFileIo.Load(stream, extension, cancellationToken, progress);
			}

			// TODO: Stream loaded content isn't cached
			// TODO: Consider Mesh cache by SHA rather than file path, doing so would allow caching stream loaded content and would simply need SHA serialized at Scene persist
			/*
			if (itemCache != null && !isMcxFile)
			{
				itemCache[meshPath] = loadedItem;
			} */

			return loadedItem;
		}

		// TODO - first attempt at deep clone
		public IObject3D Clone()
		{
			// TODO: This technique loses concrete types, seems invalid
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

			if(totalBounds.minXYZ.x == double.PositiveInfinity)
			{
				return AxisAlignedBoundingBox.Zero;
			}

			return totalBounds;
		}

		private IPrimitive traceData;

		// Cache busting on child nodes
		private long tracedHashCode = long.MinValue;

		public IPrimitive TraceData()
		{
			// Cache busting on child nodes
			long hashCode = GetLongHashCode();

			if (traceData == null || tracedHashCode != hashCode)
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

				tracedHashCode = hashCode;
			}

			// Wrap with the local transform
			return new Transform(traceData, Matrix);
		}

		public IEnumerable<MeshRenderData> VisibleMeshes(Matrix4X4 transform, RGBA_Bytes color = default(RGBA_Bytes), int materialIndex = -1, PrintOutputTypes outputType = PrintOutputTypes.Default)
		{
			// If there is no color set yet and the object 3D is specifying a color
			if (color.Alpha0To255 == 0
				&& this.Color.Alpha0To255 != 0)
			{
				// use this as the color for all recursize children
				color = this.Color;
			}

			// If there is no material set yet and the object 3D is specifying a material
			if (materialIndex == -1
				&& this.MaterialIndex != -1)
			{
				// use this as the color for all recursize children
				materialIndex = this.MaterialIndex;
			}

			if(outputType == PrintOutputTypes.Default
				&& this.OutputType != PrintOutputTypes.Default)
			{
				outputType = this.OutputType;
			}

			Matrix4X4 totalTransform = this.Matrix * transform;

			if (this.Mesh == null)
			{
				foreach (var child in Children)
				{
					if (this.ItemType != Object3DTypes.Group || child.OutputType != PrintOutputTypes.Hole)
					{
						foreach (var meshTransform in child.VisibleMeshes(totalTransform, color, materialIndex))
						{
							yield return meshTransform;
						}
					}
				}
			}

			if (this.Mesh != null)
			{
				if (color.Alpha0To255 > 0)
				{
					yield return new MeshRenderData(this.Mesh, totalTransform, color, materialIndex, outputType);
				}
				else
				{
					yield return new MeshRenderData(this.Mesh, totalTransform, RGBA_Bytes.White, materialIndex, outputType);
				}
			}
		}

		// Hashcode for lists as proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		public long GetLongHashCode()
		{
			long hash = 19;

			hash = hash * 31 + Matrix.GetLongHashCode();

			foreach (var child in Children)
			{
				hash = hash * 31 + child.GetLongHashCode();
			}

			return hash;
		}

		public string ComputeSha1()
		{
			return ComputeSha1(this.ToJson());
		}

		private string ComputeSha1(string json)
		{
			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
			{
				return GenerateSha1(memoryStream);
			}
		}

		private string GenerateSha1(Stream stream)
		{
			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(stream);
				string SHA1 = BitConverter.ToString(hash).Replace("-", String.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(
						this,
						Formatting.Indented,
						new JsonSerializerSettings
						{
							ContractResolver = new IObject3DContractResolver(),
							NullValueHandling = NullValueHandling.Ignore
						});
		}
	}
}