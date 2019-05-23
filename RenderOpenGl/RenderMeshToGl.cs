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
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public enum RenderTypes
	{
		Hidden,
		Shaded,
		Outlines,
		Manifold,
		Polygons,
		Overhang,
		Materials,
		Wireframe
	}

	public static class GLHelper
	{
		private static Mesh scaledLineMesh = PlatonicSolids.CreateCube();

		private static Mesh unscaledLineMesh = PlatonicSolids.CreateCube();

		private const float GL_MODULATE = (float)0x2100;

		private const float GL_REPLACE = (float)0x1E01;

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

		public static void Render(Mesh meshToRender, Color partColor, RenderTypes renderType = RenderTypes.Shaded, Matrix4X4? meshToViewTransform = null, Color wireFrameColor = default(Color), Action meshChanged = null, bool blendTexture = true)
		{
			Render(meshToRender, partColor, Matrix4X4.Identity, renderType, meshToViewTransform, wireFrameColor, meshChanged, blendTexture);
		}

		public static void Render(Mesh meshToRender, Color color, Matrix4X4 transform, RenderTypes renderType, Matrix4X4? meshToViewTransform = null, Color wireFrameColor = default(Color), Action meshChanged = null, bool blendTexture = true, bool allowBspRendering = true)
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
						if (color.Alpha0To255 > 0)
						{
							GL.Enable(EnableCap.PolygonOffsetFill);
							GL.PolygonOffset(1, 1);
							DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform, allowBspRendering: allowBspRendering);
							GL.PolygonOffset(0, 0);
							GL.Disable(EnableCap.PolygonOffsetFill);
						}

						DrawWireOverlay(meshToRender, renderType, wireFrameColor, meshChanged);
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
						DrawToGL(meshToRender, color.Alpha0To1 < 1, meshToViewTransform, blendTexture, allowBspRendering);
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

		public static void Render3DLineNoPrep(this WorldView world, Frustum clippingFrustum, Vector3Float start, Vector3Float end, Color color, double width = 1)
		{
			world.Render3DLineNoPrep(clippingFrustum, new Vector3(start), new Vector3(end), new Color(color), width);
		}

		public static void Render3DLineNoPrep(this WorldView world, Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, double width = 1)
		{
			if (clippingFrustum.ClipLine(ref start, ref end))
			{
				double unitsPerPixelStart = world.GetWorldUnitsPerScreenPixelAtPosition(start);
				double unitsPerPixelEnd = world.GetWorldUnitsPerScreenPixelAtPosition(end);

				Vector3 delta = start - end;
				var deltaLength = delta.Length;
				var rotateTransform = Matrix4X4.CreateRotation(new Quaternion(Vector3.UnitX + new Vector3(.0001, -.00001, .00002), -delta / deltaLength));
				var scaleTransform = Matrix4X4.CreateScale(deltaLength, 1, 1);
				Vector3 lineCenter = (start + end) / 2;
				Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(lineCenter);

				var startScale = unitsPerPixelStart * width;
				var endScale = unitsPerPixelEnd * width;
				for (int i = 0; i < unscaledLineMesh.Vertices.Count; i++)
				{
					var vertexPosition = unscaledLineMesh.Vertices[i];
					if (vertexPosition.X < 0)
					{
						scaledLineMesh.Vertices[i] = new Vector3Float(vertexPosition.X, vertexPosition.Y * startScale, vertexPosition.Z * startScale);
					}
					else
					{
						scaledLineMesh.Vertices[i] = new Vector3Float(vertexPosition.X, vertexPosition.Y * endScale, vertexPosition.Z * endScale);
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
						// GL.Disable(EnableCap.Blend);
					}

					GL.MatrixMode(MatrixMode.Modelview);
					GL.PushMatrix();
					GL.MultMatrix(lineTransform.GetAsFloatArray());

					GL.Begin(BeginMode.Triangles);
					for (int faceIndex = 0; faceIndex < scaledLineMesh.Faces.Count; faceIndex++)
					{
						var face = scaledLineMesh.Faces[faceIndex];
						var vertices = scaledLineMesh.Vertices;
						var position = vertices[face.v0];
						GL.Vertex3(position.X, position.Y, position.Z);
						position = vertices[face.v1];
						GL.Vertex3(position.X, position.Y, position.Z);
						position = vertices[face.v2];
						GL.Vertex3(position.X, position.Y, position.Z);
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

		private static void DrawToGL(Mesh meshToRender, bool isTransparent, Matrix4X4? meshToViewTransform, bool blendTexture = true, bool allowBspRendering = true)
		{
			if (!blendTexture)
			{
				// Turn off default GL_MODULATE mode
				GL.TexEnv(TextureEnvironmentTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, GL_REPLACE);
			}

			if (meshToViewTransform != null
				&& isTransparent
				&& meshToRender.FaceBspTree != null
				&& meshToRender.Faces.Count > 0
				&& allowBspRendering)
			{
				var invMeshToViewTransform = meshToViewTransform.Value;
				invMeshToViewTransform.Invert();
				DrawToGLUsingBsp(meshToRender, meshToViewTransform.Value, invMeshToViewTransform);

				if (!blendTexture)
				{
					// Restore default GL_MODULATE mode
					GL.TexEnv(TextureEnvironmentTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, GL_MODULATE);
				}

				return;
			}

			var glMeshPlugin = GLMeshTrianglePlugin.Get(meshToRender);
			for (int i = 0; i < glMeshPlugin.subMeshs.Count; i++)
			{
				SubTriangleMesh subMesh = glMeshPlugin.subMeshs[i];
				// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
				if (subMesh.texture != null)
				{
					if (subMesh.texture.HasTransparency)
					{
						GL.Enable(EnableCap.Blend);
					}

					var glPlugin = ImageGlPlugin.GetImageGlPlugin(subMesh.texture, true);
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

			if (!blendTexture)
			{
				// Restore default GL_MODULATE mode
				GL.TexEnv(TextureEnvironmentTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, GL_MODULATE);
			}
		}

		// There can be a singleton of this because GL must always render on the UI thread and can't overlap this array
		private static void DrawToGLUsingBsp(Mesh mesh, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			ImageBuffer lastFaceTexture = null;
			var bspFaceList = FaceBspTree.GetFacesInVisibiltyOrder(mesh, mesh.FaceBspTree, meshToViewTransform, invMeshToViewTransform);
			foreach (var face in bspFaceList)
			{
				if (face == -1)
				{
					continue;
				}

				FaceTextureData faceTexture;
				mesh.FaceTextures.TryGetValue(face, out faceTexture);
				if (faceTexture != null
					&& faceTexture.image != lastFaceTexture)
				{
					// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
					if (faceTexture != null)
					{
						var glPlugin = ImageGlPlugin.GetImageGlPlugin(faceTexture.image, true);
						GL.Enable(EnableCap.Texture2D);
						GL.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					}
					else
					{
						GL.Disable(EnableCap.Texture2D);
					}

					lastFaceTexture = faceTexture.image;
				}

				GL.Begin(BeginMode.Triangles);
				var normal = mesh.Faces[face].normal;
				GL.Normal3(normal.X, normal.Y, normal.Z);
				// load up the uvs
				if (faceTexture != null)
				{
					GL.TexCoord2(faceTexture.uv0);
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v0]);

					GL.TexCoord2(faceTexture.uv1);
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v1]);

					GL.TexCoord2(faceTexture.uv2);
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v2]);
				}
				else
				{
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v0]);
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v1]);
					GL.Vertex3(mesh.Vertices[mesh.Faces[face].v2]);
				}

				GL.End();
			}
		}

		private static void DrawWireOverlay(Mesh meshToRender, RenderTypes renderType, Color color, Action meshChanged = null)
		{
			GL.Color4(color.red, color.green, color.blue, color.alpha == 0 ? 255 : color.alpha);

			GL.Disable(EnableCap.Lighting);

			GL.DisableClientState(ArrayCap.TextureCoordArray);
			GLMeshWirePlugin glWireMeshPlugin = null;
			if (renderType == RenderTypes.Outlines)
			{
				glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender, MathHelper.Tau / 8, meshChanged);
			}
			else
			{
				glWireMeshPlugin = GLMeshWirePlugin.Get(meshToRender);
			}

			GL.EnableClientState(ArrayCap.VertexArray);
			VectorPOD<WireVertexData> edgeLines = glWireMeshPlugin.EdgeLines;

			unsafe
			{
				fixed (WireVertexData* pv = edgeLines.Array)
				{
					GL.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(pv));
					GL.DrawArrays(BeginMode.Lines, 0, edgeLines.Count);
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
			// ClearToGradient();

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

			var lightDirectionVector = new Vector3(lighting.LightDirection0[0], lighting.LightDirection0[1], lighting.LightDirection0[2]);
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