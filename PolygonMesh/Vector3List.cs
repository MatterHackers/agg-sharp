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

using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	// I have a plan to have an intenal storage that is IList so
	// we can have a wrapper around arrays or other structures that make them work with this type
	public class Vector3List : List<Vector3>
	{
		public Vector3List()
		{

		}

		public Vector3List(double[] v)
		{
			for (int i = 0; i < v.Length; i += 3)
			{
				Add(new Vector3(v[i], v[i + 1], v[i + 2]));
			}
		}

		public double[] ToDoubleArray()
		{
			var da = new double[Count * 3];
			int i = 0;
			foreach (var vertex in this)
			{
				da[i++] = vertex[0];
				da[i++] = vertex[1];
				da[i++] = vertex[2];
			}

			return da;
		}

		public void Transform(Matrix4X4 matrix)
		{
			for (int i = 0; i < this.Count; i++)
			{
				this[i] = Vector3.Transform(this[i], matrix);
			}
		}
	}
}