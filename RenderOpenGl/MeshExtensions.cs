/*
Copyright (c) 2020, Lars Brubaker
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
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public static class MeshExtensions
	{
		/*
		public static IEnumerable<int> GetFacesInVisibiltyOrder(Mesh mesh, Matrix4X4 meshToViewTransform)
		{
			var renderOrder = new Stack<IPrimitive>(new IPrimitive[] { mesh.bvh root.RenderOrder(mesh, meshToViewTransform, invMeshToViewTransform) });

			do
			{
				var lastBack = renderOrder.Peek().BackNode;
				while (lastBack != null
					&& lastBack.Index != -1)
				{
					renderOrder.Peek().BackNode = null;
					renderOrder.Push(lastBack.RenderOrder(mesh, meshToViewTransform, invMeshToViewTransform));
					lastBack = renderOrder.Peek().BackNode;
				}

				var node = renderOrder.Pop();
				if (node.Index != -1)
				{
					yield return node.Index;
				}

				var lastFront = node.FrontNode;
				if (lastFront != null && lastFront.Index != -1)
				{
					renderOrder.Push(lastFront.RenderOrder(mesh, meshToViewTransform, invMeshToViewTransform));
				}
			}
			while (renderOrder.Any());
		}
		*/
	}
}