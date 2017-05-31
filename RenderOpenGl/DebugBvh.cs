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

using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.RenderOpenGl
{
	public static class DebugBvhEntensions
	{
		public static void RenderBvhRecursive(this IPrimitive bvhToRender, int startRenderLevel = 0, int endRenderLevel = int.MaxValue)
		{
			foreach (var bvhAndTransform in new BvhIterator(bvhToRender).Where(x =>
					x.Depth >= startRenderLevel 
					&& x.Depth <= endRenderLevel 
					&& x.Bvh.GetType() != typeof(TriangleShape)
				))
			{
				for (int i = 0; i < 4; i++)
				{
					Vector3 bottomStartPosition = Vector3.Transform(bvhAndTransform.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), bvhAndTransform.TransformToWorld);
					Vector3 bottomEndPosition = Vector3.Transform(bvhAndTransform.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner((i + 1) % 4), bvhAndTransform.TransformToWorld);
					GLHelper.Render3DLine(bottomStartPosition, bottomEndPosition, 1, 1, RGBA_Bytes.Black);

					Vector3 topStartPosition = Vector3.Transform(bvhAndTransform.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), bvhAndTransform.TransformToWorld);
					Vector3 topEndPosition = Vector3.Transform(bvhAndTransform.Bvh.GetAxisAlignedBoundingBox().GetTopCorner((i + 1) % 4), bvhAndTransform.TransformToWorld);
					GLHelper.Render3DLine(topStartPosition, topEndPosition, 1, 1, RGBA_Bytes.Black);

					GLHelper.Render3DLine(topStartPosition, bottomStartPosition, 1, 1, RGBA_Bytes.Black);
				}
			}
		}
	}
}