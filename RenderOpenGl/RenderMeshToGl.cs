﻿/*
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
using MatterHackers.Agg.Image;
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

		public static void ExtendLineEnds(ref Vector3 start, ref Vector3 end, double length)
		{
			// extend both sides
			ExtendLineEnd(start, ref end, length);
			ExtendLineEnd(end, ref start, length);
		}

		public static void ExtendLineEnd(Vector3 start, ref Vector3 end, double length)
		{
			// extend the start position by the length in the direction of the line
			var direction = (end - start).GetNormal();
			end += direction * length;
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
				GL.Enable(EnableCap.CullFace);

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
						OverhangRender.EnsureUpdated(meshToRender, transform);
						DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform);
						break;

					case RenderTypes.Shaded:
					case RenderTypes.Materials:
						DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform);
						break;
				}

				GL.PopMatrix();
			}
		}

		/// <summary>
		/// Draw a line in the scene in 3D but scale it such that it appears as a 2D line in the view.
		/// If drawing lots of lines call with a pre-calculated clipping frustum.
		/// </summary>
		/// <param name="world"></param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="color"></param>
		/// <param name="doDepthTest"></param>
		/// <param name="width"></param>
		public static void Render3DLine(this WorldView world, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			world.Render3DLine(GetClippingFrustum(world), start, end, color, doDepthTest, width);
		}

		/// <summary>
		/// Draw a line in the scene in 3D but scale it such that it appears as a 2D line in the view.
		/// </summary>
		/// <param name="world"></param>
		/// <param name="clippingFrustum">This is a cache of the frustum from world.
		/// Much faster to pass this way if drawing lots of lines.</param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="color"></param>
		/// <param name="doDepthTest"></param>
		/// <param name="width"></param>
		public static void Render3DLine(this WorldView world, Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			PrepareFor3DLineRender(doDepthTest);
			world.Render3DLineNoPrep(clippingFrustum, start, end, color, width);
		}

		public static void Render3DLineNoPrep(this WorldView world, Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, double width = 1)
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
					if (vertexPosition.X < 0)
					{
						scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.X, vertexPosition.Y * startScale, vertexPosition.Z * startScale);
					}
					else
					{
						scaledLineMesh.Vertices[i].Position = new Vector3(vertexPosition.X, vertexPosition.Y * endScale, vertexPosition.Z * endScale);
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
							GL.Vertex3(vertex.p0.X, vertex.p0.Y, vertex.p0.Z);
							GL.Vertex3(vertex.p1.X, vertex.p1.Y, vertex.p1.Z);
							GL.Vertex3(vertex.p2.X, vertex.p2.Y, vertex.p2.Z);
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
				&& meshToRender.FaceBspTree != null
				&& meshToRender.Faces.Count > 0)
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
					if(subMesh.texture.HasTransparency)
					{
						GL.Enable(EnableCap.Blend);
					}
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
		private static void DrawToGLUsingBsp(Mesh meshToRender, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			ImageBuffer lastFaceTexture = null;
			var bspFaceList = FaceBspTree.GetFacesInVisibiltyOrder(meshToRender.Faces, meshToRender.FaceBspTree, meshToViewTransform, invMeshToViewTransform);
			foreach (var face in bspFaceList)
			{
				if (face == null)
				{
					continue;
				}

				ImageBuffer faceTexture;
				meshToRender.FaceTexture.TryGetValue((face, 0), out faceTexture);
				if (faceTexture != lastFaceTexture)
				{
					// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
					if (faceTexture != null)
					{
						ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(faceTexture, true);
						GL.Enable(EnableCap.Texture2D);
						GL.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					}
					else
					{
						GL.Disable(EnableCap.Texture2D);
					}

					lastFaceTexture = faceTexture;
				}

				GL.Begin(BeginMode.Triangles);
				GL.Normal3(face.Normal.X, face.Normal.Y, face.Normal.Z);
				// load up the uvs
				if (faceTexture != null)
				{
					foreach (var vertex in face.AsUvTriangles())
					{
						GL.TexCoord2(vertex.v0.uv);
						GL.Vertex3(vertex.v0.p);

						GL.TexCoord2(vertex.v1.uv);
						GL.Vertex3(vertex.v1.p);

						GL.TexCoord2(vertex.v2.uv);
						GL.Vertex3(vertex.v2.p);
					}
				}
				else
				{
					foreach (var vertex in face.AsTriangles())
					{
						GL.Vertex3(vertex.p0);
						GL.Vertex3(vertex.p1);
						GL.Vertex3(vertex.p2);
					}
				}
				GL.End();
			}
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

		public static void SetGlContext(WorldView worldView, RectangleDouble screenRect, LightingData lighting)
		{
			GL.ClearDepth(1.0);
			GL.Clear(ClearBufferMask.DepthBufferBit);   // Clear the Depth Buffer

			GL.PushAttrib(AttribMask.ViewportBit);
			GL.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

			GL.ShadeModel(ShadingModel.Smooth);

			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.CullFace(CullFaceMode.Back);

			GL.DepthFunc(DepthFunction.Lequal);

			GL.Disable(EnableCap.DepthTest);
			//ClearToGradient();

			GL.Light(LightName.Light0, LightParameter.Ambient, lighting.AmbientLight);
			GL.Light(LightName.Light0, LightParameter.Diffuse, lighting.DiffuseLight0);
			GL.Light(LightName.Light0, LightParameter.Specular, lighting.SpecularLight0);

			GL.Light(LightName.Light1, LightParameter.Diffuse, lighting.DiffuseLight1);
			GL.Light(LightName.Light1, LightParameter.Specular, lighting.SpecularLight1);

			GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.Light1);
			GL.Enable(EnableCap.DepthTest);
			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.Normalize);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.ColorMaterial);

			Vector3 lightDirectionVector = new Vector3(lighting.LightDirection0[0], lighting.LightDirection0[1], lighting.LightDirection0[2]);
			lightDirectionVector.Normalize();
			lighting.LightDirection0[0] = (float)lightDirectionVector.X;
			lighting.LightDirection0[1] = (float)lightDirectionVector.Y;
			lighting.LightDirection0[2] = (float)lightDirectionVector.Z;
			GL.Light(LightName.Light0, LightParameter.Position, lighting.LightDirection0);
			GL.Light(LightName.Light1, LightParameter.Position, lighting.LightDirection1);

			// set the projection matrix
			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadMatrix(worldView.ProjectionMatrix.GetAsDoubleArray());

			// set the modelview matrix
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.LoadMatrix(worldView.ModelviewMatrix.GetAsDoubleArray());
		}

		public static void UnsetGlContext()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.PopMatrix();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();

			GL.Disable(EnableCap.ColorMaterial);
			GL.Disable(EnableCap.Lighting);
			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Light1);

			GL.Disable(EnableCap.Normalize);
			GL.Disable(EnableCap.Blend);
			GL.Disable(EnableCap.DepthTest);

			GL.PopAttrib();
		}


	}
}