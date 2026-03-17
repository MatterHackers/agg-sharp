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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;
using AggColor = MatterHackers.Agg.Color;

namespace MatterHackers.RenderGl
{
	public partial class VorticeD3DGl
	{
		private const int BedCompositeTextureSize = 2048;
		private const int BedShadowTextureSize = 2048;
		private const float BedShadowStrength = .70f;
		private const float BedShadowViewDistance = 1000;
		private const int SceneEffectVertexFloatStride = SceneEdgeShaderDataPlugin.TotalVertexFloatStride;
		private const int SceneEffectVertexStride = SceneEffectVertexFloatStride * sizeof(float);
		private int depthPeelingLayers = 6;

		public int DepthPeelingLayers
		{
			get => depthPeelingLayers;
			set => depthPeelingLayers = SceneTransparencyModeUtilities.NormalizeDepthPeelingLayers(value);
		}

		private readonly List<MeshRenderCommand> queuedSceneCommands = new();
		private readonly List<MeshRenderCommand> queuedOverlayCommands = new();
		private BedRenderCommand queuedBedCommand;
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
		private ColorTextureTarget transparentOverlayTarget;
		private ColorTextureTarget dualDepthPeelTarget0;
		private ColorTextureTarget dualDepthPeelTarget1;
		private ColorTextureTarget dualFrontAccumTarget;
		private ColorTextureTarget dualBackAccumTarget;
		private ColorTextureTarget resolvedSceneTarget;
		private ColorTextureTarget bedShadowMaskTarget;
		private ColorTextureTarget bedShadowBlurTargetA;
		private ColorTextureTarget bedShadowBlurTargetB;
		private ColorTextureTarget bedCompositeTarget;

		private ID3D11VertexShader sceneEffectVS;
		private ID3D11VertexShader sceneEffectSelectionVS;
		private ID3D11PixelShader sceneEffectColorPS;
		private ID3D11PixelShader sceneEffectTexturePS;
		private ID3D11PixelShader sceneEffectAlphaBlendColorPS;
		private ID3D11PixelShader sceneEffectAlphaBlendTexturePS;
		private ID3D11PixelShader sceneEffectSelectionPS;
		private ID3D11PixelShader sceneEffectDepthPS;
		private ID3D11PixelShader sceneEffectDualDepthInitPS;
		private ID3D11PixelShader sceneEffectDualPeelColorPS;
		private ID3D11PixelShader sceneEffectDualPeelTexturePS;
		private ID3D11InputLayout sceneEffectInputLayout;
		private ID3D11InputLayout sceneEffectSelectionInputLayout;

		private ID3D11VertexShader fullscreenVS;
		private ID3D11PixelShader copyTexturePS;
		private ID3D11PixelShader resolveDualPeelPS;
		private ID3D11PixelShader bedShadowBlurPS;
		private ID3D11PixelShader bedShadowCompositePS;
		private ID3D11PixelShader outlineCompositePS;
		private ID3D11BlendState alphaApproximationBlendState;
		private ID3D11BlendState dualDepthPeelBlendState;
		private ID3D11BlendState premultipliedSceneBlitBlendState;
		private ID3D11BlendState resolvedSceneBlitBlendState;

		private ID3D11Buffer sceneEffectBuffer;
		private ID3D11Buffer outlineCompositeBuffer;
		private ID3D11Buffer bedShadowPostProcessBuffer;
		private ID3D11SamplerState pointClampSampler;
		private ID3D11SamplerState linearClampSampler;
		private ID3D11Texture2D whiteTexture;
		private ID3D11ShaderResourceView whiteTextureView;
		private ImageTextureSource bedBaseTexture;
		private int lastBedShadowSignature;

		private bool sceneEffectsInitialized;

		private sealed class SelectionOutlineCommand
		{
			public AggColor Color;
			public Mesh Mesh;
			public Matrix4X4 Transform;
		}

		private sealed class TransparentSceneDrawCommand
		{
			public MeshRenderCommand Command;
			public bool EnableWireframe;
			public ID3D11ShaderResourceView ForcedTextureView;
			public bool Unlit;
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

		private sealed class ImageTextureSource : IDisposable
		{
			public bool ConvertPremultipliedToStraightAlpha;
			public MatterHackers.Agg.Image.ImageBuffer SourceImage;
			public ID3D11ShaderResourceView ShaderResourceView;
			public ID3D11Texture2D Texture;

