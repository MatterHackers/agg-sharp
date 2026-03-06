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

namespace MatterHackers.RenderOpenGl
{
	public sealed class NativeSceneRenderPlan
	{
		public NativeSceneRenderPlan(
			IReadOnlyList<MeshRenderCommand> opaqueCommands,
			IReadOnlyList<MeshRenderCommand> transparentCommands,
			IReadOnlyList<MeshRenderCommand> selectedCommands)
		{
			OpaqueCommands = opaqueCommands;
			TransparentCommands = transparentCommands;
			SelectedCommands = selectedCommands;
		}

		public IReadOnlyList<MeshRenderCommand> OpaqueCommands { get; }

		public IReadOnlyList<MeshRenderCommand> TransparentCommands { get; }

		public IReadOnlyList<MeshRenderCommand> SelectedCommands { get; }
	}

	public static class NativeSceneRenderPlanner
	{
		public static NativeSceneRenderPlan Build(IReadOnlyList<MeshRenderCommand> commands)
		{
			var opaque = new List<MeshRenderCommand>(commands.Count);
			var transparent = new List<MeshRenderCommand>();
			var selected = new List<MeshRenderCommand>();

			foreach (var command in commands)
			{
				if (RequiresTransparency(command))
				{
					transparent.Add(command);
				}
				else
				{
					opaque.Add(command);
				}

				if (command.IsSelected)
				{
					selected.Add(command);
				}
			}

			return new NativeSceneRenderPlan(opaque, transparent, selected);
		}

		public static bool RequiresTransparency(MeshRenderCommand command)
		{
			if (command == null)
			{
				return false;
			}

			if (command.Color.Alpha0To1 < 1)
			{
				return true;
			}

			if (!command.ForceCullBackFaces)
			{
				return true;
			}

			var mesh = command.Mesh;
			if (mesh == null)
			{
				return false;
			}

			return mesh.FaceTextures.Values.Any(faceTexture => faceTexture?.image?.HasTransparency == true);
		}
	}
}
