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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public static class MeshHelper
	{
		public static Mesh CreatePlane(double xScale = 1, double yScale = 1)
		{
			return CreatePlane(new Vector2(xScale, yScale));
		}

		public static Mesh CreatePlane(Vector2 scaleIn)
		{
			Vector3 scale = new Vector3(scaleIn * .5); // the plane is -1 to 1 and we want it to be -.5 to .5 so it is a unit cube.
			Mesh plane = new Mesh();
			plane.Vertices.Add(new Vector3(-1, -1, 0) * scale);
			plane.Vertices.Add(new Vector3(1, -1, 0) * scale);
			plane.Vertices.Add(new Vector3(1, 1, 0) * scale);
			plane.Vertices.Add(new Vector3(-1, 1, 0) * scale);

			// front
			plane.Faces.Add(0, 1, 2, plane.Vertices);
			plane.Faces.Add(0, 2, 3, plane.Vertices);

			return plane;
		}
	}
}