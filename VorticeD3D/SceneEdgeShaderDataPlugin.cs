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
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public sealed class SceneEdgeShaderSubMeshData
	{
		public float[] InterleavedData { get; init; }

		public bool HasVertexColors { get; init; }

		public object CachedGpuBuffer { get; set; }
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

		public static string SceneEdgeShaderDataPluginName => nameof(SceneEdgeShaderDataPluginName);

		public static SceneEdgeShaderDataPlugin Get(Mesh mesh, RenderTypes renderType)
		{
			mesh.PropertyBag.TryGetValue(SceneEdgeShaderDataPluginName, out object meshData);
			if (meshData is Dictionary<RenderTypes, SceneEdgeShaderDataPlugin> pluginsByRenderType
				&& pluginsByRenderType.TryGetValue(renderType, out var plugin))
			{
				if (plugin.meshUpdateCount == mesh.ChangedCount)
				{
					return plugin;
				}

				pluginsByRenderType.Remove(renderType);
			}
			else
			{
				pluginsByRenderType = new Dictionary<RenderTypes, SceneEdgeShaderDataPlugin>();
				mesh.PropertyBag[SceneEdgeShaderDataPluginName] = pluginsByRenderType;
			}

			var newPlugin = new SceneEdgeShaderDataPlugin();
			newPlugin.CreateRenderData(mesh, renderType);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			newPlugin.renderType = renderType;
			pluginsByRenderType[renderType] = newPlugin;

			return newPlugin;
		}

		private void CreateRenderData(Mesh mesh, RenderTypes renderType)
		{
			var trianglePlugin = MeshTrianglePlugin.Get(mesh);
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

			foreach (var meshEdge in mesh.GetMeshEdges())
			{
				int edgeClass = 0;
				switch (renderType)
				{
					case RenderTypes.Outlines:
						if (meshEdge.Faces.Count() == 2)
						{
							var faceNormal0 = mesh.Faces[meshEdge.Faces[0]].normal;
							var faceNormal1 = mesh.Faces[meshEdge.Faces[1]].normal;
							double angle = faceNormal0.CalculateAngle(faceNormal1);
							if (angle > SceneRenderModeUtilities.OutlineFeatureAngleRadians)
							{
								edgeClass = 1;
							}
						}
						break;

					case RenderTypes.NonManifold:
						edgeClass = meshEdge.Faces.Count() == 2 ? 0 : 2;
						break;
				}

				if (edgeClass == 0)
				{
					continue;
				}

				foreach (int faceIndex in meshEdge.Faces)
				{
					int faceEdgeIndex = GetFaceEdgeIndex(mesh.Faces[faceIndex], meshEdge.Vertex0Index, meshEdge.Vertex1Index);
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
