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

using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using MatterHackers.Agg;

namespace MatterHackers.DataConverters3D
{
	public enum Object3DTypes
	{
		Model,
		Group,
		SelectionGroup,
		GenericObject
	};

	public class MeshAndTransform
	{
		public Mesh MeshData { get; }
		public Matrix4X4 Matrix { get; }

		public MeshAndTransform(Mesh meshData, Matrix4X4 matrix)
		{
			MeshData = meshData;
			Matrix = matrix;
		}
	}

	public interface IObject3D
	{
		string ActiveEditor { get; set; }

		[JsonConverter(typeof(IObject3DChildrenConverter))]
		List<IObject3D> Children { get; set; }
		RGBA_Bytes Color { get; set; }
		PlatingData ExtraData { get; }
		MeshGroup Flatten();
		bool HasChildren { get; }
		Object3DTypes ItemType { get; set; }

		[JsonConverter(typeof(MatrixConverter))]
		Matrix4X4 Matrix { get; set; }

		Mesh Mesh { get; set; }
		string MeshPath { get; set; }

		string Name { get; set; }

		bool PersistNode { get; set; }

		bool Visible { get; set; }

		/// <summary>
		/// Create a deep copy of the IObject3D objects
		/// </summary>
		/// <returns></returns>
		IObject3D Clone();

		/// <summary>
		/// Get the Axis Aligned Bounding Box transformed by the given offest
		/// </summary>
		/// <param name="offet">The initial offset to use for the bounds</param>
		/// <returns></returns>
		AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 offet);

		/// <summary>
		/// Return ray tracing data for the current data. This is used
		/// for intersections (mouse hit) and possibly rendering.
		/// </summary>
		/// <returns></returns>
		IPrimitive TraceData();

		/// <summary>
		/// Enumerator to get the currently visble set of meshes for rendering.
		/// The returned set may include placeholder or proxy data while
		/// long operations are happening such as loading or mesh processing.
		/// </summary>
		/// <param name="transform">The final transform to apply to the returend 
		/// transforms as the tree is decended. Often passed as Matrix4X4.Identity.</param>
		/// <returns></returns>
		IEnumerable<MeshAndTransform> VisibleMeshes(Matrix4X4 transform);
	}
}