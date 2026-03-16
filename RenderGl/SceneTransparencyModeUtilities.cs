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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public enum SceneTransparencyMode
	{
		AlphaBlendApproximate,
		DualDepthPeeling,
	}

	public static class SceneTransparencyModeUtilities
	{
		public static bool ShouldUseDualDepthPeelResolve(int depthPeelingLayers)
		{
			return GetSceneTransparencyMode(depthPeelingLayers) == SceneTransparencyMode.DualDepthPeeling;
		}

		public static SceneTransparencyMode GetSceneTransparencyMode(int depthPeelingLayers)
		{
			return NormalizeDepthPeelingLayers(depthPeelingLayers) > 0
				? SceneTransparencyMode.DualDepthPeeling
				: SceneTransparencyMode.AlphaBlendApproximate;
		}

		public static int NormalizeDepthPeelingLayers(int depthPeelingLayers)
		{
			return depthPeelingLayers <= 2 ? 0 : depthPeelingLayers;
		}

		public static IReadOnlyList<MeshRenderCommand> SortTransparentCommandsBackToFront(
			IReadOnlyList<MeshRenderCommand> commands,
			Matrix4X4 viewMatrix)
		{
			if (commands == null || commands.Count == 0)
			{
				return commands ?? new List<MeshRenderCommand>();
			}

			return commands
				.Select((command, index) => new
				{
					Command = command,
					Index = index,
					Depth = GetTransparentSortDepth(command, viewMatrix),
				})
				.OrderBy(item => item.Depth)
				.ThenBy(item => item.Index)
				.Select(item => item.Command)
				.ToList();
		}

		public static double GetTransparentSortDepth(MeshRenderCommand command, Matrix4X4 viewMatrix)
		{
			if (command?.Mesh == null)
			{
				return double.PositiveInfinity;
			}

			var worldBounds = command.Mesh.GetAxisAlignedBoundingBox(command.Transform);
			var viewCenter = worldBounds.Center.TransformPosition(viewMatrix);
			return viewCenter.Z;
		}

		public static bool ShouldRenderBedAfterTransparentObjects(Matrix4X4 bedTransform, Vector3 eyePositionWorld)
		{
			var eyeInBedSpace = eyePositionWorld.TransformPosition(Matrix4X4.Invert(bedTransform));
			return eyeInBedSpace.Z < 0;
		}
	}
}
