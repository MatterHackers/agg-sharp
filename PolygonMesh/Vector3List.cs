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

using System.Collections;
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public static class Vector3ListEx
	{
		public static List<Vector3> ToVector3List(this double[] v)
		{
			var list = new List<Vector3>();
			for (int i = 0; i < v.Length; i += 3)
			{
				list.Add(new Vector3(v[i], v[i + 1], v[i + 2]));
			}

			return list;
		}

		public static double[] ToDoubleArray(this List<Vector3> list)
		{
			var da = new double[list.Count * 3];
			int i = 0;
			foreach (var vertex in list)
			{
				da[i++] = vertex[0];
				da[i++] = vertex[1];
				da[i++] = vertex[2];
			}

			return da;
		}

		public static void Transform(this List<Vector3> list, Matrix4X4 matrix)
		{
			for (int i = 0; i < list.Count; i++)
			{
				list[i] = Vector3Ex.Transform(list[i], matrix);
			}
		}

		public static AxisAlignedBoundingBox Bounds(this List<Vector3> list)
		{
			var bounds = AxisAlignedBoundingBox.Empty();
			foreach (var position in list)
			{
				bounds.ExpandToInclude(position);
			}

			return bounds;
		}

		public static void AddRange(this List<Vector3> list, List<Vector3Float> vector3List)
		{
			foreach (var vector3 in vector3List)
			{
				list.Add(vector3.AsVector3());
			}
		}
	}

	public static class Vector3FloatListEx
	{
		public static List<Vector3Float> ToVector3FloatList(this double[] v)
		{
			var list = new List<Vector3Float>();
			for (int i = 0; i < v.Length; i += 3)
			{
				list.Add(new Vector3Float(v[i], v[i + 1], v[i + 2]));
			}

			return list;
		}

		public static double[] ToDoubleArray(this List<Vector3Float> list, Matrix4X4 matrix)
		{
			var da = new double[list.Count * 3];
			int i = 0;
			foreach (var vector3Float in list)
			{
				var transformed = vector3Float.Transform(matrix);

				da[i++] = transformed[0];
				da[i++] = transformed[1];
				da[i++] = transformed[2];
			}

			return da;
		}

		public static void Transform(this List<Vector3Float> list, Matrix4X4 matrix)
		{
			for (int i = 0; i < list.Count; i++)
			{
				list[i] = Vector3FloatEx.Transform(list[i], matrix);
			}
		}

		public static AxisAlignedBoundingBox Bounds(this List<Vector3Float> list)
		{
			var bounds = AxisAlignedBoundingBox.Empty();
			foreach (var position in list)
			{
				bounds.ExpandToInclude(new Vector3(position));
			}

			return bounds;
		}

		public static void AddRange(this List<Vector3Float> list, List<Vector3> vector3List)
		{
			foreach (var vector3 in vector3List)
			{
				list.Add(new Vector3Float(vector3));
			}
		}

		public static void Add(this List<Vector3Float> list, Vector3 vector3)
		{
			list.Add(new Vector3Float(vector3));
		}
	}
}