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
using MatterHackers.Csg;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.RenderOpenGl
{
	public class DebugBvh
	{
		public static void Render(IPrimitive objectToProcess, Matrix4X4 startingMatrix, int start = 0, int end = int.MaxValue)
		{
			DebugBvh visitor = new DebugBvh();
			visitor.RenderRecursive((dynamic)objectToProcess, start, end, 0, startingMatrix);
		}

		public DebugBvh()
		{
		}

		public void RenderRecursive(IPrimitive objectToProcess, int start, int end, int currentRecursion, Matrix4X4 parentSpace)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		public void RenderRecursive(TriangleShape objectToProcess, int start, int end, int currentRecursion, Matrix4X4 parentSpace)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox(), parentSpace, currentRecursion);
		}

		public void RenderRecursive(UnboundCollection objectToProcess, int start, int end, int currentRecursion, Matrix4X4 parentSpace)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox(), parentSpace, currentRecursion);
			currentRecursion++;
			foreach (var item in objectToProcess.Items)
			{
				RenderRecursive((dynamic)item, start, end, currentRecursion, parentSpace);
			}
		}

		public void RenderRecursive(Transform objectToProcess, int start, int end, int currentRecursion, Matrix4X4 parentSpace)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox(), parentSpace, currentRecursion);
			currentRecursion++;
			parentSpace = objectToProcess.WorldToAxis * parentSpace;
			RenderRecursive((dynamic)objectToProcess.Child, start, end, currentRecursion, parentSpace);
		}

		private void RenderBounds(AxisAlignedBoundingBox axisAlignedBoundingBox, Matrix4X4 parentSpace, int recursion)
		{
			for (int i = 0; i < 4; i++)
			{
				Vector3 bottomStartPosition = Vector3.Transform(axisAlignedBoundingBox.GetBottomCorner(i), parentSpace);
				Vector3 bottomEndPosition = Vector3.Transform(axisAlignedBoundingBox.GetBottomCorner((i + 1) % 4), parentSpace);
				GLHelper.Render3DLine(bottomStartPosition, bottomEndPosition, 1, 1, RGBA_Bytes.Black);

				Vector3 topStartPosition = Vector3.Transform(axisAlignedBoundingBox.GetTopCorner(i), parentSpace);
				Vector3 topEndPosition = Vector3.Transform(axisAlignedBoundingBox.GetTopCorner((i + 1) % 4), parentSpace);
				GLHelper.Render3DLine(topStartPosition, topEndPosition, 1, 1, RGBA_Bytes.Black);

				GLHelper.Render3DLine(topStartPosition, bottomStartPosition, 1, 1, RGBA_Bytes.Black);
			}
		}
	}
}