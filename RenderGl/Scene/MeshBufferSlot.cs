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
using System.Collections.Generic;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderCore;

namespace MatterHackers.RenderGl.Scene
{
	/// <summary>
	/// The vertex buffers one mesh render-data plugin generation minted, and which generation that
	/// was. See <see cref="WebGpuSceneRenderer"/>'s meshBufferSlots table.
	/// </summary>
	internal sealed class MeshBufferSlot
	{
		/// <summary>The plugin instance these buffers belong to. Compared by reference only.</summary>
		public object Owner;

		/// <summary>
		/// Whether the renderer's retainedMeshBuffers list is currently holding this slot. A swept or released
		/// slot comes off that list but stays reachable from the weak key table, so the next draw through
		/// it has to put it back before it mints buffers nothing would own.
		/// </summary>
		public bool IsRetained;

		/// <param name="mesh">The mesh this slot's buffers were built from.</param>
		public MeshBufferSlot(Mesh mesh)
		{
			this.Mesh = new WeakReference<Mesh>(mesh);
		}

		/// <summary>
		/// The mesh the slot is keyed on, weakly - the slot is what keeps the plugin generation (and its
		/// multi-megabyte interleaved vertex data) alive, so it must never be what keeps the mesh alive.
		/// A dead target is how the sweep recognises a slot nothing can ever draw through again.
		/// </summary>
		public WeakReference<Mesh> Mesh { get; }

		/// <summary>
		/// The buffers minted for that instance: one per chunk, of every submesh drawn so far.
		/// </summary>
		public List<IGpuBuffer> Buffers { get; } = new List<IGpuBuffer>();

		/// <summary>
		/// Nulls the per-submesh caches that point at <see cref="Buffers"/>. One per submesh rather than
		/// one per buffer - a submesh caches its whole chunk list on one field - because the two kinds of
		/// submesh cache it on unrelated fields of unrelated types.
		/// </summary>
		private List<Action> CacheClears { get; } = new List<Action>();

		/// <summary>Records the buffers of one submesh this slot owns.</summary>
		/// <param name="buffers">The chunks just minted for that submesh.</param>
		/// <param name="clearSubMeshCache">Nulls the submesh field that now caches them.</param>
		public void Add(IReadOnlyList<IGpuBuffer> buffers, Action clearSubMeshCache)
		{
			this.Buffers.AddRange(buffers);
			this.CacheClears.Add(clearSubMeshCache);
		}

		/// <summary>Hands the buffers to the caller's retirement list and empties the slot.</summary>
		/// <remarks>
		/// The submesh caches are cleared here as well. On the mesh-edit path that is redundant (the
		/// edit replaced the submeshes wholesale), but on the release path the submeshes outlive their
		/// buffers, and a cache still pointing at a disposed buffer would be handed to the next draw.
		/// </remarks>
		/// <param name="retired">The list that owns them until the next submit has happened.</param>
		public void RetireInto(List<IGpuBuffer> retired)
		{
			retired.AddRange(this.Buffers);
			this.Buffers.Clear();

			foreach (var clearSubMeshCache in this.CacheClears)
			{
				clearSubMeshCache();
			}

			this.CacheClears.Clear();
		}
	}
}
