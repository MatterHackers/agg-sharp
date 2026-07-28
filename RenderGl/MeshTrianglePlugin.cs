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
		public VectorPOD<VertexTextureData> textureData = new VectorPOD<VertexTextureData>();
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
		/// Renderer-specific cached GPU buffer (e.g. ID3D11Buffer). Avoids per-frame upload when set.
		/// </summary>
		public object CachedGpuBuffer;

		/// <summary>
		/// Renderer-specific cached GPU buffer for position-only passes such as selection masks.
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

		private void CreateRenderData(GL gl, Mesh meshToBuildListFor, Func<Vector3Float, Color> getColorFunc)
		{
			bool hasFaceColors = meshToBuildListFor.FaceColors != null
				&& meshToBuildListFor.FaceColors.Length > 0;

			subMeshs = new List<SubTriangleMesh>();
			SubTriangleMesh currentSubMesh = null;
			VectorPOD<VertexTextureData> textureData = null;
			VectorPOD<VertexColorData> colorData = null;
			VectorPOD<VertexNormalData> normalData = null;
			VectorPOD<VertexPositionData> positionData = null;
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
					SubTriangleMesh newSubMesh = new SubTriangleMesh();
					newSubMesh.texture = faceTexture == null ? null : faceTexture.image;
					subMeshs.Add(newSubMesh);
					if (getColorFunc != null || hasFaceColors)
					{
						newSubMesh.UseVertexColors = true;
					}

					currentSubMesh = subMeshs[subMeshs.Count - 1];
					textureData = currentSubMesh.textureData;
					colorData = currentSubMesh.colorData;
					normalData = currentSubMesh.normalData;
					positionData = currentSubMesh.positionData;
				}

				VertexColorData color = new VertexColorData();

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
				tempTexture.textureU = faceTexture == null ? 0 : (float)faceTexture.uv0.X;
				tempTexture.textureV = faceTexture == null ? 0 : (float)faceTexture.uv0.Y;
				var normal = meshToBuildListFor.Faces[faceIndex].normal;
				tempNormal.normalX = normal.X;
				tempNormal.normalY = normal.Y;
				tempNormal.normalZ = normal.Z;
				int vertexIndex = meshToBuildListFor.Faces[faceIndex].v0;
				tempPosition.positionX = (float)meshToBuildListFor.Vertices[vertexIndex].X;
				tempPosition.positionY = (float)meshToBuildListFor.Vertices[vertexIndex].Y;
				tempPosition.positionZ = (float)meshToBuildListFor.Vertices[vertexIndex].Z;
				textureData.Add(tempTexture);
				normalData.Add(tempNormal);
				positionData.Add(tempPosition);
				colorData.Add(color);

				tempTexture.textureU = faceTexture == null ? 0 : (float)faceTexture.uv1.X;
				tempTexture.textureV = faceTexture == null ? 0 : (float)faceTexture.uv1.Y;
				vertexIndex = meshToBuildListFor.Faces[faceIndex].v1;
				tempPosition.positionX = (float)meshToBuildListFor.Vertices[vertexIndex].X;
				tempPosition.positionY = (float)meshToBuildListFor.Vertices[vertexIndex].Y;
				tempPosition.positionZ = (float)meshToBuildListFor.Vertices[vertexIndex].Z;
				textureData.Add(tempTexture);
				normalData.Add(tempNormal);
				positionData.Add(tempPosition);
				colorData.Add(color);

				tempTexture.textureU = faceTexture == null ? 0 : (float)faceTexture.uv2.X;
				tempTexture.textureV = faceTexture == null ? 0 : (float)faceTexture.uv2.Y;
				vertexIndex = meshToBuildListFor.Faces[faceIndex].v2;
				tempPosition.positionX = (float)meshToBuildListFor.Vertices[vertexIndex].X;
				tempPosition.positionY = (float)meshToBuildListFor.Vertices[vertexIndex].Y;
				tempPosition.positionZ = (float)meshToBuildListFor.Vertices[vertexIndex].Z;
				textureData.Add(tempTexture);
				normalData.Add(tempNormal);
				positionData.Add(tempPosition);
				colorData.Add(color);
			}

			// Build pre-interleaved vertex arrays for fast GPU upload
			foreach (var subMesh in subMeshs)
			{
				int vertexCount = subMesh.positionData.Count;
				subMesh.interleavedData = new float[vertexCount * SubTriangleMesh.InterleavedStride];
				var positions = subMesh.positionData.Array;
				var normals = subMesh.normalData.Array;
				var textures = subMesh.textureData.Array;
				for (int i = 0; i < vertexCount; i++)
				{
					int offset = i * SubTriangleMesh.InterleavedStride;
					subMesh.interleavedData[offset + 0] = positions[i].positionX;
					subMesh.interleavedData[offset + 1] = positions[i].positionY;
					subMesh.interleavedData[offset + 2] = positions[i].positionZ;
					subMesh.interleavedData[offset + 3] = normals[i].normalX;
					subMesh.interleavedData[offset + 4] = normals[i].normalY;
					subMesh.interleavedData[offset + 5] = normals[i].normalZ;
					subMesh.interleavedData[offset + 6] = textures[i].textureU;
					subMesh.interleavedData[offset + 7] = textures[i].textureV;
				}
			}
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