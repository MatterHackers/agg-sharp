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
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using AggColor = MatterHackers.Agg.Color;

namespace MatterHackers.RenderOpenGl
{
	public partial class VorticeD3DGl
	{
		private const int SceneEffectVertexStride = 8 * sizeof(float);
		private const int TransparentPeelLayerCount = 6;

		private readonly List<MeshRenderCommand> queuedSceneCommands = new();
		private readonly List<SelectionOutlineCommand> queuedSelectionOutlines = new();
		private readonly List<SceneTextureTarget> transparentLayerTargets = new();

		private SceneTextureTarget sceneColorTarget;
		private SceneTextureTarget sceneDepthTarget;
		private SceneTextureTarget selectionTarget;

		private ID3D11VertexShader sceneEffectVS;
		private ID3D11PixelShader sceneEffectColorPS;
		private ID3D11PixelShader sceneEffectTexturePS;
		private ID3D11PixelShader sceneEffectSelectionPS;
		private ID3D11PixelShader sceneEffectDepthPS;
		private ID3D11InputLayout sceneEffectInputLayout;

		private ID3D11VertexShader fullscreenVS;
		private ID3D11PixelShader copyTexturePS;
		private ID3D11PixelShader outlineCompositePS;

		private ID3D11Buffer sceneEffectBuffer;
		private ID3D11Buffer outlineCompositeBuffer;
		private ID3D11SamplerState pointClampSampler;
		private ID3D11Texture2D whiteTexture;
		private ID3D11ShaderResourceView whiteTextureView;

		private bool sceneEffectsInitialized;

		private sealed class SelectionOutlineCommand
		{
			public AggColor Color;
			public Mesh Mesh;
			public Matrix4X4 Transform;
		}

		private sealed class SceneTextureTarget : IDisposable
		{
			public ID3D11Texture2D ColorTexture;
			public ID3D11RenderTargetView RenderTargetView;
			public ID3D11ShaderResourceView ColorShaderResourceView;
			public ID3D11Texture2D DepthTexture;
			public ID3D11DepthStencilView DepthStencilView;
			public ID3D11ShaderResourceView DepthShaderResourceView;
			public int Height;
			public int Width;

			public void Dispose()
			{
				ColorShaderResourceView?.Dispose();
				RenderTargetView?.Dispose();
				ColorTexture?.Dispose();
				DepthShaderResourceView?.Dispose();
				DepthStencilView?.Dispose();
				DepthTexture?.Dispose();
			}
		}

		private void EnsureSceneEffectsInitialized()
		{
			if (sceneEffectsInitialized)
			{
				return;
			}

			CreateSceneEffectShaders();
			CreateSceneEffectBuffers();
			CreateSceneEffectStates();
			CreateWhiteTexture();
			sceneEffectsInitialized = true;
		}

