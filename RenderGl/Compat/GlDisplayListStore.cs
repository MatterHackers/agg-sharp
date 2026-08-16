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
*/

using System;
using System.Collections.Generic;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// GL display lists, recorded at the compat level and replayed as retained geometry.
	/// <para>
	/// <b>Geometry only is baked.</b> <c>glCallList</c> in the classic path replays through the same
	/// flush that live immediate mode uses, so it reads whatever state is current at replay time: the
	/// bound texture, blend and depth state, the color write mask, the matrix stack. Baking a pipeline
	/// or a bind group into the list would freeze all of that at record time and quietly mis-render the
	/// second caller. So a replay resolves the pipeline and bind group from live state, and only the
	/// vertex buffer is reused.
	/// </para>
	/// <para>
	/// The one piece of live state that <em>does</em> reach the vertex bytes is flat shading, because
	/// GL's provoking vertex rule is applied on the CPU while interleaving (see
	/// <see cref="GlImmediateModeBuffer.ColorIndexForFlatShading"/>), and so is the choice of vertex
	/// layout. Baked buffers are therefore keyed by (textured, flat shading) - a list replayed both ways
	/// bakes twice and each variant is then reused.
	/// </para>
	/// </summary>
	public class GlDisplayListStore : IDisposable
	{
		private readonly IRenderDevice device;
		private readonly Dictionary<int, List<GlDisplayListEntry>> lists = new Dictionary<int, List<GlDisplayListEntry>>();
		private int nextListName = 1;

		/// <summary>Creates a display list store over a device.</summary>
		/// <param name="device">The device baked vertex buffers are created on.</param>
		public GlDisplayListStore(IRenderDevice device)
		{
			this.device = device ?? throw new ArgumentNullException(nameof(device));
		}

		/// <summary>The list currently being recorded into, or 0.</summary>
		public int RecordingList { get; private set; }

		/// <summary>True between <c>glNewList</c> and <c>glEndList</c>.</summary>
		public bool IsRecording => this.RecordingList != 0;

		/// <summary>Reserves a run of list names, as <c>glGenLists</c> does.</summary>
		/// <param name="count">How many names to reserve.</param>
		/// <returns>The first name of the run.</returns>
		public int GenerateNames(int count)
		{
			int first = this.nextListName;
			for (int i = 0; i < count; i++)
			{
				this.lists[this.nextListName] = new List<GlDisplayListEntry>();
				this.nextListName++;
			}

			return first;
		}

		/// <summary>Starts recording into a list, discarding anything it held.</summary>
		/// <param name="name">The list name.</param>
		public void BeginRecording(int name)
		{
			this.RecordingList = name;
			if (!this.lists.TryGetValue(name, out var entries))
			{
				entries = new List<GlDisplayListEntry>();
				this.lists[name] = entries;
			}

			foreach (var entry in entries)
			{
				entry.Dispose();
			}

			entries.Clear();
		}

		/// <summary>Stops recording.</summary>
		public void EndRecording() => this.RecordingList = 0;

		/// <summary>
		/// Appends one <c>glBegin</c>/<c>glEnd</c> batch to the list being recorded. The lists are copied
		/// because the accumulator reuses its own.
		/// </summary>
		/// <param name="immediate">The accumulator holding the batch.</param>
		public void Record(GlImmediateModeBuffer immediate)
		{
			if (!this.IsRecording || !this.lists.TryGetValue(this.RecordingList, out var entries))
			{
				return;
			}

			entries.Add(new GlDisplayListEntry(
				immediate.Mode,
				new List<float>(immediate.Positions),
				new List<byte>(immediate.Colors),
				new List<float>(immediate.TexCoords)));
		}

		/// <summary>The recorded batches of a list, or an empty sequence when the name is unknown.</summary>
		/// <param name="name">The list name.</param>
		public IReadOnlyList<GlDisplayListEntry> Entries(int name)
			=> this.lists.TryGetValue(name, out var entries) ? entries : Array.Empty<GlDisplayListEntry>();

		/// <summary>Deletes a run of lists and the buffers they baked.</summary>
		/// <param name="first">First list name.</param>
		/// <param name="count">How many names to delete.</param>
		public void Delete(int first, int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (this.lists.TryGetValue(first + i, out var entries))
				{
					foreach (var entry in entries)
					{
						entry.Dispose();
					}

					this.lists.Remove(first + i);
				}
			}
		}

		/// <summary>
		/// Returns the baked vertex buffer for one batch under a given interleave, creating it on first
		/// replay. This is where "record at the compat level, bake on first replay" actually happens.
		/// </summary>
		/// <param name="entry">The recorded batch.</param>
		/// <param name="textured">Whether the replay is drawing textured.</param>
		/// <param name="flatShading">Whether flat shading is in effect at replay time.</param>
		public IGpuBuffer GetBakedGeometry(GlDisplayListEntry entry, bool textured, bool flatShading)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			var key = (textured, flatShading);
			if (entry.Baked.TryGetValue(key, out var buffer))
			{
				FrameProfiler.Count("BakeReplay");
				return buffer;
			}

			FrameProfiler.Count("BakeNew");

			byte[] bytes = textured
				? GlImmediateModeBuffer.BuildTexturedVertices(entry.Mode, entry.Positions, entry.Colors, entry.TexCoords, flatShading)
				: GlImmediateModeBuffer.BuildColoredVertices(entry.Mode, entry.Positions, entry.Colors, flatShading);

			buffer = this.device.CreateBuffer(BufferUsage.Vertex, (ulong)bytes.Length, bytes);
			entry.Baked[key] = buffer;
			return buffer;
		}

		/// <summary>Releases every list and baked buffer.</summary>
		public void Dispose()
		{
			foreach (var entries in this.lists.Values)
			{
				foreach (var entry in entries)
				{
					entry.Dispose();
				}
			}

			this.lists.Clear();
		}
	}

	/// <summary>
	/// One <c>glBegin</c>/<c>glEnd</c> batch inside a display list, plus the vertex buffers it has been
	/// baked into so far.
	/// </summary>
	public class GlDisplayListEntry : IDisposable
	{
		/// <summary>Creates a recorded batch.</summary>
		/// <param name="mode">The primitive mode, already fan-converted if it was a fan.</param>
		/// <param name="positions">Three floats per vertex.</param>
		/// <param name="colors">Four bytes per vertex.</param>
		/// <param name="texCoords">Two floats per vertex, or empty.</param>
		public GlDisplayListEntry(BeginMode mode, List<float> positions, List<byte> colors, List<float> texCoords)
		{
			this.Mode = mode;
			this.Positions = positions;
			this.Colors = colors;
			this.TexCoords = texCoords;
		}

		/// <summary>The primitive mode.</summary>
		public BeginMode Mode { get; }

		/// <summary>Recorded positions, three floats per vertex.</summary>
		public List<float> Positions { get; }

		/// <summary>Recorded colors, four bytes per vertex.</summary>
		public List<byte> Colors { get; }

		/// <summary>Recorded texture coordinates.</summary>
		public List<float> TexCoords { get; }

		/// <summary>Number of vertices in the batch.</summary>
		public int VertexCount => this.Positions.Count / 3;

		/// <summary>Whether the batch recorded any texture coordinates.</summary>
		public bool HasTexCoords => this.TexCoords.Count > 0;

		/// <summary>Vertex buffers baked from this batch, keyed by the interleave they were baked for.</summary>
		public Dictionary<(bool Textured, bool FlatShading), IGpuBuffer> Baked { get; }
			= new Dictionary<(bool, bool), IGpuBuffer>();

		/// <summary>Releases the baked buffers.</summary>
		public void Dispose()
		{
			foreach (var buffer in this.Baked.Values)
			{
				buffer.Dispose();
			}

			this.Baked.Clear();
		}
	}
}
