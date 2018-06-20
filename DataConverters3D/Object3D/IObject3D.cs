/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.DataConverters3D
{
	public enum PrintOutputTypes
	{
		Default,
		Solid,
		Support
	};

	public enum InvalidateType
	{
		Matrix,
		Color,
		Material,
		Content,
		Redraw,
		Mesh,
		Path,
		Image,
	};

	[Flags]
	public enum Object3DPropertyFlags
	{
		Matrix = 0x01,
		Color = 0x02,
		MaterialIndex = 0x04,
		Name = 0x8,
		OutputType = 0x10,
		Visible = 0x20,
		All = 0xFF,
	};

	public class InvalidateArgs : EventArgs
	{
		public IObject3D Source { get; }
		public InvalidateType InvalidateType { get; }
		public UndoBuffer UndoBuffer { get; }
		public InvalidateArgs(IObject3D source, InvalidateType invalidateType, UndoBuffer undoBuffer = null)
		{
			this.Source = source;
			this.InvalidateType = invalidateType;
			this.UndoBuffer = undoBuffer;
		}
	}

	public abstract class SuspendLock : IDisposable
	{
		protected IObject3D item;

		public SuspendLock(IObject3D item)
		{
			this.item = item;
		}

		public abstract void Dispose();
	}

	public class MeshPrintOutputSettings
	{
		public int ExtruderIndex { get; set; }
		public PrintOutputTypes PrintOutputTypes { get; set; }
	}

	public static class Object3DHelperExtensions
	{
		public static void AddRange(this IList<IObject3D> list, IEnumerable<IObject3D> addItems)
		{
			list.AddRange(addItems);
		}

		public static bool HasChildren(this IObject3D object3D)
		{
			return object3D.Children.Count > 0;
		}

		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(this IObject3D object3D)
		{
			return object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
		}

		/// <summary>
		/// Got the top of this objects parent tree and get change the name of the object if
		/// required to make sure it is not the same as any other decendant
		/// </summary>
		/// <param name="root"></param>
		public static void MakeNameNonColliding(this IObject3D item)
		{
			var topParent = item.Ancestors().LastOrDefault();
			if (topParent != null)
			{
				var names = topParent.DescendantsAndSelf().Where((i) => i != item).Select((i2) => i2.Name).ToList();

				if (string.IsNullOrEmpty(item.Name))
				{
					// Object3D authors should give their objects a simplified name, but if they fail to do so,
					// fallback to a sane default before calling into GetNonCollidingName
					item.Name = item.TypeName;
				}

				item.Name = agg_basics.GetNonCollidingName(item.Name, names);
			}
		}

		/// <summary>
		/// Count all the vertices in all the visible meshes of this object
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public static int EstimatedMemory(this IObject3D root)
		{
			var vertex = new Vertex();
			var face = new Face();
			var meshEdge = new MeshEdge();
			int total = 0;
			foreach (var item in root.VisibleMeshes())
			{
				var mesh = item.Mesh;
				if (mesh != null)
				{
					total += mesh.Vertices.Count * SizeOf(vertex);
					total += mesh.Faces.Count * SizeOf(face);
					total += mesh.MeshEdges.Count * SizeOf(meshEdge);
				}
			}

			return total;
		}

		public static long MeshRenderId(this IObject3D root)
		{
			long hash = 19;

			using (root.RebuildLock())
			{
				var oldMatrix = root.Matrix;
				root.Matrix = Matrix4X4.Identity;

				foreach (var item in root.VisibleMeshes())
				{
					unchecked
					{
						hash = hash * 31 + item.Mesh.GetLongHashCode();
						hash = hash * 31 + item.WorldMatrix(root).GetLongHashCode();
						hash = hash * 31 + item.WorldColor(root).GetLongHashCode();
					}
				}

				root.Matrix = oldMatrix;
			}

			return hash;
		}

		public static int SizeOf<T>(T obj)
		{
			return SizeOfCache<T>.SizeOf;
		}

		private static class SizeOfCache<T>
		{
			public static readonly int SizeOf;

			static SizeOfCache()
			{
				var dm = new DynamicMethod("func", typeof(int), Type.EmptyTypes, typeof(Object3DHelperExtensions));

				ILGenerator il = dm.GetILGenerator();
				il.Emit(OpCodes.Sizeof, typeof(T));
				il.Emit(OpCodes.Ret);

				var func = (Func<int>)dm.CreateDelegate(typeof(Func<int>));
				SizeOf = func();
			}
		}

		/// <summary>
		/// Enumerator to get the currently visible object that has a meshe for rendering.
		/// The returned set may include placeholder or proxy data while
		/// long operations are happening such as loading or mesh processing.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<IObject3D> VisibleMeshes(this IObject3D root)
		{ 
			if (root.Visible)
			{
				var items = new Stack<IObject3D>(new[] { root });
				while (items.Count > 0)
				{
					var item = items.Pop();

					// store the mesh so we are thread safe regarding having a valid object (not null)
					var mesh = item.Mesh;
					if (mesh != null)
					{
						// there is a mesh return the object
						yield return item;
					}
					else // there is no mesh go into the object and iterate its children
					{
						foreach (var n in item.Children)
						{
							if (n.Visible)
							{
								items.Push(n);
							}
						}
					}
				}
			}

		}
	}

	public interface IPathObject
	{
		IVertexSource VertexSource { get; set; }
	}

	public interface IObject3D : IAscendable<IObject3D>
	{
		event EventHandler<InvalidateArgs> Invalidated;

		string OwnerID { get; set; }

		[JsonConverter(typeof(IObject3DChildrenConverter))]
		SafeList<IObject3D> Children { get; set; }

		[JsonIgnore]
		new IObject3D Parent { get; set; }

		Color Color { get; set; }
		int MaterialIndex { get; set; }

		PrintOutputTypes OutputType { get; set; }

		[JsonConverter(typeof(MatrixConverter))]
		Matrix4X4 Matrix { get; set; }

		string TypeName { get; }

		/// <summary>
		/// The associated mesh for this content. Setting to a new value invalidates the MeshPath, TraceData and notifies all active listeners
		/// </summary>
		[JsonIgnore]
		Mesh Mesh { get; set; }

		string MeshPath { get; set; }

		string Name { get; set; }

		SuspendLock RebuildLock();
		[JsonIgnore]
		bool RebuildSuspended { get; }


		bool Persistable { get; }

		bool Visible { get; set; }

		string ID { get; set; }

		/// <summary>
		/// Directly assigns a mesh without firing events or invalidating
		/// </summary>
		/// <param name="mesh"></param>
		void SetMeshDirect(Mesh mesh);

		/// <summary>
		/// Create a deep copy of the IObject3D objects
		/// </summary>
		/// <returns></returns>
		IObject3D Clone();

		/// <summary>
		/// Mark that this object has changed (and notify its parent)
		/// </summary>
		void Invalidate(InvalidateArgs invalidateType);

		[JsonIgnore]
		/// <summary>
		/// Allow this object to be drilled into and edited
		/// </summary>
		bool CanEdit { get; }

		[JsonIgnore]
		bool CanApply { get; }

		/// <summary>
		/// Remove the IObject3D from the tree and keep whatever functionality it was adding.
		/// This may require removing many child objects from the tree depending on implementation.
		/// </summary>
		void Apply(UndoBuffer undoBuffer);

		[JsonIgnore]
		bool CanRemove { get; }
		/// <summary>
		/// Remove the IObject3D from the tree and undo whatever functionality it was adding (if appropriate).
		/// This may require removing many child objects from the tree depending on implementation.
		/// </summary>
		void Remove(UndoBuffer undoBuffer);

		/// <summary>
		/// Get the Axis Aligned Bounding Box transformed by the given offset
		/// </summary>
		/// <param name="matrix">The Matrix4X4 to use for the bounds</param>
		/// <returns></returns>
		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix);

		/// <summary>
		/// return a 64 bit hash code of the transforms and children and transforms
		/// </summary>
		/// <returns></returns>
		long GetLongHashCode();

		/// <summary>
		/// Serialize the current instance to Json
		/// </summary>
		/// <returns></returns>
		string ToJson();

		/// <summary>
		/// Return ray tracing data for the current data. This is used
		/// for intersections (mouse hit) and possibly rendering.
		/// </summary>
		/// <returns></returns>
		IPrimitive TraceData();
	}

	public class Object3DIterator : IEnumerable<Object3DIterator>
	{
		public Matrix4X4 TransformToWorld { get; private set; }
		public IObject3D IObject3D { get; private set; }
		public int Depth { get; private set; } = 0;
		Func<Object3DIterator, bool> DecentFilter = null;

		public Object3DIterator(IObject3D referenceItem, Matrix4X4 initialTransform = default(Matrix4X4), int initialDepth = 0, Func<Object3DIterator, bool> decentFilter = null)
		{
			TransformToWorld = initialTransform;
			if (TransformToWorld == default(Matrix4X4))
			{
				TransformToWorld = Matrix4X4.Identity;
			}
			Depth = initialDepth;

			IObject3D = referenceItem;
			this.DecentFilter = decentFilter;
		}

		public IEnumerator<Object3DIterator> GetEnumerator()
		{
			foreach (var child in IObject3D.Children)
			{
				var iterator = new Object3DIterator(child, TransformToWorld * child.Matrix, Depth + 1, DecentFilter);

				if (DecentFilter?.Invoke(iterator) != false)
				{
					yield return iterator;

					foreach(var subIterator in iterator)
					{
						if (DecentFilter?.Invoke(subIterator) != false)
						{
							yield return subIterator;
						}
					}
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}
}