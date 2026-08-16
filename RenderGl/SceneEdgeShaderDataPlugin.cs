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
using MatterHackers.PolygonMesh;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public sealed class SceneEdgeShaderSubMeshData
	{
		public float[] InterleavedData { get; init; }

		public bool HasVertexColors { get; init; }

		/// <summary>
		/// The vertex buffers a renderer minted for this submesh, or null until one has. Several rather
		/// than one because <see cref="InterleavedData"/> can be larger than the device's
		/// <see cref="RenderCore.DeviceLimits.MaxBufferSize"/> - an untextured mesh is a single submesh
		/// holding every face, and at 60 bytes a vertex a 5M face mesh wants nearly a gigabyte. The
		/// renderer then uploads it as consecutive chunks of whole faces and draws one per chunk; see
		/// <c>WebGpuSceneRenderer.EnsureMeshBuffers</c>.
		/// </summary>
		public IReadOnlyList<IGpuBuffer> CachedGpuBufferChunks { get; set; }
	}

	public sealed class SceneEdgeShaderDataPlugin
	{
		private const int BaseVertexStride = SubTriangleMesh.InterleavedStride;
		private const int EdgeHintStride = 3;
		private const int ColorStride = 4; // RGBA as floats

		/// <summary>
		/// Total floats per vertex in the scene effect interleaved data.
		/// </summary>
		public const int TotalVertexFloatStride = BaseVertexStride + EdgeHintStride + ColorStride;

		private int meshUpdateCount;

		private RenderTypes renderType;

		private readonly List<SceneEdgeShaderSubMeshData> subMeshes = new();

		public IReadOnlyList<SceneEdgeShaderSubMeshData> SubMeshes => subMeshes;

		// A plugin is bound to one context: NativeSceneEffects fills each submesh's
		// SceneEdgeShaderSubMeshData.CachedGpuBuffer with a GPU buffer minted by one specific device.
		// MatterCAD renders thumbnails on background worker threads that each own their own GL context,
		// so caching by mesh alone would bind the ui window device's buffer on the thumbnail device.
		// Key by mesh, then by context, then by render type.
		// Both outer levels are weak tables: the cache has to die with the mesh, and a
		// Dictionary<GL, ...> would keep every context that ever drew this mesh alive forever, so a
		// closed window would leak its whole gl cache.
		private static readonly ConditionalWeakTable<Mesh, ConditionalWeakTable<GL, Dictionary<RenderTypes, SceneEdgeShaderDataPlugin>>> pluginsByMesh = new ConditionalWeakTable<Mesh, ConditionalWeakTable<GL, Dictionary<RenderTypes, SceneEdgeShaderDataPlugin>>>();

		// Guards the compound lookup and the publish, including the plain per render type Dictionary at
		// the leaf. Get is called concurrently by the ui thread and the thumbnail workers. This cache
		// used to live in Mesh.PropertyBag, a plain Dictionary that those threads mutated unguarded -
		// concurrent adds of the plugin key corrupted the bag outright.
		// It is held only across the two short sections of Get, never across the build between them, so
		// MeshTrianglePlugin's cache lock (taken from inside CreateRenderData) is never acquired while
		// this one is held. The one way order that does exist is this plugin -> MeshTrianglePlugin ->
		// ImageTexturePlugin, and nothing anywhere walks it backwards.
		private static readonly object cacheLock = new object();

		/// <summary>
		/// Gets the scene edge render data for a mesh on a specific context and render type, building it
		/// if it is missing or stale.
		/// </summary>
		public static SceneEdgeShaderDataPlugin Get(GL gl, Mesh mesh, RenderTypes renderType)
		{
			lock (cacheLock)
			{
				if (TryGetFreshPlugin(gl, mesh, renderType, out var cachedPlugin))
				{
					return cachedPlugin;
				}
			}

			// Build outside the lock - this walks every edge of the mesh and re-interleaves every
			// vertex, so holding the global lock across it would stall the ui thread's render path
			// behind a thumbnail worker chewing on a large mesh.
			// Read the change count before building, exactly as MeshTrianglePlugin.Get does. Stamping
			// the post-build count instead would let a mesh edited mid-build leave this plugin looking
			// fresh while the triangle plugin it was built against looks stale: NativeSceneEffects then
			// rebuilds the triangle plugin, cache hits this one, and walks the two submesh lists by the
			// same index straight off the end of the shorter one.
			var changedCountBuiltFor = mesh.ChangedCount;
			var newPlugin = new SceneEdgeShaderDataPlugin();
			newPlugin.CreateRenderData(gl, mesh, renderType);
			newPlugin.meshUpdateCount = changedCountBuiltFor;
			newPlugin.renderType = renderType;

			lock (cacheLock)
			{
				// Another thread may have published for this context and render type while we were
				// building. Hand back what is already visible rather than swapping it out, so repeat
				// fetches on one context keep returning one instance.
				if (TryGetFreshPlugin(gl, mesh, renderType, out var publishedPlugin))
				{
					return publishedPlugin;
				}

				// Re-fetch the tables instead of reusing ones looked up before the build: nothing in here
				// keeps the mesh alive, so if the caller's own reference died the outer entry could have
				// been collected, and publishing into an orphaned table would rebuild forever.
				// Only ever replace this context's own entry for this render type - another context's
				// plugin owns gpu buffers that only its own thread may touch, and it picks the mesh
				// change up on its own next fetch.
				var pluginsForMesh = pluginsByMesh.GetValue(
					mesh,
					_ => new ConditionalWeakTable<GL, Dictionary<RenderTypes, SceneEdgeShaderDataPlugin>>());
				var pluginsByRenderType = pluginsForMesh.GetValue(
					gl,
					_ => new Dictionary<RenderTypes, SceneEdgeShaderDataPlugin>());
				pluginsByRenderType[renderType] = newPlugin;
			}

			return newPlugin;
		}

		/// <summary>
		/// Looks up the cached plugin for a context and render type and reports whether it was built for
		/// the mesh as it stands now. Callers must hold <see cref="cacheLock"/>.
		/// </summary>
		private static bool TryGetFreshPlugin(GL gl, Mesh mesh, RenderTypes renderType, out SceneEdgeShaderDataPlugin freshPlugin)
		{
			if (pluginsByMesh.TryGetValue(mesh, out var pluginsForMesh)
				&& pluginsForMesh.TryGetValue(gl, out var pluginsByRenderType)
				&& pluginsByRenderType.TryGetValue(renderType, out var plugin)
				&& plugin.meshUpdateCount == mesh.ChangedCount)
			{
				freshPlugin = plugin;
				return true;
			}

			freshPlugin = null;
			return false;
		}

		private void CreateRenderData(GL gl, Mesh mesh, RenderTypes renderType)
		{
			var trianglePlugin = MeshTrianglePlugin.Get(gl, mesh);
			var edgeHintsByFace = BuildEdgeHintsByFace(mesh, renderType);
			var edgeHintsBySubMesh = BuildEdgeHintsBySubMesh(mesh, edgeHintsByFace);

			subMeshes.Clear();
			for (int subMeshIndex = 0; subMeshIndex < trianglePlugin.subMeshs.Count; subMeshIndex++)
			{
				var baseSubMesh = trianglePlugin.subMeshs[subMeshIndex];
				var edgeHints = edgeHintsBySubMesh[subMeshIndex];
				int vertexCount = baseSubMesh.interleavedData.Length / BaseVertexStride;
				var interleavedData = new float[vertexCount * TotalVertexFloatStride];

				bool hasVertexColors = baseSubMesh.UseVertexColors && baseSubMesh.colorData.Count == vertexCount;

				for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
				{
					int baseOffset = vertexIndex * BaseVertexStride;
					int sceneOffset = vertexIndex * TotalVertexFloatStride;

					// Copy base vertex data (position, normal, texcoord)
					Array.Copy(baseSubMesh.interleavedData, baseOffset, interleavedData, sceneOffset, BaseVertexStride);

					// Edge hints
					int edgeOffset = sceneOffset + BaseVertexStride;
					interleavedData[edgeOffset + 0] = edgeHints[vertexIndex * EdgeHintStride + 0];
					interleavedData[edgeOffset + 1] = edgeHints[vertexIndex * EdgeHintStride + 1];
					interleavedData[edgeOffset + 2] = edgeHints[vertexIndex * EdgeHintStride + 2];

					// Per-vertex color (RGBA as 0-1 floats)
					int colorOffset = sceneOffset + BaseVertexStride + EdgeHintStride;
					if (hasVertexColors)
					{
						var c = baseSubMesh.colorData.Array[vertexIndex];
						interleavedData[colorOffset + 0] = c.red / 255f;
						interleavedData[colorOffset + 1] = c.green / 255f;
						interleavedData[colorOffset + 2] = c.blue / 255f;
						interleavedData[colorOffset + 3] = c.alpha / 255f;
					}
					else
					{
						interleavedData[colorOffset + 0] = 1f;
						interleavedData[colorOffset + 1] = 1f;
						interleavedData[colorOffset + 2] = 1f;
						interleavedData[colorOffset + 3] = 1f;
					}
				}

				subMeshes.Add(new SceneEdgeShaderSubMeshData
				{
					InterleavedData = interleavedData,
					HasVertexColors = hasVertexColors,
				});
			}
		}

		private static List<List<float>> BuildEdgeHintsBySubMesh(Mesh mesh, int[][] edgeHintsByFace)
		{
			var edgeHintsBySubMesh = new List<List<float>>();
			List<float> currentSubMesh = null;

			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				mesh.FaceTextures.TryGetValue(faceIndex, out FaceTextureData faceTexture);
				var texture = faceTexture?.image;

				if (edgeHintsBySubMesh.Count == 0
					|| !ReferenceEquals(texture, GetSubMeshTexture(mesh, faceIndex - 1)))
				{
					currentSubMesh = new List<float>();
					edgeHintsBySubMesh.Add(currentSubMesh);
				}

				var faceHints = edgeHintsByFace[faceIndex];
				for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
				{
					currentSubMesh.Add(faceHints[0]);
					currentSubMesh.Add(faceHints[1]);
					currentSubMesh.Add(faceHints[2]);
				}
			}

			return edgeHintsBySubMesh;
		}

		private static object GetSubMeshTexture(Mesh mesh, int faceIndex)
		{
			if (faceIndex < 0)
			{
				return null;
			}

			mesh.FaceTextures.TryGetValue(faceIndex, out FaceTextureData faceTexture);
			return faceTexture?.image;
		}

		private static int[][] BuildEdgeHintsByFace(Mesh mesh, RenderTypes renderType)
		{
			var edgeHintsByFace = new int[mesh.Faces.Count][];
			for (int faceIndex = 0; faceIndex < edgeHintsByFace.Length; faceIndex++)
			{
				edgeHintsByFace[faceIndex] = new int[3];
			}

			if (renderType == RenderTypes.Polygons || renderType == RenderTypes.Wireframe)
			{
				for (int faceIndex = 0; faceIndex < edgeHintsByFace.Length; faceIndex++)
				{
					edgeHintsByFace[faceIndex][0] = 1;
					edgeHintsByFace[faceIndex][1] = 1;
					edgeHintsByFace[faceIndex][2] = 1;
				}

				return edgeHintsByFace;
			}

			// The flat graph rather than GetMeshEdges() - this pass only reads each edge once, and on big
			// meshes the object-per-edge list is hundreds of megabytes of pure waste.
			var edgeGraph = mesh.GetMeshEdgeGraph();
			for (int edgeIndex = 0; edgeIndex < edgeGraph.EdgeCount; edgeIndex++)
			{
				int edgeFaceCount = edgeGraph.GetFaceCount(edgeIndex);
				int edgeClass = 0;
				switch (renderType)
				{
					case RenderTypes.Outlines:
						if (edgeFaceCount == 2)
						{
							var faceNormal0 = mesh.Faces[edgeGraph.GetFace(edgeIndex, 0)].normal;
							var faceNormal1 = mesh.Faces[edgeGraph.GetFace(edgeIndex, 1)].normal;
							double angle = faceNormal0.CalculateAngle(faceNormal1);
							if (angle > SceneRenderModeUtilities.OutlineFeatureAngleRadians)
							{
								edgeClass = 1;
							}
						}
						break;

					case RenderTypes.NonManifold:
						edgeClass = edgeFaceCount == 2 ? 0 : 2;
						break;
				}

				if (edgeClass == 0)
				{
					continue;
				}

				int vertex0Index = edgeGraph.GetVertex0(edgeIndex);
				int vertex1Index = edgeGraph.GetVertex1(edgeIndex);
				for (int faceOffset = 0; faceOffset < edgeFaceCount; faceOffset++)
				{
					int faceIndex = edgeGraph.GetFace(edgeIndex, faceOffset);
					int faceEdgeIndex = GetFaceEdgeIndex(mesh.Faces[faceIndex], vertex0Index, vertex1Index);
					if (faceEdgeIndex >= 0)
					{
						edgeHintsByFace[faceIndex][faceEdgeIndex] = edgeClass;
					}
				}
			}

			return edgeHintsByFace;
		}

		private static int GetFaceEdgeIndex(Face face, int vertexA, int vertexB)
		{
			if (MatchesEdge(face.v1, face.v2, vertexA, vertexB))
			{
				return 0;
			}

			if (MatchesEdge(face.v2, face.v0, vertexA, vertexB))
			{
				return 1;
			}

			if (MatchesEdge(face.v0, face.v1, vertexA, vertexB))
			{
				return 2;
			}

			return -1;
		}

		private static bool MatchesEdge(int faceVertex0, int faceVertex1, int vertexA, int vertexB)
		{
			return (faceVertex0 == vertexA && faceVertex1 == vertexB)
				|| (faceVertex0 == vertexB && faceVertex1 == vertexA);
		}
	}
}
