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

using MatterHackers.Agg.Image;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public partial class VorticeD3DGl
	{
		private SceneRenderContext activeSceneRenderContext;
		private Matrix4X4 savedSceneModelView = Matrix4X4.Identity;
		private Matrix4X4 savedSceneProjection = Matrix4X4.Identity;

		public bool IsSceneRenderingActive => activeSceneRenderContext != null;

		public void BeginSceneRendering(SceneRenderContext context)
		{
			activeSceneRenderContext = context;
			savedSceneModelView = modelViewStack.Peek();
			savedSceneProjection = projectionStack.Peek();
			ClearQueuedSceneEffects();
		}

		public bool CanRender(MeshRenderCommand command)
		{
			return activeSceneRenderContext != null
				&& command?.Mesh != null
				&& (command.RenderType == RenderTypes.Shaded
					|| command.RenderType == RenderTypes.Outlines
					|| command.RenderType == RenderTypes.NonManifold
					|| command.RenderType == RenderTypes.Wireframe
					|| command.RenderType == RenderTypes.Polygons);
		}

		public void EndSceneRendering()
		{
			RenderQueuedSceneEffects();
			ClearQueuedSceneEffects();
			activeSceneRenderContext = null;
			SetSceneMatrices(savedSceneModelView, savedSceneProjection);
		}

		public bool TryRender(MeshRenderCommand command)
		{
			if (!CanRender(command))
			{
				return false;
			}

			QueueSceneCommand(command);
			return true;
		}

		public bool TryRender(BedRenderCommand command)
		{
			if (activeSceneRenderContext == null
				|| command?.Mesh == null
				|| command.TopBaseTexture == null
				|| command.UnderBaseTexture == null)
			{
				return false;
			}

			queuedBedCommand = command;
			return true;
		}

		private void ApplySceneLighting(LightingData lighting)
		{
			if (lighting == null)
			{
				return;
			}

			// In OpenGL, glLightfv(GL_POSITION) transforms the direction by the current
			// modelview matrix. RenderHelper.SetGlContext sets lights before loading the
			// camera modelview, so the GL modelview is identity — placing lights in eye
			// space (camera-attached). We must match that here: temporarily set identity
			// so the Light(Position) transform produces eye-space directions.
			var savedModelView = modelViewStack.Peek();
			ReplaceStackTop(modelViewStack, Matrix4X4.Identity);

			Light(LightName.Light0, LightParameter.Ambient, lighting.AmbientLight);
			Light(LightName.Light0, LightParameter.Diffuse, lighting.DiffuseLight0);
			Light(LightName.Light0, LightParameter.Specular, lighting.SpecularLight0);
			Light(LightName.Light0, LightParameter.Position, lighting.LightDirection0);

			Light(LightName.Light1, LightParameter.Diffuse, lighting.DiffuseLight1);
			Light(LightName.Light1, LightParameter.Specular, lighting.SpecularLight1);
			Light(LightName.Light1, LightParameter.Position, lighting.LightDirection1);

			ReplaceStackTop(modelViewStack, savedModelView);

			Enable((int)EnableCap.Light0);
			Enable((int)EnableCap.Light1);
			Enable((int)EnableCap.Normalize);
			Enable((int)EnableCap.Lighting);
			Enable((int)EnableCap.ColorMaterial);
			ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
		}

		private void ConfigureShadedMeshState(MeshRenderCommand command)
		{
			FrontFace(FrontFaceDirection.Ccw);
			CullFace(CullFaceMode.Back);
			DepthFunc((int)DepthFunction.Lequal);
			Enable((int)EnableCap.DepthTest);
			Enable((int)EnableCap.Blend);
			BlendFunc((int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha);

			if (command.Color.Alpha0To1 < 1)
			{
				if (command.ForceCullBackFaces)
				{
					Enable((int)EnableCap.CullFace);
				}
				else
				{
					Disable((int)EnableCap.CullFace);
				}
			}
			else
			{
				Enable((int)EnableCap.CullFace);
			}

			ApplySceneLighting(activeSceneRenderContext.Lighting);
		}

		private void RenderShadedMesh(MeshRenderCommand command)
		{
			ConfigureShadedMeshState(command);
			SetSceneMatrices(command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix, activeSceneRenderContext.WorldView.ProjectionMatrix);

			var glMeshPlugin = MeshTrianglePlugin.Get(command.Mesh);
			foreach (var subMesh in glMeshPlugin.subMeshs)
			{
				bool useTexture = subMesh.texture != null;
				if (useTexture)
				{
					var glPlugin = ImageTexturePlugin.GetImageTexturePlugin(subMesh.texture, true);
					Enable((int)EnableCap.Texture2D);
					BindTexture((int)TextureTarget.Texture2D, glPlugin.GLTextureHandle);
					EnableClientState(ArrayCap.TextureCoordArray);
				}
				else
				{
					Disable((int)EnableCap.Texture2D);
					DisableClientState(ArrayCap.TextureCoordArray);
				}

				// Use per-vertex face colors when the mesh has them, unless an ancestor
				// in the scene hierarchy has an explicit color override.
				bool hasFaceColors = command.Mesh.FaceColors != null && subMesh.UseVertexColors;
				bool useFaceColors = hasFaceColors && !command.OverrideFaceColors;

				byte red, green, blue, alpha;
				if (useFaceColors)
				{
					// Per-face vertex colors provide RGB; apply alpha multiplier for transparency override
					red = 255;
					green = 255;
					blue = 255;
					alpha = (byte)(255 * command.AlphaMultiplier);
				}
				else
				{
					red = useTexture && !command.BlendTexture ? (byte)255 : (byte)command.Color.Red0To255;
					green = useTexture && !command.BlendTexture ? (byte)255 : (byte)command.Color.Green0To255;
					blue = useTexture && !command.BlendTexture ? (byte)255 : (byte)command.Color.Blue0To255;
					alpha = (byte)command.Color.Alpha0To255;
				}

				Color4(red, green, blue, alpha);

				if (useFaceColors || (subMesh.UseVertexColors && !hasFaceColors))
				{
					EnableClientState(ArrayCap.ColorArray);
				}
				else
				{
					DisableClientState(ArrayCap.ColorArray);
				}

				EnableClientState(ArrayCap.NormalArray);
				EnableClientState(ArrayCap.VertexArray);

				unsafe
				{
					fixed (VertexTextureData* pTextureData = subMesh.textureData.Array)
					fixed (VertexColorData* pColorData = subMesh.colorData.Array)
					fixed (VertexNormalData* pNormalData = subMesh.normalData.Array)
					fixed (VertexPositionData* pPosition = subMesh.positionData.Array)
					{
						VertexPointer(3, VertexPointerType.Float, 0, new System.IntPtr(pPosition));
						NormalPointer(NormalPointerType.Float, 0, new System.IntPtr(pNormalData));

						if (useTexture)
						{
							TexCoordPointer(2, TexCordPointerType.Float, 0, new System.IntPtr(pTextureData));
						}

						if (subMesh.UseVertexColors && pColorData != null)
						{
							ColorPointer(4, ColorPointerType.UnsignedByte, 0, new System.IntPtr(pColorData));
						}

						DrawArrays(BeginMode.Triangles, 0, subMesh.positionData.Count);
					}
				}

				DisableClientState(ArrayCap.NormalArray);
				DisableClientState(ArrayCap.VertexArray);
				DisableClientState(ArrayCap.ColorArray);
			}
		}

		private void SetSceneMatrices(Matrix4X4 modelView, Matrix4X4 projection)
		{
			ReplaceStackTop(modelViewStack, modelView);
			ReplaceStackTop(projectionStack, projection);
			transformDirty = true;
		}

		private static void ReplaceStackTop(System.Collections.Generic.Stack<Matrix4X4> stack, Matrix4X4 value)
		{
			if (stack.Count == 0)
			{
				stack.Push(value);
				return;
			}

			stack.Pop();
			stack.Push(value);
		}
	}
}
