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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public enum RenderTypes
	{
		Hidden,
		Shaded,
		Outlines,
		NonManifold,
		Polygons,
		Overhang,
		Wireframe
	}

	// NOTE: GL render path is deprecated and will be removed. D3D is the active render path.
	public static class RenderHelper
	{
		private const float GL_MODULATE = (float)0x2100;

		private const float GL_REPLACE = (float)0x1E01;

		private static int suppressBedShadowCastingDepth;

		private static int legacyMeshFallbackCount;

		/// <summary>
		/// How many times a mesh has fallen through to the legacy immediate-mode GL draw below
		/// <i>while a scene frame was open</i>, process wide.
		/// </summary>
		/// <remarks>
		/// Zero is the only correct value: inside a scene frame the native renderer owns every mesh, and
		/// the WebGPU backend's compat layer does not implement the client-array draws the fallback needs -
		/// so a non-zero count is a gap in <c>INativeSceneRenderer.CanRender</c> that would render nothing
		/// (or throw) on that backend. Tests assert against it rather than against pixels, because a mesh
		/// that silently vanished looks exactly like a mesh that was never queued.
		/// <para>
		/// Draws outside a scene frame are not counted; those are the 2D and out-of-frame sites the port
		/// plan closes in Phase 5.
		/// </para>
		/// </remarks>
		public static int LegacyMeshFallbackCount => Volatile.Read(ref legacyMeshFallbackCount);

		/// <summary>Resets <see cref="LegacyMeshFallbackCount"/>, so a test can measure one frame.</summary>
		public static void ResetLegacyMeshFallbackCount() => Interlocked.Exchange(ref legacyMeshFallbackCount, 0);

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

		internal static void PrepareFor3DLineRender(GL gl, bool doDepthTest)
		{
			gl.Disable(EnableCap.Texture2D);

			gl.Enable(EnableCap.Blend);
			gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			gl.Disable(EnableCap.Lighting);
			if (doDepthTest)
			{
				gl.Enable(EnableCap.DepthTest);
			}
			else
			{
				gl.Disable(EnableCap.DepthTest);
			}
		}

		public static MeshRenderCommand CreateBedShadowCommand(MeshRenderCommand command)
		{
			if (command == null)
			{
				return null;
			}

			return new MeshRenderCommand
			{
				Mesh = command.Mesh,
				Color = command.Color,
				Transform = command.Transform,
				RenderType = command.RenderType,
				MeshToViewTransform = command.MeshToViewTransform,
				WireFrameColor = command.WireFrameColor,
				MeshChanged = command.MeshChanged,
				BlendTexture = command.BlendTexture,
				AllowBspRendering = command.AllowBspRendering,
				ForceCullBackFaces = false,
				IsSelected = command.IsSelected,
				OverrideFaceColors = command.OverrideFaceColors,
				AlphaMultiplier = command.AlphaMultiplier,
				Unlit = command.Unlit,
				CastsBedShadow = command.CastsBedShadow,
			};
		}

		public static bool ResolveBedShadowCasting(bool castsBedShadow)
		{
			return castsBedShadow
				&& suppressBedShadowCastingDepth == 0;
		}

		public static bool ShouldRenderInBedShadow(MeshRenderCommand command, RectangleDouble bedBounds)
		{
			if (command?.Mesh == null
				|| !command.CastsBedShadow)
			{
				return false;
			}

			switch (command.RenderType)
			{
				case RenderTypes.Shaded:
				case RenderTypes.Outlines:
				case RenderTypes.NonManifold:
				case RenderTypes.Wireframe:
				case RenderTypes.Polygons:
					break;

				default:
					return false;
			}

			var bounds = command.Mesh.GetAxisAlignedBoundingBox(command.Transform);
			if (bounds.MaxXYZ.Z <= 0)
			{
				return false;
			}

			return !(bounds.MaxXYZ.X < bedBounds.Left
				|| bounds.MinXYZ.X > bedBounds.Right
				|| bounds.MaxXYZ.Y < bedBounds.Bottom
				|| bounds.MinXYZ.Y > bedBounds.Top);
		}

		public static IDisposable SuppressBedShadowCasting()
		{
			suppressBedShadowCastingDepth++;
			return new DisposableScope(() => suppressBedShadowCastingDepth--);
		}

		internal static void Render(GL gl,
			Mesh meshToRender,
			Color partColor,
			RenderTypes renderType = RenderTypes.Shaded,
			Matrix4X4? meshToViewTransform = null,
			Color wireFrameColor = default(Color),
            Action meshChanged = null,
			bool blendTexture = true,
			bool forceCullBackFaces = true,
			bool castsBedShadow = true,
			bool isSelected = false,
			bool overrideFaceColors = false,
			float alphaMultiplier = 1.0f)
		{
			Render(gl, meshToRender, partColor, Matrix4X4.Identity, renderType, meshToViewTransform, wireFrameColor, meshChanged, blendTexture, forceCullBackFaces: forceCullBackFaces, castsBedShadow: castsBedShadow, isSelected: isSelected, overrideFaceColors: overrideFaceColors, alphaMultiplier: alphaMultiplier);
		}

		internal static void Render(GL gl,
			Mesh meshToRender,
			Color color,
            Matrix4X4 transform,
			RenderTypes renderType = RenderTypes.Shaded,
			Matrix4X4? meshToViewTransform = null,
			Color wireFrameColor = default(Color),
            Action meshChanged = null,
			bool blendTexture = true,
			bool allowBspRendering = false,
			bool forceCullBackFaces = true,
			bool castsBedShadow = true,
			bool isSelected = false,
			bool overrideFaceColors = false,
			float alphaMultiplier = 1.0f)
		{
			if (meshToRender != null)
			{
				if (gl?.GpuContext is INativeSceneRenderer nativeSceneRenderer)
				{
					var command = new MeshRenderCommand
					{
						Mesh = meshToRender,
						Color = color,
						Transform = transform,
						RenderType = renderType,
						MeshToViewTransform = meshToViewTransform,
						WireFrameColor = wireFrameColor,
						MeshChanged = meshChanged,
						BlendTexture = blendTexture,
						AllowBspRendering = allowBspRendering,
						ForceCullBackFaces = forceCullBackFaces,
						CastsBedShadow = ResolveBedShadowCasting(castsBedShadow),
						IsSelected = isSelected,
						OverrideFaceColors = overrideFaceColors,
						AlphaMultiplier = alphaMultiplier,
					};

					if (nativeSceneRenderer.CanRender(command)
						&& nativeSceneRenderer.TryRender(command))
					{
						return;
					}

					if (nativeSceneRenderer.IsSceneRenderingActive)
					{
						// Inside a scene frame the native renderer is supposed to draw everything - the
						// compat layer behind the WebGPU backend does not implement the client-array draws
						// the code below needs, so reaching here is a hole in CanRender rather than a
						// graceful degradation. Counted so tests can assert it stays at zero; see
						// LegacyMeshFallbackCount.
						Interlocked.Increment(ref legacyMeshFallbackCount);
					}
				}

				gl.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);

				gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

				if (color.Alpha0To1 < 1)
				{
					if (forceCullBackFaces)
					{
						gl.Enable(EnableCap.CullFace);
					}
					else
					{
						// by default render back faces of transparent objects
						gl.Disable(EnableCap.CullFace);
					}
					gl.Enable(EnableCap.Blend);
				}
				else
				{
					gl.Enable(EnableCap.CullFace);
					gl.Enable(EnableCap.Blend);
				}

				gl.MatrixMode(MatrixMode.Modelview);
				gl.PushMatrix();
				gl.MultMatrix(transform.GetAsFloatArray());

				switch (renderType)
				{
					case RenderTypes.Hidden:
						break;

					case RenderTypes.Polygons:
					case RenderTypes.Outlines:
					case RenderTypes.NonManifold:
						if (color.Alpha0To255 > 0)
						{
							gl.Enable(EnableCap.PolygonOffsetFill);
							gl.PolygonOffset(1, 1);
							DrawToGL(gl, meshToRender, color.Alpha0To1 < 1, meshToViewTransform, allowBspRendering: allowBspRendering);
							gl.PolygonOffset(0, 0);
							gl.Disable(EnableCap.PolygonOffsetFill);
						}

						DrawWireOverlay(gl, meshToRender, renderType, wireFrameColor, meshChanged);
						break;

					case RenderTypes.Wireframe:
						DrawWireOverlay(gl, meshToRender, renderType, wireFrameColor);
						break;

					case RenderTypes.Overhang:
						OverhangRender.EnsureUpdated(gl, meshToRender, transform);
						DrawToGL(gl, meshToRender, color.Alpha0To1 < 1, meshToViewTransform);
						break;

					case RenderTypes.Shaded:
						DrawToGL(gl, meshToRender, color.Alpha0To1 < 1, meshToViewTransform, blendTexture, allowBspRendering);
						break;
				}

				gl.PopMatrix();
			}
		}

		private static void DrawToGL(GL gl, Mesh meshToRender, bool isTransparent, Matrix4X4? meshToViewTransform, bool blendTexture = true, bool allowBspRendering = true)
		{
			if (!blendTexture)
			{
				// Turn off default GL_MODULATE mode
				gl.TexEnv(TextureEnvironmentTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, GL_REPLACE);
			}

			var glMeshPlugin = MeshTrianglePlugin.Get(gl, meshToRender);
			for (int i = 0; i < glMeshPlugin.subMeshs.Count; i++)
			{
				SubTriangleMesh subMesh = glMeshPlugin.subMeshs[i];
				// Make sure the GLMeshPlugin has a reference to hold onto the image so it does not go away before this.
				if (subMesh.texture != null)
				{
					if (subMesh.texture.HasTransparency)
					{
						gl.Enable(EnableCap.Blend);
					}

					var glPlugin = ImageTexturePlugin.GetImageTexturePlugin(gl, subMesh.texture, true);
					gl.Enable(EnableCap.Texture2D);
					gl.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					gl.EnableClientState(ArrayCap.TextureCoordArray);
				}
				else
				{
					gl.Disable(EnableCap.Texture2D);
					gl.DisableClientState(ArrayCap.TextureCoordArray);
				}

				if (subMesh.UseVertexColors)
				{
					gl.EnableClientState(ArrayCap.ColorArray);
				}

				gl.EnableClientState(ArrayCap.NormalArray);
				gl.EnableClientState(ArrayCap.VertexArray);
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
									gl.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(pPosition));
									gl.NormalPointer(NormalPointerType.Float, 0, new IntPtr(pNormalData));
									gl.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pTextureData));
									if (pColorData != null)
									{
										gl.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pColorData));
									}

									gl.DrawArrays(BeginMode.Triangles, 0, subMesh.positionData.Count);
								}
							}
						}
					}
				}

				gl.DisableClientState(ArrayCap.NormalArray);
				gl.DisableClientState(ArrayCap.VertexArray);
				gl.DisableClientState(ArrayCap.TextureCoordArray);
				gl.DisableClientState(ArrayCap.ColorArray);

				gl.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(0));
				gl.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(0));
				gl.NormalPointer(NormalPointerType.Float, 0, new IntPtr(0));
				gl.VertexPointer(3, VertexPointerType.Float, 0, new IntPtr(0));

				if (subMesh.texture != null)
				{
					gl.DisableClientState(ArrayCap.TextureCoordArray);
				}
			}

			if (!blendTexture)
			{
				// Restore default GL_MODULATE mode
				gl.TexEnv(TextureEnvironmentTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, GL_MODULATE);
			}
		}

		// There can be a singleton of this because GL must always render on the UI thread and can't overlap this array
		private static void DrawToGLZSorted(GL gl, Mesh mesh, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			ImageBuffer lastFaceTexture = null;

			// var zSortedFaceList2 = mesh.GetFacesInVisibiltyOrder(meshToViewTransform);
			var zSortedFaceList = FaceBspTree.GetFacesInVisibiltyOrder(mesh, mesh.FaceBspTree, meshToViewTransform, invMeshToViewTransform);
			foreach (var face in zSortedFaceList)
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
						var glPlugin = ImageTexturePlugin.GetImageTexturePlugin(gl, faceTexture.image, true);
						gl.Enable(EnableCap.Texture2D);
						gl.BindTexture(TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					}
					else
					{
						gl.Disable(EnableCap.Texture2D);
					}

					lastFaceTexture = faceTexture.image;
				}

				gl.Begin(BeginMode.Triangles);
				var normal = mesh.Faces[face].normal;
				gl.Normal3(normal.X, normal.Y, normal.Z);
				// load up the uvs
				if (faceTexture != null)
				{
					gl.TexCoord2(faceTexture.uv0);
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v0]);

					gl.TexCoord2(faceTexture.uv1);
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v1]);

					gl.TexCoord2(faceTexture.uv2);
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v2]);
				}
				else
				{
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v0]);
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v1]);
					gl.Vertex3(mesh.Vertices[mesh.Faces[face].v2]);
				}

				gl.End();
			}
		}

        private static void DrawWireOverlay(GL gl, Mesh meshToRender, RenderTypes renderType, Color wireColor, Action meshChanged = null)
        {
            gl.Disable(EnableCap.Lighting);
            gl.DisableClientState(ArrayCap.TextureCoordArray);
            IEdgeLinesContainer edgeLinesContainer = null;
            if (renderType == RenderTypes.Outlines)
            {
                edgeLinesContainer = MeshWirePlugin.Get(meshToRender, wireColor, MathHelper.Tau / 8, meshChanged);
            }
            else if (renderType == RenderTypes.NonManifold)
            {
                edgeLinesContainer = MeshNonManifoldPlugin.Get(meshToRender, wireColor, meshChanged);
            }
            else
            {
                edgeLinesContainer = MeshWirePlugin.Get(meshToRender, wireColor);
            }

            gl.EnableClientState(ArrayCap.VertexArray);
            gl.EnableClientState(ArrayCap.ColorArray);

            VectorPOD<WireVertexData> edgeLines = edgeLinesContainer.EdgeLines;
            unsafe
            {
                fixed (WireVertexData* pv = edgeLines.Array)
                {
                    int stride = WireVertexData.Stride;

                    // Color pointer points to the start of the structure (r,g,b bytes)
                    gl.ColorPointer(4, ColorPointerType.UnsignedByte, stride, new IntPtr(pv));

                    // Vertex pointer points to the float positions, after the color + padding byte
                    gl.VertexPointer(3, VertexPointerType.Float, stride, new IntPtr(pv) + 4);

                    gl.DrawArrays(BeginMode.Lines, 0, edgeLines.Count);
                }
            }

            gl.DisableClientState(ArrayCap.ColorArray);
            gl.DisableClientState(ArrayCap.VertexArray);
            gl.Enable(EnableCap.Lighting);
        }

        internal static void SetGlContext(GL gl, WorldView worldView, RectangleDouble screenRect, LightingData lighting)
		{
			gl.ClearDepth(1.0);
			gl.Clear(ClearBufferMask.DepthBufferBit);   // Clear the Depth Buffer

			gl.PushAttrib(AttribMask.ViewportBit);
			gl.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

			gl.ShadeModel(ShadingModel.Smooth);

			gl.FrontFace(FrontFaceDirection.Ccw);
			gl.CullFace(CullFaceMode.Back);

			gl.DepthFunc(DepthFunction.Lequal);

			gl.Disable(EnableCap.DepthTest);
			// ClearToGradient();

			gl.Light(LightName.Light0, LightParameter.Ambient, lighting.AmbientLight);
			gl.Light(LightName.Light0, LightParameter.Diffuse, lighting.DiffuseLight0);
			gl.Light(LightName.Light0, LightParameter.Specular, lighting.SpecularLight0);

			gl.Light(LightName.Light1, LightParameter.Diffuse, lighting.DiffuseLight1);
			gl.Light(LightName.Light1, LightParameter.Specular, lighting.SpecularLight1);

			gl.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

			gl.Enable(EnableCap.Light0);
			gl.Enable(EnableCap.Light1);
			gl.Enable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Blend);
			gl.Enable(EnableCap.Normalize);
			gl.Enable(EnableCap.Lighting);
			gl.Enable(EnableCap.ColorMaterial);

			var lightDirectionVector = new Vector3(lighting.LightDirection0[0], lighting.LightDirection0[1], lighting.LightDirection0[2]);
			lightDirectionVector.Normalize();
			lighting.LightDirection0[0] = (float)lightDirectionVector.X;
			lighting.LightDirection0[1] = (float)lightDirectionVector.Y;
			lighting.LightDirection0[2] = (float)lightDirectionVector.Z;
			gl.Light(LightName.Light0, LightParameter.Position, lighting.LightDirection0);
			gl.Light(LightName.Light1, LightParameter.Position, lighting.LightDirection1);

			// set the projection matrix
			gl.MatrixMode(MatrixMode.Projection);
			gl.PushMatrix();
			gl.LoadMatrix(worldView.ProjectionMatrix.GetAsDoubleArray());

			// set the modelview matrix
			gl.MatrixMode(MatrixMode.Modelview);
			gl.PushMatrix();
			gl.LoadMatrix(worldView.ModelviewMatrix.GetAsDoubleArray());
		}

		internal static void UnsetGlContext(GL gl)
		{
			gl.MatrixMode(MatrixMode.Projection);
			gl.PopMatrix();

			gl.MatrixMode(MatrixMode.Modelview);
			gl.PopMatrix();

			gl.Disable(EnableCap.ColorMaterial);
			gl.Disable(EnableCap.Lighting);
			gl.Disable(EnableCap.Light0);
			gl.Disable(EnableCap.Light1);

			gl.Disable(EnableCap.Normalize);
			gl.Disable(EnableCap.Blend);
			gl.Disable(EnableCap.DepthTest);

			gl.PopAttrib();
		}

		private sealed class DisposableScope : IDisposable
		{
			private readonly Action onDispose;
			private bool disposed;

			public DisposableScope(Action onDispose)
			{
				this.onDispose = onDispose;
			}

			public void Dispose()
			{
				if (disposed)
				{
					return;
				}

				disposed = true;
				onDispose?.Invoke();
			}
		}
	}
}
