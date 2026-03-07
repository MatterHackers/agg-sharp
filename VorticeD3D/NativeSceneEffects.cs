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
			private static readonly int DualDepthPeelIterationCount = DualDepthPeelingMath.GetIterationCount(TransparentPeelLayerCount);

		private readonly List<MeshRenderCommand> queuedSceneCommands = new();
		private readonly List<SelectionOutlineCommand> queuedSelectionOutlines = new();
		private readonly NativeSceneRenderPlanner renderPlanner = new();

		// Pipeline state tracking to skip redundant D3D calls across render commands.
		// These must be reset whenever another pass mutates the pipeline outside this cached path.
		private ID3D11PixelShader lastBoundPixelShader;
		private ID3D11ShaderResourceView lastBoundTextureView;
		private ID3D11RasterizerState lastBoundRasterizerState;
		private ID3D11BlendState lastBoundBlendState;
		private ID3D11DepthStencilState lastBoundDepthStencilState;

		private SceneTextureTarget sceneColorTarget;
		private SceneTextureTarget sceneDepthTarget;
		private SceneTextureTarget selectionTarget;
			private ColorTextureTarget dualDepthPeelTarget0;
			private ColorTextureTarget dualDepthPeelTarget1;
			private ColorTextureTarget dualFrontAccumTarget;
			private ColorTextureTarget dualBackAccumTarget;

		private ID3D11VertexShader sceneEffectVS;
		private ID3D11PixelShader sceneEffectColorPS;
		private ID3D11PixelShader sceneEffectTexturePS;
		private ID3D11PixelShader sceneEffectSelectionPS;
		private ID3D11PixelShader sceneEffectDepthPS;
			private ID3D11PixelShader sceneEffectDualDepthInitPS;
			private ID3D11PixelShader sceneEffectDualPeelColorPS;
			private ID3D11PixelShader sceneEffectDualPeelTexturePS;
		private ID3D11InputLayout sceneEffectInputLayout;

		private ID3D11VertexShader fullscreenVS;
		private ID3D11PixelShader copyTexturePS;
			private ID3D11PixelShader resolveDualPeelPS;
		private ID3D11PixelShader outlineCompositePS;
			private ID3D11BlendState dualDepthPeelBlendState;

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

		private sealed class ColorTextureTarget : IDisposable
		{
			public Format ColorFormat;
			public ID3D11Texture2D Texture;
			public ID3D11RenderTargetView RenderTargetView;
			public ID3D11ShaderResourceView ShaderResourceView;
			public int Height;
			public int Width;

			public void Dispose()
			{
				ShaderResourceView?.Dispose();
				RenderTargetView?.Dispose();
				Texture?.Dispose();
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
			byte[] dualDepthInitPsByteCode = Compiler.Compile(sceneEffectsHlsl, "DualDepthInitPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] dualPeelColorPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneColorDualPeelPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] dualPeelTexturePsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneTextureDualPeelPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();

			sceneEffectVS = device.CreateVertexShader(sceneVsByteCode);
			sceneEffectColorPS = device.CreatePixelShader(sceneColorPsByteCode);
			sceneEffectTexturePS = device.CreatePixelShader(sceneTexturePsByteCode);
			sceneEffectSelectionPS = device.CreatePixelShader(selectionPsByteCode);
			sceneEffectDepthPS = device.CreatePixelShader(depthPsByteCode);
			sceneEffectDualDepthInitPS = device.CreatePixelShader(dualDepthInitPsByteCode);
			sceneEffectDualPeelColorPS = device.CreatePixelShader(dualPeelColorPsByteCode);
			sceneEffectDualPeelTexturePS = device.CreatePixelShader(dualPeelTexturePsByteCode);
			sceneEffectInputLayout = device.CreateInputLayout(new[]
			{
				new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
				new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
				new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
			}, sceneVsByteCode);

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] fullscreenVsByteCode = Compiler.Compile(postProcessHlsl, "FullScreenVS", "NodeDesignerPostProcess.hlsl", "vs_5_0").ToArray();
			byte[] copyPsByteCode = Compiler.Compile(postProcessHlsl, "CopyTexturePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] resolvePsByteCode = Compiler.Compile(postProcessHlsl, "ResolveDualPeelPS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] outlinePsByteCode = Compiler.Compile(postProcessHlsl, "OutlineCompositePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();

			fullscreenVS = device.CreateVertexShader(fullscreenVsByteCode);
			copyTexturePS = device.CreatePixelShader(copyPsByteCode);
			resolveDualPeelPS = device.CreatePixelShader(resolvePsByteCode);
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

			dualDepthPeelBlendState = CreateDualDepthPeelBlendState();
		}

		private ID3D11BlendState CreateDualDepthPeelBlendState()
		{
			var blendDescription = new BlendDescription
			{
				AlphaToCoverageEnable = false,
				IndependentBlendEnable = true,
			};

			blendDescription.RenderTarget[0] = new RenderTargetBlendDescription
			{
				BlendEnable = true,
				SourceBlend = Blend.One,
				DestinationBlend = Blend.One,
				BlendOperation = BlendOperation.Max,
				SourceBlendAlpha = Blend.One,
				DestinationBlendAlpha = Blend.One,
				BlendOperationAlpha = BlendOperation.Max,
				RenderTargetWriteMask = ColorWriteEnable.Red | ColorWriteEnable.Green,
			};

			blendDescription.RenderTarget[1] = new RenderTargetBlendDescription
			{
				BlendEnable = true,
				SourceBlend = Blend.DestinationAlpha,
				DestinationBlend = Blend.One,
				BlendOperation = BlendOperation.Add,
				SourceBlendAlpha = Blend.Zero,
				DestinationBlendAlpha = Blend.InverseSourceAlpha,
				BlendOperationAlpha = BlendOperation.Add,
				RenderTargetWriteMask = ColorWriteEnable.All,
			};

			blendDescription.RenderTarget[2] = new RenderTargetBlendDescription
			{
				BlendEnable = true,
				SourceBlend = Blend.SourceAlpha,
				DestinationBlend = Blend.InverseSourceAlpha,
				BlendOperation = BlendOperation.Add,
				SourceBlendAlpha = Blend.One,
				DestinationBlendAlpha = Blend.InverseSourceAlpha,
				BlendOperationAlpha = BlendOperation.Add,
				RenderTargetWriteMask = ColorWriteEnable.All,
			};

			return device.CreateBlendState(blendDescription);
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

			ResetPipelineStateTracking();
			var renderPlan = renderPlanner.Build(queuedSceneCommands);

			RenderOpaqueCommands(renderPlan.OpaqueCommands);
			RenderSceneDepth(renderPlan);
			RenderTransparentLayers(renderPlan.TransparentCommands);
			CompositeSceneTargets();
			RenderTransparentWireOverlays(renderPlan.TransparentCommands);
			RenderSelectionOutlines();
			RestoreDefaultSceneTarget();
		}

		private void EnsureSceneTargets(int width, int height)
		{
			sceneColorTarget = EnsureSceneTarget(sceneColorTarget, width, height, withColor: true);
			sceneDepthTarget = EnsureSceneTarget(sceneDepthTarget, width, height, withColor: false);
			selectionTarget = EnsureSceneTarget(selectionTarget, width, height, withColor: true);
			dualDepthPeelTarget0 = EnsureColorTarget(dualDepthPeelTarget0, width, height, Format.R32G32_Float);
			dualDepthPeelTarget1 = EnsureColorTarget(dualDepthPeelTarget1, width, height, Format.R32G32_Float);
			dualFrontAccumTarget = EnsureColorTarget(dualFrontAccumTarget, width, height, Format.R16G16B16A16_Float);
			dualBackAccumTarget = EnsureColorTarget(dualBackAccumTarget, width, height, Format.R16G16B16A16_Float);
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

		private ColorTextureTarget EnsureColorTarget(ColorTextureTarget existingTarget, int width, int height, Format format)
		{
			if (existingTarget != null
				&& existingTarget.Width == width
				&& existingTarget.Height == height
				&& existingTarget.ColorFormat == format)
			{
				return existingTarget;
			}

			existingTarget?.Dispose();
			var newTarget = new ColorTextureTarget
			{
				Width = width,
				Height = height,
				ColorFormat = format,
			};

			var textureDescription = new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = format,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
			};

			newTarget.Texture = device.CreateTexture2D(textureDescription);
			newTarget.RenderTargetView = device.CreateRenderTargetView(newTarget.Texture);
			newTarget.ShaderResourceView = device.CreateShaderResourceView(newTarget.Texture);
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
				if (SceneRenderModeUtilities.ShouldRenderFilledSurface(command.RenderType))
				{
					RenderMeshCommand(
						command,
						null,
						enableWireframe: false,
						wireframeOnly: false,
						offsetFill: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
						enableDepthPeeling: false,
						firstPeelPass: false,
						opaqueDepthView: null,
						nearDepthView: null);
				}

				RenderEdgeLines(command, enableDepthPeeling: false, firstPeelPass: false, opaqueDepthView: null, nearDepthView: null);
			}
		}

		private void RenderSceneDepth(NativeSceneRenderPlan renderPlan)
		{
			BindSceneTarget(sceneDepthTarget);
			ClearDepthOnlyTarget(sceneDepthTarget);

			foreach (var command in renderPlan.OpaqueCommands)
			{
				RenderMeshCommand(command, sceneEffectDepthPS, false, false, false, false, false, null, null, colorWritesEnabled: false);
			}

			foreach (var command in renderPlan.TransparentCommands)
			{
				RenderMeshCommand(command, sceneEffectDepthPS, false, false, false, false, false, null, null, colorWritesEnabled: false);
			}
		}

		private void RenderTransparentLayers(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			ClearColorTarget(dualFrontAccumTarget, new Color4(0, 0, 0, 1));
			ClearColorTarget(dualBackAccumTarget, new Color4(0, 0, 0, 0));
			ClearColorTarget(dualDepthPeelTarget0, new Color4(-1, -1, 0, 0));
			ClearColorTarget(dualDepthPeelTarget1, new Color4(-1, -1, 0, 0));

			if (transparentCommands.Count == 0)
			{
				return;
			}

			var dualPeelDepthState = GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false);
			InitializeDualDepthPeel(transparentCommands, dualPeelDepthState);

			var sourceDepthTarget = dualDepthPeelTarget0;
			var destinationDepthTarget = dualDepthPeelTarget1;
			for (int iterationIndex = 0; iterationIndex < DualDepthPeelIterationCount; iterationIndex++)
			{
				BindDualPeelTargets(destinationDepthTarget, dualFrontAccumTarget, dualBackAccumTarget);
				ClearColorTarget(destinationDepthTarget, new Color4(-1, -1, 0, 0));

				foreach (var command in transparentCommands)
				{
					if (!SceneRenderModeUtilities.ShouldRenderFilledSurface(command.RenderType))
					{
						continue;
					}

					RenderMeshCommand(
						command,
						null,
						enableWireframe: false,
						wireframeOnly: false,
						offsetFill: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
						enableDepthPeeling: false,
						firstPeelPass: false,
						opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
						nearDepthView: sourceDepthTarget.ShaderResourceView,
						colorWritesEnabled: true,
						blendStateOverride: dualDepthPeelBlendState,
						depthStencilStateOverride: dualPeelDepthState,
						useDualDepthPeelingShader: true);
				}

				(sourceDepthTarget, destinationDepthTarget) = (destinationDepthTarget, sourceDepthTarget);
			}
		}

		private void CompositeSceneTargets()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			ApplyDefaultSceneViewport();
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetSampler(1, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			DrawFullscreenResolve(sceneColorTarget.ColorShaderResourceView, dualFrontAccumTarget.ShaderResourceView, dualBackAccumTarget.ShaderResourceView);

			UnbindSceneTextures();
		}

		private void RenderTransparentWireOverlays(IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			if (transparentCommands.Count == 0)
			{
				return;
			}

			context.OMSetRenderTargets(renderTargetView, sceneDepthTarget.DepthStencilView);
			ApplyDefaultSceneViewport();

			foreach (var command in transparentCommands)
			{
				RenderEdgeLines(command, enableDepthPeeling: false, firstPeelPass: false, opaqueDepthView: null, nearDepthView: null);
			}
		}

		private void InitializeDualDepthPeel(IReadOnlyList<MeshRenderCommand> transparentCommands, ID3D11DepthStencilState depthState)
		{
			BindColorTarget(dualDepthPeelTarget0);
			ClearColorTarget(dualDepthPeelTarget0, new Color4(-1, -1, 0, 0));

			foreach (var command in transparentCommands)
			{
				if (!SceneRenderModeUtilities.ShouldRenderFilledSurface(command.RenderType))
				{
					continue;
				}

				RenderMeshCommand(
					command,
					sceneEffectDualDepthInitPS,
					enableWireframe: false,
					wireframeOnly: false,
					offsetFill: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
					nearDepthView: null,
					colorWritesEnabled: true,
					blendStateOverride: dualDepthPeelBlendState,
					depthStencilStateOverride: depthState);
			}
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
					offsetFill: false,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: null,
					nearDepthView: null);
			}

			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			ApplyDefaultSceneViewport();
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

		private void DrawFullscreenResolve(
			ID3D11ShaderResourceView sceneColorView,
			ID3D11ShaderResourceView frontAccumView,
			ID3D11ShaderResourceView backAccumView)
		{
			context.PSSetShader(resolveDualPeelPS);
			context.PSSetShaderResource(0, sceneColorView);
			context.PSSetShaderResource(1, frontAccumView);
			context.PSSetShaderResource(2, backAccumView);
			context.Draw(3, 0);
		}

		private void BindSceneTarget(SceneTextureTarget target)
		{
			context.OMSetRenderTargets(target.RenderTargetView, target.DepthStencilView);
			context.RSSetViewport(new Viewport(target.Width, target.Height));
		}

		private void BindColorTarget(ColorTextureTarget target)
		{
			context.OMSetRenderTargets(target.RenderTargetView, (ID3D11DepthStencilView)null);
			context.RSSetViewport(new Viewport(target.Width, target.Height));
		}

		private void BindDualPeelTargets(ColorTextureTarget depthRangeTarget, ColorTextureTarget frontAccumTarget, ColorTextureTarget backAccumTarget)
		{
			context.OMSetRenderTargets(
				3,
				new[]
				{
					depthRangeTarget.RenderTargetView,
					frontAccumTarget.RenderTargetView,
					backAccumTarget.RenderTargetView,
				},
				(ID3D11DepthStencilView)null);
			context.RSSetViewport(new Viewport(depthRangeTarget.Width, depthRangeTarget.Height));
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

		private void ClearColorTarget(ColorTextureTarget target, Color4 clearColor)
		{
			context.ClearRenderTargetView(target.RenderTargetView, clearColor);
		}

		private unsafe void RenderMeshCommand(
			MeshRenderCommand command,
			ID3D11PixelShader overridePixelShader,
			bool enableWireframe,
			bool wireframeOnly,
			bool offsetFill,
			bool enableDepthPeeling,
			bool firstPeelPass,
			ID3D11ShaderResourceView opaqueDepthView,
			ID3D11ShaderResourceView nearDepthView,
			bool colorWritesEnabled = true,
			ID3D11BlendState blendStateOverride = null,
			ID3D11DepthStencilState depthStencilStateOverride = null,
			bool useDualDepthPeelingShader = false)
		{
			SetSceneMatrices(command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix, activeSceneRenderContext.WorldView.ProjectionMatrix);
			UpdateTransformBuffer();
			UpdateSceneEffectBuffer(command.Color, command.WireFrameColor, enableWireframe, wireframeOnly, enableDepthPeeling, firstPeelPass, (float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height);

			context.IASetInputLayout(sceneEffectInputLayout);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(sceneEffectVS);
			context.VSSetConstantBuffer(0, transformBuffer);
			context.PSSetConstantBuffer(1, lightBuffer);
			context.PSSetConstantBuffer(2, sceneEffectBuffer);
			context.PSSetSampler(0, defaultSampler);
			context.PSSetSampler(1, pointClampSampler);
			context.PSSetShaderResource(1, opaqueDepthView);
			context.PSSetShaderResource(2, nearDepthView);
			var depthState = depthStencilStateOverride ?? GetOrCreateDepthStencilState(true, ComparisonFunction.LessEqual, true);
			if (ShouldBindDepthStencilState(depthState))
			{
				context.OMSetDepthStencilState(depthState);
			}

			var blendState = blendStateOverride ?? GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, colorWritesEnabled ? ColorWriteEnable.All : ColorWriteEnable.None);
			if (ShouldBindBlendState(blendState))
			{
				context.OMSetBlendState(blendState);
			}

			var rasterizerState = GetSceneRasterizerState(command.ForceCullBackFaces, offsetFill);
			if (ShouldBindRasterizerState(rasterizerState))
			{
				context.RSSetState(rasterizerState);
			}

			var glMeshPlugin = MeshTrianglePlugin.Get(command.Mesh);
			foreach (var subMesh in glMeshPlugin.subMeshs)
			{
				var pixelShader = overridePixelShader
					?? (useDualDepthPeelingShader
						? (subMesh.texture != null ? sceneEffectDualPeelTexturePS : sceneEffectDualPeelColorPS)
						: (subMesh.texture != null ? sceneEffectTexturePS : sceneEffectColorPS));

				if (ShouldBindPixelShader(pixelShader))
				{
					context.PSSetShader(pixelShader);
				}

				var textureView = whiteTextureView;
				if (subMesh.texture != null)
				{
					var texturePlugin = ImageTexturePlugin.GetImageTexturePlugin(subMesh.texture, true);
					if (texturePlugin != null
						&& textures.TryGetValue(texturePlugin.GLTextureHandle, out var textureInfo)
						&& textureInfo.ShaderResourceView != null)
					{
						textureView = textureInfo.ShaderResourceView;
					}
				}

				if (ShouldBindTextureView(textureView))
				{
					context.PSSetShaderResource(0, textureView);
				}

				int vertexCount = subMesh.interleavedData.Length / SubTriangleMesh.InterleavedStride;

				// Try to use or create a static GPU buffer for this submesh
				var staticBuffer = subMesh.CachedGpuBuffer as ID3D11Buffer;
				if (staticBuffer == null && subMesh.interleavedData != null)
				{
					fixed (float* pData = subMesh.interleavedData)
					{
						staticBuffer = device.CreateBuffer(
							new BufferDescription
							{
								ByteWidth = (uint)(subMesh.interleavedData.Length * sizeof(float)),
								Usage = ResourceUsage.Immutable,
								BindFlags = BindFlags.VertexBuffer,
							},
							new SubresourceData((IntPtr)pData));
					}

					subMesh.CachedGpuBuffer = staticBuffer;
				}

				if (staticBuffer != null)
				{
					// Fast path: bind static buffer and draw in one call (no Map/Unmap)
					context.IASetInputLayout(sceneEffectInputLayout);
					context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
					context.IASetVertexBuffer(0, staticBuffer, SceneEffectVertexStride);
					context.Draw((uint)vertexCount, 0);
				}
				else
				{
					// Fallback: dynamic upload in batches
					context.IASetInputLayout(sceneEffectInputLayout);
					context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
					context.IASetVertexBuffer(0, dynamicTexVertexBuffer, SceneEffectVertexStride);

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

						int sourceFloatOffset = batchOffset * SubTriangleMesh.InterleavedStride;
						int copyFloats = batchCount * SubTriangleMesh.InterleavedStride;
						fixed (float* pSource = subMesh.interleavedData)
						{
							Buffer.MemoryCopy(
								pSource + sourceFloatOffset,
								(float*)mapped.DataPointer,
								(long)copyFloats * sizeof(float),
								(long)copyFloats * sizeof(float));
						}

						context.Unmap(dynamicTexVertexBuffer, 0);
						context.Draw((uint)batchCount, 0);
						batchOffset += batchCount;
					}
				}
			}

			UnbindSceneTextures();
		}

		private void ResetPipelineStateTracking()
		{
			lastBoundPixelShader = null;
			lastBoundTextureView = null;
			lastBoundRasterizerState = null;
			lastBoundBlendState = null;
			lastBoundDepthStencilState = null;
		}

		private bool ShouldBindBlendState(ID3D11BlendState blendState)
		{
			if (blendState == lastBoundBlendState)
			{
				return false;
			}

			lastBoundBlendState = blendState;
			return true;
		}

		private bool ShouldBindDepthStencilState(ID3D11DepthStencilState depthStencilState)
		{
			if (depthStencilState == lastBoundDepthStencilState)
			{
				return false;
			}

			lastBoundDepthStencilState = depthStencilState;
			return true;
		}

		private bool ShouldBindPixelShader(ID3D11PixelShader pixelShader)
		{
			if (pixelShader == lastBoundPixelShader)
			{
				return false;
			}

			lastBoundPixelShader = pixelShader;
			return true;
		}

		private bool ShouldBindRasterizerState(ID3D11RasterizerState rasterizerState)
		{
			if (rasterizerState == lastBoundRasterizerState)
			{
				return false;
			}

			lastBoundRasterizerState = rasterizerState;
			return true;
		}

		private bool ShouldBindTextureView(ID3D11ShaderResourceView textureView)
		{
			if (textureView == lastBoundTextureView)
			{
				return false;
			}

			lastBoundTextureView = textureView;
			return true;
		}

		private void RenderEdgeLines(
			MeshRenderCommand command,
			bool enableDepthPeeling,
			bool firstPeelPass,
			ID3D11ShaderResourceView opaqueDepthView,
			ID3D11ShaderResourceView nearDepthView)
		{
			ResetPipelineStateTracking();

			if (!SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType))
			{
				return;
			}

			var edgeLines = GetEdgeLines(command);
			if (edgeLines == null
				|| edgeLines.Count == 0)
			{
				return;
			}

			SetSceneMatrices(command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix, activeSceneRenderContext.WorldView.ProjectionMatrix);
			UpdateTransformBuffer();
			UpdateSceneEffectBuffer(command.Color, command.WireFrameColor, false, false, enableDepthPeeling, firstPeelPass, (float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height);

			context.IASetInputLayout(posColorInputLayout);
			context.IASetPrimitiveTopology(PrimitiveTopology.LineList);
			context.IASetVertexBuffer(0, dynamicVertexBuffer, 7 * sizeof(float));
			context.VSSetShader(posColorVS);
			context.PSSetShader(posColorPS);
			context.VSSetConstantBuffer(0, transformBuffer);
			context.PSSetShaderResource(1, opaqueDepthView);
			context.PSSetShaderResource(2, nearDepthView);
			context.PSSetConstantBuffer(2, sceneEffectBuffer);
			context.PSSetSampler(1, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(true, ComparisonFunction.LessEqual, false));
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			context.RSSetState(rasterizerNoCull);

			int offset = 0;
			while (offset < edgeLines.Count)
			{
				int batchCount = Math.Min(MaxVertices, edgeLines.Count - offset);
				batchCount -= batchCount % 2;
				if (batchCount <= 0)
				{
					break;
				}

				UploadEdgeLineBatch(edgeLines, offset, batchCount);
				context.Draw((uint)batchCount, 0);
				offset += batchCount;
			}

			UnbindSceneTextures();
		}

		private unsafe void UploadEdgeLineBatch(VectorPOD<WireVertexData> edgeLines, int offset, int count)
		{
			var mapped = context.Map(dynamicVertexBuffer, MapMode.WriteDiscard);
			float* destination = (float*)mapped.DataPointer;

			fixed (WireVertexData* source = edgeLines.Array)
			{
				for (int vertexIndex = 0; vertexIndex < count; vertexIndex++)
				{
					var wireVertex = source[offset + vertexIndex];
					int destinationIndex = vertexIndex * 7;

					destination[destinationIndex + 0] = wireVertex.PositionsX;
					destination[destinationIndex + 1] = wireVertex.PositionsY;
					destination[destinationIndex + 2] = wireVertex.PositionsZ;
					destination[destinationIndex + 3] = wireVertex.r / 255f;
					destination[destinationIndex + 4] = wireVertex.g / 255f;
					destination[destinationIndex + 5] = wireVertex.b / 255f;
					destination[destinationIndex + 6] = wireVertex.a / 255f;
				}
			}

			context.Unmap(dynamicVertexBuffer, 0);
		}

		private static VectorPOD<WireVertexData> GetEdgeLines(MeshRenderCommand command)
		{
			var wireColor = command.WireFrameColor.Alpha0To1 > 0
				? command.WireFrameColor
				: new AggColor(25, 25, 25);

			return command.RenderType switch
			{
				RenderTypes.Outlines => MeshWirePlugin.Get(command.Mesh, wireColor, SceneRenderModeUtilities.OutlineFeatureAngleRadians, command.MeshChanged).EdgeLines,
				RenderTypes.NonManifold => MeshNonManifoldPlugin.Get(command.Mesh, wireColor, command.MeshChanged).EdgeLines,
				RenderTypes.Polygons => MeshWirePlugin.Get(command.Mesh, wireColor, meshChanged: command.MeshChanged).EdgeLines,
				RenderTypes.Wireframe => MeshWirePlugin.Get(command.Mesh, wireColor, meshChanged: command.MeshChanged).EdgeLines,
				_ => null,
			};
		}

		private ID3D11RasterizerState GetSceneRasterizerState(bool forceCullBackFaces, bool offsetFill)
		{
			if (!offsetFill)
			{
				return forceCullBackFaces ? rasterizerCullBack : rasterizerNoCull;
			}

			var cullMode = forceCullBackFaces ? CullMode.Back : CullMode.None;
			return GetOrCreateRasterizerState(cullMode, scissor: false, depthBias: 1, slopeBias: 1);
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
			values[14] = SceneRenderModeUtilities.DefaultWireframeWidth;
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
			ApplyDefaultSceneViewport();
			renderStateDirty = true;
			transformDirty = true;
		}

		private void ApplyDefaultSceneViewport()
		{
			var viewport = SceneViewportUtilities.CreateDefaultFramebufferViewport(activeSceneRenderContext.Viewport, renderTargetHeight);
			context.RSSetViewport(viewport);
		}

		private void UnbindSceneTextures()
		{
			context.PSSetShaderResource(0, null);
			context.PSSetShaderResource(1, null);
			context.PSSetShaderResource(2, null);
			lastBoundTextureView = null;
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
			dualDepthPeelTarget0?.Dispose();
			dualDepthPeelTarget1?.Dispose();
			dualFrontAccumTarget?.Dispose();
			dualBackAccumTarget?.Dispose();
			dualDepthPeelTarget0 = null;
			dualDepthPeelTarget1 = null;
			dualFrontAccumTarget = null;
			dualBackAccumTarget = null;

			sceneEffectVS?.Dispose();
			sceneEffectColorPS?.Dispose();
			sceneEffectTexturePS?.Dispose();
			sceneEffectSelectionPS?.Dispose();
			sceneEffectDepthPS?.Dispose();
			sceneEffectDualDepthInitPS?.Dispose();
			sceneEffectDualPeelColorPS?.Dispose();
			sceneEffectDualPeelTexturePS?.Dispose();
			sceneEffectInputLayout?.Dispose();
			fullscreenVS?.Dispose();
			copyTexturePS?.Dispose();
			resolveDualPeelPS?.Dispose();
			outlineCompositePS?.Dispose();
			sceneEffectBuffer?.Dispose();
			outlineCompositeBuffer?.Dispose();
			pointClampSampler?.Dispose();
			dualDepthPeelBlendState?.Dispose();
			whiteTextureView?.Dispose();
			whiteTexture?.Dispose();

			sceneEffectVS = null;
			sceneEffectColorPS = null;
			sceneEffectTexturePS = null;
			sceneEffectSelectionPS = null;
			sceneEffectDepthPS = null;
			sceneEffectDualDepthInitPS = null;
			sceneEffectDualPeelColorPS = null;
			sceneEffectDualPeelTexturePS = null;
			sceneEffectInputLayout = null;
			fullscreenVS = null;
			copyTexturePS = null;
			resolveDualPeelPS = null;
			outlineCompositePS = null;
			sceneEffectBuffer = null;
			outlineCompositeBuffer = null;
			pointClampSampler = null;
			dualDepthPeelBlendState = null;
			whiteTextureView = null;
			whiteTexture = null;
			sceneEffectsInitialized = false;
		}
	}
}
