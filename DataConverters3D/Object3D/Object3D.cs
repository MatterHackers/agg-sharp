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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.DataConverters3D
{
	public class Object3D : IObject3D
	{
		public event EventHandler<InvalidateArgs> Invalidated;

		public Object3D()
			: this (null)
		{
		}

		public Object3D(IEnumerable<IObject3D> children)
		{
			var type = this.GetType();
			if (type != typeof(Object3D)
				&& type.Name != "InteractiveScene")
			{
				this.TypeName = type.Name;
			}

			if (children != null)
			{
				Children = new SafeList<IObject3D>(children, this);
			}
			else
			{
				Children = new SafeList<IObject3D>(this);
			}
		}

		public static string AssetsPath { get; set; }

		public string ID { get; set; } = Guid.NewGuid().ToString();

		public string OwnerID { get; set; }

		public SafeList<IObject3D> Children { get; set; }

		public string TypeName { get; }

		public IObject3D Parent { get; set; }

		private Color _color = Color.Transparent;
		public Color Color
		{
			get { return _color; }
			set
			{
				if (_color != value)
				{
					_color = value;
					if (_color.alpha != 255)
					{
						EnsureTransparentSorting();
					}

					Invalidate(new InvalidateArgs(this, InvalidateType.Color, null));
				}
			}
		}

		public void EnsureTransparentSorting()
		{
			var localMesh = Mesh;
			if (localMesh != null
				&& localMesh.FaceBspTree == null
				&& localMesh.Faces.Count < 2000
				&& !buildingFaceBsp)
			{
				this.buildingFaceBsp = true;
				Task.Run(() =>
				{
					// TODO: make a SHA1 based cache for the sorting on this mesh and use them from memory or disk
					var bspTree = FaceBspTree.Create(localMesh);
					UiThread.RunOnIdle(() => localMesh.FaceBspTree = bspTree);
					this.buildingFaceBsp = false;
				});
			}
		}

		private int _materialIndex = -1;
		public int MaterialIndex
		{
			get
			{
				return _materialIndex;
			}
			set
			{
				if (value != _materialIndex)
				{
					_materialIndex = value;
					Invalidate(new InvalidateArgs(this, InvalidateType.Material, null));
				}
			}
		}

		private PrintOutputTypes _outputType = PrintOutputTypes.Default;
		public PrintOutputTypes OutputType
		{
			get
			{
				return _outputType;
			}
			set
			{
				if (_outputType != value)
				{
					// prevent recursion errors by holding a local pointer
					var localMesh = Mesh;
					_outputType = value;
					if (_outputType == PrintOutputTypes.Support
						&& localMesh != null
						&& localMesh.FaceBspTree == null
						&& localMesh.Faces.Count < 2000)
					{
						Task.Run(() =>
						{
							var bspTree = FaceBspTree.Create(localMesh);
							UiThread.RunOnIdle(() => localMesh.FaceBspTree = bspTree);
						});
					}
				}
			}
		}

		private Matrix4X4 _matrix = Matrix4X4.Identity;
		public Matrix4X4 Matrix
		{
			get => _matrix;
			set
			{
				if(value != _matrix)
				{
					_matrix = value;
					Invalidate(new InvalidateArgs(this, InvalidateType.Matrix, null));
				}
			}
		}

		private object locker = new object();

		[JsonIgnore]
		private Mesh _mesh;
		public virtual Mesh Mesh
		{
			get => _mesh;
			set
			{
				lock (locker)
				{
					if (_mesh != value)
					{
						_mesh = value;
						traceData = null;
						this.MeshPath = null;

						Invalidate(new InvalidateArgs(this, InvalidateType.Mesh, null));

						AsyncCleanAndMerge();
					}
				}
			}
		}

		[JsonIgnore]
		public bool RebuildLocked
		{
			get
			{
				return this.DescendantsAndSelf().Where((i) =>
				{
					if(i is Object3D object3D)
					{
						return object3D.RebuildLockCount > 0;
					}
					return false;
				}).Any();
			}
		}

		private void AsyncCleanAndMerge()
		{
			var mesh = Mesh;
			// keep track of the mesh we are copying
			if (mesh != null
				&& mesh.Vertices != null
				&& !mesh.Vertices.Sorted)
			{
				var rebuildLock = RebuildLock();

				Task.Run(() =>
				{
					var meshThatWasCopied = mesh;
					// make the copy
					var copyMesh = meshThatWasCopied.Copy(CancellationToken.None);
					// clean the copy
					copyMesh.CleanAndMergeMesh(CancellationToken.None);

					lock (locker)
					{
						// if we have not changed to a new mesh (they are still the same)
						if (meshThatWasCopied == Mesh)
						{
							// store the new clean mesh
							_mesh = copyMesh;
							UiThread.RunOnIdle(() =>
							{
								rebuildLock.Dispose();
								this.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh, null));
							});
						}
						else // we still need to resume the building
						{
							UiThread.RunOnIdle(() =>
							{
								rebuildLock.Dispose();
							});
						}
					}
				});
			}
		}

		public string MeshPath { get; set; }

		public string Name { get; set; }

		[JsonIgnore]
		public virtual bool Persistable { get; set; } = true;

		public virtual bool Visible { get; set; } = true;

		public virtual bool CanApply => false;
		public virtual bool CanRemove => false;
		public virtual bool CanEdit => this.HasChildren();

		[JsonIgnore]
		internal int RebuildLockCount { get; set; }

		private class Object3DRebuildLock : RebuildLock
		{
			public Object3DRebuildLock(IObject3D item)
				: base(item)
			{
				if (item is Object3D object3D)
				{
					object3D.RebuildLockCount++;
				}
			}

			public override void Dispose()
			{
				if (item is Object3D object3D)
				{
					object3D.RebuildLockCount--;
					item.DebugDepth($"Decrease Lock Count {object3D.RebuildLockCount}");
				}
			}
		}

		public RebuildLock RebuildLock()
		{
			this.DebugDepth($"Increase Lock Count {RebuildLockCount}");
			return new Object3DRebuildLock(this);
		}

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
				loadedItem = JsonConvert.DeserializeObject<Object3D>(
					json,
					new JsonSerializerSettings
					{
						ContractResolver = new IObject3DContractResolver(),
						NullValueHandling = NullValueHandling.Ignore
					});

				loadedItem?.LoadMeshLinks(cancellationToken, itemCache, progress);
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

		/// <summary>
		/// Called when loading existing content and needing to bypass the clearing of MeshPath that normally occurs in the this.Mesh setter
		/// </summary>
		/// <param name="mesh">The loaded mesh to assign this instance</param>
		public void SetMeshDirect(Mesh mesh)
		{
			lock (locker)
			{
				if (_mesh != mesh)
				{
					_mesh = mesh;
					AsyncCleanAndMerge();
				}
			}
		}

		public virtual void OnInvalidate(InvalidateArgs invalidateType)
		{
			Invalidated?.Invoke(this, invalidateType);

			if (Parent != null)
			{
				Parent.Invalidate(invalidateType);
			}
		}

		public void Invalidate(InvalidateArgs invalidateType)
		{
			if (!RebuildLocked)
			{
				this.OnInvalidate(invalidateType);
			}
		}
		public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public static IEnumerable<PropertyInfo> GetChildSelectorPropreties(IObject3D item)
		{
			return item.GetType().GetProperties(OwnedPropertiesOnly)
				.Where((pi) =>
				{
					return pi.PropertyType == typeof(ChildrenSelector);
				});
		}

		// Deep clone via json serialization
		public IObject3D Clone()
		{
			var rebuildLock = this.RebuilLockAll();
			var originalParent = this.Parent;

			// Index items by ID
			// but make sure we don't blow up if we find duplicate ids (had bad data that did this)
			var allItemsByID = this.DescendantsAndSelf()
				.GroupBy(p => p.ID, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First());

			IObject3D clonedItem;

			using (var memoryStream = new MemoryStream())
			using (var writer = new StreamWriter(memoryStream))
			{
				// Wrap with a temporary container
				var wrapper = new Object3D();
				wrapper.Children.Add(this);

				// Push json into stream and reset to start
				writer.Write(JsonConvert.SerializeObject(wrapper, Formatting.Indented));
				writer.Flush();
				memoryStream.Position = 0;

				// Load serialized content
				var roundTripped = Object3D.Load(memoryStream, ".mcx", CancellationToken.None);

				// Remove temp container
				clonedItem = roundTripped.Children.First();
			}

			var cloneLocks = clonedItem.RebuilLockAll();
			Dictionary<string, string> idRemaping = new Dictionary<string, string>();
			// Copy mesh instances to cloned tree
			foreach (var descendant in clonedItem.DescendantsAndSelf())
			{
				descendant.SetMeshDirect(allItemsByID[descendant.ID].Mesh);

				// store the original id
				string originalId = descendant.ID;
				// update it to a new ID
				descendant.ID = Guid.NewGuid().ToString();
				// Now OwnerID must be reprocessed after changing ID to ensure consistency
				foreach (var child in descendant.DescendantsAndSelf().Where((c) => c.OwnerID == originalId))
				{
					child.OwnerID = descendant.ID;
				}

				if (!idRemaping.ContainsKey(originalId))
				{
					idRemaping.Add(originalId, descendant.ID);
				}
			}

			// Clean up any child references in the objects
			foreach (var descendant in clonedItem.DescendantsAndSelf())
			{
				// find all ObjecIdListAttributes and update them
				foreach (var property in GetChildSelectorPropreties(descendant))
				{
					var newChildrenSelector = new ChildrenSelector();
					bool foundReplacement = false;

					// sync ids
					foreach (var id in (ChildrenSelector)property.GetGetMethod().Invoke(descendant, null))
					{
						// update old id to new id
						if (idRemaping.ContainsKey(id))
						{
							newChildrenSelector.Add(idRemaping[id]);
							foundReplacement = true;
						}
						else
						{
							// this really should never happen
							newChildrenSelector.Add(id);
						}
					}

					if (foundReplacement)
					{
						property.GetSetMethod().Invoke(descendant, new[] { newChildrenSelector });
					}
				}
			}
			// the cloned item does not have a parent
			clonedItem.Parent = null;
			cloneLocks.ResumeAll();

			// restore the parent
			this.Parent = originalParent;

			rebuildLock.ResumeAll();

			return clonedItem;
		}

		public override string ToString()
		{
			if (Parent != null)
			{
				return $"{this.GetType().Name}, ID = {ID}, Parent = {Parent.ID}";
			}

			return $"{this.GetType().Name}, ID = {ID}";
		}

		public virtual AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			var totalTransorm = this.Matrix * matrix;

			AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty;
			// Set the initial bounding box to empty or the bounds of the objects MeshGroup
			if (this.Mesh != null)
			{
				totalBounds = this.Mesh.GetAxisAlignedBoundingBox(totalTransorm);
			}
			else if (Children.Count > 0)
			{
				foreach (IObject3D child in Children)
				{
					if (child.Visible)
					{
						// Add the bounds of each child object
						var childBounds = child.GetAxisAlignedBoundingBox(totalTransorm);
						// Check if the child actually has any bounds
						if (childBounds.XSize > 0)
						{
							totalBounds += childBounds;
						}
					}
				}
			}

			// Make sure we have some data. Else return 0 bounds.
			if (totalBounds.minXYZ.X == double.PositiveInfinity)
			{
				return AxisAlignedBoundingBox.Zero;
			}

			return totalBounds;
		}

		private IPrimitive traceData;

		// Cache busting on child nodes
		private long tracedHashCode = long.MinValue;
		private bool buildingFaceBsp;

		public IPrimitive TraceData()
		{
			var processingMesh = Mesh;
			// Cache busting on child nodes
			long hashCode = GetLongHashCode();

			if (traceData == null || tracedHashCode != hashCode)
			{
				var traceables = new List<IPrimitive>();
				// Check if we have a mesh at this level
				if (processingMesh != null)
				{
					// we have a mesh so don't recurse into children
					object objectData;
					processingMesh.PropertyBag.TryGetValue("MeshTraceData", out objectData);
					IPrimitive meshTraceData = objectData as IPrimitive;
					if (meshTraceData == null
						&& processingMesh.Faces.Count > 0)
					{
						// Get the trace data for the local mesh
						// First create trace data that builds fast but traces slow
						var simpleTraceData = processingMesh.CreateTraceData(0);
						if (simpleTraceData != null)
						{
							try
							{
								processingMesh.PropertyBag.Add("MeshTraceData", simpleTraceData);
							}
							catch
							{

							}
						}
						traceables.Add(simpleTraceData);
						// Then create trace data that traces fast but builds slow
						//var completeTraceData = processingMesh.CreateTraceData(0);
						//processingMesh.PropertyBag["MeshTraceData"] = completeTraceData;
					}
					else
					{
						traceables.Add(meshTraceData);
					}
				}
				else // No mesh, so get the trace data for all children
				{
					foreach (Object3D child in Children)
					{
						if (child.Visible)
						{
							traceables.Add(child.TraceData());
						}
					}
				}

				// Wrap with a BVH
				traceData = BoundingVolumeHierarchy.CreateNewHierachy(traceables, 0);
				tracedHashCode = hashCode;
			}

			// Wrap with the local transform
			return new Transform(traceData, Matrix);
		}

		// Hashcode for lists as proposed by Jon Skeet
		// http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
		public long GetLongHashCode()
		{
			long hash = 19;

			unchecked
			{
				hash = hash * 31 + Matrix.GetLongHashCode();

				if(Mesh != null)
				{
					hash = hash * 32 + Mesh.GetLongHashCode();
				}

				foreach (var child in Children)
				{
					// The children need to include their transfroms
					hash = hash * 31 + child.GetLongHashCode();
				}
			}

			return hash;
		}

		public string ComputeSHA1()
		{
			// *******************************************************************************************************************************
			// TODO: We must ensure we always compute with a stream that marks for UTF encoding with BOM, irrelevant of in-memory or on disk
			// *******************************************************************************************************************************

			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(this.ToJson())))
			{
				return ComputeSHA1(memoryStream);
			}
		}

		public static string ComputeFileSHA1(string filePath)
		{
			using (var stream = new BufferedStream(File.OpenRead(filePath), 1200000))
			{
				return ComputeSHA1(stream);
			}
		}

		public static string ComputeSHA1(Stream stream)
		{
			// var timer = Stopwatch.StartNew();

			// Alternatively: MD5.Create(),  new SHA256Managed()
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(stream);
				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);

				return BitConverter.ToString(hash).Replace("-", String.Empty);
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

		public virtual void Apply(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				List<IObject3D> newChildren = new List<IObject3D>();
				// push our matrix into a copy of our children
				foreach (var child in this.Children)
				{
					var newChild = child.Clone();
					newChildren.Add(newChild);
					newChild.Matrix *= this.Matrix;
				}

				// and replace us with the children
				undoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { this }, newChildren));
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content, undoBuffer));
		}

		public virtual void Remove(UndoBuffer undoBuffer)
		{
			var parent = this.Parent;

			using (RebuildLock())
			{
				if (undoBuffer != null)
				{
					var newTree = this.Clone();
					using (newTree.RebuildLock())
					{
						// push our matrix into a copy of our children (so they don't jump away)
						foreach (var child in newTree.Children)
						{
							using (child.RebuildLock())
							{
								child.Matrix *= this.Matrix;
							}
						}
					}

					// and replace us with the children
					undoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { this }, newTree.Children.ToList()));
				}
				else
				{
					// push our matrix into a copy of our children (so they don't jump away)
					foreach (var child in this.Children)
					{
						child.Matrix *= this.Matrix;
					}

					parent.Children.Modify(list =>
					{
						list.Remove(this);
						list.AddRange(this.Children);
						parent.Invalidate(new InvalidateArgs(parent, InvalidateType.Content, null));
					});
				}
			}

			parent.Invalidate(new InvalidateArgs(this, InvalidateType.Content, null));
		}
	}
}