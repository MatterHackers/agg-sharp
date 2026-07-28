/*
Copyright (c) 2026, Lars Brubaker
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
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	internal class NormalZ
	{
		internal double z;
	}

	public static class OverhangRender
	{
		// The marker that records which way face 0 was facing the last time this mesh was colored for
		// overhangs. Pure cpu state - it holds no gl handles - so it is keyed by mesh alone, but it used
		// to live in Mesh.PropertyBag, a plain Dictionary that the ui thread and the thumbnail workers
		// mutated unguarded. A weak table so the marker dies with the mesh, and GetValue is atomic, so
		// two threads arriving at once get one marker rather than an ArgumentException or a corrupted
		// bucket chain.
		private static readonly ConditionalWeakTable<Mesh, NormalZ> face0WorldZAngleByMesh = new ConditionalWeakTable<Mesh, NormalZ>();

		/// <summary>
		/// Colors a mesh by how far each face overhangs, rebuilding the coloring if the mesh has turned
		/// since the last pass.
		/// </summary>
		/// <remarks>
		/// Today only the 3d view reaches this, on the ui thread: it is the one caller that renders with
		/// RenderTypes.Overhang, and the D3D thumbnail workers ask for RenderTypes.Outlines. The compare
		/// and set below is still guarded, because it is a read-modify-write that ends in MarkAsChanged
		/// and a second overhang viewport would otherwise have the two of them re-marking the mesh past
		/// each other forever, re-tesselating it on every frame.
		/// </remarks>
		public static void EnsureUpdated(GL gl, Mesh meshToRender, Matrix4X4 transform)
		{
			var faces = meshToRender.Faces;

			if (faces?.Count < 1)
			{
				return;
			}

			var normalZ = face0WorldZAngleByMesh.GetValue(meshToRender, _ => new NormalZ());

			var face0Normal = faces[0].normal.TransformNormal(transform).GetNormal();

			var error = .0001;
			bool meshTurnedSinceLastPass;
			lock (normalZ)
			{
				meshTurnedSinceLastPass = normalZ.z < face0Normal.Z - error
					|| normalZ.z > face0Normal.Z + error;
				if (meshTurnedSinceLastPass)
				{
					normalZ.z = face0Normal.Z;
				}
			}

			// Outside the lock, and after the marker is recorded: MarkAsChanged raises Mesh.Changed to
			// arbitrary handlers, and neither an unknown handler's locks nor a handler that renders its
			// way back in here should be able to tangle with this one.
			if (meshTurnedSinceLastPass)
			{
				meshToRender.MarkAsChanged();
			}

			// change the color to be the right thing per face normal
			MeshTrianglePlugin.Get(
				gl,
				meshToRender,
				(normal) =>
				{
					normal = normal.TransformNormal(transform).GetNormal();

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