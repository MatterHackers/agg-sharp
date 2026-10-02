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
using MatterHackers.Agg;

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

		/// <summary>
		/// Drops the commands from the last <see cref="Build"/>, releasing the meshes they reference.
		/// </summary>
		/// <remarks>
		/// The plan is a single reused instance, so without this it holds the last frame's commands
		/// until some later frame happens to rebuild it. The renderer itself lives in a process-lifetime
		/// static (the thumbnail renderer's cached device), which made that "until" mean "forever":
		/// the last rendered mesh stayed rooted, and with it the ConditionalWeakTable render caches keyed
		/// on the mesh - measured at ~2.3 GB retained after one 5.1M-face thumbnail. The plan is rebuilt
		/// from scratch every frame, so releasing it at end of frame costs nothing.
		/// </remarks>
		public void ReleasePlan()
		{
			plan.Clear();
		}

		/// <summary>
		/// Sorts the given commands into the opaque, transparent and selected passes for one frame.
		/// The returned plan is a reused instance owned by this planner; it is valid only until the
		/// next <see cref="Build"/> or <see cref="ReleasePlan"/> call.
		/// </summary>
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

			GroupByMeshInSubmissionOrder(plan.opaque);

			return plan;
		}

		/// <summary>
		/// Pulls every draw of a mesh up beside that mesh's first draw (so draws sharing GPU buffers sit
		/// together) and otherwise keeps submission order.
		/// </summary>
		/// <remarks>
		/// The order has to be deterministic, not just grouped. Where two meshes meet at exactly equal depth
		/// the later draw wins the pixel under the LessEqual test, so an order that varies between runs is a
		/// picture that varies between runs. This used to sort by <c>RuntimeHelpers.GetHashCode</c>, which
		/// is randomised per object, and one pixel of the golden Scene.Opaque flipped between the sphere and
		/// the cube from CI run to CI run.
		/// </remarks>
		private static void GroupByMeshInSubmissionOrder(List<MeshRenderCommand> commands)
		{
			if (commands.Count < 2)
			{
				return;
			}

			var groups = new Dictionary<object, List<MeshRenderCommand>>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
			var groupOrder = new List<List<MeshRenderCommand>>();
			var nullMeshGroup = new List<MeshRenderCommand>();

			foreach (var command in commands)
			{
				List<MeshRenderCommand> group;
				if (command.Mesh == null)
				{
					group = nullMeshGroup;
					if (group.Count == 0)
					{
						groupOrder.Add(group);
					}
				}
				else if (!groups.TryGetValue(command.Mesh, out group))
				{
					group = new List<MeshRenderCommand>();
					groups.Add(command.Mesh, group);
					groupOrder.Add(group);
				}

				group.Add(command);
			}

			commands.Clear();
			foreach (var group in groupOrder)
			{
				commands.AddRange(group);
			}
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

			if (command.AlphaMultiplier < 1.0f)
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

			// Check if any per-face colors have transparency
			if (mesh.FaceColors != null && mesh.FaceColors.Any(c => c.alpha < 255))
			{
				return true;
			}

			return mesh.FaceTextures.Values.Any(faceTexture => faceTexture?.image?.HasTransparency == true);
		}
	}
}
