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
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public class Face
	{
		public Vector3Float normal;
		public int v0;
		public int v1;
		public int v2;

		public Face(int v0, int v1, int v2, List<Vector3Float> vertices)
		{
			this.v0 = v0;
			this.v1 = v1;
			this.v2 = v2;

			normal = Vector3Float.Zero;

			CalculateNormal(vertices);
		}

		public Face(int v0, int v1, int v2, Vector3Float normal)
		{
			this.v0 = v0;
			this.v1 = v1;
			this.v2 = v2;

			this.normal = normal;
		}

		/// <summary>
		/// Recomputes this face's normal from its three vertex positions.
		/// </summary>
		/// <remarks>
		/// A face with no area gets the zero vector rather than a normalized one. Zero area faces are
		/// topologically valid - they occur at the seams where a solid touches itself, carrying real edges
		/// that hold the mesh manifold - so they cannot simply be dropped. But their cross product is zero
		/// length, and normalizing that divides by zero: nothing downstream in PolygonMesh guards against a
		/// NaN normal, so one such face poisons every normal-tolerance vertex merge and coplanar-face walk
		/// it takes part in (NaN compares false against every tolerance).
		/// </remarks>
		public void CalculateNormal(List<Vector3Float> vertices)
		{
			var position0 = vertices[this.v0];
			var position1 = vertices[this.v1];
			var position2 = vertices[this.v2];
			var v11MinusV0 = position1 - position0;
			var v2MinusV0 = position2 - position0;
			var cross = v11MinusV0.Cross(v2MinusV0);
			var lengthSquared = cross.LengthSquared;

			// The NaN test catches a non-finite input position as well as an overflowed cross product -
			// both would otherwise pass a plain "> 0" check.
			normal = (lengthSquared > 0 && !float.IsNaN(lengthSquared) && !float.IsInfinity(lengthSquared))
				? cross.GetNormal()
				: Vector3Float.Zero;
		}

        public Face Flipped()
        {
            return new Face(v0, v2, v1, -normal);
        }
    }
}