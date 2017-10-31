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

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public enum RenderTypes { Hidden, Shaded, Outlines, Polygons, Overhang, Materials, Wireframe };

	public static class GLHelper
	{
		private static Mesh scaledLineMesh = PlatonicSolids.CreateCube();

		private static Mesh unscaledLineMesh = PlatonicSolids.CreateCube();

		public static Frustum GetClippingFrustum(this WorldView world)
		{
			var frustum = Frustum.FrustumFromProjectionMatrix(world.ProjectionMatrix);
			var frustum2 = Frustum.Transform(frustum, world.InverseModelviewMatrix);

			return frustum2;
		}

		public static void PrepareFor3DLineRender(bool doDepthTest)
		{
			GL.Disable(EnableCap.Texture2D);

			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Disable(EnableCap.Lighting);
			if (doDepthTest)
			{
				GL.Enable(EnableCap.DepthTest);
			}
			else
			{
				GL.Disable(EnableCap.DepthTest);
			}
		}

		public static void Render(Mesh meshToRender, Color partColor, RenderTypes renderType = RenderTypes.Shaded, Matrix4X4? meshToViewTransform = null, Color wireFrameColor = default(Color))
		{
			Render(meshToRender, partColor, Matrix4X4.Identity, renderType, meshToViewTransform, wireFrameColor);
		}

		public static void Render(Mesh meshToRender, Color color, Matrix4X4 transform, RenderTypes renderType, Matrix4X4? meshToViewTransform = null, Color wireFrameColor = default(Color))
		{
			if (meshToRender != null)
			{
				GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);

				if (color.Alpha0To1 < 1)
				{
					GL.Enable(EnableCap.Blend);
				}
				else
				{
					GL.Disable(EnableCap.Blend);
				}

				GL.MatrixMode(MatrixMode.Modelview);
				GL.PushMatrix();
				GL.MultMatrix(transform.GetAsFloatArray());

				switch (renderType)
				{
					case RenderTypes.Hidden:
						break;

					case RenderTypes.Polygons:
					case RenderTypes.Outlines:
						GL.Enable(EnableCap.PolygonOffsetFill);
						GL.PolygonOffset(1, 1);
						DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform);
						GL.PolygonOffset(0, 0);
						GL.Disable(EnableCap.PolygonOffsetFill);

						DrawWireOverlay(meshToRender, renderType, wireFrameColor);
						break;

					case RenderTypes.Wireframe:
						DrawWireOverlay(meshToRender, renderType, wireFrameColor);
						break;

					case RenderTypes.Overhang:
					case RenderTypes.Shaded:
					case RenderTypes.Materials:
						DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform);
						break;
				}

				GL.PopMatrix();
			}
		}

		public static void Render3DLine(WorldView world, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			Render3DLine(GetClippingFrustum(world), world, start, end, color, doDepthTest, width);
		}

		public static void Render3DLine(Frustum clippingFrustum, WorldView world, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			PrepareFor3DLineRender(doDepthTest);
			Render3DLineNoPrep(clippingFrustum, world, start, end, color, width);
		}

		public static void Render3DLineNoPrep(Frustum clippingFrustum, WorldView world, Vector3 start, Vector3 end, Color color, double width = 1)
		{
			if (clippingFrustum.ClipLine(ref start, ref end))
			{
				double unitsPerPixelStart = world.GetWorldUnitsPerScreenPixelAtPosition(start);
				double unitsPerPixelEnd = world.GetWorldUnitsPerScreenPixelAtPosition(end);

				Vector3 delta = start - end;
				var deltaLength = delta.Length;
				Matrix4X4 rotateTransform = Matrix4X4.CreateRotation(new Quaternion(Vector3.UnitX + new Vector3(.0001, -.00001, .00002), -delta / deltaLength));
				Matrix4X4 scaleTransform = Matrix4X4.CreateScale(deltaLength, 1, 1);
				Vector3 lineCenter = (start + end) / 2;
				Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(lineCenter);

				var startScale = unitsPerPixelStart * width;
				var endScale = unitsPerPixelEnd * width;
				for (int i = 0; i < unscaledLineMesh.Vertices.Count; i++)
				{
					Vector3 vertexPosition = unscaledLineMesh.Vertices[i].Position;
					if (vertexPosition.x < 0)
					{
						scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.x, vertexPosition.y * startScale, vertexPosition.z * startScale);
					}
					else
					{
						scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.x, vertexPosition.y * endScale, vertexPosition.z * endScale);
					}
				}

				if (true)
				{
					GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);

					if (color.Alpha0To1 < 1)
					{
						GL.Enable(EnableCap.Blend);
					}
					else
					{
						//GL.Disable(EnableCap.Blend);
					}

					GL.MatrixMode(MatrixMode.Modelview);
					GL.PushMatrix();
					GL.MultMatrix(lineTransform.GetAsFloatArray());

					GL.Begin(BeginMode.Triangles);
					foreach (var face in scaledLineMesh.Faces)
					{
						foreach (var vertex in face.AsTriangles())
						{
							GL.Vertex3(vertex.Item1.x, vertex.Item1.y, vertex.Item1.z);
							GL.Vertex3(vertex.Item2.x, vertex.Item2.y, vertex.Item2.z);
							GL.Vertex3(vertex.Item3.x, vertex.Item3.y, vertex.Item3.z);
						}
					}
					GL.End();
					GL.PopMatrix();
				}
				else
				{
					scaledLineMesh.MarkAsChanged();

					GLHelper.Render(scaledLineMesh, color, lineTransform, RenderTypes.Shaded);
				}
			}
		}

		private static void DrawToGL(Mesh meshToRender, bool isTransparent, Matrix4X4? meshToViewTransform)
		{
			if (meshToViewTransform != null
				&& isTransparent
				&& meshToRender.FaceBspTree != null)
			{
				var invMeshToViewTransform = meshToViewTransform.Value;
				invMeshToViewTransform.Invert();
				DrawToGLUsingBsp(meshToRender, meshToViewTransform.Value, invMeshToViewTransform);
				return;
			}

			GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshToRender);
			for (int i = 0; i < glMeshPlugin.subMeshs.Count; i++)
			{
				SubTriangleMesh subMesh = glMeshPlugin.subMeshs[i];
				// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
				if (subMesh.texture != null)
				{
					ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(subMesh.texture, true);
					GL.Enable(EnableCap.Texture2D);
					GL.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					GL.EnableClientState(ArrayCap.TextureCoordArray);
				}
				else
				{
					GL.Disable(EnableCap.Texture2D);
					GL.DisableClientState(ArrayCap.TextureCoordArray);
				}

				if (subMesh.UseVertexColors)
				{
					GL.EnableClientState(ArrayCap.ColorArray);
				}

				GL.EnableClientState(ArrayCap.NormalArray);
				GL.EnableClientState(ArrayCap.VertexArray);
				unsafe
				{
					fixed (VertexTextureData* pTextureData = subMesh.textureData.Array)
					{
						fixed (VertexColorData* pColorData = subMesh.colorData.Array)
						{
							fixed (VertexNormalData* pNormalData = subMesh.normalData.Array)
							{
								fixed (VertexPositionData* pPosition = subMesh.positionData.Array)
								{
									GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pTextureData));
									if (pColorData != null)
									{
										GL.ColorPointer(3, ColorPointerType.UnsignedByte, 0, new IntPtr(pColorData));
									}
									GL.NormalPointer(NormalPointerType.Float, 0, new IntPtr(pNormalData));
									GL.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(pPosition));
									GL.DrawArrays(BeginMode.Triangles, 0, subMesh.positionData.Count);
								}
							}
						}
					}
				}

				GL.DisableClientState(ArrayCap.NormalArray);
				GL.DisableClientState(ArrayCap.VertexArray);
				GL.DisableClientState(ArrayCap.TextureCoordArray);
				GL.DisableClientState(ArrayCap.ColorArray);

				GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(0));
				GL.ColorPointer(3, ColorPointerType.UnsignedByte, 0, new IntPtr(0));
				GL.NormalPointer(NormalPointerType.Float, 0, new IntPtr(0));
				GL.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(0));

				if (subMesh.texture != null)
				{
					GL.DisableClientState(ArrayCap.TextureCoordArray);
				}
			}
		}

		// There can be a singleton of this because GL must always render on the UI thread and can't overlap this array
		static List<Face> bspFaceList = new List<Face>();
		private static void DrawToGLUsingBsp(Mesh meshToRender, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			GL.Begin(BeginMode.Triangles);

			bspFaceList.Clear();
			FaceBspTree.GetFacesInVisibiltyOrder(meshToRender.Faces, meshToRender.FaceBspTree, meshToViewTransform, invMeshToViewTransform, bspFaceList);
			foreach (var face in bspFaceList)
			{
				if (face == null)
				{
					continue;
				}

				/*
				// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
				if (subMesh.texture != null)
				{
					ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(subMesh.texture, true);
					GL.Enable(EnableCap.Texture2D);
					GL.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					GL.EnableClientState(ArrayCap.TextureCoordArray);
				}
				else
				{
					GL.Disable(EnableCap.Texture2D);
					GL.DisableClientState(ArrayCap.TextureCoordArray);
				}
				*/

				GL.Normal3(face.Normal.x, face.Normal.y, face.Normal.z);

				foreach (var vertex in face.AsTriangles())
				{
					GL.Vertex3(vertex.Item1.x, vertex.Item1.y, vertex.Item1.z);
					GL.Vertex3(vertex.Item2.x, vertex.Item2.y, vertex.Item2.z);
					GL.Vertex3(vertex.Item3.x, vertex.Item3.y, vertex.Item3.z);
				}
			}
			GL.End();
		}

		private static void DrawWireOverlay(Mesh meshToRender, RenderTypes renderType, Color color)
		{
			GL.Color4(color.red, color.green, color.blue, color.alpha == 0 ? 255 : color.alpha);

			GL.Disable(EnableCap.Lighting);

			GL.DisableClientState(ArrayCap.TextureCoordArray);
			GLMeshWirePlugin glWireMeshPlugin = null;
			if (renderType == RenderTypes.Outlines)
			{
				glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender, MathHelper.Tau / 8);
			}
			else
			{
				glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender);
			}

			VectorPOD<WireVertexData> edegLines = glWireMeshPlugin.edgeLinesData;
			GL.EnableClientState(ArrayCap.VertexArray);

			unsafe
			{
				fixed (WireVertexData* pv = edegLines.Array)
				{
					GL.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(pv));
					GL.DrawArrays(BeginMode.Lines, 0, edegLines.Count);
				}
			}

			GL.DisableClientState(ArrayCap.VertexArray);
			GL.Enable(EnableCap.Lighting);
		}
	}
}