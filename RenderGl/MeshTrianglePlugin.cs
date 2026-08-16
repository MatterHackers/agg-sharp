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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MatterHackers.RenderGl
{
	public struct VertexTextureData
	{
		public float textureU;
		public float textureV;
		public static readonly int Stride = Marshal.SizeOf(default(VertexTextureData));
	}

	public struct VertexColorData
	{
		public byte red;
		public byte green;
		public byte blue;
		public byte alpha;
		public static readonly int Stride = Marshal.SizeOf(default(VertexColorData));
	}

	public struct VertexNormalData
	{
		public float normalX;
		public float normalY;
		public float normalZ;
		public static readonly int Stride = Marshal.SizeOf(default(VertexNormalData));
	}

	public struct VertexPositionData
	{
		public float positionX;
		public float positionY;
		public float positionZ;
		public static readonly int Stride = Marshal.SizeOf(default(VertexPositionData));
	}

	public class SubTriangleMesh
	{
		public ImageBuffer texture = null;

		/// <summary>
		/// Per vertex uv coordinates. Only filled when <see cref="texture"/> is set - every draw path
		/// binds the texture coordinate array only for a textured submesh, so filling this for an
		/// untextured mesh is 8 bytes a vertex that nothing reads (122 MB on a 5.1M face mesh). The uvs
		/// are in <see cref="interleavedData"/> either way.
		/// </summary>
		public VectorPOD<VertexTextureData> textureData = new VectorPOD<VertexTextureData>();

		/// <summary>
		/// Per vertex colors. Only filled when <see cref="UseVertexColors"/> is true - the color array is
		/// only bound in that case, so an uncolored mesh left it full of zeroes nobody read.
		/// </summary>
		public VectorPOD<VertexColorData> colorData = new VectorPOD<VertexColorData>();
		public VectorPOD<VertexNormalData> normalData = new VectorPOD<VertexNormalData>();
		public VectorPOD<VertexPositionData> positionData = new VectorPOD<VertexPositionData>();

		/// <summary>
		/// Pre-interleaved vertex data: [posX, posY, posZ, normX, normY, normZ, texU, texV] per vertex.
		/// Built once in CreateRenderData so the D3D render loop can memcpy instead of scatter-gathering.
		/// </summary>
		public float[] interleavedData;

		public const int InterleavedStride = 8; // floats per vertex

		/// <summary>
		/// Renderer-specific cached GPU buffer (e.g. an <c>IGpuBuffer</c>). Avoids per-frame upload when set.
		/// </summary>
		public object CachedGpuBuffer;

		/// <summary>
		/// Renderer-specific cached GPU buffers for position-only passes such as selection masks: an
		/// <c>IGpuBuffer[]</c> of one or more chunks, each holding whole faces. More than one when the
		/// positions of this submesh exceed the device's maxBufferSize. Left as <see cref="object"/>
		/// (unlike the scene plugin's typed chunk list) because it is long-standing public API of a type
		/// that predates the render seam.
		/// </summary>
		public object CachedSelectionGpuBuffer;

		public bool UseVertexColors { get; set; }
	}

	public class MeshTrianglePlugin
	{
		public delegate void DrawToGL(Mesh meshToRender);

		public List<SubTriangleMesh> subMeshs;

		private int meshUpdateCount;

		// A plugin is bound to one context: its submeshes hold renderer minted gpu buffers
		// (SubTriangleMesh.CachedGpuBuffer and CachedSelectionGpuBuffer) that name a device resource on
		// the context that created them, and CreateRenderData mints the face textures through the gl it
		// was handed. MatterCAD renders thumbnails on background worker threads that each own their own
		// GL context, so caching by mesh alone would hand a worker the ui thread's device buffers - and
		// the render loop would then bind one device's buffer on the other device. Key by mesh and then
		// by context.
		// Both levels are weak tables: the cache has to die with the mesh, and a Dictionary<GL, ...>
		// would keep every context that ever drew this mesh alive forever, so a closed window would leak
		// its whole gl cache.
		private static readonly ConditionalWeakTable<Mesh, ConditionalWeakTable<GL, MeshTrianglePlugin>> pluginsByMesh = new ConditionalWeakTable<Mesh, ConditionalWeakTable<GL, MeshTrianglePlugin>>();

		// Guards the compound lookup and the publish over the inner per-mesh tables. Get is called
		// concurrently by the ui thread and the thumbnail workers. This cache used to live in
		// Mesh.PropertyBag, a plain Dictionary that those threads mutated unguarded.
		// It is held only across the two short sections of Get, never across the tessellation between
		// them, so ImageTexturePlugin's cache lock (taken from inside CreateRenderData) is never
		// acquired while this one is held. The one way order that does exist is
		// SceneEdgeShaderDataPlugin -> MeshTrianglePlugin -> ImageTexturePlugin, and nothing anywhere
		// walks it backwards.
		private static readonly object cacheLock = new object();

		/// <summary>
		/// Gets the render data for a mesh on a specific context, building it if it is missing or stale.
		/// </summary>
		/// <remarks>
		/// <paramref name="getColorFunc"/> is deliberately not part of the cache identity - the first
		/// caller for a context wins, which is the behavior every caller has always depended on.
		/// </remarks>
		static public MeshTrianglePlugin Get(GL gl, Mesh mesh, Func<Vector3Float, Color> getColorFunc = null)
		{
			lock (cacheLock)
			{
				if (TryGetFreshPlugin(gl, mesh, out var cachedPlugin))
				{
					return cachedPlugin;
				}
			}

			// Build outside the lock. CreateRenderData is a full mesh tessellation and can run for
			// ~100ms on a large mesh, so holding the global lock across it would stall the ui thread's
			// whole render path behind a thumbnail worker. Nothing is lost by letting two contexts
			// build the same changed mesh at once: they publish under different keys, so neither can
			// clobber the other, and the duplicated work is work each of them needed to do anyway.
			// Read the change count before building so a mesh edited mid-build leaves this entry stale
			// and forces a rebuild on the next Get, rather than being masked as up to date.
			var changedCountBuiltFor = mesh.ChangedCount;
			var newPlugin = new MeshTrianglePlugin();
			newPlugin.CreateRenderData(gl, mesh, getColorFunc);
			newPlugin.meshUpdateCount = changedCountBuiltFor;

			lock (cacheLock)
			{
				// Another thread may have published for this context while we were building. Hand back
				// what is already visible rather than swapping it out, so repeat fetches on one context
				// keep returning one instance - callers hold a plugin's subMeshs across a whole frame.
				if (TryGetFreshPlugin(gl, mesh, out var publishedPlugin))
				{
					return publishedPlugin;
				}

				// Fetch the inner table here rather than before the build. mesh is a live parameter all
				// the way through, so the outer entry cannot have been collected out from under us -
				// this is just the cheapest place to get the table, under the lock that has to be held
				// to publish into it anyway.
				// A plugin is only published once its render data is complete - RenderHelper.DrawToGL
				// starts iterating subMeshs the moment it has one - and this lock is the barrier that
				// makes the finished render data visible to the thread that picks the plugin up.
				// Only ever replace this context's own entry. Another context's plugin owns device
				// buffers that only its thread may use or free, and it may be rendering from them right
				// now - it picks the mesh change up on its own next fetch.
				pluginsByMesh.GetValue(mesh, _ => new ConditionalWeakTable<GL, MeshTrianglePlugin>())
					.AddOrUpdate(gl, newPlugin);
			}

			return newPlugin;
		}

		/// <summary>
		/// Looks up the cached plugin for a context and reports whether it was built for the mesh as it
		/// stands now. Callers must hold <see cref="cacheLock"/>.
		/// </summary>
		private static bool TryGetFreshPlugin(GL gl, Mesh mesh, out MeshTrianglePlugin freshPlugin)
		{
			if (pluginsByMesh.TryGetValue(mesh, out var pluginsForMesh)
				&& pluginsForMesh.TryGetValue(gl, out var plugin)
				&& plugin.meshUpdateCount == mesh.ChangedCount)
			{
				freshPlugin = plugin;
				return true;
			}

			freshPlugin = null;
			return false;
		}

		private MeshTrianglePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		/// <summary>
		/// Walks the faces once and returns the exact face count of each submesh the fill below will
		/// produce, in the order it will produce them.
		/// </summary>
		/// <remarks>
		/// The split is driven only by which texture image each face carries, so it can be measured
		/// ahead of the fill. Guessing instead gets it wrong at both ends: an even share of the
		/// remaining faces reserves almost nothing for a mesh where every face shares one texture
		/// (FaceTextures has an entry per face, not per image), while reserving the whole remainder
		/// for a mesh that alternates textures is O(n^2).
		/// </remarks>
		private static List<int> MeasureSubMeshFaceCounts(Mesh mesh)
		{
			var faceCounts = new List<int>();
			ImageBuffer runTexture = null;
			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				mesh.FaceTextures.TryGetValue(faceIndex, out var faceTexture);

				// Same test as the fill: submeshes break on the texture object, not on its contents.
				var texture = faceTexture?.image;
				if (faceCounts.Count == 0
					|| !ReferenceEquals(texture, runTexture))
				{
					faceCounts.Add(0);
					runTexture = texture;
				}

				faceCounts[faceCounts.Count - 1]++;
			}

			return faceCounts;
		}

		/// <summary>
		/// Tessellates the mesh into per texture submeshes of loose triangles, filling both the vertex
		/// arrays the immediate mode GL path binds and the interleaved buffer the D3D path uploads.
		/// </summary>
		/// <remarks>
		/// This is the heaviest allocator in the thumbnail pipeline - a 5.1M face mesh measured 8,348 MB
		/// allocated and 1,958 MB retained - so the vertex buffers are reserved to their final size up
		/// front (VectorPOD otherwise grows by re-copying, ~5x the final bytes through the LOH), the
		/// interleaved data is written as the triangles are emitted rather than in a second pass over the
		/// finished arrays, and the uv and color arrays are left empty when no draw path will read them.
		/// </remarks>
		private void CreateRenderData(GL gl, Mesh meshToBuildListFor, Func<Vector3Float, Color> getColorFunc)
		{
			bool hasFaceColors = meshToBuildListFor.FaceColors != null
				&& meshToBuildListFor.FaceColors.Length > 0;
			bool useVertexColors = getColorFunc != null || hasFaceColors;

			// With no face textures the whole mesh is a single submesh, so its exact vertex count is
			// known before the fill and nothing has to grow at all.
			bool meshHasFaceTextures = meshToBuildListFor.FaceTextures.Count > 0;
			List<int> subMeshFaceCounts = meshHasFaceTextures ? MeasureSubMeshFaceCounts(meshToBuildListFor) : null;

			subMeshs = new List<SubTriangleMesh>();
			SubTriangleMesh currentSubMesh = null;
			VectorPOD<VertexTextureData> textureData = null;
			VectorPOD<VertexColorData> colorData = null;
			VectorPOD<VertexNormalData> normalData = null;
			VectorPOD<VertexPositionData> positionData = null;
			bool fillTextureData = false;
			float[] interleavedData = null;
			int interleavedCount = 0;
			// first make sure all the textures are created
			for (int faceIndex = 0; faceIndex < meshToBuildListFor.Faces.Count; faceIndex++)
			{
				FaceTextureData faceTexture;
				meshToBuildListFor.FaceTextures.TryGetValue(faceIndex, out faceTexture);
				if (faceTexture?.image != null)
				{
					ImageTexturePlugin.GetImageTexturePlugin(gl, faceTexture.image, true);
				}

				// don't compare the data of the texture but rather if they are just the same object
				if (subMeshs.Count == 0
					|| (faceTexture != null
						&& (object)subMeshs[subMeshs.Count - 1].texture != (object)faceTexture.image)
					|| (faceTexture == null
						&& subMeshs[subMeshs.Count - 1].texture != null))
				{
					// The submesh that was being filled is complete now that a new one is starting.
					FinishSubMesh(currentSubMesh, interleavedData, interleavedCount);

					SubTriangleMesh newSubMesh = new SubTriangleMesh();
					newSubMesh.texture = faceTexture == null ? null : faceTexture.image;
					subMeshs.Add(newSubMesh);
					if (useVertexColors)
					{
						newSubMesh.UseVertexColors = true;
					}

					currentSubMesh = newSubMesh;
					textureData = currentSubMesh.textureData;
					colorData = currentSubMesh.colorData;
					normalData = currentSubMesh.normalData;
					positionData = currentSubMesh.positionData;
					fillTextureData = currentSubMesh.texture != null;

					// Untextured meshes are one submesh holding every face; textured ones were measured
					// above, so either way this submesh's final size is known before it is filled.
					int facesToReserve = subMeshFaceCounts == null
						? meshToBuildListFor.Faces.Count
						: subMeshFaceCounts[subMeshs.Count - 1];

					int verticesToReserve = facesToReserve * 3;
					positionData.Capacity(verticesToReserve);
					normalData.Capacity(verticesToReserve);
					if (fillTextureData)
					{
						textureData.Capacity(verticesToReserve);
					}

					if (useVertexColors)
					{
						colorData.Capacity(verticesToReserve);
					}

					interleavedData = new float[verticesToReserve * SubTriangleMesh.InterleavedStride];
					interleavedCount = 0;
				}

				// One face is three vertices. The reservation above should already cover them, but keep
				// the check so a face is never written into a buffer that is short of a whole face.
				int interleavedNeeded = interleavedCount + (3 * SubTriangleMesh.InterleavedStride);
				if (interleavedNeeded > interleavedData.Length)
				{
					Array.Resize(ref interleavedData, Math.Max(interleavedNeeded, interleavedData.Length * 2));
				}

				VertexColorData color = default(VertexColorData);

				if (getColorFunc != null)
				{
					var faceColor = getColorFunc(meshToBuildListFor.Faces[faceIndex].normal);
					color = new VertexColorData
					{
						red = faceColor.red,
						green = faceColor.green,
						blue = faceColor.blue,
						alpha = faceColor.alpha
					};
				}
				else if (hasFaceColors && faceIndex < meshToBuildListFor.FaceColors.Length)
				{
					var faceColor = meshToBuildListFor.FaceColors[faceIndex];
					color = new VertexColorData
					{
						red = faceColor.red,
						green = faceColor.green,
						blue = faceColor.blue,
						alpha = faceColor.alpha
					};
				}

				VertexTextureData tempTexture;
				VertexNormalData tempNormal;
				VertexPositionData tempPosition;
				var face = meshToBuildListFor.Faces[faceIndex];
				var normal = face.normal;
				tempNormal.normalX = normal.X;
				tempNormal.normalY = normal.Y;
				tempNormal.normalZ = normal.Z;

				for (int cornerIndex = 0; cornerIndex < 3; cornerIndex++)
				{
					Vector2Float uv;
					int vertexIndex;
					switch (cornerIndex)
					{
						case 0:
							uv = faceTexture == null ? default(Vector2Float) : faceTexture.uv0;
							vertexIndex = face.v0;
							break;

						case 1:
							uv = faceTexture == null ? default(Vector2Float) : faceTexture.uv1;
							vertexIndex = face.v1;
							break;

						default:
							uv = faceTexture == null ? default(Vector2Float) : faceTexture.uv2;
							vertexIndex = face.v2;
							break;
					}

					tempTexture.textureU = uv.X;
					tempTexture.textureV = uv.Y;
					tempPosition.positionX = (float)meshToBuildListFor.Vertices[vertexIndex].X;
					tempPosition.positionY = (float)meshToBuildListFor.Vertices[vertexIndex].Y;
					tempPosition.positionZ = (float)meshToBuildListFor.Vertices[vertexIndex].Z;

					normalData.Add(tempNormal);
					positionData.Add(tempPosition);

					if (fillTextureData)
					{
						textureData.Add(tempTexture);
					}

					if (useVertexColors)
					{
						colorData.Add(color);
					}

					// Interleave as we go rather than in a second pass over the finished arrays: on a
					// 5.1M face mesh that pass re-read ~550 MB of vertex data to write 490 MB back out.
					interleavedData[interleavedCount + 0] = tempPosition.positionX;
					interleavedData[interleavedCount + 1] = tempPosition.positionY;
					interleavedData[interleavedCount + 2] = tempPosition.positionZ;
					interleavedData[interleavedCount + 3] = tempNormal.normalX;
					interleavedData[interleavedCount + 4] = tempNormal.normalY;
					interleavedData[interleavedCount + 5] = tempNormal.normalZ;
					interleavedData[interleavedCount + 6] = tempTexture.textureU;
					interleavedData[interleavedCount + 7] = tempTexture.textureV;
					interleavedCount += SubTriangleMesh.InterleavedStride;
				}
			}

			FinishSubMesh(currentSubMesh, interleavedData, interleavedCount);
		}

		/// <summary>
		/// Publishes the interleaved buffer that was filled for a submesh, trimming it to the vertices
		/// actually emitted. The trim only copies when the reservation overshot, which cannot happen for
		/// a mesh with no face textures.
		/// </summary>
		private static void FinishSubMesh(SubTriangleMesh subMesh, float[] interleavedData, int usedFloats)
		{
			if (subMesh == null)
			{
				return;
			}

			if (interleavedData.Length != usedFloats)
			{
				Array.Resize(ref interleavedData, usedFloats);
			}

			subMesh.interleavedData = interleavedData;
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