			public void Dispose()
			{
				ShaderResourceView?.Dispose();
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
			byte[] selectionVsByteCode = Compiler.Compile(sceneEffectsHlsl, "SelectionVS", "NodeDesignerScene.hlsl", "vs_5_0").ToArray();
			byte[] sceneColorPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneColorPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] sceneTexturePsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneTexturePS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] sceneAlphaBlendColorPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneColorAlphaBlendPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] sceneAlphaBlendTexturePsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneTextureAlphaBlendPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] selectionPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SelectionMaskPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] depthPsByteCode = Compiler.Compile(sceneEffectsHlsl, "DepthOnlyPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] dualDepthInitPsByteCode = Compiler.Compile(sceneEffectsHlsl, "DualDepthInitPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] dualPeelColorPsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneColorDualPeelPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();
			byte[] dualPeelTexturePsByteCode = Compiler.Compile(sceneEffectsHlsl, "SceneTextureDualPeelPS", "NodeDesignerScene.hlsl", "ps_5_0").ToArray();

			sceneEffectVS = device.CreateVertexShader(sceneVsByteCode);
			sceneEffectSelectionVS = device.CreateVertexShader(selectionVsByteCode);
			sceneEffectColorPS = device.CreatePixelShader(sceneColorPsByteCode);
			sceneEffectTexturePS = device.CreatePixelShader(sceneTexturePsByteCode);
			sceneEffectAlphaBlendColorPS = device.CreatePixelShader(sceneAlphaBlendColorPsByteCode);
			sceneEffectAlphaBlendTexturePS = device.CreatePixelShader(sceneAlphaBlendTexturePsByteCode);
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
				new InputElementDescription("TEXCOORD", 1, Format.R32G32B32_Float, 32, 0),
				new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 44, 0),
			}, sceneVsByteCode);
			sceneEffectSelectionInputLayout = device.CreateInputLayout(new[]
			{
				new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
			}, selectionVsByteCode);

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] fullscreenVsByteCode = Compiler.Compile(postProcessHlsl, "FullScreenVS", "NodeDesignerPostProcess.hlsl", "vs_5_0").ToArray();
			byte[] copyPsByteCode = Compiler.Compile(postProcessHlsl, "CopyTexturePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] resolvePsByteCode = Compiler.Compile(postProcessHlsl, "ResolveDualPeelPS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] bedBlurPsByteCode = Compiler.Compile(postProcessHlsl, "BedShadowBlurPS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] bedCompositePsByteCode = Compiler.Compile(postProcessHlsl, "BedShadowCompositePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			byte[] outlinePsByteCode = Compiler.Compile(postProcessHlsl, "OutlineCompositePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();

			fullscreenVS = device.CreateVertexShader(fullscreenVsByteCode);
			copyTexturePS = device.CreatePixelShader(copyPsByteCode);
			resolveDualPeelPS = device.CreatePixelShader(resolvePsByteCode);
			bedShadowBlurPS = device.CreatePixelShader(bedBlurPsByteCode);
			bedShadowCompositePS = device.CreatePixelShader(bedCompositePsByteCode);
			outlineCompositePS = device.CreatePixelShader(outlinePsByteCode);
		}

		private void CreateSceneEffectBuffers()
		{
			sceneEffectBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 80, // 5 float4s: MeshColor, WireframeColor, EffectFlags, ResolutionAndWidth, ExtraFlags
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

			bedShadowPostProcessBuffer = device.CreateBuffer(new BufferDescription
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

			linearClampSampler = device.CreateSamplerState(new SamplerDescription
			{
				Filter = Filter.MinMagMipLinear,
				AddressU = TextureAddressMode.Clamp,
				AddressV = TextureAddressMode.Clamp,
				AddressW = TextureAddressMode.Clamp,
				ComparisonFunc = ComparisonFunction.Never,
				MinLOD = 0,
				MaxLOD = float.MaxValue,
			});

			alphaApproximationBlendState = CreateAlphaApproximationBlendState();
			dualDepthPeelBlendState = CreateDualDepthPeelBlendState();
		}

		private ID3D11BlendState CreateAlphaApproximationBlendState()
		{
			var blendDescription = new BlendDescription();
			blendDescription.RenderTarget[0] = new RenderTargetBlendDescription
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

		private void QueueBedCommand(BedRenderCommand command)
		{
			if (command?.Mesh == null)
			{
				return;
			}

			queuedBedCommand = command;
		}

		private void RenderQueuedSceneEffects()
		{
			if (activeSceneRenderContext == null)
			{
				ClearQueuedSceneEffects();
				return;
			}

			if (queuedSceneCommands.Count == 0
				&& queuedOverlayCommands.Count == 0
				&& queuedSelectionOutlines.Count == 0
				&& queuedBedCommand == null)
			{
				return;
			}

			EnsureSceneEffectsInitialized();
			ApplySceneLighting(activeSceneRenderContext.Lighting);
			UpdateLightBuffer(true, true);

			int width = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Width));
			int height = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Height));

			EnsureSceneTargets(width, height);
			if (queuedBedCommand != null)
			{
				RenderBedShadowTexture(queuedBedCommand);
			}

			ResetPipelineStateTracking();
			var renderPlan = renderPlanner.Build(queuedSceneCommands);

			RenderOpaqueCommands(renderPlan.OpaqueCommands);
			RenderSceneDepth(renderPlan, queuedBedCommand);
			if (SceneTransparencyModeUtilities.GetSceneTransparencyMode(DepthPeelingLayers) == SceneTransparencyMode.DualDepthPeeling)
			{
				RenderTransparentLayers(renderPlan.TransparentCommands, queuedBedCommand);
			}
			else
			{
				RenderTransparentAlphaBlend(renderPlan.TransparentCommands, queuedBedCommand);
			}
			RenderTransparentOverlays();
			CompositeSceneTargets();
			BlitResolvedSceneToScreen();
			RenderSelectionOutlines();
			RestoreDefaultSceneTarget();
		}

		private void EnsureSceneTargets(int width, int height)
		{
			sceneColorTarget = EnsureSceneTarget(sceneColorTarget, width, height, withColor: true);
			sceneDepthTarget = EnsureSceneTarget(sceneDepthTarget, width, height, withColor: false);
			selectionTarget = EnsureSceneTarget(selectionTarget, width, height, withColor: true);
			resolvedSceneTarget = EnsureColorTarget(resolvedSceneTarget, width, height, Format.R8G8B8A8_UNorm);
			transparentOverlayTarget = EnsureColorTarget(transparentOverlayTarget, width, height, Format.R8G8B8A8_UNorm);
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

		private ImageTextureSource EnsureImageTextureSource(
			ImageTextureSource existingSource,
			MatterHackers.Agg.Image.ImageBuffer sourceImage,
			bool convertPremultipliedToStraightAlpha = false)
		{
			if (sourceImage == null)
			{
				existingSource?.Dispose();
				return null;
			}

			if (ReferenceEquals(existingSource?.SourceImage, sourceImage)
				&& existingSource.ConvertPremultipliedToStraightAlpha == convertPremultipliedToStraightAlpha
				&& existingSource.ShaderResourceView != null)
			{
				return existingSource;
			}

			existingSource?.Dispose();
			var textureSource = new ImageTextureSource
			{
				ConvertPremultipliedToStraightAlpha = convertPremultipliedToStraightAlpha,
				SourceImage = sourceImage,
			};

			var textureDescription = new Texture2DDescription
			{
				Width = (uint)sourceImage.Width,
				Height = (uint)sourceImage.Height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.B8G8R8A8_UNorm,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.ShaderResource,
			};

			var pixels = convertPremultipliedToStraightAlpha
				? ImageAlphaConverter.ConvertPremultipliedBgraToStraightAlpha(sourceImage.GetBuffer())
				: sourceImage.GetBuffer();
			var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
			try
			{
				textureSource.Texture = device.CreateTexture2D(
					textureDescription,
					new[]
					{
						new SubresourceData(handle.AddrOfPinnedObject(), (uint)(sourceImage.Width * 4))
					});
			}
			finally
			{
				handle.Free();
			}

			textureSource.ShaderResourceView = device.CreateShaderResourceView(textureSource.Texture);
			return textureSource;
		}

		private void EnsureBedTargets(int width, int height)
		{
			var shadowWidth = Math.Min(width, BedShadowTextureSize);
			var shadowHeight = Math.Min(height, BedShadowTextureSize);

			var prevComposite = bedCompositeTarget;
			bedShadowMaskTarget = EnsureColorTarget(bedShadowMaskTarget, shadowWidth, shadowHeight, Format.R8G8B8A8_UNorm);
			bedShadowBlurTargetA = EnsureColorTarget(bedShadowBlurTargetA, shadowWidth, shadowHeight, Format.R8G8B8A8_UNorm);
			bedShadowBlurTargetB = EnsureColorTarget(bedShadowBlurTargetB, shadowWidth, shadowHeight, Format.R8G8B8A8_UNorm);
			bedCompositeTarget = EnsureColorTarget(
				bedCompositeTarget,
				Math.Min(width, BedCompositeTextureSize),
				Math.Min(height, BedCompositeTextureSize),
				Format.R8G8B8A8_UNorm);

			if (bedCompositeTarget != prevComposite)
			{
				lastBedShadowSignature = 0;
			}
		}

		private void RenderBedShadowTexture(BedRenderCommand bedCommand)
		{
			if (bedCommand?.TopBaseTexture == null)
			{
				return;
			}

			EnsureBedTargets(bedCommand.TopBaseTexture.Width, bedCommand.TopBaseTexture.Height);
			// AGG stores this generated texture with premultiplied color channels.
			// Convert it back to straight alpha for the D3D textured mesh pipeline so
			// a translucent white bed stays visually white instead of turning gray.
			bedBaseTexture = EnsureImageTextureSource(
				bedBaseTexture,
				bedCommand.TopBaseTexture,
				convertPremultipliedToStraightAlpha: true);

			var signature = ComputeBedShadowSignature(bedCommand);
			if (signature == lastBedShadowSignature
				&& bedCompositeTarget?.ShaderResourceView != null)
			{
				return;
			}

			lastBedShadowSignature = signature;

			RenderBedShadowMask(bedCommand);
			RenderBedBlurPass(bedShadowMaskTarget.ShaderResourceView, bedShadowBlurTargetA, 1.0f / bedShadowMaskTarget.Width, 0);
			RenderBedBlurPass(bedShadowBlurTargetA.ShaderResourceView, bedShadowBlurTargetB, 0, 1.0f / bedShadowMaskTarget.Height);
			RenderBedCompositePass(bedCommand);
		}

		private int ComputeBedShadowSignature(BedRenderCommand bedCommand)
		{
			var hash = new HashCode();
			hash.Add(bedCommand.ObjectsBelowBed);
			hash.Add(bedCommand.BedBounds.Left);
			hash.Add(bedCommand.BedBounds.Right);
			hash.Add(bedCommand.BedBounds.Bottom);
			hash.Add(bedCommand.BedBounds.Top);
			hash.Add(RuntimeHelpers.GetHashCode(bedCommand.TopBaseTexture));

			foreach (var command in queuedSceneCommands)
			{
				if (!ShouldRenderCommandIntoBedShadow(command, bedCommand.BedBounds))
				{
					continue;
				}

				hash.Add(RuntimeHelpers.GetHashCode(command.Mesh));
				hash.Add(command.Mesh.ChangedCount);
				hash.Add(command.Transform);
			}

			return hash.ToHashCode();
		}

		private void RenderBedShadowMask(BedRenderCommand bedCommand)
		{
			BindColorTarget(bedShadowMaskTarget);
			ClearColorTarget(bedShadowMaskTarget, new Color4(0, 0, 0, 0));

			var bedCenter = new Vector3(
				(bedCommand.BedBounds.Left + bedCommand.BedBounds.Right) * .5,
				(bedCommand.BedBounds.Bottom + bedCommand.BedBounds.Top) * .5,
				0);
			var shadowView = Matrix4X4.LookAt(
				bedCenter + new Vector3(0, 0, BedShadowViewDistance),
				bedCenter,
				Vector3.UnitY);
			var shadowProjection = Matrix4X4.CreateOrthographicOffCenter(
				bedCommand.BedBounds.Left,
				bedCommand.BedBounds.Right,
				bedCommand.BedBounds.Bottom,
				bedCommand.BedBounds.Top,
				1,
				BedShadowViewDistance * 2);

			foreach (var command in queuedSceneCommands)
			{
				if (!ShouldRenderCommandIntoBedShadow(command, bedCommand.BedBounds))
				{
					continue;
				}

				// Glyph meshes can have mixed winding on their caps and sides; forcing the
				// shadow mask to render without culling avoids clipped letter silhouettes.
				var shadowCommand = RenderHelper.CreateBedShadowCommand(command);
				RenderFlatMask(shadowCommand, shadowCommand.Transform * shadowView, shadowProjection, AggColor.Black, enableDepthTest: false);
			}

			UnbindSceneTextures();
		}

		private static bool ShouldRenderCommandIntoBedShadow(MeshRenderCommand command, RectangleDouble bedBounds)
		{
			return RenderHelper.ShouldRenderInBedShadow(command, bedBounds);
		}

		private void RenderBedBlurPass(ID3D11ShaderResourceView sourceTexture, ColorTextureTarget destinationTarget, float directionX, float directionY)
		{
			BindColorTarget(destinationTarget);
			ClearColorTarget(destinationTarget, new Color4(0, 0, 0, 0));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(bedShadowBlurPS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetSampler(1, linearClampSampler);
			context.PSSetConstantBuffer(1, bedShadowPostProcessBuffer);
			context.PSSetShaderResource(0, sourceTexture);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			context.RSSetState(rasterizerNoCull);
			UpdateBedShadowPostProcessBuffer(directionX, directionY, BedShadowStrength, AggColor.Transparent);
			context.Draw(3, 0);
			UnbindSceneTextures();
		}

		private void RenderBedCompositePass(BedRenderCommand bedCommand)
		{
			if (bedBaseTexture?.ShaderResourceView == null)
			{
				return;
			}

			BindColorTarget(bedCompositeTarget);
			ClearColorTarget(bedCompositeTarget, new Color4(0, 0, 0, 0));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(bedShadowCompositePS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetSampler(1, linearClampSampler);
			context.PSSetConstantBuffer(1, bedShadowPostProcessBuffer);
			context.PSSetShaderResource(0, bedBaseTexture.ShaderResourceView);
			context.PSSetShaderResource(1, bedShadowBlurTargetB.ShaderResourceView);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			context.RSSetState(rasterizerNoCull);
			UpdateBedShadowPostProcessBuffer(0, 0, BedShadowStrength, bedCommand.ShadowColor);
			context.Draw(3, 0);
			UnbindSceneTextures();
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
				if (SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType))
				{
					RenderMeshCommand(
						command,
						null,
						enableWireframe: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
						wireframeOnly: SceneRenderModeUtilities.IsWireframeOnly(command.RenderType),
						offsetFill: false,
						enableDepthPeeling: false,
						firstPeelPass: false,
						opaqueDepthView: null,
						nearDepthView: null);
				}
			}
		}

		private void RenderSceneDepth(NativeSceneRenderPlan renderPlan, BedRenderCommand bedCommand)
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

			if (bedCommand != null && bedCompositeTarget?.ShaderResourceView != null)
			{
				RenderMeshCommand(
					CreateBedSceneCommand(bedCommand),
					sceneEffectDepthPS,
					false,
					false,
					false,
					false,
					false,
					null,
					null,
					colorWritesEnabled: false,
					forcedTextureView: bedCompositeTarget.ShaderResourceView);
			}
		}

		private void RenderTransparentLayers(IReadOnlyList<MeshRenderCommand> transparentCommands, BedRenderCommand bedCommand)
		{
			ClearTransparentCompositeTargets();

			if (transparentCommands.Count == 0 && bedCommand == null)
			{
				return;
			}

			var dualPeelDepthState = GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false);
			InitializeDualDepthPeel(transparentCommands, bedCommand, dualPeelDepthState);

			var sourceDepthTarget = dualDepthPeelTarget0;
			var destinationDepthTarget = dualDepthPeelTarget1;
			for (int iterationIndex = 0; iterationIndex < DualDepthPeelingMath.GetIterationCount(DepthPeelingLayers); iterationIndex++)
			{
				BindDualPeelTargets(destinationDepthTarget, dualFrontAccumTarget, dualBackAccumTarget);
				ClearColorTarget(destinationDepthTarget, new Color4(-1, -1, 0, 0));

				foreach (var command in transparentCommands)
				{
					if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType)
						|| !SceneRenderModeUtilities.ShouldRenderTransparentFill(command.RenderType))
					{
						continue;
					}

					RenderMeshCommand(
						command,
						null,
						enableWireframe: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
						wireframeOnly: SceneRenderModeUtilities.IsWireframeOnly(command.RenderType),
						offsetFill: false,
						enableDepthPeeling: false,
						firstPeelPass: false,
						opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
						nearDepthView: sourceDepthTarget.ShaderResourceView,
						colorWritesEnabled: true,
						blendStateOverride: dualDepthPeelBlendState,
						depthStencilStateOverride: dualPeelDepthState,
						useDualDepthPeelingShader: true);
				}

				if (bedCommand != null && bedCompositeTarget?.ShaderResourceView != null)
				{
					RenderMeshCommand(
						CreateBedSceneCommand(bedCommand),
						null,
						enableWireframe: false,
						wireframeOnly: false,
						offsetFill: false,
						enableDepthPeeling: false,
						firstPeelPass: false,
						opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
						nearDepthView: sourceDepthTarget.ShaderResourceView,
						colorWritesEnabled: true,
						blendStateOverride: dualDepthPeelBlendState,
						depthStencilStateOverride: dualPeelDepthState,
						useDualDepthPeelingShader: true,
						forcedTextureView: bedCompositeTarget.ShaderResourceView,
						unlit: true);
				}

				(sourceDepthTarget, destinationDepthTarget) = (destinationDepthTarget, sourceDepthTarget);
			}
		}

		private void RenderTransparentAlphaBlend(IReadOnlyList<MeshRenderCommand> transparentCommands, BedRenderCommand bedCommand)
		{
			ClearTransparentCompositeTargets();

			var drawCommands = BuildTransparentAlphaBlendCommands(transparentCommands);
			var bedDrawCommand = CreateTransparentBedDrawCommand(bedCommand);
			if (drawCommands.Count == 0 && bedDrawCommand == null)
			{
				return;
			}

			BindSceneTarget(sceneColorTarget);
			var noDepthWriteState = GetOrCreateDepthStencilState(true, ComparisonFunction.LessEqual, false);

			if (bedDrawCommand != null
				&& !SceneTransparencyModeUtilities.ShouldRenderBedAfterTransparentObjects(
					bedDrawCommand.Command.Transform,
					activeSceneRenderContext.WorldView.EyePosition))
			{
				RenderTransparentBedAlphaBlend(bedDrawCommand, alphaApproximationBlendState, noDepthWriteState);
			}

			foreach (var drawCommand in drawCommands)
			{
				RenderTransparentAlphaBlendPass(drawCommand, alphaApproximationBlendState, noDepthWriteState, CullMode.Front, enableWireframe: false);
				RenderTransparentAlphaBlendPass(drawCommand, alphaApproximationBlendState, noDepthWriteState, CullMode.Back, enableWireframe: drawCommand.EnableWireframe);
			}

			if (bedDrawCommand != null
				&& SceneTransparencyModeUtilities.ShouldRenderBedAfterTransparentObjects(
					bedDrawCommand.Command.Transform,
					activeSceneRenderContext.WorldView.EyePosition))
			{
				RenderTransparentBedAlphaBlend(bedDrawCommand, alphaApproximationBlendState, noDepthWriteState);
			}
		}

		private List<TransparentSceneDrawCommand> BuildTransparentAlphaBlendCommands(
			IReadOnlyList<MeshRenderCommand> transparentCommands)
		{
			var drawCommands = new List<TransparentSceneDrawCommand>();
			foreach (var command in SceneTransparencyModeUtilities.SortTransparentCommandsBackToFront(
				transparentCommands,
				activeSceneRenderContext.WorldView.ModelviewMatrix))
			{
				if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType)
					|| !SceneRenderModeUtilities.ShouldRenderTransparentFill(command.RenderType))
				{
					continue;
				}

				drawCommands.Add(new TransparentSceneDrawCommand
				{
					Command = command,
					EnableWireframe = SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
				});
			}

			return drawCommands
				.OrderBy(drawCommand => SceneTransparencyModeUtilities.GetTransparentSortDepth(
					drawCommand.Command,
					activeSceneRenderContext.WorldView.ModelviewMatrix))
				.ToList();
		}

		private TransparentSceneDrawCommand CreateTransparentBedDrawCommand(BedRenderCommand bedCommand)
		{
			if (bedCommand == null || bedCompositeTarget?.ShaderResourceView == null)
			{
				return null;
			}

			return new TransparentSceneDrawCommand
			{
				Command = CreateBedSceneCommand(bedCommand),
				ForcedTextureView = bedCompositeTarget.ShaderResourceView,
				Unlit = true,
			};
		}

		private void RenderTransparentAlphaBlendPass(
			TransparentSceneDrawCommand drawCommand,
			ID3D11BlendState alphaBlendState,
			ID3D11DepthStencilState noDepthWriteState,
			CullMode cullMode,
			bool enableWireframe)
		{
			RenderMeshCommand(
				drawCommand.Command,
				null,
				enableWireframe: enableWireframe,
				wireframeOnly: false,
				offsetFill: false,
				enableDepthPeeling: false,
				firstPeelPass: false,
				opaqueDepthView: null,
				nearDepthView: null,
				colorWritesEnabled: true,
				blendStateOverride: alphaBlendState,
				depthStencilStateOverride: noDepthWriteState,
				useDualDepthPeelingShader: false,
				useAlphaBlendShader: true,
				forcedTextureView: drawCommand.ForcedTextureView,
				unlit: drawCommand.Unlit,
				cullModeOverride: cullMode);
		}

		private void RenderTransparentBedAlphaBlend(
			TransparentSceneDrawCommand bedDrawCommand,
			ID3D11BlendState alphaBlendState,
			ID3D11DepthStencilState noDepthWriteState)
		{
			RenderMeshCommand(
				bedDrawCommand.Command,
				null,
				enableWireframe: false,
				wireframeOnly: false,
				offsetFill: false,
				enableDepthPeeling: false,
				firstPeelPass: false,
				opaqueDepthView: null,
				nearDepthView: null,
				colorWritesEnabled: true,
				blendStateOverride: alphaBlendState,
				depthStencilStateOverride: noDepthWriteState,
				useDualDepthPeelingShader: false,
				useAlphaBlendShader: true,
				forcedTextureView: bedDrawCommand.ForcedTextureView,
				unlit: bedDrawCommand.Unlit);
		}

		private void ClearTransparentCompositeTargets()
		{
			ClearColorTarget(dualFrontAccumTarget, new Color4(0, 0, 0, 1));
			ClearColorTarget(dualBackAccumTarget, new Color4(0, 0, 0, 0));
			ClearColorTarget(dualDepthPeelTarget0, new Color4(-1, -1, 0, 0));
			ClearColorTarget(dualDepthPeelTarget1, new Color4(-1, -1, 0, 0));
		}

		private void CompositeSceneTargets()
		{
			if (!SceneTransparencyModeUtilities.ShouldUseDualDepthPeelResolve(DepthPeelingLayers))
			{
				CompositeSceneTargetsAlphaBlend();
				return;
			}

			BindColorTarget(resolvedSceneTarget);
			ClearColorTarget(resolvedSceneTarget, new Color4(0, 0, 0, 0));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetSampler(1, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			DrawFullscreenResolve(
				sceneColorTarget.ColorShaderResourceView,
				dualFrontAccumTarget.ShaderResourceView,
				dualBackAccumTarget.ShaderResourceView,
				transparentOverlayTarget.ShaderResourceView);

			UnbindSceneTextures();
		}

		private void CompositeSceneTargetsAlphaBlend()
		{
			BindColorTarget(resolvedSceneTarget);
			ClearColorTarget(resolvedSceneTarget, new Color4(0, 0, 0, 0));
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetSampler(0, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);

			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			DrawFullscreenTexture(sceneColorTarget.ColorShaderResourceView, copyTexturePS);

			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			DrawFullscreenTexture(transparentOverlayTarget.ShaderResourceView, copyTexturePS);

			UnbindSceneTextures();
		}

		private void BlitResolvedSceneToScreen()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			ApplyDefaultSceneViewport();
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(copyTexturePS);
			context.PSSetSampler(0, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);

			if (resolvedSceneBlitBlendState == null)
			{
				var desc = new BlendDescription();
				desc.RenderTarget[0] = new RenderTargetBlendDescription
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
				resolvedSceneBlitBlendState = device.CreateBlendState(desc);
			}

			if (premultipliedSceneBlitBlendState == null)
			{
				var desc = new BlendDescription();
				desc.RenderTarget[0] = new RenderTargetBlendDescription
				{
					BlendEnable = true,
					SourceBlend = Blend.One,
					DestinationBlend = Blend.InverseSourceAlpha,
					BlendOperation = BlendOperation.Add,
					SourceBlendAlpha = Blend.One,
					DestinationBlendAlpha = Blend.InverseSourceAlpha,
					BlendOperationAlpha = BlendOperation.Add,
					RenderTargetWriteMask = ColorWriteEnable.All,
				};
				premultipliedSceneBlitBlendState = device.CreateBlendState(desc);
			}

			context.OMSetBlendState(
				SceneTransparencyModeUtilities.ShouldUseDualDepthPeelResolve(DepthPeelingLayers)
					? resolvedSceneBlitBlendState
					: premultipliedSceneBlitBlendState);
			context.PSSetShaderResource(0, resolvedSceneTarget.ShaderResourceView);
			context.Draw(3, 0);
			UnbindSceneTextures();
		}

		private void RenderTransparentOverlays()
		{
			BindColorTarget(transparentOverlayTarget);
			ClearColorTarget(transparentOverlayTarget, new Color4(0, 0, 0, 0));

			if (queuedOverlayCommands.Count == 0)
			{
				return;
			}

			// Render overlay commands with no depth test and alpha blending.
			// These are 3D controls drawn as semi-transparent ghosts, always visible on top.
			var noDepthState = GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false);
			var alphaBlend = GetOrCreateBlendState(
				true,
				(int)BlendingFactorSrc.SrcAlpha,
				(int)BlendingFactorDest.OneMinusSrcAlpha,
				ColorWriteEnable.All);

			foreach (var command in queuedOverlayCommands)
			{
				if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType))
				{
					continue;
				}

				RenderMeshCommand(
					command,
					null,
					enableWireframe: false,
					wireframeOnly: false,
					offsetFill: false,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: null,
					nearDepthView: null,
					colorWritesEnabled: true,
					blendStateOverride: alphaBlend,
					depthStencilStateOverride: noDepthState);
			}
		}

		private void InitializeDualDepthPeel(IReadOnlyList<MeshRenderCommand> transparentCommands, BedRenderCommand bedCommand, ID3D11DepthStencilState depthState)
		{
			BindColorTarget(dualDepthPeelTarget0);
			ClearColorTarget(dualDepthPeelTarget0, new Color4(-1, -1, 0, 0));

			foreach (var command in transparentCommands)
			{
				if (!SceneRenderModeUtilities.RequiresSceneMeshPass(command.RenderType)
					|| !SceneRenderModeUtilities.ShouldRenderTransparentFill(command.RenderType))
				{
					continue;
				}

				RenderMeshCommand(
					command,
					sceneEffectDualDepthInitPS,
					enableWireframe: SceneRenderModeUtilities.ShouldDrawWireframeOverlay(command.RenderType),
					wireframeOnly: SceneRenderModeUtilities.IsWireframeOnly(command.RenderType),
					offsetFill: false,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
					nearDepthView: null,
					colorWritesEnabled: true,
					blendStateOverride: dualDepthPeelBlendState,
					depthStencilStateOverride: depthState);
			}

			if (bedCommand != null && bedCompositeTarget?.ShaderResourceView != null)
			{
				RenderMeshCommand(
					CreateBedSceneCommand(bedCommand),
					sceneEffectDualDepthInitPS,
					enableWireframe: false,
					wireframeOnly: false,
					offsetFill: false,
					enableDepthPeeling: false,
					firstPeelPass: false,
					opaqueDepthView: sceneColorTarget.DepthShaderResourceView,
					nearDepthView: null,
					colorWritesEnabled: true,
					blendStateOverride: dualDepthPeelBlendState,
					depthStencilStateOverride: depthState,
					forcedTextureView: bedCompositeTarget.ShaderResourceView);
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

				RenderFlatMask(
					command,
					command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix,
					activeSceneRenderContext.WorldView.ProjectionMatrix,
					command.Color,
					enableDepthTest: true);
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

		private static MeshRenderCommand CreateBedSceneCommand(BedRenderCommand bedCommand)
		{
			return bedCommand.CreateSceneCommand();
		}

		private unsafe void RenderFlatMask(MeshRenderCommand command, Matrix4X4 modelView, Matrix4X4 projection, AggColor color, bool enableDepthTest)
		{
			SetSceneMatrices(modelView, projection);
			UpdateTransformBuffer();
			UpdateSceneEffectBuffer(color, AggColor.Transparent, false, false, false, false, (float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height);

			context.IASetInputLayout(sceneEffectSelectionInputLayout);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(sceneEffectSelectionVS);
			context.VSSetConstantBuffer(0, transformBuffer);
			context.PSSetShader(sceneEffectSelectionPS);
			context.PSSetConstantBuffer(2, sceneEffectBuffer);

			var depthState = enableDepthTest
				? GetOrCreateDepthStencilState(true, ComparisonFunction.LessEqual, true)
				: GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false);
			if (ShouldBindDepthStencilState(depthState))
			{
				context.OMSetDepthStencilState(depthState);
			}

			var blendState = GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All);
			if (ShouldBindBlendState(blendState))
			{
				context.OMSetBlendState(blendState);
			}

			var rasterizerState = GetSceneRasterizerState(command.ForceCullBackFaces ? CullMode.Back : CullMode.None, offsetFill: false);
			if (ShouldBindRasterizerState(rasterizerState))
			{
				context.RSSetState(rasterizerState);
			}

			var glMeshPlugin = MeshTrianglePlugin.Get(command.Mesh);
			for (int subMeshIndex = 0; subMeshIndex < glMeshPlugin.subMeshs.Count; subMeshIndex++)
			{
				var subMesh = glMeshPlugin.subMeshs[subMeshIndex];
				var staticBuffer = subMesh.CachedSelectionGpuBuffer as ID3D11Buffer;
				if (staticBuffer == null && subMesh.positionData.Count > 0)
				{
					fixed (VertexPositionData* pPosition = subMesh.positionData.Array)
					{
						staticBuffer = device.CreateBuffer(new BufferDescription
						{
							ByteWidth = (uint)(subMesh.positionData.Count * VertexPositionData.Stride),
							Usage = ResourceUsage.Immutable,
							BindFlags = BindFlags.VertexBuffer,
						}, new SubresourceData((IntPtr)pPosition));
					}

					subMesh.CachedSelectionGpuBuffer = staticBuffer;
				}

				if (staticBuffer == null)
				{
					continue;
				}

				context.IASetVertexBuffer(0, staticBuffer, (uint)VertexPositionData.Stride);
				context.Draw((uint)subMesh.positionData.Count, 0);
			}
		}

		private void DrawFullscreenResolve(
			ID3D11ShaderResourceView sceneColorView,
			ID3D11ShaderResourceView frontAccumView,
			ID3D11ShaderResourceView backAccumView,
			ID3D11ShaderResourceView transparentOverlayView)
		{
			context.PSSetShader(resolveDualPeelPS);
			context.PSSetShaderResource(0, sceneColorView);
			context.PSSetShaderResource(1, frontAccumView);
			context.PSSetShaderResource(2, backAccumView);
			context.PSSetShaderResource(3, transparentOverlayView);
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

		private void BindColorTargetWithDepth(ColorTextureTarget target, ID3D11DepthStencilView depthStencil)
		{
			context.OMSetRenderTargets(target.RenderTargetView, depthStencil);
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
			bool useDualDepthPeelingShader = false,
			bool useAlphaBlendShader = false,
			ID3D11ShaderResourceView forcedTextureView = null,
			bool unlit = false,
			CullMode? cullModeOverride = null)
		{
			SetSceneMatrices(command.Transform * activeSceneRenderContext.WorldView.ModelviewMatrix, activeSceneRenderContext.WorldView.ProjectionMatrix);
			UpdateTransformBuffer();
			bool useVertexColor = command.Mesh.FaceColors != null && command.Mesh.FaceColors.Length > 0 && !command.OverrideFaceColors;
			UpdateSceneEffectBuffer(command.Color, command.WireFrameColor, enableWireframe, wireframeOnly, enableDepthPeeling, firstPeelPass, (float)activeSceneRenderContext.Viewport.Width, (float)activeSceneRenderContext.Viewport.Height, unlit || command.Unlit, useVertexColor, command.AlphaMultiplier);

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

			var rasterizerCullMode = cullModeOverride ?? (command.ForceCullBackFaces ? CullMode.Back : CullMode.None);
			var rasterizerState = GetSceneRasterizerState(rasterizerCullMode, offsetFill);
			if (ShouldBindRasterizerState(rasterizerState))
			{
				context.RSSetState(rasterizerState);
			}

			var glMeshPlugin = MeshTrianglePlugin.Get(command.Mesh);
			var sceneShaderData = SceneEdgeShaderDataPlugin.Get(command.Mesh, command.RenderType);
			for (int subMeshIndex = 0; subMeshIndex < glMeshPlugin.subMeshs.Count; subMeshIndex++)
			{
				var subMesh = glMeshPlugin.subMeshs[subMeshIndex];
				var sceneSubMesh = sceneShaderData.SubMeshes[subMeshIndex];
				bool useTexture = forcedTextureView != null || subMesh.texture != null;
				var pixelShader = overridePixelShader
					?? (useDualDepthPeelingShader
						? (useTexture ? sceneEffectDualPeelTexturePS : sceneEffectDualPeelColorPS)
						: useAlphaBlendShader
							? (useTexture ? sceneEffectAlphaBlendTexturePS : sceneEffectAlphaBlendColorPS)
							: (useTexture ? sceneEffectTexturePS : sceneEffectColorPS));

				if (ShouldBindPixelShader(pixelShader))
				{
					context.PSSetShader(pixelShader);
				}

				var textureView = forcedTextureView ?? whiteTextureView;
				if (forcedTextureView == null && subMesh.texture != null)
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

				int vertexCount = sceneSubMesh.InterleavedData.Length / SceneEffectVertexFloatStride;

				// Try to use or create a static GPU buffer for this submesh
				var staticBuffer = sceneSubMesh.CachedGpuBuffer as ID3D11Buffer;
				if (staticBuffer == null && sceneSubMesh.InterleavedData != null)
				{
					fixed (float* pData = sceneSubMesh.InterleavedData)
					{
						staticBuffer = device.CreateBuffer(
							new BufferDescription
							{
								ByteWidth = (uint)(sceneSubMesh.InterleavedData.Length * sizeof(float)),
								Usage = ResourceUsage.Immutable,
								BindFlags = BindFlags.VertexBuffer,
							},
							new SubresourceData((IntPtr)pData));
					}

					sceneSubMesh.CachedGpuBuffer = staticBuffer;
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

						int sourceFloatOffset = batchOffset * SceneEffectVertexFloatStride;
						int copyFloats = batchCount * SceneEffectVertexFloatStride;
						fixed (float* pSource = sceneSubMesh.InterleavedData)
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

		private ID3D11RasterizerState GetSceneRasterizerState(CullMode cullMode, bool offsetFill)
		{
			if (!offsetFill)
			{
				return cullMode == CullMode.Back
					? rasterizerCullBack
					: GetOrCreateRasterizerState(cullMode, scissor: false, depthBias: 0, slopeBias: 0);
			}

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
			float height,
			bool unlit = false,
			bool useVertexColor = false,
			float alphaMultiplier = 1.0f)
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
			values[7] = effectiveWireframeColor.Alpha0To1;

			values[8] = enableWireframe ? 1.0f : 0.0f;
			values[9] = wireframeOnly ? 1.0f : 0.0f;
			values[10] = enableDepthPeeling ? 1.0f : 0.0f;
			values[11] = firstPeelPass ? 1.0f : 0.0f;

			values[12] = width;
			values[13] = height;
			values[14] = SceneRenderModeUtilities.DefaultWireframeWidth;
			values[15] = unlit ? 1.0f : 0.0f;

			values[16] = useVertexColor ? 1.0f : 0.0f;
			values[17] = alphaMultiplier;
			values[18] = 0.0f;
			values[19] = 0.0f;

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

		private unsafe void UpdateBedShadowPostProcessBuffer(float directionX, float directionY, float shadowStrength, AggColor shadowColor)
		{
			var mapped = context.Map(bedShadowPostProcessBuffer, MapMode.WriteDiscard);
			float* values = (float*)mapped.DataPointer;
			values[0] = directionX;
			values[1] = directionY;
			values[2] = shadowStrength;
			values[3] = 0.0f;
			values[4] = shadowColor.Red0To1;
			values[5] = shadowColor.Green0To1;
			values[6] = shadowColor.Blue0To1;
			values[7] = shadowColor.Alpha0To1;
			context.Unmap(bedShadowPostProcessBuffer, 0);
		}

		private void RestoreDefaultSceneTarget()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			ApplyDefaultSceneViewport();

			// The native scene renderer changed D3D state directly, bypassing the GL
			// emulation layer. Invalidate the cached state so ApplyRenderState() will
			// re-apply the correct state on the next GL emulation draw call.
			lastAppliedBlendState = null;
			lastAppliedDepthStencilState = null;
			lastAppliedRasterizerState = null;

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
			context.PSSetShaderResource(3, null);
			lastBoundTextureView = null;
		}

		private void ClearQueuedSceneEffects()
		{
			queuedSceneCommands.Clear();
			queuedOverlayCommands.Clear();
			queuedBedCommand = null;
			queuedSelectionOutlines.Clear();
		}

		private void DisposeSceneEffects()
		{
			ClearQueuedSceneEffects();

			sceneColorTarget?.Dispose();
			sceneDepthTarget?.Dispose();
			selectionTarget?.Dispose();
			transparentOverlayTarget?.Dispose();
			resolvedSceneTarget?.Dispose();
			sceneColorTarget = null;
			sceneDepthTarget = null;
			selectionTarget = null;
			transparentOverlayTarget = null;
			resolvedSceneTarget = null;
			dualDepthPeelTarget0?.Dispose();
			dualDepthPeelTarget1?.Dispose();
			dualFrontAccumTarget?.Dispose();
			dualBackAccumTarget?.Dispose();
			bedShadowMaskTarget?.Dispose();
			bedShadowBlurTargetA?.Dispose();
			bedShadowBlurTargetB?.Dispose();
			bedCompositeTarget?.Dispose();
			dualDepthPeelTarget0 = null;
			dualDepthPeelTarget1 = null;
			dualFrontAccumTarget = null;
			dualBackAccumTarget = null;
			bedShadowMaskTarget = null;
			bedShadowBlurTargetA = null;
			bedShadowBlurTargetB = null;
			bedCompositeTarget = null;

			sceneEffectVS?.Dispose();
			sceneEffectSelectionVS?.Dispose();
			sceneEffectColorPS?.Dispose();
			sceneEffectTexturePS?.Dispose();
			sceneEffectAlphaBlendColorPS?.Dispose();
			sceneEffectAlphaBlendTexturePS?.Dispose();
			sceneEffectSelectionPS?.Dispose();
			sceneEffectDepthPS?.Dispose();
			sceneEffectDualDepthInitPS?.Dispose();
			sceneEffectDualPeelColorPS?.Dispose();
			sceneEffectDualPeelTexturePS?.Dispose();
			sceneEffectInputLayout?.Dispose();
			sceneEffectSelectionInputLayout?.Dispose();
			fullscreenVS?.Dispose();
			copyTexturePS?.Dispose();
			resolveDualPeelPS?.Dispose();
			bedShadowBlurPS?.Dispose();
			bedShadowCompositePS?.Dispose();
			outlineCompositePS?.Dispose();
			sceneEffectBuffer?.Dispose();
			outlineCompositeBuffer?.Dispose();
			bedShadowPostProcessBuffer?.Dispose();
			pointClampSampler?.Dispose();
			linearClampSampler?.Dispose();
			alphaApproximationBlendState?.Dispose();
			dualDepthPeelBlendState?.Dispose();
			premultipliedSceneBlitBlendState?.Dispose();
			resolvedSceneBlitBlendState?.Dispose();
			whiteTextureView?.Dispose();
			whiteTexture?.Dispose();
			bedBaseTexture?.Dispose();

			sceneEffectVS = null;
			sceneEffectSelectionVS = null;
			sceneEffectColorPS = null;
			sceneEffectTexturePS = null;
			sceneEffectAlphaBlendColorPS = null;
			sceneEffectAlphaBlendTexturePS = null;
			sceneEffectSelectionPS = null;
			sceneEffectDepthPS = null;
			sceneEffectDualDepthInitPS = null;
			sceneEffectDualPeelColorPS = null;
			sceneEffectDualPeelTexturePS = null;
			sceneEffectInputLayout = null;
			sceneEffectSelectionInputLayout = null;
			fullscreenVS = null;
			copyTexturePS = null;
			resolveDualPeelPS = null;
			bedShadowBlurPS = null;
			bedShadowCompositePS = null;
			outlineCompositePS = null;
			sceneEffectBuffer = null;
			outlineCompositeBuffer = null;
			bedShadowPostProcessBuffer = null;
			pointClampSampler = null;
			linearClampSampler = null;
			alphaApproximationBlendState = null;
			dualDepthPeelBlendState = null;
			premultipliedSceneBlitBlendState = null;
			whiteTextureView = null;
			whiteTexture = null;
			bedBaseTexture = null;
			sceneEffectsInitialized = false;
		}
	}
}
