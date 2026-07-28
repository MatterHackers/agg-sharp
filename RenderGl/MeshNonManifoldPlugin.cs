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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{

	public class MeshNonManifoldPlugin : IEdgeLinesContainer
	{
		public delegate void DrawToGL(Mesh meshToRender);

		// Volatile because the non manifold pass below finishes on a background task and swaps the
		// finished list in on that thread. The swap is a single reference store of an already complete
		// list, so a reader either sees the unfiltered list it started with or the finished one, never a
		// half filled one.
		private volatile VectorPOD<WireVertexData> edgeLines = new VectorPOD<WireVertexData>();

		public VectorPOD<WireVertexData> EdgeLines
		{
			get => edgeLines;
			private set => edgeLines = value;
		}

		private int meshUpdateCount;

		// Pure cpu data with no context affinity, same as MeshWirePlugin - see the note there. Keyed by
		// mesh only, in a weak table so the cache dies with the mesh.
		private static readonly ConditionalWeakTable<Mesh, MeshNonManifoldPlugin> pluginsByMesh = new ConditionalWeakTable<Mesh, MeshNonManifoldPlugin>();

		// Guards the lookup and the publish. This cache used to live in Mesh.PropertyBag, a plain
		// Dictionary that the ui thread and the thumbnail workers did Remove/Add on unguarded.
		private static readonly object cacheLock = new object();

		/// <summary>
		/// Gets the non manifold edge lines for a mesh, building them if they are missing or stale.
		/// </summary>
		/// <remarks>
		/// <paramref name="minifoldWireColor"/> is deliberately not part of the cache identity - the
		/// first caller wins, which is the behavior every caller has always depended on.
		/// </remarks>
		public static MeshNonManifoldPlugin Get(Mesh mesh, Color minifoldWireColor, Action meshChanged = null)
		{
			lock (cacheLock)
			{
				if (TryGetFreshPlugin(mesh, out var cachedPlugin))
				{
					return cachedPlugin;
				}
			}

			// Build outside the lock - walking every face is O(mesh) and the ui thread's render path
			// must not queue behind a thumbnail worker doing it. Read the change count before building
			// so a mesh edited mid build leaves this entry stale and rebuilds on the next Get.
			var changedCountBuiltFor = mesh.ChangedCount;
			var newPlugin = new MeshNonManifoldPlugin();
			newPlugin.CreateRenderData(mesh, minifoldWireColor, meshChanged);
			newPlugin.meshUpdateCount = changedCountBuiltFor;

			lock (cacheLock)
			{
				// Another thread may have published while we were building. Hand back what is already
				// visible rather than swapping it out, so repeat fetches keep returning one instance.
				if (TryGetFreshPlugin(mesh, out var publishedPlugin))
				{
					return publishedPlugin;
				}

				// Replace the entry rather than mutating the cached plugin in place: the other thread may
				// be part way through drawing from the instance it already fetched.
				pluginsByMesh.AddOrUpdate(mesh, newPlugin);
			}

			return newPlugin;
		}

		/// <summary>
		/// Looks up the cached plugin and reports whether it was built for the mesh as it stands now.
		/// Callers must hold <see cref="cacheLock"/>.
		/// </summary>
		private static bool TryGetFreshPlugin(Mesh mesh, out MeshNonManifoldPlugin freshPlugin)
		{
			if (pluginsByMesh.TryGetValue(mesh, out var plugin)
				&& plugin.meshUpdateCount == mesh.ChangedCount)
			{
				freshPlugin = plugin;
				return true;
			}

			freshPlugin = null;
			return false;
		}

		private MeshNonManifoldPlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh mesh, Color wireColor, Action meshChanged = null)
		{
			var unfilteredEdgeLines = new VectorPOD<WireVertexData>();

			// create a quick edge list of all the polygon edges
			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				var face = mesh.Faces[faceIndex];
                MeshWirePlugin.AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v0], mesh.Vertices[face.v1], wireColor);
                MeshWirePlugin.AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v1], mesh.Vertices[face.v2], wireColor);
                MeshWirePlugin.AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v2], mesh.Vertices[face.v0], wireColor);
			}

			this.EdgeLines = unfilteredEdgeLines;

			// do this in a background thread and wait for the results
			Task.Run(() =>
			{
				var filteredEdgeLines = new VectorPOD<WireVertexData>();

                foreach (var meshEdge in mesh.GetMeshEdges())
                {
                    if (meshEdge.Faces.Count() != 2)
                    {
                        MeshWirePlugin.AddEdgeLine(filteredEdgeLines,
                            mesh.Vertices[meshEdge.Vertex0Index],
                            mesh.Vertices[meshEdge.Vertex1Index],
							Color.Red);
                    }
					else
					{
                        MeshWirePlugin.AddEdgeLine(filteredEdgeLines,
                            mesh.Vertices[meshEdge.Vertex0Index],
                            mesh.Vertices[meshEdge.Vertex1Index],
							wireColor);
                    }
                }

				this.EdgeLines = filteredEdgeLines;
				meshChanged?.Invoke();
			});
		}

		public void Render()
		{
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}