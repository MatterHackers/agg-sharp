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
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.RenderOpenGl
{
	public enum RenderTypes { Hidden, Shaded, Outlines, Polygons, Overhang };

	public static class GLHelper
	{
		public static void Render(Mesh meshToRender, IColorType partColor, RenderTypes renderType = RenderTypes.Shaded)
		{
			Render(meshToRender, partColor, Matrix4X4.Identity, renderType);
		}

		public static void Render3DLine(Vector3 start, Vector3 end, double unitsPerPixelStart, double unitsPerPixelEnd, RGBA_Bytes color, bool doDepthTest = true)
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

			Vector3 delta = start - end;
			Matrix4X4 rotateTransform = Matrix4X4.CreateRotation(new Quaternion(Vector3.UnitX + new Vector3(.0001, -.00001, .00002), -delta.GetNormal()));
			Matrix4X4 scaleTransform = Matrix4X4.CreateScale((end - start).Length, 1, 1);
			Vector3 lineCenter = (start + end) / 2;
			Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(lineCenter);

			for(int i=0; i<unscaledLineMesh.Vertices.Count; i++)
			{
				Vector3 vertexPosition = unscaledLineMesh.Vertices[i].Position;
				if(vertexPosition.x < 0)
				{
					scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.x, vertexPosition.y * unitsPerPixelStart, vertexPosition.z * unitsPerPixelStart);
				}
				else
				{
					scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.x, vertexPosition.y * unitsPerPixelEnd, vertexPosition.z * unitsPerPixelEnd);
				}
			}

			scaledLineMesh.MarkAsChanged();

			GLHelper.Render(scaledLineMesh, color, lineTransform, RenderTypes.Shaded);
		}

		static Mesh scaledLineMesh = PlatonicSolids.CreateCube();
		static Mesh unscaledLineMesh = PlatonicSolids.CreateCube();

		public static void Render(Mesh meshToRender, IColorType partColor, Matrix4X4 transform, RenderTypes renderType)
		{
			if (meshToRender != null)
			{
				GL.Color4(partColor.Red0To255, partColor.Green0To255, partColor.Blue0To255, partColor.Alpha0To255);

				if (partColor.Alpha0To1 < 1)
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

					case RenderTypes.Overhang:
					case RenderTypes.Shaded:
						DrawToGL(meshToRender);
						break;

					case RenderTypes.Polygons:
					case RenderTypes.Outlines:
						DrawWithWireOverlay(meshToRender, renderType);
						break;
				}

				GL.PopMatrix();
			}
		}

		private static void DrawToGL(Mesh meshToRender)
		{
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

				if(subMesh.UseVertexColors)
				{
					GL.EnableClientState(ArrayCap.ColorArray);
				}

				GL.EnableClientState(ArrayCap.NormalArray);
				GL.EnableClientState(ArrayCap.VertexArray);
				unsafe
				{
					fixed (VertexTextureData* pTextureData = subMesh.textureData.Array)
					{
						fixed(VertexColorData* pColorData = subMesh.colorData.Array)
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

		private static void DrawWithWireOverlay(Mesh meshToRender, RenderTypes renderType)
		{
			GLMeshTrianglePlugin glMeshPlugin = GLMeshTrianglePlugin.Get(meshToRender);

			GL.Enable(EnableCap.PolygonOffsetFill);
			GL.PolygonOffset(1, 1);

			DrawToGL(meshToRender);

			GL.Color4(0, 0, 0, 255);

			GL.PolygonOffset(0, 0);
			GL.Disable(EnableCap.PolygonOffsetFill);
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