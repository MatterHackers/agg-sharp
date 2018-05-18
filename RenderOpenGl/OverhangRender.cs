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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	internal class NormalZ
	{
		internal double z;
	}

	public static class OverhangRender
	{
		public static void EnsureUpdated(Mesh meshToRender, Matrix4X4 transform)
		{
			if (!meshToRender.PropertyBag.ContainsKey("Face0WorldZAngle"))
			{
				meshToRender.PropertyBag.Add("Face0WorldZAngle", new NormalZ());
			}

			var normalZ = meshToRender.PropertyBag["Face0WorldZAngle"] as NormalZ;

			var face0Normal = Vector3.TransformVector(meshToRender.Faces[0].Normal, transform).GetNormal();

			var error = .0001;
			if (normalZ.z < face0Normal.Z - error
				|| normalZ.z > face0Normal.Z + error)
			{
				meshToRender.MarkAsChanged();
				normalZ.z = face0Normal.Z;
			}
			// change the color to be the right thing per face normal
			GLMeshTrianglePlugin.Get(
				meshToRender,
				(normal) =>
				{
					normal = Vector3.TransformVector(normal, transform).GetNormal();

					double startColor = 223.0 / 360.0;
					double endColor = 5.0 / 360.0;
					double delta = endColor - startColor;

					var polyColor = ColorF.FromHSL(startColor, .99, .49).ToColor();
					if (normal.Z < 0)
					{
						polyColor = ColorF.FromHSL(startColor - delta * normal.Z, .99, .49).ToColor();
					}

					return polyColor;
				});
		}
	}
}