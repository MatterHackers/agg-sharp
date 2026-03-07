/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MatterHackers.RenderOpenGl
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

		public bool UseVertexColors { get; set; }
	}

	public class MeshTrianglePlugin
	{
		public delegate void DrawToGL(Mesh meshToRender);

		public List<SubTriangleMesh> subMeshs;

		private int meshUpdateCount;

		public static string MeshTrianglePluginName => nameof(MeshTrianglePluginName);

		static public MeshTrianglePlugin Get(Mesh mesh, Func<Vector3Float, Color> getColorFunc = null)
		{
			object meshData;
			mesh.PropertyBag.TryGetValue(MeshTrianglePluginName, out meshData);
			if (meshData is MeshTrianglePlugin plugin)
			{
				if (mesh.ChangedCount == plugin.meshUpdateCount)
				{
					return plugin;
				}

				// else we need to rebuild the data
				plugin.meshUpdateCount = mesh.ChangedCount;
				mesh.PropertyBag.Remove(MeshTrianglePluginName);
			}

			MeshTrianglePlugin newPlugin = new MeshTrianglePlugin();
			newPlugin.CreateRenderData(mesh, getColorFunc);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			mesh.PropertyBag.Add(MeshTrianglePluginName, newPlugin);

			return newPlugin;
		}

		private MeshTrianglePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh meshToBuildListFor, Func<Vector3Float, Color> getColorFunc)
		{
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
					ImageTexturePlugin.GetImageTexturePlugin(faceTexture.image, true);
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
					if (getColorFunc != null)
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
						blue = faceColor.blue
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