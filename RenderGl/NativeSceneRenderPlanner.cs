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
using System.Runtime.CompilerServices;

namespace MatterHackers.RenderGl
{
	public sealed class NativeSceneRenderPlan
	{
		internal readonly List<MeshRenderCommand> opaque = new();
		internal readonly List<MeshRenderCommand> transparent = new();
		internal readonly List<MeshRenderCommand> selected = new();

		public IReadOnlyList<MeshRenderCommand> OpaqueCommands => opaque;

		public IReadOnlyList<MeshRenderCommand> TransparentCommands => transparent;

		public IReadOnlyList<MeshRenderCommand> SelectedCommands => selected;

		internal void Clear()
		{
			opaque.Clear();
			transparent.Clear();
			selected.Clear();
		}
	}

	public class NativeSceneRenderPlanner
	{
		private readonly NativeSceneRenderPlan plan = new();

		public NativeSceneRenderPlan Build(IReadOnlyList<MeshRenderCommand> commands)
		{
			plan.Clear();

			foreach (var command in commands)
			{
				if (RequiresTransparency(command))
				{
					plan.transparent.Add(command);
				}
				else
				{
					plan.opaque.Add(command);
				}

				if (command.IsSelected)
				{
					plan.selected.Add(command);
				}
			}

			// Sort opaque commands by mesh identity to group draws sharing the same GPU buffers/textures
			plan.opaque.Sort((a, b) => RuntimeHelpers.GetHashCode(a.Mesh).CompareTo(RuntimeHelpers.GetHashCode(b.Mesh)));

			return plan;
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
