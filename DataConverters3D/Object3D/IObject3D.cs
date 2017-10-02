/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.DataConverters3D
{
	public enum Object3DTypes
	{
		Any,
		Model,
		Group,
		SelectionGroup,
	};

	public enum PrintOutputTypes
	{
		Default,
		Solid,
		Hole,
		Support
	};

	public class MeshRenderData
	{
		public RGBA_Bytes Color { get; }
		public Mesh Mesh { get; }
		public Matrix4X4 Matrix { get; set; }
		public int MaterialIndex { get; }
		public PrintOutputTypes OutputType { get; }

		public MeshRenderData(Mesh meshData, Matrix4X4 matrix, RGBA_Bytes color, int materialIndex, PrintOutputTypes outputType)
		{
			OutputType = outputType;
			MaterialIndex = materialIndex;
			Color = color;
			Mesh = meshData;
			Matrix = matrix;
		}
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

		/// <summary>
		/// Enumerator to get the currently visible set of meshes for rendering.
		/// The returned set may include placeholder or proxy data while
		/// long operations are happening such as loading or mesh processing.
		/// </summary>
		/// <param name="transform">The final transform to apply to the returned 
		/// transforms as the tree is descended. Often passed as Matrix4X4.Identity.</param>
		/// <returns></returns>
		public static IEnumerable<MeshRenderData> VisibleMeshes(this IObject3D collection)
		{
			return collection.VisibleMeshes(Matrix4X4.Identity, collection.Color, collection.MaterialIndex, collection.OutputType);
		}

		private static IEnumerable<MeshRenderData> VisibleMeshes(this IObject3D collection, Matrix4X4 transform, RGBA_Bytes color = default(RGBA_Bytes), int materialIndex = -1, PrintOutputTypes outputType = PrintOutputTypes.Default)
		{
			// If there is no color set yet and the object 3D is specifying a color
			if (color.Alpha0To255 == 0
				&& collection.Color.Alpha0To255 != 0)
			{
				// use collection as the color for all recursize children
				color = collection.Color;
			}

			// If there is no material set yet and the object 3D is specifying a material
			if (materialIndex == -1
				&& collection.MaterialIndex != -1)
			{
				// use collection as the color for all recursize children
				materialIndex = collection.MaterialIndex;
			}

			if (outputType == PrintOutputTypes.Default
				&& collection.OutputType != PrintOutputTypes.Default)
			{
				outputType = collection.OutputType;
			}

			Matrix4X4 totalTransform = collection.Matrix * transform;

			if (collection.Mesh == null)
			{
				foreach (var child in collection.Children.ToList())
				{
					if (collection.ItemType != Object3DTypes.Group || child.OutputType != PrintOutputTypes.Hole)
					{
						foreach (var meshTransform in child.VisibleMeshes(totalTransform, color, materialIndex, outputType))
						{
							yield return meshTransform;
						}
					}
				}
			}

			if (collection.Mesh != null)
			{
				if (color.Alpha0To255 > 0)
				{
					yield return new MeshRenderData(collection.Mesh, totalTransform, color, materialIndex, outputType);
				}
				else
				{
					yield return new MeshRenderData(collection.Mesh, totalTransform, RGBA_Bytes.White, materialIndex, outputType);
				}
			}
		}
	}

	public interface IObject3D
	{
		string ActiveEditor { get; set; }
		string OwnerID { get; set; }

		[JsonConverter(typeof(IObject3DChildrenConverter))]
		SafeList<IObject3D> Children { get; set; }

		[JsonIgnore]
		IObject3D Parent { get; set; }

		RGBA_Bytes Color { get; set; }
		int MaterialIndex { get; set; }
		MeshGroup Flatten(Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = null);
		Object3DTypes ItemType { get; set; }

		PrintOutputTypes OutputType { get; set; }

		[JsonConverter(typeof(MatrixConverter))]
		Matrix4X4 Matrix { get; set; }

		Mesh Mesh { get; set; }
		string MeshPath { get; set; }

		string Name { get; set; }

		bool Persistable { get; }

		bool Visible { get; set; }
		string ID { get; set; }

		void SetAndInvalidateMesh(Mesh mesh);

		/// <summary>
		/// Create a deep copy of the IObject3D objects
		/// </summary>
		/// <returns></returns>
		IObject3D Clone();

		/// <summary>
		/// Get the Axis Aligned Bounding Box transformed by the given offset
		/// </summary>
		/// <param name="offet">The initial offset to use for the bounds</param>
		/// <returns></returns>
		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 offet);

		/// <summary>
		/// return a 64 bit hash code of the transforms and children and transforms
		/// </summary>
		/// <returns></returns>
		long GetLongHashCode();

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