		private void CreateSceneEffectShaders()
		{
			string sceneEffectsHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerScene.hlsl");
			byte[] sceneVsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneVS", "NodeDesignerScene.hlsl", "vs_5_0").ToArray();
			byte[] sceneColorPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneColorPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] sceneTexturePsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneTexturePS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] selectionPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SelectionMaskPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] depthPsByteCode = Compiler.Compile(sceneEffectsHlsl, "DepthOnlyPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();

			sceneEffectVS = device.CreateVertexShader(sceneVsByteCode);
			sceneEffectColorPS = device.CreatePixelShader(sceneColorPsByteCode);
			sceneEffectTexturePS = device.CreatePixelShader(sceneTexturePsByteCode);
			sceneEffectSelectionPS = device.CreatePixelShader(selectionPsByteCode);
			sceneEffectDepthPS = device.CreatePixelShader(depthPsByteCode);
			sceneEffectInputLayout = device.CreateInputLayout(new[]
			{
				new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
				new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
				new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
			}, sceneVsByteCode);

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] fullscreenVsByteCode = Compiler.Compile(postProcessHlsl, "FullScreenVS", "NodeDesignerPostProcess.hlsl", "vs_5_0").ToArray();
			byte[] copyPsByteCode = Compiler.Compile(postProcessHlsl, "CopyTexturePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] outlinePsByteCode = Compiler.Compile(postProcessHlsl, "OutlineCompositePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();

			fullscreenVS = device.CreateVertexShader(fullscreenVsByteCode);
			copyTexturePS = device.CreatePixelShader(copyPsByteCode);
			outlineCompositePS = device.CreatePixelShader(outlinePsByteCode);
		}

		private void CreateSceneEffectBuffers()
		{
			sceneEffectBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 64,
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});

			outlineCompositeBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 32,
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});
		}

		private void CreateSceneEffectStates()
		{
			pointClampSampler = device.CreateSamplerState(new SamplerDescription
			{
				Filter = Filter.MinMagMipPoint,
				AddressU = TextureAddressMode.Clamp,
				AddressV = TextureAddressMode.Clamp,
				AddressW = TextureAddressMode.Clamp,
				ComparisonFunc = ComparisonFunction.Never,
				MinLOD = 0,
				MaxLOD = float.MaxValue,
			});
		}

		private unsafe void CreateWhiteTexture()
		{
			var textureDescription = new Texture2DDescription
			{
				Width = 1,
				Height = 1,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.R8G8B8A8_UNorm,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Immutable,
				BindFlags = BindFlags.ShaderResource,
			};

			uint white = 0xFFFFFFFF;
			whiteTexture = device.CreateTexture2D(textureDescription, new[] { new SubresourceData(new IntPtr(&white), 4) });
			whiteTextureView = device.CreateShaderResourceView(whiteTexture);
		}

		public void QueueSelectionOutline(Mesh mesh, AggColor color, Matrix4X4 transform)
		{
			if (!IsSceneRenderingActive || mesh == null)
			{
				return;
			}

			queuedSelectionOutlines.Add(new SelectionOutlineCommand
			{
				Mesh = mesh,
				Color = color,
				Transform = transform,
			});
		}

		private void QueueSceneCommand(MeshRenderCommand command)
		{
			if (command?.Mesh == null)
			{
				return;
			}

			queuedSceneCommands.Add(command);
		}

		private void RenderQueuedSceneEffects()
		{
			if (activeSceneRenderContext == null)
			{
				ClearQueuedSceneEffects();
				return;
			}

			if (queuedSceneCommands.Count == 0 && queuedSelectionOutlines.Count == 0)
			{
				return;
			}

			EnsureSceneEffectsInitialized();
			ApplySceneLighting(activeSceneRenderContext.Lighting);
			UpdateLightBuffer(true, true);

			int width = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Width));
			int height = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Height));

			EnsureSceneTargets(width, height);

			var renderPlan = NativeSceneRenderPlanner.Build(queuedSceneCommands);

			RenderOpaqueCommands(renderPlan.OpaqueCommands);
			RenderSceneDepth(renderPlan);
			RenderTransparentLayers(renderPlan.TransparentCommands);
			CompositeSceneTargets();
			RenderSelectionOutlines();
			RestoreDefaultSceneTarget();
		}

		private void EnsureSceneTargets(int width, int height)
		{
			sceneColorTarget = EnsureSceneTarget(sceneColorTarget, width, height, withColor: true);
			sceneDepthTarget = EnsureSceneTarget(sceneDepthTarget, width, height, withColor: false);
			selectionTarget = EnsureSceneTarget(selectionTarget, width, height, withColor: true);

			while (transparentLayerTargets.Count < TransparentPeelLayerCount)
			{
				transparentLayerTargets.Add(null);
			}

			for (int i = 0; i < transparentLayerTargets.Count; i++)
			{
				transparentLayerTargets[i] = EnsureSceneTarget(transparentLayerTargets[i], width, height, withColor: true);
			}
		}

		private SceneTextureTarget EnsureSceneTarget(SceneTextureTarget existingTarget, int width, int height, bool withColor)
		{
			if (existingTarget != null
				&& existingTarget.Width == width
				&& existingTarget.Height == height
				&& (existingTarget.RenderTargetView != null) == withColor)
			{
				return existingTarget;
			}

			existingTarget?.Dispose();
			var newTarget = new SceneTextureTarget
			{
				Width = width,
				Height = height,
			};

			if (withColor)
			{
				var colorDescription = new Texture2DDescription
				{
					Width = (uint)width,
					Height = (uint)height,
					MipLevels = 1,
					ArraySize = 1,
					Format = Format.R8G8B8A8_UNorm,
					SampleDescription = new SampleDescription(1, 0),
					Usage = ResourceUsage.Default,
					BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
				};

				newTarget.ColorTexture = device.CreateTexture2D(colorDescription);
				newTarget.RenderTargetView = device.CreateRenderTargetView(newTarget.ColorTexture);
				newTarget.ColorShaderResourceView = device.CreateShaderResourceView(newTarget.ColorTexture);
			}

			var depthDescription = new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.R32_Typeless,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
			};

			newTarget.DepthTexture = device.CreateTexture2D(depthDescription);
			newTarget.DepthStencilView = device.CreateDepthStencilView(
				newTarget.DepthTexture,
				new DepthStencilViewDescription(DepthStencilViewDimension.Texture2D, Format.D32_Float));
			newTarget.DepthShaderResourceView = device.CreateShaderResourceView(
				newTarget.DepthTexture,
				new ShaderResourceViewDescription(ShaderResourceViewDimension.Texture2D, Format.R32_Float));

			return newTarget;
		}

		private void RenderOpaqueCommands(IReadOnlyList<MeshRenderCommand> commands)
		{
			if (commands.Count == 0)
			{
				ClearSceneTarget(sceneColorTarget);
				return;
			}

			BindSceneTarget(sceneColorTarget);
			ClearSceneTarget(sceneColorTarget);

			foreach (var command in commands)
			{
				RenderMeshCommand(
					command,
					null,
					enableWireframe: command.RenderType == RenderTypes.Outlines,
					wireframeOnly: command.RenderType == RenderTypes.Wireframe,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: null,
					nearDepthView: null);
			}
		}

		private void RenderSceneDepth(NativeSceneRenderPlan renderPlan)
		{
			BindSceneTarget(sceneDepthTarget);
			ClearDepthOnlyTarget(sceneDepthTarget);

			foreach (var command in renderPlan.OpaqueCommands)
			{
				RenderMeshCommand(command, sceneEffectDepthPS, false, false, false, false, null, null, colorWritesEnabled: false);
			}

			foreach (var command in renderPlan.TransparentCommands)
			{
				RenderMeshCommand(command, sceneEffectDepthPS, false, false, false, false, null, null, colorWritesEnabled: false);
			}
		}

		private void RenderTransparentLayers(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			for (int layerIndex = 0; layerIndex < transparentLayerTargets.Count; layerIndex++)
			{
				var target = transparentLayerTargets[layerIndex];
				BindSceneTarget(target);
				ClearSceneTarget(target);

				if (transparentCommands.Count == 0)
				{
					continue;
				}

				bool firstPass = layerIndex == 0;
				var nearDepth = firstPass ? null : transparentLayerTargets[layerIndex - 1].DepthShaderResourceView;

				foreach (var command in transparentCommands)
				{
					RenderMeshCommand(
						command,
						null,
						enableWireframe: command.RenderType == RenderTypes.Outlines || command.RenderType == RenderTypes.Wireframe,
						wireframeOnly: command.RenderType == RenderTypes.Wireframe,
						enableDepthPeeling: true,
						firstPeelPass: firstPass,
						opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
						nearDepthView: nearDepth);
				}
			}
		}

		private void CompositeSceneTargets()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			context.RSSetViewport(new Viewport((float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetSampler(0, defaultSampler);
			context.PSSetSampler(1, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));

			DrawFullscreenTexture(sceneColorTarget.ColorShaderResourceView, copyTexturePS);

			for (int layerIndex = transparentLayerTargets.Count - 1; layerIndex >= 0; layerIndex--)
			{
				DrawFullscreenTexture(transparentLayerTargets[layerIndex].ColorShaderResourceView, copyTexturePS);
			}

			UnbindSceneTextures();
		}

		private void RenderSelectionOutlines()
		{
			if (queuedSelectionOutlines.Count == 0)
			{
				return;
			}

			BindSceneTarget(selectionTarget);
			ClearSceneTarget(selectionTarget);

			foreach (var selectionOutline in queuedSelectionOutlines)
			{
				var command = new MeshRenderCommand
				{
					Color = selectionOutline.Color,
					Mesh = selectionOutline.Mesh,
					Transform = selectionOutline.Transform,
					RenderType = RenderTypes.Shaded,
					ForceCullBackFaces = false,
				};

				RenderMeshCommand(
					command,
					sceneEffectSelectionPS,
					enableWireframe: false,
					wireframeOnly: false,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: null,
					nearDepthView: null);
			}

			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			context.RSSetViewport(new Viewport((float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(outlineCompositePS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetShaderResource(0, selectionTarget.ColorShaderResourceView);
			context.PSSetShaderResource(1, selectionTarget.DepthShaderResourceView);
			context.PSSetShaderResource(2, sceneDepthTarget.DepthShaderResourceView);
			context.PSSetConstantBuffer(0, outlineCompositeBuffer);
			context.RSSetState(rasterizerNoCull);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			UpdateOutlineCompositeBuffer((float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height);
			context.Draw(3, 0);
			UnbindSceneTextures();
		}

		private void DrawFullscreenTexture(ID3D11ShaderResourceView textureView, ID3D11PixelShader pixelShader)
		{
			context.PSSetShader(pixelShader);
			context.PSSetShaderResource(0, textureView);
			context.Draw(3, 0);
			context.PSSetShaderResource(0, null);
		}

		private void BindSceneTarget(SceneTextureTarget target)
		{
			context.OMSetRenderTargets(target.RenderTargetView, target.DepthStencilView);
			context.RSSetViewport(new Viewport(target.Width, target.Height));
		}

		private void ClearSceneTarget(SceneTextureTarget target)
		{
			if (target.RenderTargetView != null)
			{
				context.ClearRenderTargetView(target.RenderTargetView, new Color4(0, 0, 0, 0));
			}

			context.ClearDepthStencilView(target.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
		}

		private void ClearDepthOnlyTarget(SceneTextureTarget target)
		{
			context.OMSetRenderTargets((ID3D11RenderTargetView)null, target.DepthStencilView);
			context.RSSetViewport(new Viewport(target.Width, target.Height));
			context.ClearDepthStencilView(target.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
		}

		private unsafe void RenderMeshCommand(
			MeshRenderCommand command,
			ID3D11PixelShader overridePixelShader,
			bool enableWireframe,
			bool wireframeOnly,
			bool enableDepthPeeling,
			bool firstPeelPass,
			ID3D11ShaderResourceView opaqueDepthView,
			ID3D11ShaderResourceView nearDepthView,
			bool colorWritesEnabled = true)
		{
			SetSceneMatrices(command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix, activeSceneRenderContext.WorldView.ProjectionMatrix);
			UpdateTransformBuffer();
			UpdateSceneEffectBuffer(command.Color, command.WireFrameColor, enableWireframe, wireframeOnly, enableDepthPeeling, firstPeelPass, (float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height);

			context.IASetInputLayout(sceneEffectInputLayout);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.IASetVertexBuffer(0, dynamicTexVertexBuffer, SceneEffectVertexStride);
			context.VSSetShader(sceneEffectVS);
			context.VSSetConstantBuffer(0, transformBuffer);
			context.PSSetConstantBuffer(1, lightBuffer);
			context.PSSetConstantBuffer(2, sceneEffectBuffer);
			context.PSSetSampler(0, defaultSampler);
			context.PSSetSampler(1, pointClampSampler);
			context.PSSetShaderResource(1, opaqueDepthView);
			context.PSSetShaderResource(2, nearDepthView);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(true, ComparisonFunction.LessEqual, true));
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, colorWritesEnabled ? ColorWriteEnable.All : ColorWriteEnable.None));
			context.RSSetState(command.ForceCullBackFaces ? rasterizerCullBack : rasterizerNoCull);

			var glMeshPlugin = GLMeshTrianglePlugin.Get(command.Mesh);
			foreach (var subMesh in glMeshPlugin.subMeshs)
			{
				var pixelShader = overridePixelShader ?? (subMesh.texture != null ? sceneEffectTexturePS : sceneEffectColorPS);

				context.PSSetShader(pixelShader);

				var textureView = whiteTextureView;
				if (subMesh.texture != null)
				{
					var texturePlugin = ImageGlPlugin.GetImageGlPlugin(subMesh.texture, true);
					if (texturePlugin != null
						&& textures.TryGetValue(texturePlugin.GLTextureHandle, out var textureInfo)
						&& textureInfo.ShaderResourceView != null)
					{
						textureView = textureInfo.ShaderResourceView;
					}
				}

				context.PSSetShaderResource(0, textureView);

				int vertexCount = subMesh.positionData.Count;
				int batchOffset = 0;
				while (batchOffset < vertexCount)
				{
					int batchCount = Math.Min(MaxVertices, vertexCount - batchOffset);
					batchCount -= batchCount % 3;
					if (batchCount <= 0)
					{
						break;
					}

					var mapped = context.Map(dynamicTexVertexBuffer, MapMode.WriteDiscard);
					float* destination = (float*)mapped.DataPointer;

					fixed (VertexPositionData* pPosition = subMesh.positionData.Array)
					fixed (VertexNormalData* pNormal = subMesh.normalData.Array)
					fixed (VertexTextureData* pTexture = subMesh.textureData.Array)
					{
						for (int vertexIndex = 0; vertexIndex < batchCount; vertexIndex++)
						{
							int sourceIndex = batchOffset + vertexIndex;
							int destinationIndex = vertexIndex * 8;

							destination[destinationIndex + 0] = pPosition[sourceIndex].positionX;
							destination[destinationIndex + 1] = pPosition[sourceIndex].positionY;
							destination[destinationIndex + 2] = pPosition[sourceIndex].positionZ;
							destination[destinationIndex + 3] = pNormal[sourceIndex].normalX;
							destination[destinationIndex + 4] = pNormal[sourceIndex].normalY;
							destination[destinationIndex + 5] = pNormal[sourceIndex].normalZ;
							destination[destinationIndex + 6] = pTexture[sourceIndex].textureU;
							destination[destinationIndex + 7] = pTexture[sourceIndex].textureV;
						}
					}

					context.Unmap(dynamicTexVertexBuffer, 0);
					context.Draw((uint)batchCount, 0);
					batchOffset += batchCount;
				}
			}

			UnbindSceneTextures();
		}

		private unsafe void UpdateSceneEffectBuffer(
			AggColor meshColor,
			AggColor wireframeColor,
			bool enableWireframe,
			bool wireframeOnly,
			bool enableDepthPeeling,
			bool firstPeelPass,
			float width,
			float height)
		{
			var effectiveWireframeColor = wireframeColor.Alpha0To1 > 0
				? wireframeColor
				: new AggColor(25, 25, 25);

			var mapped = context.Map(sceneEffectBuffer, MapMode.WriteDiscard);
			float* values = (float*)mapped.DataPointer;

			values[0] = meshColor.Red0To1;
			values[1] = meshColor.Green0To1;
			values[2] = meshColor.Blue0To1;
			values[3] = meshColor.Alpha0To1;

			values[4] = effectiveWireframeColor.Red0To1;
			values[5] = effectiveWireframeColor.Green0To1;
			values[6] = effectiveWireframeColor.Blue0To1;
			values[7] = Math.Max(effectiveWireframeColor.Alpha0To1, 1.0f);

			values[8] = enableWireframe ? 1.0f : 0.0f;
			values[9] = wireframeOnly ? 1.0f : 0.0f;
			values[10] = enableDepthPeeling ? 1.0f : 0.0f;
			values[11] = firstPeelPass ? 1.0f : 0.0f;

			values[12] = width;
			values[13] = height;
			values[14] = 1.2f;
			values[15] = 0.0f;

			context.Unmap(sceneEffectBuffer, 0);
		}

		private unsafe void UpdateOutlineCompositeBuffer(float width, float height)
		{
			var mapped = context.Map(outlineCompositeBuffer, MapMode.WriteDiscard);
			float* values = (float*)mapped.DataPointer;

			values[0] = 2.0f;
			values[1] = 0.35f;
			values[2] = width;
			values[3] = height;
			values[4] = 0;
			values[5] = 0;
			values[6] = 0;
			values[7] = 0;

			context.Unmap(outlineCompositeBuffer, 0);
		}

		private void RestoreDefaultSceneTarget()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			context.RSSetViewport(new Viewport((float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height));
			renderStateDirty = true;
			transformDirty = true;
		}

		private void UnbindSceneTextures()
		{
			context.PSSetShaderResource(0, null);
			context.PSSetShaderResource(1, null);
			context.PSSetShaderResource(2, null);
		}

		private void ClearQueuedSceneEffects()
		{
			queuedSceneCommands.Clear();
			queuedSelectionOutlines.Clear();
		}

		private void DisposeSceneEffects()
		{
			ClearQueuedSceneEffects();

			sceneColorTarget?.Dispose();
			sceneDepthTarget?.Dispose();
			selectionTarget?.Dispose();
			sceneColorTarget = null;
			sceneDepthTarget = null;
			selectionTarget = null;

			foreach (var target in transparentLayerTargets)
			{
				target?.Dispose();
			}

			transparentLayerTargets.Clear();

			sceneEffectVS?.Dispose();
			sceneEffectColorPS?.Dispose();
			sceneEffectTexturePS?.Dispose();
			sceneEffectSelectionPS?.Dispose();
			sceneEffectDepthPS?.Dispose();
			sceneEffectInputLayout?.Dispose();
			fullscreenVS?.Dispose();
			copyTexturePS?.Dispose();
			outlineCompositePS?.Dispose();
			sceneEffectBuffer?.Dispose();
			outlineCompositeBuffer?.Dispose();
			pointClampSampler?.Dispose();
			whiteTextureView?.Dispose();
			whiteTexture?.Dispose();

			sceneEffectVS = null;
			sceneEffectColorPS = null;
			sceneEffectTexturePS = null;
			sceneEffectSelectionPS = null;
			sceneEffectDepthPS = null;
			sceneEffectInputLayout = null;
			fullscreenVS = null;
			copyTexturePS = null;
			outlineCompositePS = null;
			sceneEffectBuffer = null;
			outlineCompositeBuffer = null;
			pointClampSampler = null;
			whiteTextureView = null;
			whiteTexture = null;
			sceneEffectsInitialized = false;
		}
	}
}
