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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	interface IEdgeLinesContainer
	{
		VectorPOD<WireVertexData> EdgeLines { get; }
	}

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WireVertexData
    {
        // Color data
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        // Position data
        public float PositionsX;
        public float PositionsY;
        public float PositionsZ;

        public static readonly int Stride = Marshal.SizeOf(default(WireVertexData));
    }

    public class MeshWirePlugin : IEdgeLinesContainer
	{
		public delegate void DrawToGL(Mesh meshToRender);

		// Volatile because the filtered build below finishes on a background task and swaps the finished
		// list in on that thread. The swap is a single reference store of an already complete list, so a
		// reader either sees the list it started with or the finished one, never a half filled one.
		private volatile VectorPOD<WireVertexData> edgeLines = new VectorPOD<WireVertexData>();

		public VectorPOD<WireVertexData> EdgeLines
		{
			get => edgeLines;
			private set => edgeLines = value;
		}

		private int meshUpdateCount;

		// The edge list is pure cpu data - RenderHelper.DrawWireOverlay hands it to gl as a client side
		// vertex array and the D3D backend copies it into a per draw dynamic buffer - so unlike
		// MeshTrianglePlugin nothing in here names a resource on one context and the cache does not have
		// to be keyed by GL. It does still have to survive being reached from two threads at once:
		// MatterCAD renders thumbnails on background workers that ask for RenderTypes.Outlines while the
		// ui thread paints. A weak table so the cache dies with the mesh, holding a small map of one
		// plugin per filter angle - a mesh is drawn at only a couple of angles in practice. The inner
		// Dictionary is a plain one rather than a concurrent one because every read and write of it
		// happens inside one of the two cacheLock sections of Get.
		private static readonly ConditionalWeakTable<Mesh, Dictionary<double, MeshWirePlugin>> pluginsByMesh = new ConditionalWeakTable<Mesh, Dictionary<double, MeshWirePlugin>>();

		// Guards the lookup and the publish. This cache used to live in Mesh.PropertyBag, a plain
		// Dictionary that the ui thread and the thumbnail workers did Remove/Add on unguarded, which
		// corrupts its bucket chains. Held only across the two short sections of Get, never across the
		// edge extraction between them.
		private static readonly object cacheLock = new object();

		/// <summary>
		/// Gets the wire frame edge lines for a mesh, building them if they are missing or stale.
		/// </summary>
		/// <remarks>
		/// <paramref name="wireColor"/> is deliberately not part of the cache identity - the first caller
		/// wins - but <paramref name="nonPlanarAngleRequired"/> is part of the cache key, because it
		/// changes which edges are in the list at all. RenderHelper asks for a filtered list for
		/// RenderTypes.Outlines and an unfiltered one for RenderTypes.Wireframe, so a mesh drawn both
		/// ways at once (a ui viewport in wireframe while a thumbnail worker renders outlines) keeps both
		/// lists cached instead of the two evicting each other and re-walking the mesh on every fetch.
		/// </remarks>
		public static MeshWirePlugin Get(Mesh mesh, Color wireColor, double nonPlanarAngleRequired = 0, Action meshChanged = null)
		{
			lock (cacheLock)
			{
				if (TryGetFreshPlugin(mesh, nonPlanarAngleRequired, out var cachedPlugin))
				{
					return cachedPlugin;
				}
			}

			// Build outside the lock. Walking every face (or every mesh edge for the filtered list) is
			// O(mesh), so holding the global lock across it would stall the ui thread's render path
			// behind a thumbnail worker. Read the change count before building so a mesh edited mid
			// build leaves this entry stale and forces a rebuild on the next Get, rather than being
			// masked as up to date.
			var changedCountBuiltFor = mesh.ChangedCount;
			var newPlugin = new MeshWirePlugin();
			newPlugin.CreateRenderData(mesh, wireColor, nonPlanarAngleRequired, meshChanged);
			newPlugin.meshUpdateCount = changedCountBuiltFor;

			lock (cacheLock)
			{
				// Another thread may have published while we were building. Hand back what is already
				// visible rather than swapping it out, so repeat fetches keep returning one instance -
				// callers hold a plugin's EdgeLines across a whole frame.
				if (TryGetFreshPlugin(mesh, nonPlanarAngleRequired, out var publishedPlugin))
				{
					return publishedPlugin;
				}

				// Replace the entry rather than mutating the cached plugin in place: the other thread may
				// be part way through drawing from the instance it already fetched, and it picks the mesh
				// change up on its own next fetch.
				// Note what is and is not complete at this point. On the unfiltered path the edge list is
				// finished and this lock is the barrier that makes it visible to the thread that picks
				// the plugin up. On the filtered path (nonPlanarAngleRequired > 0) it is NOT:
				// CreateRenderData publishes the empty list and a background task swaps the filtered one
				// in later, through the volatile EdgeLines field. So a caller can legitimately get a
				// plugin with an empty list and draw nothing for a frame or two - which is why
				// DrawWireOverlay snapshots EdgeLines into a local before iterating, and why that field
				// is volatile rather than a plain one: this lock does not cover the later swap.
				// A stale entry at some other angle is left where it is - it is replaced on its own next
				// fetch, and the map only ever holds the handful of angles the mesh is drawn at.
				var pluginsByAngle = pluginsByMesh.GetOrCreateValue(mesh);
				pluginsByAngle[nonPlanarAngleRequired] = newPlugin;
			}

			return newPlugin;
		}

		/// <summary>
		/// Looks up the cached plugin and reports whether it was built for the mesh as it stands now and
		/// for the requested filter angle. Callers must hold <see cref="cacheLock"/>.
		/// </summary>
		private static bool TryGetFreshPlugin(Mesh mesh, double nonPlanarAngleRequired, out MeshWirePlugin freshPlugin)
		{
			// The angle is the key of the inner map, so finding an entry there already proves it was built
			// for the requested filter - only the mesh's change count is left to check.
			if (pluginsByMesh.TryGetValue(mesh, out var pluginsByAngle)
				&& pluginsByAngle.TryGetValue(nonPlanarAngleRequired, out var plugin)
				&& plugin.meshUpdateCount == mesh.ChangedCount)
			{
				freshPlugin = plugin;
				return true;
			}

			freshPlugin = null;
			return false;
		}

		private MeshWirePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh mesh, Color wireColor, double nonPlanarAngleRequired = 0, Action meshChanged = null)
		{
			var unfilteredEdgeLines = new VectorPOD<WireVertexData>();

			this.EdgeLines = unfilteredEdgeLines;

			// if we are trying to have a filtered list do this in a background thread and wait for the results
			if (nonPlanarAngleRequired > 0)
			{
				Task.Run(() =>
				{
					var meshEdgeList = mesh.GetMeshEdges();

					var filteredEdgeLines = new VectorPOD<WireVertexData>();

					foreach (var meshEdge in meshEdgeList)
					{
						if (meshEdge.Faces.Count() == 2)
						{
							var faceNormal0 = mesh.Faces[meshEdge.Faces[0]].normal;
							var faceNormal1 = mesh.Faces[meshEdge.Faces[1]].normal;
							double angle = faceNormal0.CalculateAngle(faceNormal1);
							if (angle > nonPlanarAngleRequired)
							{
								AddEdgeLine(filteredEdgeLines,
									mesh.Vertices[meshEdge.Vertex0Index],
									mesh.Vertices[meshEdge.Vertex1Index],
									wireColor);
							}
						}
					}

					this.EdgeLines = filteredEdgeLines;
					meshChanged?.Invoke();
				});
			}
            else
            {
				// create a quick edge list of all the polygon edges
				for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
				{
					var face = mesh.Faces[faceIndex];
					AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v0], mesh.Vertices[face.v1], wireColor);
					AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v1], mesh.Vertices[face.v2], wireColor);
					AddEdgeLine(unfilteredEdgeLines, mesh.Vertices[face.v2], mesh.Vertices[face.v0], wireColor);
				}
			}
		}

		public static void AddEdgeLine(VectorPOD<WireVertexData> edgeLines, Vector3Float vertex0, Vector3Float vertex1, Color wireColor)
		{
			WireVertexData tempVertex;
			tempVertex.PositionsX = vertex0.X;
			tempVertex.PositionsY = vertex0.Y;
			tempVertex.PositionsZ = vertex0.Z;
			tempVertex.r = wireColor.red;
            tempVertex.g = wireColor.green;
            tempVertex.b = wireColor.blue;
			tempVertex.a = wireColor.alpha;
            edgeLines.Add(tempVertex);

			tempVertex.PositionsX = vertex1.X;
			tempVertex.PositionsY = vertex1.Y;
			tempVertex.PositionsZ = vertex1.Z;
			edgeLines.Add(tempVertex);
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