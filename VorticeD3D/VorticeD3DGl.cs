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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using AggColor = MatterHackers.Agg.Color;

namespace MatterHackers.RenderGl
{
	public class VorticeD3DGl : IGpuContext, INativeSceneRenderer
	{
		/// <summary>
		/// The GL wrapper that owns this GPU context. Set by D3D11Control after construction.
		/// </summary>
		public GL OwnerGl { get; set; }

		private ID3D11Device device;
		private ID3D11DeviceContext context;
		private IDXGISwapChain swapChain;
		private ID3D11RenderTargetView renderTargetView;
		private ID3D11Texture2D currentBackBuffer;
		private ID3D11Texture2D mainRenderTarget;

		/// <summary>
		/// The main render target texture. Used for off-screen pixel readback.
		/// </summary>
		public ID3D11Texture2D MainRenderTarget => mainRenderTarget;
		private ID3D11DepthStencilView depthStencilView;
		private ID3D11Texture2D depthStencilBuffer;

		// Shaders for position+color rendering
		private ID3D11VertexShader posColorVS;
		private ID3D11PixelShader posColorPS;
		private ID3D11PixelShader posColorFlatPS;
		private ID3D11InputLayout posColorInputLayout;

		// Shaders for position+texture rendering
		private ID3D11VertexShader posTexVS;
		private ID3D11PixelShader posTexPS;
		private ID3D11PixelShader posTexFlatPS;
		private ID3D11InputLayout posTexInputLayout;

		// Shaders for lit position+color rendering (lighting computed on GPU)
		private ID3D11VertexShader posColorLitVS;
		private ID3D11PixelShader posColorLitPS;
		private ID3D11PixelShader posColorLitFlatPS;
		private ID3D11InputLayout posColorLitInputLayout;

		// Shaders for lit position+texture rendering (lighting computed on GPU)
		private ID3D11VertexShader posTexLitVS;
		private ID3D11PixelShader posTexLitPS;
		private ID3D11PixelShader posTexLitFlatPS;
		private ID3D11InputLayout posTexLitInputLayout;

		private bool flatShading = false;

		// Constant buffers
		private ID3D11Buffer transformBuffer;
		private ID3D11Buffer lightBuffer;

		// Dynamic vertex buffers (reused every frame via MapMode.WriteDiscard)
		private ID3D11Buffer dynamicVertexBuffer;
		private ID3D11Buffer dynamicTexVertexBuffer;
		private const int MaxVertices = 65536;

		// Blend state cache
		private Dictionary<(bool blend, int src, int dst, ColorWriteEnable mask), ID3D11BlendState> blendStateCache = new Dictionary<(bool, int, int, ColorWriteEnable), ID3D11BlendState>();
		private ColorWriteEnable currentColorWriteMask = ColorWriteEnable.All;

		// Depth stencil state cache
		private Dictionary<(bool enabled, ComparisonFunction func, bool writeMask), ID3D11DepthStencilState> depthStencilCache
			= new Dictionary<(bool, ComparisonFunction, bool), ID3D11DepthStencilState>();

		// Rasterizer states
		private ID3D11RasterizerState rasterizerNoCull;
		private ID3D11RasterizerState rasterizerCullBack;
		private ID3D11RasterizerState rasterizerCullFront;
		private ID3D11RasterizerState rasterizerScissor;

		// Sampler state
		private ID3D11SamplerState defaultSampler;

		// Matrix stacks (OpenGL emulation)
		private OpenGl.MatrixMode matrixMode = OpenGl.MatrixMode.Modelview;
		private Stack<Matrix4X4> modelViewStack = new Stack<Matrix4X4>(new[] { Matrix4X4.Identity });
		private Stack<Matrix4X4> projectionStack = new Stack<Matrix4X4>(new[] { Matrix4X4.Identity });

		// Immediate mode state
		private ImmediateModeData immediateData = new ImmediateModeData();

		// Vertex array pointers (for DrawArrays with external data)
		private (int size, int stride, IntPtr pointer) vertexPointerData;
		private (int size, int stride, IntPtr pointer) colorPointerData;
		private (int size, int stride, IntPtr pointer) texCoordPointerData;
		private (int size, int stride, IntPtr pointer) normalPointerData;

		// State tracking
		private Dictionary<int, bool> enableCapState = new Dictionary<int, bool>();
		private Dictionary<ArrayCap, bool> arrayCapState = new Dictionary<ArrayCap, bool>()
		{
			[ArrayCap.VertexArray] = false,
			[ArrayCap.NormalArray] = false,
			[ArrayCap.ColorArray] = false,
			[ArrayCap.IndexArray] = false,
			[ArrayCap.TextureCoordArray] = false,
		};

		private int blendSrcFactor = (int)BlendingFactorSrc.One;
		private int blendDstFactor = (int)BlendingFactorDest.Zero;
		private bool depthMaskEnabled = true;
		private ComparisonFunction depthCompareFunc = ComparisonFunction.Less;
		private bool scissorEnabled = false;
		private CullMode currentCullMode = CullMode.Back;
		private bool frontFaceCCW = true;
		private float polygonOffsetFactor = 0;
		private float polygonOffsetUnits = 0;
		private Color4 clearColor = new Color4(0, 0, 0, 1);

		private const int GL_MODULATE = 0x2100;
		private const int GL_REPLACE = 0x1E01;
		private int texEnvMode = GL_MODULATE;

		// Dirty-state tracking to avoid redundant D3D11 calls
		private bool transformDirty = true;
		private bool renderStateDirty = true;
		private ID3D11BlendState lastAppliedBlendState;
		private ID3D11DepthStencilState lastAppliedDepthStencilState;
		private ID3D11RasterizerState lastAppliedRasterizerState;

		// Lighting data
		private class LightData
		{
			public float[] Ambient = { 0, 0, 0, 1 };
			public float[] Diffuse = { 1, 1, 1, 1 };
			public float[] Specular = { 1, 1, 1, 1 };
			public float[] Position = { 0, 0, 1, 0 }; // directional by default (w=0)
		}

		private LightData[] lights = { new LightData(), new LightData() };

		// Buffer management
		private int nextBufferId = 1;
		private Dictionary<int, ID3D11Buffer> buffers = new Dictionary<int, ID3D11Buffer>();
		private Dictionary<int, byte[]> bufferDataStore = new Dictionary<int, byte[]>();
		private int currentArrayBuffer = 0;
		private int currentElementBuffer = 0;

		// Texture management
		private int nextTextureId = 1;
		private Dictionary<int, TextureInfo> textures = new Dictionary<int, TextureInfo>();
		private int activeTextureUnit = 0;
		private int[] boundTextures = new int[8];
		private bool texture2DEnabled = false;

		private void BindActiveTextures()
		{
			for (int i = 0; i < boundTextures.Length; i++)
			{
				int tex = boundTextures[i];
				if (tex > 0 && textures.TryGetValue(tex, out var texInfo))
				{
					if (texInfo.ShaderResourceView == null)
					{
						FinalizeTextureIfReady(texInfo, force: true);
					}

					if (texInfo.ShaderResourceView != null)
					{
						context.PSSetShaderResource((uint)i, texInfo.ShaderResourceView);
						context.PSSetSampler((uint)i, texInfo.Sampler ?? defaultSampler);
					}
					else
					{
						context.PSSetShaderResource((uint)i, null);
					}
				}
				else
				{
					context.PSSetShaderResource((uint)i, null);
				}
			}
		}

		// Shader program management (for user-created shaders)
		private int nextProgramId = 1;
		private int nextShaderId = 1;
		private Dictionary<int, ShaderProgramInfo> shaderPrograms = new Dictionary<int, ShaderProgramInfo>();
		private Dictionary<int, ShaderInfo> shaderObjects = new Dictionary<int, ShaderInfo>();
		private int currentProgram = 0;

		// Display list emulation
		private int nextDisplayListId = 1;
		private Dictionary<int, DisplayList> displayLists = new Dictionary<int, DisplayList>();
		private int recordingDisplayListId = 0;
		private bool isRecordingDisplayList = false;

		// Framebuffer management
		private int nextFramebufferId = 1;
		private class FramebufferInfo
		{
			public int TextureId;
			public ID3D11RenderTargetView RenderTargetView;
			public ID3D11DepthStencilView DepthStencilView;
		}
		private Dictionary<int, FramebufferInfo> framebuffers = new Dictionary<int, FramebufferInfo>();
		private int currentBoundFramebuffer = 0;

		// VAO management
		private int nextVaoId = 1;

		// Render target dimensions (for OpenGL-to-D3D11 coordinate conversion)
		private int renderTargetHeight;

		// Viewport
		private int viewportX, viewportY, viewportWidth, viewportHeight;
		private int scissorX, scissorY, scissorWidth, scissorHeight;

		public bool GlHasBufferObjects => true;

		public VorticeD3DGl()
		{
		}

		public void Initialize(ID3D11Device device, ID3D11DeviceContext context, IDXGISwapChain swapChain)
		{
			this.device = device;
			this.context = context;
			this.swapChain = swapChain;
			this.offscreenWidth = 0;
			this.offscreenHeight = 0;

			CreateRenderTarget();
			CreateShaders();
			CreateStates();
			CreateDynamicVertexBuffer();
			CreateTransformBuffer();
			CreateLightBuffer();
		}

		/// <summary>
		/// Initializes for off-screen rendering without a swap chain or window.
		/// </summary>
		public void InitializeOffscreen(ID3D11Device device, ID3D11DeviceContext context, int width, int height)
		{
			this.device = device;
			this.context = context;
			this.swapChain = null;
			this.offscreenWidth = width;
			this.offscreenHeight = height;

			CreateRenderTarget();
			CreateShaders();
			CreateStates();
			CreateDynamicVertexBuffer();
			CreateTransformBuffer();
			CreateLightBuffer();
		}

		private int offscreenWidth;
		private int offscreenHeight;

		private void CreateRenderTarget()
		{
			currentBackBuffer?.Dispose();

			if (swapChain != null)
			{
				currentBackBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
			}
			else
			{
				// Off-screen mode: create a standalone backbuffer texture
				currentBackBuffer = device.CreateTexture2D(new Texture2DDescription
				{
					Width = (uint)offscreenWidth,
					Height = (uint)offscreenHeight,
					MipLevels = 1,
					ArraySize = 1,
					Format = Format.B8G8R8A8_UNorm,
					SampleDescription = new SampleDescription(1, 0),
					Usage = ResourceUsage.Default,
					BindFlags = BindFlags.RenderTarget,
				});
			}

			renderTargetHeight = (int)currentBackBuffer.Description.Height;

			mainRenderTarget?.Dispose();
			var desc = currentBackBuffer.Description;
			desc.SampleDescription = new SampleDescription(1, 0);
			desc.BindFlags = BindFlags.RenderTarget;
			mainRenderTarget = device.CreateTexture2D(desc);

			renderTargetView?.Dispose();
			renderTargetView = device.CreateRenderTargetView(mainRenderTarget);

			var depthDesc = new Texture2DDescription
			{
				Width = (uint)currentBackBuffer.Description.Width,
				Height = (uint)currentBackBuffer.Description.Height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.D24_UNorm_S8_UInt,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil,
			};

			depthStencilBuffer = device.CreateTexture2D(depthDesc);
			depthStencilView = device.CreateDepthStencilView(depthStencilBuffer);

			context.OMSetRenderTargets(renderTargetView, depthStencilView);
		}

		public void ResizeBuffers(int width, int height)
		{
			if (width <= 0 || height <= 0) return;

			context.OMSetRenderTargets((ID3D11RenderTargetView)null, (ID3D11DepthStencilView)null);
			renderTargetView?.Dispose();
			renderTargetView = null;
			currentBackBuffer?.Dispose();
			currentBackBuffer = null;
			mainRenderTarget?.Dispose();
			mainRenderTarget = null;
			depthStencilView?.Dispose();
			depthStencilBuffer?.Dispose();

			if (swapChain != null)
			{
				swapChain.ResizeBuffers(0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None);
			}
			else
			{
				offscreenWidth = width;
				offscreenHeight = height;
			}

			CreateRenderTarget();
		}

		private void CreateShaders()
		{
			// Position+Color shader
			{
				string hlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.PositionColor.hlsl");
				byte[] vsByteCode = Compiler.Compile(hlsl, "VS", "PositionColor.hlsl", "vs_5_0").ToArray();
				byte[] psByteCode = Compiler.Compile(hlsl, "PS", "PositionColor.hlsl", "ps_5_0").ToArray();

				string hlslFlat = hlsl.Replace("float4 Color : COLOR;", "nointerpolation float4 Color : COLOR;");
				byte[] psFlatByteCode = Compiler.Compile(hlslFlat, "PS", "PositionColor.hlsl", "ps_5_0").ToArray();

				posColorVS = device.CreateVertexShader(vsByteCode);
				posColorPS = device.CreatePixelShader(psByteCode);
				posColorFlatPS = device.CreatePixelShader(psFlatByteCode);

				var inputElements = new[]
				{
					new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
				};

				posColorInputLayout = device.CreateInputLayout(inputElements, vsByteCode);
			}

			// Position+Texture shader
			{
				string hlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.PositionTexture.hlsl");
				byte[] vsByteCode = Compiler.Compile(hlsl, "VS", "PositionTexture.hlsl", "vs_5_0").ToArray();
				byte[] psByteCode = Compiler.Compile(hlsl, "PS", "PositionTexture.hlsl", "ps_5_0").ToArray();

				string hlslFlat = hlsl.Replace("float4 Color : COLOR;", "nointerpolation float4 Color : COLOR;");
				byte[] psFlatByteCode = Compiler.Compile(hlslFlat, "PS", "PositionTexture.hlsl", "ps_5_0").ToArray();

				posTexVS = device.CreateVertexShader(vsByteCode);
				posTexPS = device.CreatePixelShader(psByteCode);
				posTexFlatPS = device.CreatePixelShader(psFlatByteCode);

				var inputElements = new[]
				{
					new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
					new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 20, 0),
				};

				posTexInputLayout = device.CreateInputLayout(inputElements, vsByteCode);
			}

			// Lit Position+Color shader (lighting on GPU)
			{
				string hlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.PositionColorLit.hlsl");
				byte[] vsByteCode = Compiler.Compile(hlsl, "VS", "PositionColorLit.hlsl", "vs_5_0").ToArray();
				byte[] psByteCode = Compiler.Compile(hlsl, "PS", "PositionColorLit.hlsl", "ps_5_0").ToArray();

				string hlslFlat = hlsl.Replace("float4 Color : COLOR;", "nointerpolation float4 Color : COLOR;");
				byte[] psFlatByteCode = Compiler.Compile(hlslFlat, "PS", "PositionColorLit.hlsl", "ps_5_0").ToArray();

				posColorLitVS = device.CreateVertexShader(vsByteCode);
				posColorLitPS = device.CreatePixelShader(psByteCode);
				posColorLitFlatPS = device.CreatePixelShader(psFlatByteCode);

				var inputElements = new[]
				{
					new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
					new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 24, 0),
				};

				posColorLitInputLayout = device.CreateInputLayout(inputElements, vsByteCode);
			}

			// Lit Position+Texture shader (lighting on GPU)
			{
				string hlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.PositionTextureLit.hlsl");
				byte[] vsByteCode = Compiler.Compile(hlsl, "VS", "PositionTextureLit.hlsl", "vs_5_0").ToArray();
				byte[] psByteCode = Compiler.Compile(hlsl, "PS", "PositionTextureLit.hlsl", "ps_5_0").ToArray();

				string hlslFlat = hlsl.Replace("float4 Color : COLOR;", "nointerpolation float4 Color : COLOR;");
				byte[] psFlatByteCode = Compiler.Compile(hlslFlat, "PS", "PositionTextureLit.hlsl", "ps_5_0").ToArray();

				posTexLitVS = device.CreateVertexShader(vsByteCode);
				posTexLitPS = device.CreatePixelShader(psByteCode);
				posTexLitFlatPS = device.CreatePixelShader(psFlatByteCode);

				var inputElements = new[]
				{
					new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
					new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
					new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 32, 0),
				};

				posTexLitInputLayout = device.CreateInputLayout(inputElements, vsByteCode);
			}
		}

		private void CreateStates()
		{
			// Blend states are created on demand via GetOrCreateBlendState

			// Depth stencil states are created on demand via GetOrCreateDepthStencilState

			// Rasterizer states
			{
				rasterizerNoCull = device.CreateRasterizerState(new RasterizerDescription
				{
					FillMode = FillMode.Solid,
					CullMode = CullMode.None,
					ScissorEnable = false,
					DepthClipEnable = true,
				});

				rasterizerCullBack = device.CreateRasterizerState(new RasterizerDescription
				{
					FillMode = FillMode.Solid,
					CullMode = CullMode.Back,
					FrontCounterClockwise = true,
					ScissorEnable = false,
					DepthClipEnable = true,
				});

				rasterizerCullFront = device.CreateRasterizerState(new RasterizerDescription
				{
					FillMode = FillMode.Solid,
					CullMode = CullMode.Front,
					FrontCounterClockwise = true,
					ScissorEnable = false,
					DepthClipEnable = true,
				});

				rasterizerScissor = device.CreateRasterizerState(new RasterizerDescription
				{
					FillMode = FillMode.Solid,
					CullMode = CullMode.None,
					ScissorEnable = true,
					DepthClipEnable = true,
				});
			}

			// Sampler state
			{
				defaultSampler = device.CreateSamplerState(new SamplerDescription
				{
					Filter = Filter.MinMagMipLinear,
					AddressU = TextureAddressMode.Wrap,
					AddressV = TextureAddressMode.Wrap,
					AddressW = TextureAddressMode.Wrap,
					ComparisonFunc = ComparisonFunction.Never,
					MinLOD = 0,
					MaxLOD = float.MaxValue,
				});
			}

			// Set initial states
			context.RSSetState(rasterizerNoCull);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Less, true));
			context.OMSetBlendState(GetOrCreateBlendState(false, blendSrcFactor, blendDstFactor, currentColorWriteMask));
		}

		private void CreateDynamicVertexBuffer()
		{
			// Colored: pos(3) + normal(3) + color(4) = 10 floats (enough for lit and unlit)
			int coloredVertexSize = 10 * sizeof(float);
			dynamicVertexBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = (uint)(MaxVertices * coloredVertexSize),
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.VertexBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});

			// Textured: pos(3) + normal(3) + texcoord(2) + color(4) = 12 floats (enough for lit and unlit)
			int texturedVertexSize = 12 * sizeof(float);
			dynamicTexVertexBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = (uint)(MaxVertices * texturedVertexSize),
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.VertexBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});
		}

		private void CreateTransformBuffer()
		{
			var desc = new BufferDescription
			{
				ByteWidth = (uint)(2 * 16 * sizeof(float)), // two 4x4 matrices
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			};
			transformBuffer = device.CreateBuffer(desc);
		}

		private void CreateLightBuffer()
		{
			// 2 lights * (position float4 + ambient float4 + diffuse float4) + flags float4 = 7 float4s = 112 bytes
			lightBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 112,
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});
		}

		private void UpdateLightBuffer(bool light0On, bool light1On)
		{
			// Transform light position into view space for the shader if needed?
			// The shaders (posColorLit and posTexLit) receive the normal in object space.
			// Wait, if the normal is in object space, we need the light direction in object space too!
			// OpenGL lighting is calculated in EYE space (view space). The shader does:
			// "normal = normalize(mul(input.normal, (float3x3)WorldView));"
			// "float nDotL = max(0.0, dot(normal, lightDir));"
			// But wait, our HLSL shaders don't have the normal transformation logic! Let's check the shaders.

			var mapped = context.Map(lightBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				// Light 0: position(4) + ambient(4) + diffuse(4)
				ptr[0] = lights[0].Position[0]; ptr[1] = lights[0].Position[1]; ptr[2] = lights[0].Position[2]; ptr[3] = lights[0].Position[3];
				ptr[4] = lights[0].Ambient[0]; ptr[5] = lights[0].Ambient[1]; ptr[6] = lights[0].Ambient[2]; ptr[7] = lights[0].Ambient[3];
				ptr[8] = lights[0].Diffuse[0]; ptr[9] = lights[0].Diffuse[1]; ptr[10] = lights[0].Diffuse[2]; ptr[11] = lights[0].Diffuse[3];
				// Light 1: position(4) + ambient(4) + diffuse(4)
				ptr[12] = lights[1].Position[0]; ptr[13] = lights[1].Position[1]; ptr[14] = lights[1].Position[2]; ptr[15] = lights[1].Position[3];
				ptr[16] = lights[1].Ambient[0]; ptr[17] = lights[1].Ambient[1]; ptr[18] = lights[1].Ambient[2]; ptr[19] = lights[1].Ambient[3];
				ptr[20] = lights[1].Diffuse[0]; ptr[21] = lights[1].Diffuse[1]; ptr[22] = lights[1].Diffuse[2]; ptr[23] = lights[1].Diffuse[3];
				// Flags
				ptr[24] = light0On ? 1.0f : 0.0f;
				ptr[25] = light1On ? 1.0f : 0.0f;
				ptr[26] = 0;
				ptr[27] = 0;
			}
			context.Unmap(lightBuffer, 0);
		}

		private string ReadEmbeddedResource(string name)
		{
			var assembly = Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(name);
			if (stream == null)
				throw new FileNotFoundException($"Embedded resource not found: {name}");
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		private void UpdateTransformBuffer()
		{
			if (!transformDirty) return;
			transformDirty = false;

			var mv = modelViewStack.Peek();
			// Apply Z correction: map OpenGL clip-space Z [-1,1] to D3D11 [0,1]
			var p = projectionStack.Peek();

			double flipY = (currentBoundFramebuffer != 0) ? -1.0 : 1.0;

			var proj = new Matrix4X4(
				new Vector4(p.Row0.X, p.Row0.Y * flipY, p.Row0.Z * 0.5 + p.Row0.W * 0.5, p.Row0.W),
				new Vector4(p.Row1.X, p.Row1.Y * flipY, p.Row1.Z * 0.5 + p.Row1.W * 0.5, p.Row1.W),
				new Vector4(p.Row2.X, p.Row2.Y * flipY, p.Row2.Z * 0.5 + p.Row2.W * 0.5, p.Row2.W),
				new Vector4(p.Row3.X, p.Row3.Y * flipY, p.Row3.Z * 0.5 + p.Row3.W * 0.5, p.Row3.W));

			var mapped = context.Map(transformBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				WriteMatrix(ptr, mv);
				WriteMatrix(ptr + 16, proj);
			}
			context.Unmap(transformBuffer, 0);
		}

		private static unsafe void WriteMatrix(float* dest, Matrix4X4 m)
		{
			dest[0] = (float)m.Row0.X; dest[1] = (float)m.Row0.Y; dest[2] = (float)m.Row0.Z; dest[3] = (float)m.Row0.W;
			dest[4] = (float)m.Row1.X; dest[5] = (float)m.Row1.Y; dest[6] = (float)m.Row1.Z; dest[7] = (float)m.Row1.W;
			dest[8] = (float)m.Row2.X; dest[9] = (float)m.Row2.Y; dest[10] = (float)m.Row2.Z; dest[11] = (float)m.Row2.W;
			dest[12] = (float)m.Row3.X; dest[13] = (float)m.Row3.Y; dest[14] = (float)m.Row3.Z; dest[15] = (float)m.Row3.W;
		}

		private void FlushImmediateMode()
		{
			int vertexCount = immediateData.Positions.Count / 3;
			if (vertexCount == 0) return;

			bool hasTexCoords = immediateData.TexCoords.Count > 0 && texture2DEnabled && boundTextures[0] != 0;

			if (hasTexCoords)
			{
				FlushTexturedVertices(vertexCount);
			}
			else
			{
				FlushColoredVertices(vertexCount);
			}
		}

		private int GetColorIndexForFlatShading(BeginMode mode, int i, int count)
		{
			if (!flatShading) return i;

			int colorIdx = i;
			if (mode == BeginMode.Triangles)
			{
				int triStart = i - (i % 3);
				colorIdx = triStart + 2;
			}
			else if (mode == BeginMode.TriangleStrip)
			{
				colorIdx = i + 2;
			}
			else if (mode == BeginMode.Lines)
			{
				int lineStart = i - (i % 2);
				colorIdx = lineStart + 1;
			}

			if (colorIdx >= count) colorIdx = count - 1;
			return colorIdx;
		}

		private void FlushColoredVertices(int vertexCount)
		{
			int stride = 7 * sizeof(float); // pos(3) + color(4)
			int batchSize = Math.Min(vertexCount, MaxVertices);

			int offset = 0;
			while (offset < vertexCount)
			{
				int count = Math.Min(batchSize, vertexCount - offset);

				var mapped = context.Map(dynamicVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* ptr = (float*)mapped.DataPointer;
					for (int i = 0; i < count; i++)
					{
						int vi = (offset + i) * 3;
						int colorIdx = GetColorIndexForFlatShading(immediateData.Mode, i, count);
						int ci = (offset + colorIdx) * 4;

						ptr[i * 7 + 0] = immediateData.Positions[vi];
						ptr[i * 7 + 1] = immediateData.Positions[vi + 1];
						ptr[i * 7 + 2] = immediateData.Positions[vi + 2];
						ptr[i * 7 + 3] = immediateData.Colors[ci] / 255f;
						ptr[i * 7 + 4] = immediateData.Colors[ci + 1] / 255f;
						ptr[i * 7 + 5] = immediateData.Colors[ci + 2] / 255f;
						ptr[i * 7 + 6] = immediateData.Colors[ci + 3] / 255f;
					}
				}
				context.Unmap(dynamicVertexBuffer, 0);

				UpdateTransformBuffer();

				context.IASetInputLayout(posColorInputLayout);
				context.IASetVertexBuffer(0, dynamicVertexBuffer, (uint)stride);
				context.IASetPrimitiveTopology(GetTopology(immediateData.Mode));
				if (currentProgram == 0)
				{
					context.VSSetShader(posColorVS);
					context.PSSetShader(flatShading ? posColorFlatPS : posColorPS);
				}
				context.VSSetConstantBuffer(0, transformBuffer);

				BindActiveTextures();
				ApplyRenderState();

				context.Draw((uint)count, 0);

				offset += count;
			}
		}

		private void FlushTexturedVertices(int vertexCount)
		{
			int stride = 9 * sizeof(float); // pos(3) + texcoord(2) + color(4)
			int batchSize = Math.Min(vertexCount, MaxVertices);

			int offset = 0;
			while (offset < vertexCount)
			{
				int count = Math.Min(batchSize, vertexCount - offset);

				var mapped = context.Map(dynamicTexVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* ptr = (float*)mapped.DataPointer;
					for (int i = 0; i < count; i++)
					{
						int si = offset + i;
						int vi = si * 3;
						int ti = si * 2;
						int colorIdx = GetColorIndexForFlatShading(immediateData.Mode, i, count);
						int ci = (offset + colorIdx) * 4;

						ptr[i * 9 + 0] = immediateData.Positions[vi];
						ptr[i * 9 + 1] = immediateData.Positions[vi + 1];
						ptr[i * 9 + 2] = immediateData.Positions[vi + 2];
						ptr[i * 9 + 3] = ti < immediateData.TexCoords.Count ? immediateData.TexCoords[ti] : 0;
						ptr[i * 9 + 4] = ti + 1 < immediateData.TexCoords.Count ? immediateData.TexCoords[ti + 1] : 0;
						ptr[i * 9 + 5] = immediateData.Colors[ci] / 255f;
						ptr[i * 9 + 6] = immediateData.Colors[ci + 1] / 255f;
						ptr[i * 9 + 7] = immediateData.Colors[ci + 2] / 255f;
						ptr[i * 9 + 8] = immediateData.Colors[ci + 3] / 255f;
					}
				}
				context.Unmap(dynamicTexVertexBuffer, 0);

				UpdateTransformBuffer();

				context.IASetInputLayout(posTexInputLayout);
				context.IASetVertexBuffer(0, dynamicTexVertexBuffer, (uint)stride);
				context.IASetPrimitiveTopology(GetTopology(immediateData.Mode));
				if (currentProgram == 0)
				{
					context.VSSetShader(posTexVS);
					context.PSSetShader(flatShading ? posTexFlatPS : posTexPS);
				}
				context.VSSetConstantBuffer(0, transformBuffer);

				BindActiveTextures();

				ApplyRenderState();

				context.Draw((uint)count, 0);

				offset += count;
			}
		}

		private static PrimitiveTopology GetTopology(BeginMode mode)
		{
			return mode switch
			{
				BeginMode.Triangles => PrimitiveTopology.TriangleList,
				BeginMode.TriangleStrip => PrimitiveTopology.TriangleStrip,
				BeginMode.Lines => PrimitiveTopology.LineList,
				BeginMode.TriangleFan => PrimitiveTopology.TriangleList, // will be converted
				_ => PrimitiveTopology.TriangleList,
			};
		}

		private ID3D11DepthStencilState GetOrCreateDepthStencilState(bool enabled, ComparisonFunction func, bool writeMask)
		{
			var key = (enabled, func, writeMask);
			if (!depthStencilCache.TryGetValue(key, out var state))
			{
				state = device.CreateDepthStencilState(new DepthStencilDescription
				{
					DepthEnable = enabled,
					DepthWriteMask = writeMask ? DepthWriteMask.All : DepthWriteMask.Zero,
					DepthFunc = enabled ? func : ComparisonFunction.Always,
				});
				depthStencilCache[key] = state;
			}
			return state;
		}

		private Dictionary<(CullMode cull, bool scissor, int depthBias, float slopeBias), ID3D11RasterizerState> rasterizerCache
			= new Dictionary<(CullMode, bool, int, float), ID3D11RasterizerState>();

		private ID3D11RasterizerState GetOrCreateRasterizerState(CullMode cull, bool scissor, int depthBias, float slopeBias)
		{
			var key = (cull, scissor, depthBias, slopeBias);
			if (!rasterizerCache.TryGetValue(key, out var state))
			{
				state = device.CreateRasterizerState(new RasterizerDescription
				{
					FillMode = FillMode.Solid,
					CullMode = cull,
					FrontCounterClockwise = frontFaceCCW,
					ScissorEnable = scissor,
					DepthClipEnable = true,
					DepthBias = depthBias,
					SlopeScaledDepthBias = slopeBias,
				});
				rasterizerCache[key] = state;
			}
			return state;
		}

		private void ApplyRenderState()
		{
			if (!renderStateDirty) return;
			renderStateDirty = false;

			bool blendEnabled = enableCapState.TryGetValue((int)EnableCap.Blend, out var b) && b;
			var desiredBlend = GetOrCreateBlendState(blendEnabled, blendSrcFactor, blendDstFactor, currentColorWriteMask);
			if (desiredBlend != lastAppliedBlendState)
			{
				context.OMSetBlendState(desiredBlend);
				lastAppliedBlendState = desiredBlend;
			}

			bool depthEnabled = enableCapState.TryGetValue((int)EnableCap.DepthTest, out var d) && d;
			var desiredDepth = GetOrCreateDepthStencilState(depthEnabled, depthCompareFunc, depthMaskEnabled);
			if (desiredDepth != lastAppliedDepthStencilState)
			{
				context.OMSetDepthStencilState(desiredDepth);
				lastAppliedDepthStencilState = desiredDepth;
			}

			bool cullEnabled = enableCapState.TryGetValue((int)EnableCap.CullFace, out var c) && c;
			bool polyOffsetEnabled = enableCapState.TryGetValue((int)EnableCap.PolygonOffsetFill, out var po) && po;
			CullMode cull = cullEnabled ? currentCullMode : CullMode.None;
			int depthBias = polyOffsetEnabled ? (int)(polygonOffsetUnits) : 0;
			float slopeBias = polyOffsetEnabled ? polygonOffsetFactor : 0;

			var desiredRasterizer = GetOrCreateRasterizerState(cull, scissorEnabled, depthBias, slopeBias);
			if (desiredRasterizer != lastAppliedRasterizerState)
			{
				context.RSSetState(desiredRasterizer);
				lastAppliedRasterizerState = desiredRasterizer;
			}

			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.UniformsDirty && prog.ConstantBuffer != null)
				{
					var mapped = context.Map(prog.ConstantBuffer, MapMode.WriteDiscard);
					unsafe
					{
						fixed (float* pUniforms = prog.Uniforms)
						{
							System.Runtime.CompilerServices.Unsafe.CopyBlock((void*)mapped.DataPointer, pUniforms, (uint)(prog.Uniforms.Length * sizeof(float)));
						}
					}
					context.Unmap(prog.ConstantBuffer, 0);
					prog.UniformsDirty = false;
				}
				if (prog.ConstantBuffer != null)
				{
					context.VSSetConstantBuffer(0, prog.ConstantBuffer);
					context.PSSetConstantBuffer(0, prog.ConstantBuffer);
				}
			}
		}

		private List<float> ConvertTriangleFanToList(List<float> positions, List<byte> colors, List<float> texCoords)
		{
			int vertCount = positions.Count / 3;
			if (vertCount < 3) return positions;

			var newPositions = new List<float>();
			var newColors = new List<byte>();
			var newTexCoords = new List<float>();

			for (int i = 1; i < vertCount - 1; i++)
			{
				// Triangle: v0, vi, vi+1
				AddVertex(newPositions, positions, 0);
				AddVertex(newPositions, positions, i);
				AddVertex(newPositions, positions, i + 1);

				AddColor(newColors, colors, 0);
				AddColor(newColors, colors, i);
				AddColor(newColors, colors, i + 1);

				if (texCoords.Count > 0)
				{
					AddTexCoord(newTexCoords, texCoords, 0);
					AddTexCoord(newTexCoords, texCoords, i);
					AddTexCoord(newTexCoords, texCoords, i + 1);
				}
			}

			immediateData.Positions = newPositions;
			immediateData.Colors = newColors;
			immediateData.TexCoords = newTexCoords;
			immediateData.Mode = BeginMode.Triangles;

			return newPositions;
		}

		private static void AddVertex(List<float> dest, List<float> src, int index)
		{
			dest.Add(src[index * 3]);
			dest.Add(src[index * 3 + 1]);
			dest.Add(src[index * 3 + 2]);
		}

		private static void AddColor(List<byte> dest, List<byte> src, int index)
		{
			dest.Add(src[index * 4]);
			dest.Add(src[index * 4 + 1]);
			dest.Add(src[index * 4 + 2]);
			dest.Add(src[index * 4 + 3]);
		}

		private static void AddTexCoord(List<float> dest, List<float> src, int index)
		{
			dest.Add(src[index * 2]);
			dest.Add(src[index * 2 + 1]);
		}

		// --- IGpuContext implementation ---

		public void Begin(BeginMode mode)
		{
			immediateData.Mode = mode;
			immediateData.Positions.Clear();
			immediateData.Colors.Clear();
			immediateData.TexCoords.Clear();
			immediateData.Normals.Clear();
		}

		public void End()
		{

			if (immediateData.Mode == BeginMode.TriangleFan)
			{
				ConvertTriangleFanToList(immediateData.Positions, immediateData.Colors, immediateData.TexCoords);
			}

			if (isRecordingDisplayList)
			{
				RecordToDisplayList();
				return;
			}

			FlushImmediateMode();
		}

		public void Vertex2(double x, double y)
		{
			immediateData.Positions.Add((float)x);
			immediateData.Positions.Add((float)y);
			immediateData.Positions.Add(0f);

			immediateData.Colors.Add(immediateData.CurrentColor[0]);
			immediateData.Colors.Add(immediateData.CurrentColor[1]);
			immediateData.Colors.Add(immediateData.CurrentColor[2]);
			immediateData.Colors.Add(immediateData.CurrentColor[3]);
		}

		public void Vertex3(double x, double y, double z)
		{
			immediateData.Positions.Add((float)x);
			immediateData.Positions.Add((float)y);
			immediateData.Positions.Add((float)z);

			immediateData.Colors.Add(immediateData.CurrentColor[0]);
			immediateData.Colors.Add(immediateData.CurrentColor[1]);
			immediateData.Colors.Add(immediateData.CurrentColor[2]);
			immediateData.Colors.Add(immediateData.CurrentColor[3]);
		}

		public void Color4(byte red, byte green, byte blue, byte alpha)
		{
			immediateData.CurrentColor[0] = red;
			immediateData.CurrentColor[1] = green;
			immediateData.CurrentColor[2] = blue;
			immediateData.CurrentColor[3] = alpha;
		}

		public void TexCoord2(double x, double y)
		{
			immediateData.TexCoords.Add((float)x);
			immediateData.TexCoords.Add((float)y);
		}

		public void Normal3(double x, double y, double z)
		{
			immediateData.Normals.Add((float)x);
			immediateData.Normals.Add((float)y);
			immediateData.Normals.Add((float)z);
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
			if (isRecordingDisplayList) return;

			if (count <= 0) return;

			bool hasVertexPointer = arrayCapState.TryGetValue(ArrayCap.VertexArray, out var va) && va;
			bool hasColorPointer = arrayCapState.TryGetValue(ArrayCap.ColorArray, out var ca) && ca;
			bool hasTexCoordPointer = arrayCapState.TryGetValue(ArrayCap.TextureCoordArray, out var ta) && ta;
			bool hasNormalPointer = arrayCapState.TryGetValue(ArrayCap.NormalArray, out var na) && na;

			if (!hasVertexPointer && immediateData.Positions.Count == 0) return;

			if (hasVertexPointer && vertexPointerData.pointer != IntPtr.Zero)
			{
				bool useTexture = hasTexCoordPointer && texture2DEnabled && boundTextures[0] != 0
					&& texCoordPointerData.pointer != IntPtr.Zero;

				bool lightingOn = enableCapState.TryGetValue((int)EnableCap.Lighting, out var lit) && lit;
				bool light0On = enableCapState.TryGetValue((int)EnableCap.Light0, out var l0) && l0;
				bool light1On = enableCapState.TryGetValue((int)EnableCap.Light1, out var l1) && l1;

				if (useTexture)
				{
					DrawArraysTextured(mode, first, count, hasColorPointer, hasNormalPointer, lightingOn, light0On, light1On);
				}
				else
				{
					DrawArraysColored(mode, first, count, hasColorPointer, hasNormalPointer, lightingOn, light0On, light1On);
				}
			}
			else
			{
				immediateData.Mode = mode;
				FlushImmediateMode();
			}
		}

		private void GetVertexColor(int localIndex, int absoluteIndex, bool hasColorPointer, out float r, out float g, out float b, out float a)
		{
			if (hasColorPointer && colorPointerData.pointer != IntPtr.Zero)
			{
				unsafe
				{
					byte* srcColor = (byte*)colorPointerData.pointer;
					int colorSize = colorPointerData.size > 0 ? colorPointerData.size : 3;
					int colorStride = colorPointerData.stride > 0 ? colorPointerData.stride : colorSize;
					int ci = absoluteIndex * colorStride;
					r = srcColor[ci] / 255f;
					g = srcColor[ci + 1] / 255f;
					b = srcColor[ci + 2] / 255f;
					a = colorSize >= 4 ? srcColor[ci + 3] / 255f : 1.0f;
				}
			}
			else
			{
				r = immediateData.CurrentColor[0] / 255f;
				g = immediateData.CurrentColor[1] / 255f;
				b = immediateData.CurrentColor[2] / 255f;
				a = immediateData.CurrentColor[3] / 255f;
			}
		}

		private void ApplyLighting(ref float r, ref float g, ref float b, float nx, float ny, float nz, bool light0On, bool light1On)
		{
			// Simple Lambert lighting on CPU
			float lr = 0, lg = 0, lb = 0;

			// We need the inverse transpose of the ModelView matrix to transform normals correctly.
			// However, since we're transforming a normal into view space, and the ModelView matrix is usually just rotation/translation
			// we can use the 3x3 portion of the ModelView matrix to transform the normal.
			var mv = modelViewStack.Peek();
			
			// Transform normal to view space (M * v) where M's columns are stored in Row0, Row1, etc.
			float vnx = (float)(nx * mv.Row0.X + ny * mv.Row1.X + nz * mv.Row2.X);
			float vny = (float)(nx * mv.Row0.Y + ny * mv.Row1.Y + nz * mv.Row2.Y);
			float vnz = (float)(nx * mv.Row0.Z + ny * mv.Row1.Z + nz * mv.Row2.Z);

			// Normalize the view-space normal
			float nlen = (float)Math.Sqrt(vnx * vnx + vny * vny + vnz * vnz);
			if (nlen > 0)
			{
				vnx /= nlen;
				vny /= nlen;
				vnz /= nlen;
			}
			else
			{
				vnx = nx;
				vny = ny;
				vnz = nz;
			}

			void AddLight(LightData light)
			{
				// Light position is already in view space (stored when glLightfv was called)
				float dx = light.Position[0];
				float dy = light.Position[1];
				float dz = light.Position[2];

				// If w=0, it's a directional light
				float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
				if (len > 0) { dx /= len; dy /= len; dz /= len; }

				float ndotl = vnx * dx + vny * dy + vnz * dz;
				if (ndotl < 0) ndotl = 0;

				lr += light.Ambient[0] + light.Diffuse[0] * ndotl;
				lg += light.Ambient[1] + light.Diffuse[1] * ndotl;
				lb += light.Ambient[2] + light.Diffuse[2] * ndotl;
			}

			if (light0On) AddLight(lights[0]);
			if (light1On) AddLight(lights[1]);

			// Add global ambient? OpenGl defaults to a global ambient of (0.2, 0.2, 0.2, 1.0)
			lr += 0.2f;
			lg += 0.2f;
			lb += 0.2f;

			// If both lights are off, maybe we should just return? Wait, if lighting is on but no lights are on, it should just be ambient.
			
			r = Math.Min(1.0f, r * lr);
			g = Math.Min(1.0f, g * lg);
			b = Math.Min(1.0f, b * lb);
		}

		private int GetAlignedBatchCount(BeginMode mode, int remaining, int maxVertices)
		{
			int batchCount = Math.Min(remaining, maxVertices);
			if (mode == BeginMode.Triangles) batchCount -= batchCount % 3;
			else if (mode == BeginMode.Lines) batchCount -= batchCount % 2;
			return Math.Max(1, batchCount);
		}

		private void DrawArraysColored(BeginMode mode, int first, int totalCount, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			bool useLitShader = lightingOn && hasNormalPointer && normalPointerData.pointer != IntPtr.Zero;

			int stride;
			if (useLitShader)
			{
				stride = 10 * sizeof(float); // pos(3) + normal(3) + color(4)
				UpdateLightBuffer(light0On, light1On);
				context.IASetInputLayout(posColorLitInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posColorLitVS);
					context.PSSetShader(flatShading ? posColorLitFlatPS : posColorLitPS);
				}
				context.PSSetConstantBuffer(1, lightBuffer);
			}
			else
			{
				stride = 7 * sizeof(float); // pos(3) + color(4)
				context.IASetInputLayout(posColorInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posColorVS);
					context.PSSetShader(flatShading ? posColorFlatPS : posColorPS);
				}
			}

			UpdateTransformBuffer();

			context.IASetVertexBuffer(0, dynamicVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetConstantBuffer(0, transformBuffer);
			BindActiveTextures();
			ApplyRenderState();

			int offset = 0;
			while (offset < totalCount)
			{
				int batchCount = GetAlignedBatchCount(mode, totalCount - offset, MaxVertices);

				var mapped = context.Map(dynamicVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* dest = (float*)mapped.DataPointer;
					float* srcVert = (float*)vertexPointerData.pointer;
					float* srcNormal = (hasNormalPointer && normalPointerData.pointer != IntPtr.Zero)
						? (float*)normalPointerData.pointer : null;

					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					int normStride = normalPointerData.stride > 0 ? normalPointerData.stride / sizeof(float) : 3;

					if (useLitShader)
					{
						for (int i = 0; i < batchCount; i++)
						{
							int globalIdx = first + offset + i;
							int srcIdx = globalIdx * vertStride;
							int ni = globalIdx * normStride;

							dest[i * 10 + 0] = srcVert[srcIdx];
							dest[i * 10 + 1] = srcVert[srcIdx + 1];
							dest[i * 10 + 2] = vertexPointerData.size >= 3 ? srcVert[srcIdx + 2] : 0;
							dest[i * 10 + 3] = srcNormal[ni];
							dest[i * 10 + 4] = srcNormal[ni + 1];
							dest[i * 10 + 5] = srcNormal[ni + 2];

							int colorIdx = GetColorIndexForFlatShading(mode, i, batchCount);
							int globalColorIdx = first + offset + colorIdx;
							GetVertexColor(colorIdx, globalColorIdx, hasColorPointer, out float r, out float g, out float b, out float a);
							dest[i * 10 + 6] = r;
							dest[i * 10 + 7] = g;
							dest[i * 10 + 8] = b;
							dest[i * 10 + 9] = a;
						}
					}
					else
					{
						for (int i = 0; i < batchCount; i++)
						{
							int globalIdx = first + offset + i;
							int srcIdx = globalIdx * vertStride;
							dest[i * 7 + 0] = srcVert[srcIdx];
							dest[i * 7 + 1] = srcVert[srcIdx + 1];
							dest[i * 7 + 2] = vertexPointerData.size >= 3 ? srcVert[srcIdx + 2] : 0;

							int colorIdx = GetColorIndexForFlatShading(mode, i, batchCount);
							int globalColorIdx = first + offset + colorIdx;
							GetVertexColor(colorIdx, globalColorIdx, hasColorPointer, out float r, out float g, out float b, out float a);

							if (lightingOn && srcNormal != null)
							{
								int ni = globalIdx * normStride;
								ApplyLighting(ref r, ref g, ref b, srcNormal[ni], srcNormal[ni + 1], srcNormal[ni + 2], light0On, light1On);
							}

							dest[i * 7 + 3] = r;
							dest[i * 7 + 4] = g;
							dest[i * 7 + 5] = b;
							dest[i * 7 + 6] = a;
						}
					}
				}
				context.Unmap(dynamicVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		private void DrawArraysTextured(BeginMode mode, int first, int totalCount, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			// GL_REPLACE means the texture color replaces the vertex/lighting result entirely
			bool texReplace = texEnvMode == GL_REPLACE;
			bool useLitShader = lightingOn && hasNormalPointer && normalPointerData.pointer != IntPtr.Zero && !texReplace;

			int stride;
			if (useLitShader)
			{
				stride = 12 * sizeof(float); // pos(3) + normal(3) + texcoord(2) + color(4)
				UpdateLightBuffer(light0On, light1On);
				context.IASetInputLayout(posTexLitInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posTexLitVS);
					context.PSSetShader(flatShading ? posTexLitFlatPS : posTexLitPS);
				}
				context.PSSetConstantBuffer(1, lightBuffer);
			}
			else
			{
				stride = 9 * sizeof(float); // pos(3) + texcoord(2) + color(4)
				context.IASetInputLayout(posTexInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posTexVS);
					context.PSSetShader(flatShading ? posTexFlatPS : posTexPS);
				}
			}

			UpdateTransformBuffer();
			context.IASetVertexBuffer(0, dynamicTexVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetConstantBuffer(0, transformBuffer);

			BindActiveTextures();

			ApplyRenderState();

			int offset = 0;
			while (offset < totalCount)
			{
				int batchCount = GetAlignedBatchCount(mode, totalCount - offset, MaxVertices);

				var mapped = context.Map(dynamicTexVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* dest = (float*)mapped.DataPointer;
					float* srcVert = (float*)vertexPointerData.pointer;
					float* srcTex = (float*)texCoordPointerData.pointer;
					float* srcNormal = (hasNormalPointer && normalPointerData.pointer != IntPtr.Zero)
						? (float*)normalPointerData.pointer : null;

					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					int texStride = texCoordPointerData.stride > 0 ? texCoordPointerData.stride / sizeof(float) : texCoordPointerData.size;
					int normStride = normalPointerData.stride > 0 ? normalPointerData.stride / sizeof(float) : 3;

					if (useLitShader)
					{
						for (int i = 0; i < batchCount; i++)
						{
							int globalIdx = first + offset + i;
							int vi = globalIdx * vertStride;
							int ti = globalIdx * texStride;
							int ni = globalIdx * normStride;

							dest[i * 12 + 0] = srcVert[vi];
							dest[i * 12 + 1] = srcVert[vi + 1];
							dest[i * 12 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;
							dest[i * 12 + 3] = srcNormal[ni];
							dest[i * 12 + 4] = srcNormal[ni + 1];
							dest[i * 12 + 5] = srcNormal[ni + 2];
							dest[i * 12 + 6] = srcTex[ti];
							dest[i * 12 + 7] = srcTex[ti + 1];

							GetVertexColor(i, globalIdx, hasColorPointer, out float r, out float g, out float b, out float a);
							dest[i * 12 + 8] = r;
							dest[i * 12 + 9] = g;
							dest[i * 12 + 10] = b;
							dest[i * 12 + 11] = a;
						}
					}
					else
					{
						for (int i = 0; i < batchCount; i++)
						{
							int globalIdx = first + offset + i;
							int vi = globalIdx * vertStride;
							int ti = globalIdx * texStride;

							dest[i * 9 + 0] = srcVert[vi];
							dest[i * 9 + 1] = srcVert[vi + 1];
							dest[i * 9 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;
							dest[i * 9 + 3] = srcTex[ti];
							dest[i * 9 + 4] = srcTex[ti + 1];

							GetVertexColor(i, globalIdx, hasColorPointer, out float r, out float g, out float b, out float a);

							if (lightingOn && !texReplace && srcNormal != null)
							{
								int ni = globalIdx * normStride;
								ApplyLighting(ref r, ref g, ref b, srcNormal[ni], srcNormal[ni + 1], srcNormal[ni + 2], light0On, light1On);
							}

							if (texReplace)
							{
								r = 1; g = 1; b = 1; a = 1;
							}

							dest[i * 9 + 5] = r;
							dest[i * 9 + 6] = g;
							dest[i * 9 + 7] = b;
							dest[i * 9 + 8] = a;
						}
					}
				}
				context.Unmap(dynamicTexVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			DrawElements((int)mode, count, (int)type, indices);
		}

		public void DrawElements(int mode, int count, int elementType, IntPtr indices)
		{
			if (isRecordingDisplayList) return;
			if (count <= 0) return;

			bool hasVertexPointer = arrayCapState.TryGetValue(ArrayCap.VertexArray, out var va) && va;
			if (!hasVertexPointer || vertexPointerData.pointer == IntPtr.Zero) return;

			bool hasColorPointer = arrayCapState.TryGetValue(ArrayCap.ColorArray, out var ca) && ca;
			bool hasTexCoordPointer = arrayCapState.TryGetValue(ArrayCap.TextureCoordArray, out var ta) && ta;
			bool hasNormalPointer = arrayCapState.TryGetValue(ArrayCap.NormalArray, out var na) && na;

			bool useTexture = hasTexCoordPointer && texture2DEnabled && boundTextures[0] != 0
				&& texCoordPointerData.pointer != IntPtr.Zero;

			bool lightingOn = enableCapState.TryGetValue((int)EnableCap.Lighting, out var lit) && lit;
			bool light0On = enableCapState.TryGetValue((int)EnableCap.Light0, out var l0) && l0;
			bool light1On = enableCapState.TryGetValue((int)EnableCap.Light1, out var l1) && l1;

			if (useTexture)
			{
				DrawElementsTextured((BeginMode)mode, count, elementType, indices, hasColorPointer, hasNormalPointer, lightingOn, light0On, light1On);
			}
			else
			{
				DrawElementsColored((BeginMode)mode, count, elementType, indices, hasColorPointer, hasNormalPointer, lightingOn, light0On, light1On);
			}
		}

		private unsafe int GetIndex(void* indicesPtr, int type, int i)
		{
			if (type == 5121) return ((byte*)indicesPtr)[i]; // GL_UNSIGNED_BYTE
			if (type == 5123) return ((ushort*)indicesPtr)[i]; // GL_UNSIGNED_SHORT
			return (int)((uint*)indicesPtr)[i]; // GL_UNSIGNED_INT
		}

		private unsafe void ProcessBatchColored(BeginMode mode, float* dest, float* srcVert, float* srcNormal, int vertStride, int normStride, void* indicesPtr, int type, int offset, int batchCount, bool useLitShader, bool hasColorPointer, bool lightingOn, bool light0On, bool light1On)
		{
			for (int i = 0; i < batchCount; i++)
			{
				int index = GetIndex(indicesPtr, type, offset + i);
				int colorElementIndex = GetColorIndexForFlatShading(mode, i, batchCount);
				int colorIndex = GetIndex(indicesPtr, type, offset + colorElementIndex);

				int vi = index * vertStride;
				int ni = index * normStride;

				if (useLitShader)
				{
					dest[i * 10 + 0] = srcVert[vi];
					dest[i * 10 + 1] = srcVert[vi + 1];
					dest[i * 10 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;
					dest[i * 10 + 3] = srcNormal[ni];
					dest[i * 10 + 4] = srcNormal[ni + 1];
					dest[i * 10 + 5] = srcNormal[ni + 2];

					GetVertexColor(colorElementIndex, colorIndex, hasColorPointer, out float r, out float g, out float b, out float a);
					dest[i * 10 + 6] = r;
					dest[i * 10 + 7] = g;
					dest[i * 10 + 8] = b;
					dest[i * 10 + 9] = a;
				}
				else
				{
					dest[i * 7 + 0] = srcVert[vi];
					dest[i * 7 + 1] = srcVert[vi + 1];
					dest[i * 7 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;

					GetVertexColor(colorElementIndex, colorIndex, hasColorPointer, out float r, out float g, out float b, out float a);

					if (lightingOn && srcNormal != null)
					{
						ApplyLighting(ref r, ref g, ref b, srcNormal[ni], srcNormal[ni + 1], srcNormal[ni + 2], light0On, light1On);
					}

					dest[i * 7 + 3] = r;
					dest[i * 7 + 4] = g;
					dest[i * 7 + 5] = b;
					dest[i * 7 + 6] = a;
				}
			}
		}

		private void DrawElementsColored(BeginMode mode, int count, int type, IntPtr indices, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			bool useLitShader = lightingOn && hasNormalPointer && normalPointerData.pointer != IntPtr.Zero;

			int stride = useLitShader ? 10 * sizeof(float) : 7 * sizeof(float);
			if (useLitShader)
			{
				UpdateLightBuffer(light0On, light1On);
				context.IASetInputLayout(posColorLitInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posColorLitVS);
					context.PSSetShader(posColorLitPS);
				}
				context.PSSetConstantBuffer(1, lightBuffer);
			}
			else
			{
				context.IASetInputLayout(posColorInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posColorVS);
					context.PSSetShader(posColorPS);
				}
			}

			UpdateTransformBuffer();

			context.IASetVertexBuffer(0, dynamicVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetConstantBuffer(0, transformBuffer);
			BindActiveTextures();
			ApplyRenderState();

			int offset = 0;
			byte[] vboData = null;
			if (currentElementBuffer > 0 && bufferDataStore.TryGetValue(currentElementBuffer, out var data))
			{
				vboData = data;
			}

			while (offset < count)
			{
				int batchCount = GetAlignedBatchCount(mode, count - offset, MaxVertices);

				var mapped = context.Map(dynamicVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* dest = (float*)mapped.DataPointer;
					float* srcVert = (float*)vertexPointerData.pointer;
					float* srcNormal = (hasNormalPointer && normalPointerData.pointer != IntPtr.Zero)
						? (float*)normalPointerData.pointer : null;

					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					int normStride = normalPointerData.stride > 0 ? normalPointerData.stride / sizeof(float) : 3;

					void* indicesPtr = (void*)indices;
					if (vboData != null)
					{
						fixed (byte* vboPtr = vboData)
						{
							indicesPtr = vboPtr + (int)indices;
							ProcessBatchColored(mode, dest, srcVert, srcNormal, vertStride, normStride, indicesPtr, type, offset, batchCount, useLitShader, hasColorPointer, lightingOn, light0On, light1On);
						}
					}
					else
					{
						ProcessBatchColored(mode, dest, srcVert, srcNormal, vertStride, normStride, indicesPtr, type, offset, batchCount, useLitShader, hasColorPointer, lightingOn, light0On, light1On);
					}
				}
				context.Unmap(dynamicVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		private unsafe void ProcessBatchTextured(BeginMode mode, float* dest, float* srcVert, float* srcTex, float* srcNormal, int vertStride, int texStride, int normStride, void* indicesPtr, int type, int offset, int batchCount, bool useLitShader, bool hasColorPointer, bool lightingOn, bool light0On, bool light1On, bool texReplace = false)
		{
			for (int i = 0; i < batchCount; i++)
			{
				int index = GetIndex(indicesPtr, type, offset + i);
				int colorElementIndex = GetColorIndexForFlatShading(mode, i, batchCount);
				int colorIndex = GetIndex(indicesPtr, type, offset + colorElementIndex);

				int vi = index * vertStride;
				int ti = index * texStride;
				int ni = index * normStride;

				if (useLitShader)
				{
					dest[i * 12 + 0] = srcVert[vi];
					dest[i * 12 + 1] = srcVert[vi + 1];
					dest[i * 12 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;
					dest[i * 12 + 3] = srcNormal[ni];
					dest[i * 12 + 4] = srcNormal[ni + 1];
					dest[i * 12 + 5] = srcNormal[ni + 2];
					dest[i * 12 + 6] = srcTex[ti];
					dest[i * 12 + 7] = srcTex[ti + 1];

					GetVertexColor(colorElementIndex, colorIndex, hasColorPointer, out float r, out float g, out float b, out float a);
					dest[i * 12 + 8] = r;
					dest[i * 12 + 9] = g;
					dest[i * 12 + 10] = b;
					dest[i * 12 + 11] = a;
				}
				else
				{
					dest[i * 9 + 0] = srcVert[vi];
					dest[i * 9 + 1] = srcVert[vi + 1];
					dest[i * 9 + 2] = vertexPointerData.size >= 3 ? srcVert[vi + 2] : 0;
					dest[i * 9 + 3] = srcTex[ti];
					dest[i * 9 + 4] = srcTex[ti + 1];

					GetVertexColor(colorElementIndex, colorIndex, hasColorPointer, out float r, out float g, out float b, out float a);

					if (lightingOn && !texReplace && srcNormal != null)
					{
						ApplyLighting(ref r, ref g, ref b, srcNormal[ni], srcNormal[ni + 1], srcNormal[ni + 2], light0On, light1On);
					}

					if (texReplace)
					{
						r = 1; g = 1; b = 1; a = 1;
					}

					dest[i * 9 + 5] = r;
					dest[i * 9 + 6] = g;
					dest[i * 9 + 7] = b;
					dest[i * 9 + 8] = a;
				}
			}
		}

		private void DrawElementsTextured(BeginMode mode, int count, int type, IntPtr indices, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			bool texReplace = texEnvMode == GL_REPLACE;
			bool useLitShader = lightingOn && hasNormalPointer && normalPointerData.pointer != IntPtr.Zero && !texReplace;

			int stride = useLitShader ? 12 * sizeof(float) : 9 * sizeof(float);
			if (useLitShader)
			{
				UpdateLightBuffer(light0On, light1On);
				context.IASetInputLayout(posTexLitInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posTexLitVS);
					context.PSSetShader(posTexLitPS);
				}
				context.PSSetConstantBuffer(1, lightBuffer);
			}
			else
			{
				context.IASetInputLayout(posTexInputLayout);
				if (currentProgram == 0)
				{
					context.VSSetShader(posTexVS);
					context.PSSetShader(posTexPS);
				}
			}

			UpdateTransformBuffer();
			context.IASetVertexBuffer(0, dynamicTexVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetConstantBuffer(0, transformBuffer);

			BindActiveTextures();

			ApplyRenderState();

			int offset = 0;
			byte[] vboData = null;
			if (currentElementBuffer > 0 && bufferDataStore.TryGetValue(currentElementBuffer, out var data))
			{
				vboData = data;
			}

			while (offset < count)
			{
				int batchCount = GetAlignedBatchCount(mode, count - offset, MaxVertices);

				var mapped = context.Map(dynamicTexVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* dest = (float*)mapped.DataPointer;
					float* srcVert = (float*)vertexPointerData.pointer;
					float* srcTex = (float*)texCoordPointerData.pointer;
					float* srcNormal = (hasNormalPointer && normalPointerData.pointer != IntPtr.Zero)
						? (float*)normalPointerData.pointer : null;

					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					int texStride = texCoordPointerData.stride > 0 ? texCoordPointerData.stride / sizeof(float) : texCoordPointerData.size;
					int normStride = normalPointerData.stride > 0 ? normalPointerData.stride / sizeof(float) : 3;

					void* indicesPtr = (void*)indices;
					if (vboData != null)
					{
						fixed (byte* vboPtr = vboData)
						{
							indicesPtr = vboPtr + (int)indices;
							ProcessBatchTextured(mode, dest, srcVert, srcTex, srcNormal, vertStride, texStride, normStride, indicesPtr, type, offset, batchCount, useLitShader, hasColorPointer, lightingOn, light0On, light1On, texReplace);
						}
					}
					else
					{
						ProcessBatchTextured(mode, dest, srcVert, srcTex, srcNormal, vertStride, texStride, normStride, indicesPtr, type, offset, batchCount, useLitShader, hasColorPointer, lightingOn, light0On, light1On, texReplace);
					}
				}
				context.Unmap(dynamicTexVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		// --- State management ---

		public void Enable(int cap)
		{
			enableCapState[cap] = true;
			if (cap == (int)EnableCap.Texture2D) texture2DEnabled = true;
			if (cap == (int)EnableCap.ScissorTest) scissorEnabled = true;
			renderStateDirty = true;
		}

		public void Disable(int cap)
		{
			enableCapState[cap] = false;
			if (cap == (int)EnableCap.Texture2D) texture2DEnabled = false;
			if (cap == (int)EnableCap.ScissorTest) scissorEnabled = false;
			renderStateDirty = true;
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
			arrayCapState[arrayCap] = true;
		}

		public void DisableClientState(ArrayCap array)
		{
			arrayCapState[array] = false;
		}

		public void BlendFunc(int sfactor, int dfactor)
		{
			blendSrcFactor = sfactor;
			blendDstFactor = dfactor;
			renderStateDirty = true;
		}

		private ID3D11BlendState GetOrCreateBlendState(bool blendEnable, int srcFactor, int dstFactor, ColorWriteEnable writeMask)
		{
			if (!blendEnable)
			{
				srcFactor = 0;
				dstFactor = 0;
			}
			var key = (blendEnable, srcFactor, dstFactor, writeMask);
			if (!blendStateCache.TryGetValue(key, out var state))
			{
				var desc = new BlendDescription();
				desc.RenderTarget[0] = new RenderTargetBlendDescription
				{
					BlendEnable = blendEnable,
					SourceBlend = MapBlendFactor(srcFactor),
					DestinationBlend = MapBlendFactor(dstFactor),
					BlendOperation = BlendOperation.Add,
					SourceBlendAlpha = MapBlendFactor(srcFactor),
					DestinationBlendAlpha = MapBlendFactor(dstFactor),
					BlendOperationAlpha = BlendOperation.Add,
					RenderTargetWriteMask = writeMask,
				};
				state = device.CreateBlendState(desc);
				blendStateCache[key] = state;
			}
			return state;
		}

		private static Blend MapBlendFactor(int glFactor)
		{
			return glFactor switch
			{
				0 => Blend.Zero,                          // GL_ZERO
				1 => Blend.One,                           // GL_ONE
				0x0300 => Blend.SourceColor,              // GL_SRC_COLOR
				0x0301 => Blend.InverseSourceColor,       // GL_ONE_MINUS_SRC_COLOR
				0x0302 => Blend.SourceAlpha,              // GL_SRC_ALPHA
				0x0303 => Blend.InverseSourceAlpha,       // GL_ONE_MINUS_SRC_ALPHA
				0x0304 => Blend.DestinationAlpha,         // GL_DST_ALPHA
				0x0305 => Blend.InverseDestinationAlpha,  // GL_ONE_MINUS_DST_ALPHA
				0x0306 => Blend.DestinationColor,         // GL_DST_COLOR
				0x0307 => Blend.InverseDestinationColor,  // GL_ONE_MINUS_DST_COLOR
				_ => Blend.One,
			};
		}

		public void DepthFunc(int func)
		{
			renderStateDirty = true;
			depthCompareFunc = func switch
			{
				0x0200 => ComparisonFunction.Never,       // GL_NEVER
				0x0201 => ComparisonFunction.Less,        // GL_LESS
				0x0202 => ComparisonFunction.Equal,       // GL_EQUAL
				0x0203 => ComparisonFunction.LessEqual,   // GL_LEQUAL
				0x0204 => ComparisonFunction.Greater,     // GL_GREATER
				0x0205 => ComparisonFunction.NotEqual,    // GL_NOTEQUAL
				0x0206 => ComparisonFunction.GreaterEqual, // GL_GEQUAL
				0x0207 => ComparisonFunction.Always,      // GL_ALWAYS
				_ => ComparisonFunction.Less,
			};
		}

		public void DepthMask(bool flag)
		{
			depthMaskEnabled = flag;
			renderStateDirty = true;
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
			ColorWriteEnable mask = ColorWriteEnable.None;
			if (red) mask |= ColorWriteEnable.Red;
			if (green) mask |= ColorWriteEnable.Green;
			if (blue) mask |= ColorWriteEnable.Blue;
			if (alpha) mask |= ColorWriteEnable.Alpha;
			
			if (currentColorWriteMask != mask)
			{
				currentColorWriteMask = mask;
				renderStateDirty = true;
			}
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode) { }

		public void CullFace(CullFaceMode mode)
		{
			currentCullMode = mode == CullFaceMode.Front ? CullMode.Front : CullMode.Back;
			renderStateDirty = true;
		}

		public void FrontFace(FrontFaceDirection mode)
		{
			frontFaceCCW = mode == FrontFaceDirection.Ccw;
			renderStateDirty = true;
		}

		public void ShadeModel(ShadingModel model)
		{
			flatShading = model == ShadingModel.Flat;
		}

		public void PolygonOffset(float factor, float units)
		{
			polygonOffsetFactor = factor;
			polygonOffsetUnits = units;
			renderStateDirty = true;
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
			int idx = light == LightName.Light0 ? 0 : 1;
			if (idx >= lights.Length || param == null) return;

			switch (pname)
			{
				case LightParameter.Ambient: Array.Copy(param, lights[idx].Ambient, Math.Min(param.Length, 4)); break;
				case LightParameter.Diffuse: Array.Copy(param, lights[idx].Diffuse, Math.Min(param.Length, 4)); break;
				case LightParameter.Specular: Array.Copy(param, lights[idx].Specular, Math.Min(param.Length, 4)); break;
				case LightParameter.Position:
					// OpenGL transforms light position by the current modelview matrix
					var mv = modelViewStack.Peek();
					float px = param.Length > 0 ? param[0] : 0;
					float py = param.Length > 1 ? param[1] : 0;
					float pz = param.Length > 2 ? param[2] : 0;
					float pw = param.Length > 3 ? param[3] : 0;
					
					// M * v where M's columns are stored in Row0, Row1, etc.
					lights[idx].Position[0] = (float)(px * mv.Row0.X + py * mv.Row1.X + pz * mv.Row2.X + pw * mv.Row3.X);
					lights[idx].Position[1] = (float)(px * mv.Row0.Y + py * mv.Row1.Y + pz * mv.Row2.Y + pw * mv.Row3.Y);
					lights[idx].Position[2] = (float)(px * mv.Row0.Z + py * mv.Row1.Z + pz * mv.Row2.Z + pw * mv.Row3.Z);
					lights[idx].Position[3] = (float)(px * mv.Row0.W + py * mv.Row1.W + pz * mv.Row2.W + pw * mv.Row3.W);
					break;
			}
		}

		// --- Matrix operations ---

		public void MatrixMode(OpenGl.MatrixMode mode)
		{
			matrixMode = mode;
		}

		public void LoadIdentity()
		{
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Pop();
				modelViewStack.Push(Matrix4X4.Identity);
			}
			else
			{
				projectionStack.Pop();
				projectionStack.Push(Matrix4X4.Identity);
			}
			transformDirty = true;
		}

		public void LoadMatrix(double[] m)
		{
			var matrix = new Matrix4X4(
				m[0], m[1], m[2], m[3],
				m[4], m[5], m[6], m[7],
				m[8], m[9], m[10], m[11],
				m[12], m[13], m[14], m[15]);

			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Pop();
				modelViewStack.Push(matrix);
			}
			else
			{
				projectionStack.Pop();
				projectionStack.Push(matrix);
			}
			transformDirty = true;
		}

		public void MultMatrix(float[] m)
		{
			var matrix = new Matrix4X4(m);
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(matrix * modelViewStack.Pop());
			}
			else
			{
				projectionStack.Push(matrix * projectionStack.Pop());
			}
			transformDirty = true;
		}

		public void PushMatrix()
		{
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(modelViewStack.Peek());
			}
			else
			{
				projectionStack.Push(projectionStack.Peek());
			}
		}

		public void PopMatrix()
		{
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				if (modelViewStack.Count > 1) modelViewStack.Pop();
			}
			else
			{
				if (projectionStack.Count > 1) projectionStack.Pop();
			}
			transformDirty = true;
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			double w = right - left;
			double h = top - bottom;
			double d = zFar - zNear;

			var ortho = new Matrix4X4(
				2.0 / w, 0, 0, 0,
				0, 2.0 / h, 0, 0,
				0, 0, -2.0 / d, 0,
				-(right + left) / w, -(top + bottom) / h, -(zFar + zNear) / d, 1);

			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(ortho * modelViewStack.Pop());
			}
			else
			{
				projectionStack.Push(ortho * projectionStack.Pop());
			}
			transformDirty = true;
		}

		public void Translate(Vector3 vector)
		{
			Translate(vector.X, vector.Y, vector.Z);
		}

		public void Translate(double x, double y, double z)
		{
			var translation = Matrix4X4.CreateTranslation(x, y, z);
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(translation * modelViewStack.Pop());
			}
			else
			{
				projectionStack.Push(translation * projectionStack.Pop());
			}
			transformDirty = true;
		}

		public void Rotate(double angle, double x, double y, double z)
		{
			double radians = VectorMath.MathHelper.DegreesToRadians(angle);
			var axis = new Vector3(x, y, z);
			var rotation = Matrix4X4.CreateRotation(axis, radians);

			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(rotation * modelViewStack.Pop());
			}
			else
			{
				projectionStack.Push(rotation * projectionStack.Pop());
			}
			transformDirty = true;
		}

		public void Scale(double x, double y, double z)
		{
			var scale = Matrix4X4.CreateScale(x, y, z);
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(scale * modelViewStack.Pop());
			}
			else
			{
				projectionStack.Push(scale * projectionStack.Pop());
			}
			transformDirty = true;
		}

		private Stack<(AttribMask mask, int x, int y, int w, int h)> attribStack = new Stack<(AttribMask, int, int, int, int)>();

		public void PushAttrib(AttribMask mask)
		{
			attribStack.Push((mask, currentViewport.x, currentViewport.y, currentViewport.w, currentViewport.h));
		}

		public void PopAttrib()
		{
			if (attribStack.Count > 0)
			{
				var saved = attribStack.Pop();
				if ((saved.mask & AttribMask.ViewportBit) != 0)
				{
					Viewport(saved.x, saved.y, saved.w, saved.h);
				}
			}
		}

		// --- Clear and viewport ---

		public void Clear(int mask)
		{
			if ((mask & 0x00004000) != 0) // GL_COLOR_BUFFER_BIT
			{
				if (currentBoundFramebuffer == 0)
				{
					context.ClearRenderTargetView(renderTargetView, clearColor);
				}
				else if (framebuffers.TryGetValue(currentBoundFramebuffer, out var fbo) && fbo.RenderTargetView != null)
				{
					context.ClearRenderTargetView(fbo.RenderTargetView, clearColor);
				}
			}
			if ((mask & 0x00000100) != 0) // GL_DEPTH_BUFFER_BIT
			{
				if (currentBoundFramebuffer == 0)
				{
					context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
				}
				else if (framebuffers.TryGetValue(currentBoundFramebuffer, out var fbo) && fbo.DepthStencilView != null)
				{
					context.ClearDepthStencilView(fbo.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
				}
			}
		}

		public void ClearDepth(double depth) { }

		public void ClearColor(double r, double g, double b, double a)
		{
			clearColor = new Color4((float)r, (float)g, (float)b, (float)a);
		}

		private (int x, int y, int w, int h) currentViewport;

		/// <summary>
		/// Scale from logical GL coordinates to device pixels for the active render
		/// target. User framebuffers (render-to-texture) are addressed in their own
		/// texture pixels and are never supersampled, so only the default framebuffer
		/// scales during full-frame capture.
		/// </summary>
		private int ActiveCoordinateScale => currentBoundFramebuffer == 0 ? supersampleScale : 1;

		public void Viewport(int x, int y, int width, int height)
		{
			viewportX = x;
			viewportY = y;
			viewportWidth = width;
			viewportHeight = height;
			currentViewport = (x, y, width, height);

			// Convert from OpenGL convention (y=0 at bottom) to D3D11 convention (y=0 at top).
			// renderTargetHeight is in device pixels (already scaled during supersample
			// capture), so scale the logical coordinates to match.
			int scale = ActiveCoordinateScale;
			int d3dY = renderTargetHeight - (y + height) * scale;
			context.RSSetViewport(x * scale, d3dY, width * scale, height * scale);
		}

		public void Scissor(int x, int y, int width, int height)
		{
			scissorX = x;
			scissorY = y;
			scissorWidth = width;
			scissorHeight = height;

			if (width < 0) width = 0;
			if (height < 0) height = 0;

			int scale = ActiveCoordinateScale;
			int d3dY = renderTargetHeight - (y + height) * scale;
			context.RSSetScissorRect(x * scale, d3dY, width * scale, height * scale);
		}

		// --- Buffer management ---

		public int GenBuffer()
		{
			int id = nextBufferId++;
			return id;
		}

		public void GenBuffers(int n, out int buffer)
		{
			buffer = nextBufferId++;
		}

		public void DeleteBuffer(int buffer)
		{
			if (buffers.TryGetValue(buffer, out var buf))
			{
				buf.Dispose();
				buffers.Remove(buffer);
			}
			bufferDataStore.Remove(buffer);
		}

		public void BindBuffer(int target, int buffer)
		{
			if (target == 0x8892) // GL_ARRAY_BUFFER
				currentArrayBuffer = buffer;
			else if (target == 0x8893) // GL_ELEMENT_ARRAY_BUFFER
				currentElementBuffer = buffer;
		}

		public void BufferData(int target, int size, IntPtr data, int usage)
		{
			int bufferId = target == 0x8892 ? currentArrayBuffer : currentElementBuffer;
			if (bufferId <= 0) return;

			byte[] managedData = new byte[size];
			if (data != IntPtr.Zero)
			{
				Marshal.Copy(data, managedData, 0, size);
			}
			bufferDataStore[bufferId] = managedData;

			if (buffers.TryGetValue(bufferId, out var oldBuf))
			{
				oldBuf.Dispose();
			}

			var desc = new BufferDescription
			{
				ByteWidth = (uint)size,
				Usage = ResourceUsage.Default,
				BindFlags = target == 0x8892 ? BindFlags.VertexBuffer : BindFlags.IndexBuffer,
			};

			unsafe
			{
				fixed (byte* ptr = managedData)
				{
					var initData = new SubresourceData((IntPtr)ptr, (uint)size);
					buffers[bufferId] = device.CreateBuffer(desc, initData);
				}
			}
		}

		// --- Vertex pointers ---

		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
		{
			vertexPointerData = (size, stride, pointer);
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
			colorPointerData = (size, stride, pointer);
		}

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
			texCoordPointerData = (size, stride, pointer);
		}

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
			normalPointerData = (0, stride, pointer);
		}

		public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer) { }

		// --- Texture management ---

		public int GenTexture()
		{
			int id = nextTextureId++;
			textures[id] = new TextureInfo();
			return id;
		}

		public void GenTextures(int n, out int texId)
		{
			texId = GenTexture();
		}

		public void DeleteTexture(int texture)
		{
			if (textures.TryGetValue(texture, out var info))
			{
				info.Sampler?.Dispose();
				info.ShaderResourceView?.Dispose();
				info.Texture?.Dispose();
				textures.Remove(texture);
			}
		}

		public void BindTexture(int target, int texture)
		{
			if (activeTextureUnit >= 0 && activeTextureUnit < boundTextures.Length)
			{
				boundTextures[activeTextureUnit] = texture;
			}
		}

		public void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels)
		{
			int currentTex = boundTextures[activeTextureUnit];
			if (currentTex <= 0 || !textures.ContainsKey(currentTex)) return;

			var texInfo = textures[currentTex];

			// 0x1908 = GL_RGBA, 0x80E1 = GL_BGRA
			var d3dFormat = format == 0x80E1 ? Format.B8G8R8A8_UNorm : Format.R8G8B8A8_UNorm;

			if (level == 0)
			{
				texInfo.ShaderResourceView?.Dispose();
				texInfo.Texture?.Dispose();
				texInfo.Texture = null;
				texInfo.ShaderResourceView = null;

				texInfo.D3DFormat = d3dFormat;
				texInfo.Width = width;
				texInfo.Height = height;
				texInfo.ExpectedMipCount = 1 + (int)Math.Floor(Math.Log(Math.Max(width, height), 2));
				texInfo.PendingMipData = new List<(byte[], int, int)> { (pixels != null ? (byte[])pixels.Clone() : null, width, height) };
			}
			else
			{
				if (texInfo.PendingMipData != null)
				{
					while (texInfo.PendingMipData.Count <= level)
						texInfo.PendingMipData.Add((null, 0, 0));
					texInfo.PendingMipData[level] = (pixels != null ? (byte[])pixels.Clone() : null, width, height);
				}
			}

			FinalizeTextureIfReady(texInfo);
		}

		private void FinalizeTextureIfReady(TextureInfo texInfo, bool force = false)
		{
			if (texInfo.PendingMipData == null || texInfo.PendingMipData.Count == 0) return;

			if (!force && texInfo.PendingMipData.Count < texInfo.ExpectedMipCount) return;

			int mipCount = texInfo.PendingMipData.Count;

			texInfo.Texture?.Dispose();
			texInfo.ShaderResourceView?.Dispose();

			var texDesc = new Texture2DDescription
			{
				Width = (uint)texInfo.Width,
				Height = (uint)texInfo.Height,
				MipLevels = (uint)mipCount,
				ArraySize = 1,
				Format = texInfo.D3DFormat,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
			};

			var initDataArray = new SubresourceData[mipCount];
			var handles = new System.Runtime.InteropServices.GCHandle[mipCount];
			bool hasAnyData = false;
			foreach (var d in texInfo.PendingMipData) if (d.pixels != null) hasAnyData = true;

			try
			{
				if (hasAnyData)
				{
					for (int i = 0; i < mipCount; i++)
					{
						var (pixels, w, h) = texInfo.PendingMipData[i];
						if (pixels != null)
						{
							handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
							initDataArray[i] = new SubresourceData(handles[i].AddrOfPinnedObject(), (uint)(w * 4));
						}
						else
						{
							initDataArray[i] = new SubresourceData(IntPtr.Zero, (uint)(w * 4));
						}
					}

					texInfo.Texture = device.CreateTexture2D(texDesc, initDataArray);
				}
				else
				{
					texInfo.Texture = device.CreateTexture2D(texDesc);
				}
			}
			finally
			{
				for (int i = 0; i < mipCount; i++)
				{
					if (handles[i].IsAllocated) handles[i].Free();
				}
			}

			texInfo.ShaderResourceView = device.CreateShaderResourceView(texInfo.Texture);
			texInfo.PendingMipData = null;
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			int currentTex = boundTextures[activeTextureUnit];
			if (currentTex <= 0 || !textures.TryGetValue(currentTex, out var texInfo)) return;

			bool changed = false;
			switch ((int)pname)
			{
				case 10240: // TextureMagFilter
					bool magLinear = param == 9729; // GL_LINEAR
					if (texInfo.MagFilterLinear != magLinear) { texInfo.MagFilterLinear = magLinear; changed = true; }
					break;
				case 10241: // TextureMinFilter
					bool minLinear = param != 9728; // anything except GL_NEAREST
					if (texInfo.MinFilterLinear != minLinear) { texInfo.MinFilterLinear = minLinear; changed = true; }
					break;
				case 10242: // TextureWrapS
				case 10243: // TextureWrapT
					bool clamp = param == 33071; // GL_CLAMP_TO_EDGE
					if (texInfo.Clamp != clamp) { texInfo.Clamp = clamp; changed = true; }
					break;
			}

			if (changed)
			{
				texInfo.Sampler?.Dispose();
				texInfo.Sampler = device.CreateSamplerState(new SamplerDescription
				{
					Filter = (texInfo.MagFilterLinear && texInfo.MinFilterLinear)
						? Filter.MinMagMipLinear : Filter.MinMagMipPoint,
					AddressU = texInfo.Clamp ? TextureAddressMode.Clamp : TextureAddressMode.Wrap,
					AddressV = texInfo.Clamp ? TextureAddressMode.Clamp : TextureAddressMode.Wrap,
					AddressW = TextureAddressMode.Wrap,
					ComparisonFunc = ComparisonFunction.Never,
					MinLOD = 0,
					MaxLOD = float.MaxValue,
				});
			}
		}

		public void TexParameteri(int target, int pname, int param)
		{
			TexParameter(TextureTarget.Texture2D, (TextureParameterName)pname, param);
		}
		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
		{
			if (pname == TextureEnvParameter.TextureEnvMode)
			{
				texEnvMode = (int)param;
			}
		}
		public void ActiveTexture(int texture)
		{
			if (texture >= 0x84C0 && texture < 0x84C0 + 8) // GL_TEXTURE0
			{
				activeTextureUnit = texture - 0x84C0;
			}
			else if (texture >= 0 && texture < 8)
			{
				activeTextureUnit = texture;
			}
		}

		// --- Shader program management ---

		public int CreateProgram()
		{
			int id = nextProgramId++;
			shaderPrograms[id] = new ShaderProgramInfo();
			return id;
		}

		public int CreateShader(int shaderType)
		{
			int id = nextShaderId++;
			shaderObjects[id] = new ShaderInfo { Type = shaderType };
			return id;
		}

		public void ShaderSource(int id, int count, string src, object p)
		{
			if (shaderObjects.TryGetValue(id, out var info))
			{
				info.Source = src;
			}
		}

		public void CompileShader(int id)
		{
			if (!shaderObjects.TryGetValue(id, out var info)) return;
			if (string.IsNullOrEmpty(info.Source)) return;

			string profile = info.Type == 0x8B31 ? "vs_5_0" : "ps_5_0"; // VERTEX_SHADER / FRAGMENT_SHADER
			string entry = info.Type == 0x8B31 ? "VS" : "PS";

			try
			{
				info.ByteCode = Compiler.Compile(info.Source, entry, "shader", profile).ToArray();
			}
			catch (Exception ex)
			{
				info.CompileErrors = ex.Message;
			}
		}

		public void AttachShader(int program, int shader)
		{
			if (shaderPrograms.TryGetValue(program, out var prog) && shaderObjects.TryGetValue(shader, out var shdr))
			{
				if (shdr.Type == 0x8B31) prog.VertexShaderId = shader;
				else prog.FragmentShaderId = shader;
			}
		}

		private void ReflectUniforms(ShaderProgramInfo prog, byte[] bytecode)
		{
			try
			{
				using var reflection = Compiler.Reflect<Vortice.Direct3D11.Shader.ID3D11ShaderReflection>(bytecode);
				for (int i = 0; i < reflection.Description.ConstantBuffers; i++)
				{
					var cb = reflection.GetConstantBufferByIndex((uint)i);
					var desc = cb.Description;
					if (desc.Type.ToString() == "ConstantBuffer")
					{
						prog.UniformBufferSize = Math.Max(prog.UniformBufferSize, (int)desc.Size);
						for (uint v = 0; v < desc.VariableCount; v++)
						{
							var varInfo = cb.GetVariableByIndex(v);
							var vDesc = varInfo.Description;
							if (!prog.UniformLocations.ContainsKey(vDesc.Name))
							{
								// Store the offset in floats
								prog.UniformLocations[vDesc.Name] = (int)(vDesc.StartOffset / 4);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Shader reflection failed: {ex.Message}");
			}
		}

		public void LinkProgram(int id)
		{
			if (!shaderPrograms.TryGetValue(id, out var prog)) return;

			if (prog.VertexShaderId > 0 && shaderObjects.TryGetValue(prog.VertexShaderId, out var vs) && vs.ByteCode != null)
			{
				prog.VertexShader = device.CreateVertexShader(vs.ByteCode);
				ReflectUniforms(prog, vs.ByteCode);
			}

			if (prog.FragmentShaderId > 0 && shaderObjects.TryGetValue(prog.FragmentShaderId, out var fs) && fs.ByteCode != null)
			{
				prog.PixelShader = device.CreatePixelShader(fs.ByteCode);
				ReflectUniforms(prog, fs.ByteCode);
			}

			if (prog.ConstantBuffer == null && prog.UniformBufferSize > 0)
			{
				// Constant buffers must be a multiple of 16 bytes
				int bufferSize = (prog.UniformBufferSize + 15) & ~15;
				prog.Uniforms = new float[bufferSize / 4];
				
				prog.ConstantBuffer = device.CreateBuffer(new BufferDescription
				{
					ByteWidth = (uint)bufferSize,
					BindFlags = BindFlags.ConstantBuffer,
					Usage = ResourceUsage.Dynamic,
					CPUAccessFlags = CpuAccessFlags.Write
				});
			}
		}

		public void UseProgram(int program)
		{
			currentProgram = program;
			if (program == 0)
			{
				// Restore default shaders
				context.VSSetShader(null);
				context.PSSetShader(null);
				return;
			}

			if (shaderPrograms.TryGetValue(program, out var prog))
			{
				if (prog.VertexShader != null) context.VSSetShader(prog.VertexShader);
				if (prog.PixelShader != null) context.PSSetShader(prog.PixelShader);
			}
		}

		public void DeleteShader(int shader)
		{
			shaderObjects.Remove(shader);
		}

		public void DeleteProgram(int program)
		{
			if (shaderPrograms.TryGetValue(program, out var prog))
			{
				prog.Dispose();
				shaderPrograms.Remove(program);
			}
		}

		public void DetachShader(int id, int shader) { }

		public int GetUniformLocation(int program, string name)
		{
			if (shaderPrograms.TryGetValue(program, out var prog))
			{
				if (prog.UniformLocations.TryGetValue(name, out var loc))
				{
					return loc;
				}
			}
			return -1;
		}

		public void Uniform1i(int location, int v0) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.Uniforms != null && location >= 0 && location < prog.Uniforms.Length)
				{
					prog.Uniforms[location] = v0;
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}

		public void Uniform1f(int location, float v0) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.Uniforms != null && location >= 0 && location < prog.Uniforms.Length)
				{
					prog.Uniforms[location] = v0;
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}

		public void Uniform2f(int location, float v0, float v1) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.Uniforms != null && location >= 0 && location + 1 < prog.Uniforms.Length)
				{
					prog.Uniforms[location + 0] = v0;
					prog.Uniforms[location + 1] = v1;
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}

		public void Uniform3f(int location, float v0, float v1, float v2) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.Uniforms != null && location >= 0 && location + 2 < prog.Uniforms.Length)
				{
					prog.Uniforms[location + 0] = v0;
					prog.Uniforms[location + 1] = v1;
					prog.Uniforms[location + 2] = v2;
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}

		public void Uniform4f(int location, float v0, float v1, float v2, float v3) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				if (prog.Uniforms != null && location >= 0 && location + 3 < prog.Uniforms.Length)
				{
					prog.Uniforms[location + 0] = v0;
					prog.Uniforms[location + 1] = v1;
					prog.Uniforms[location + 2] = v2;
					prog.Uniforms[location + 3] = v3;
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}

		public void UniformMatrix4fv(int location, int count, int transpose, float[] value) 
		{ 
			if (currentProgram != 0 && shaderPrograms.TryGetValue(currentProgram, out var prog))
			{
				int floatsNeeded = count * 16;
				if (prog.Uniforms != null && location >= 0 && location + floatsNeeded - 1 < prog.Uniforms.Length && value != null && value.Length >= floatsNeeded)
				{
					for (int c = 0; c < count; c++)
					{
						int baseLoc = location + c * 16;
						int baseVal = c * 16;

						if (transpose != 0)
						{
							// Source is transposed, write transposed
							for (int i = 0; i < 4; i++)
							{
								for (int j = 0; j < 4; j++)
								{
									prog.Uniforms[baseLoc + i * 4 + j] = value[baseVal + j * 4 + i];
								}
							}
						}
						else
						{
							for (int i = 0; i < 16; i++)
							{
								prog.Uniforms[baseLoc + i] = value[baseVal + i];
							}
						}
					}
					prog.UniformsDirty = true;
					renderStateDirty = true;
				}
			}
		}
		public void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer) { }
		public void EnableVertexAttribArray(int index) { }
		public void BindVertexArray(int vertexArray) { }

		public void GenVertexArrays(int n, out int arrays)
		{
			arrays = nextVaoId++;
		}

		public string GetShaderInfoLog(int shader)
		{
			if (shaderObjects.TryGetValue(shader, out var info))
			{
				return info.CompileErrors ?? "";
			}
			return "";
		}

		// --- Framebuffer ---

		public void BindFramebuffer(int target, int buffer)
		{
			currentBoundFramebuffer = buffer;
			transformDirty = true; // Projection depends on framebuffer state

			if (buffer == 0)
			{
				context.OMSetRenderTargets(renderTargetView, depthStencilView);
				if (currentBackBuffer != null)
				{
					// During full-frame capture the default framebuffer is the
					// supersample target, which is supersampleScale times the backbuffer.
					renderTargetHeight = (int)currentBackBuffer.Description.Height * supersampleScale;
				}
			}
			else if (framebuffers.TryGetValue(buffer, out var fbo))
			{
				context.OMSetRenderTargets(fbo.RenderTargetView, fbo.DepthStencilView);
				if (fbo.TextureId > 0 && textures.TryGetValue(fbo.TextureId, out var texInfo))
				{
					renderTargetHeight = texInfo.Height;
				}
			}

			// Re-apply viewport and scissor with the new renderTargetHeight
			Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
			if (scissorEnabled)
			{
				Scissor(scissorX, scissorY, scissorWidth, scissorHeight);
			}
		}

		public int GenFramebuffer()
		{
			int id = nextFramebufferId++;
			framebuffers[id] = new FramebufferInfo();
			return id;
		}

		public void GenFramebuffers(int v, out int fbo)
		{
			fbo = GenFramebuffer();
		}

		public void DeleteFramebuffers(int n, ref int framebuffers)
		{
			if (n == 1 && this.framebuffers.TryGetValue(framebuffers, out var fbo))
			{
				fbo.RenderTargetView?.Dispose();
				fbo.DepthStencilView?.Dispose();
				this.framebuffers.Remove(framebuffers);
				if (currentBoundFramebuffer == framebuffers)
				{
					BindFramebuffer(0x8D40, 0);
				}
			}
		}

		public int CheckFramebufferStatus(int target)
		{
			return 0x8CD5; // GL_FRAMEBUFFER_COMPLETE
		}

		public void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level)
		{
			if (currentBoundFramebuffer <= 0 || !framebuffers.TryGetValue(currentBoundFramebuffer, out var fbo)) return;

			if (attachment == 0x8CE0) // GL_COLOR_ATTACHMENT0
			{
				fbo.TextureId = texture;
				fbo.RenderTargetView?.Dispose();
				fbo.RenderTargetView = null;

				if (texture > 0 && textures.TryGetValue(texture, out var texInfo) && texInfo.Texture != null)
				{
					fbo.RenderTargetView = device.CreateRenderTargetView(texInfo.Texture);
				}

				// Re-bind to apply changes if it is the current FBO
				BindFramebuffer(target, currentBoundFramebuffer);
			}
			else if (attachment == 0x8D00 || attachment == 0x8D20) // GL_DEPTH_ATTACHMENT or GL_DEPTH_STENCIL_ATTACHMENT
			{
				// Simplification: if requested, we could create a DepthStencilView here.
				// For basic FBOs without depth, or sharing the default depth, we can leave it null.
			}
		}

		// --- Display list emulation ---

		public int GenLists(int v)
		{
			int firstId = nextDisplayListId;
			for (int i = 0; i < v; i++)
			{
				displayLists[nextDisplayListId] = new DisplayList();
				nextDisplayListId++;
			}
			return firstId;
		}

		public void NewList(int displayListId, object compile)
		{
			recordingDisplayListId = displayListId;
			isRecordingDisplayList = true;
			if (!displayLists.ContainsKey(displayListId))
			{
				displayLists[displayListId] = new DisplayList();
			}
			displayLists[displayListId].Entries.Clear();
		}

		public void EndList()
		{
			isRecordingDisplayList = false;
			recordingDisplayListId = 0;
		}

		public void CallList(int displayListId)
		{
			if (!displayLists.TryGetValue(displayListId, out var list)) return;

			foreach (var entry in list.Entries)
			{
				immediateData.Mode = entry.Mode;
				immediateData.Positions = new List<float>(entry.Positions);
				immediateData.Colors = new List<byte>(entry.Colors);
				immediateData.TexCoords = new List<float>(entry.TexCoords);
				FlushImmediateMode();
			}
		}

		public void DeleteLists(int id, int v)
		{
			for (int i = 0; i < v; i++)
			{
				displayLists.Remove(id + i);
			}
		}

		private void RecordToDisplayList()
		{
			if (!displayLists.TryGetValue(recordingDisplayListId, out var list)) return;

			list.Entries.Add(new DisplayListEntry
			{
				Mode = immediateData.Mode,
				Positions = new List<float>(immediateData.Positions),
				Colors = new List<byte>(immediateData.Colors),
				TexCoords = new List<float>(immediateData.TexCoords),
			});
		}

		// --- Misc ---

		public ErrorCode GetError() => ErrorCode.NoError;

		public string GetString(StringName name)
		{
			if (name == StringName.Extensions)
			{
				// D3D11 always supports non-power-of-two textures;
				// ImageTexturePlugin checks for this to avoid unnecessary padding
				return "ARB_texture_non_power_of_two";
			}

			return "Vortice Direct3D 11";
		}
		public void Finish() { }

		public void Present()
		{
			if (mainRenderTarget != null && currentBackBuffer != null)
			{
				context.CopyResource(currentBackBuffer, mainRenderTarget);
			}

			if (swapChain != null)
			{
				swapChain.Present(0, PresentFlags.None);

				// With FlipDiscard, the back buffer changes after Present.
				currentBackBuffer?.Dispose();
				currentBackBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
			}

			// renderTargetView points to mainRenderTarget (stable), not the swapchain backbuffer
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
		}

		public void Dispose()
		{
			DisposeSupersampleResources();
			DisposeSceneEffects();

			foreach (var buf in buffers.Values) buf?.Dispose();
			foreach (var tex in textures.Values)
			{
				tex.ShaderResourceView?.Dispose();
				tex.Texture?.Dispose();
			}

			mainRenderTarget?.Dispose();

			foreach (var prog in shaderPrograms.Values) prog?.Dispose();
			shaderPrograms.Clear();

			foreach (var bs in blendStateCache.Values) bs?.Dispose();
			blendStateCache.Clear();

			foreach (var fbo in framebuffers.Values)
			{
				fbo.RenderTargetView?.Dispose();
				fbo.DepthStencilView?.Dispose();
			}
			framebuffers.Clear();

			dynamicVertexBuffer?.Dispose();
			dynamicTexVertexBuffer?.Dispose();
			transformBuffer?.Dispose();
			lightBuffer?.Dispose();
			posColorVS?.Dispose();
			posColorPS?.Dispose();
			posColorFlatPS?.Dispose();
			posColorInputLayout?.Dispose();
			posTexVS?.Dispose();
			posTexPS?.Dispose();
			posTexFlatPS?.Dispose();
			posTexInputLayout?.Dispose();
			posColorLitVS?.Dispose();
			posColorLitPS?.Dispose();
			posColorLitFlatPS?.Dispose();
			posColorLitInputLayout?.Dispose();
			posTexLitVS?.Dispose();
			posTexLitPS?.Dispose();
			posTexLitFlatPS?.Dispose();
			posTexLitInputLayout?.Dispose();
			foreach (var ds in depthStencilCache.Values) ds?.Dispose();
			depthStencilCache.Clear();
			rasterizerNoCull?.Dispose();
			rasterizerCullBack?.Dispose();
			rasterizerCullFront?.Dispose();
			rasterizerScissor?.Dispose();
			foreach (var rs in rasterizerCache.Values) rs?.Dispose();
			rasterizerCache.Clear();
			defaultSampler?.Dispose();
			depthStencilView?.Dispose();
			depthStencilBuffer?.Dispose();
			renderTargetView?.Dispose();
			currentBackBuffer?.Dispose();
		}

		// --- Helper classes ---

		private class ImmediateModeData
		{
			public byte[] CurrentColor = new byte[] { 255, 255, 255, 255 };
			public BeginMode Mode;
			public List<float> Positions = new List<float>();
			public List<byte> Colors = new List<byte>();
			public List<float> TexCoords = new List<float>();
			public List<float> Normals = new List<float>();
		}

		private class TextureInfo
		{
			public ID3D11Texture2D Texture;
			public ID3D11ShaderResourceView ShaderResourceView;
			public int Width;
			public int Height;
			public bool Clamp;
			public bool MagFilterLinear = true;
			public bool MinFilterLinear = true;
			public ID3D11SamplerState Sampler;
			public Format D3DFormat;
			public int ExpectedMipCount;
			public List<(byte[] pixels, int width, int height)> PendingMipData;
		}

		private class ShaderProgramInfo : IDisposable
		{
			public int VertexShaderId;
			public int FragmentShaderId;
			public ID3D11VertexShader VertexShader;
			public ID3D11PixelShader PixelShader;
			public Dictionary<string, int> UniformLocations = new Dictionary<string, int>();
			public float[] Uniforms;
			public int UniformBufferSize;
			public ID3D11Buffer ConstantBuffer;
			public bool UniformsDirty = false;

			public void Dispose()
			{
				VertexShader?.Dispose();
				PixelShader?.Dispose();
				ConstantBuffer?.Dispose();
			}
		}

		private class ShaderInfo
		{
			public int Type;
			public string Source;
			public byte[] ByteCode;
			public string CompileErrors;
		}

		private class DisplayList
		{
			public List<DisplayListEntry> Entries = new List<DisplayListEntry>();
		}

		private class DisplayListEntry
		{
			public BeginMode Mode;
			public List<float> Positions;
			public List<byte> Colors;
			public List<float> TexCoords;
		}

		// ---- 3x supersampled anti-aliasing ----
		// 3x3 (9x) supersampled anti-aliasing. The whole frame is rendered into an
		// off-screen target at 3x the backbuffer resolution, then downsampled to the
		// backbuffer with a 9-tap box filter — one render pass, immediate result.
		// This replaced the temporal 16x Halton-jitter accumulator: no multi-frame
		// convergence, no scene-change fingerprinting, the same quality every frame.
		// Full-frame capture swaps renderTargetView/depthStencilView so ALL rendering
		// (scene pipeline AND GL immediate mode gizmos/lines/controls) goes to the
		// supersample target.

		/// <summary>
		/// Linear supersample factor: the capture target is 3x the backbuffer in
		/// each dimension, so every screen pixel averages a 3x3 block of samples.
		/// </summary>
		public const int SupersampleScale = 3;

		// Full-frame capture: off-screen target at SupersampleScale times the
		// backbuffer size that receives ALL rendering during capture.
		private ColorTextureTarget sampleFrameTarget;
		private ID3D11Texture2D sampleFrameDepthTexture;
		private ID3D11DepthStencilView sampleFrameDepthView;
		private ID3D11RenderTargetView savedMainRTV;
		private ID3D11DepthStencilView savedMainDSV;
		private int savedRenderTargetHeight;

		private ID3D11PixelShader downsamplePS;
		private ID3D11Buffer downsampleBuffer;

		// The device-pixel scale between logical GL/backbuffer coordinates and the
		// active default render target. 1 normally; SupersampleScale while a
		// full-frame capture is redirecting rendering to the 3x sample target.
		// Applied wherever logical coordinates cross into D3D device coordinates
		// (viewport, scissor, scene-effect target sizes, pixel-space shader widths).
		private int supersampleScale = 1;

		/// <summary>
		/// Redirects all subsequent rendering to the off-screen supersample target by
		/// swapping the renderTargetView and depthStencilView fields. The target is
		/// SupersampleScale times the backbuffer size; Viewport/Scissor and the scene
		/// pipeline scale all logical coordinates by <see cref="supersampleScale"/> so
		/// GL→D3D coordinate math lands on the same relative positions.
		/// </summary>
		public void BeginFullFrameCapture(Agg.RectangleDouble viewport)
		{
			if (currentBackBuffer == null)
			{
				return;
			}

			int width = (int)currentBackBuffer.Description.Width * SupersampleScale;
			int height = (int)currentBackBuffer.Description.Height * SupersampleScale;

			sampleFrameTarget = EnsureColorTarget(sampleFrameTarget, width, height, Format.R8G8B8A8_UNorm);
			EnsureSampleFrameDepth(width, height);

			// Save the real render targets
			savedMainRTV = renderTargetView;
			savedMainDSV = depthStencilView;
			savedRenderTargetHeight = renderTargetHeight;

			// Swap to the supersample target — all rendering now goes here
			renderTargetView = sampleFrameTarget.RenderTargetView;
			depthStencilView = sampleFrameDepthView;
			supersampleScale = SupersampleScale;
			renderTargetHeight = height;

			// Clear to transparent so only the 3D viewport region has content.
			// When blitting back, transparent pixels contribute nothing via alpha blending.
			context.ClearRenderTargetView(renderTargetView, new Color4(0, 0, 0, 0));
			context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// Re-apply viewport and scissor so they pick up the new scale
			Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
			if (scissorEnabled)
			{
				Scissor(scissorX, scissorY, scissorWidth, scissorHeight);
			}
		}

		/// <summary>
		/// Restores the original render targets after full-frame capture.
		/// </summary>
		public void EndFullFrameCapture()
		{
			if (savedMainRTV == null)
			{
				return;
			}

			renderTargetView = savedMainRTV;
			depthStencilView = savedMainDSV;
			savedMainRTV = null;
			savedMainDSV = null;
			supersampleScale = 1;
			renderTargetHeight = savedRenderTargetHeight;

			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// Re-apply viewport and scissor at the restored 1x scale
			Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
			if (scissorEnabled)
			{
				Scissor(scissorX, scissorY, scissorWidth, scissorHeight);
			}
		}

		/// <summary>
		/// Box-downsamples the 3x supersample target onto the backbuffer with a
		/// 9-tap filter. Call after EndFullFrameCapture — this completes the frame.
		/// </summary>
		public void DownsampleAndBlitFullFrame()
		{
			if (sampleFrameTarget == null)
			{
				return;
			}

			EnsureDownsampleResources();

			// Upload the source texel size for the 9-tap box filter
			var mapped = context.Map(downsampleBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				ptr[0] = 1.0f / sampleFrameTarget.Width;
				ptr[1] = 1.0f / sampleFrameTarget.Height;
				ptr[2] = 0;
				ptr[3] = 0;
			}

			context.Unmap(downsampleBuffer, 0);

			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// The supersample texture covers the whole backbuffer (scaled 3x) with the
			// 3D content in the viewport region and transparent (0,0,0,0) everywhere
			// else. Blit with full-backbuffer viewport and alpha blending — transparent
			// pixels outside the 3D viewport contribute nothing to the backbuffer.
			int bbWidth = (int)(currentBackBuffer?.Description.Width ?? (uint)renderTargetHeight);
			int bbHeight = (int)(currentBackBuffer?.Description.Height ?? (uint)renderTargetHeight);
			context.RSSetViewport(new Viewport(bbWidth, bbHeight));

			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(downsamplePS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetConstantBuffer(2, downsampleBuffer);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			// Use One/OneMinusSrcAlpha (premultiplied alpha blending) because the
			// captured content has premultiplied RGB (from SrcAlpha blending when
			// the scene was rendered to the sample target). Using SrcAlpha here would
			// double-premultiply, making semi-transparent content like the bed too dark.
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			context.PSSetShaderResource(0, sampleFrameTarget.ShaderResourceView);
			context.Draw(3, 0);
			context.PSSetShaderResource(0, null);

			// Invalidate GL state tracking — we set D3D blend/depth/rasterizer state
			// directly above, bypassing the GL abstraction. Without this, the next
			// ApplyRenderState() may skip re-applying the correct state because
			// lastApplied* still points to the pre-blit values.
			lastAppliedBlendState = null;
			lastAppliedDepthStencilState = null;
			lastAppliedRasterizerState = null;
			renderStateDirty = true;
		}

		private void EnsureDownsampleResources()
		{
			if (downsamplePS != null)
			{
				return;
			}

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] downsamplePsByteCode = Vortice.D3DCompiler.Compiler.Compile(
				postProcessHlsl, "Downsample3x3PS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			downsamplePS = device.CreatePixelShader(downsamplePsByteCode);

			downsampleBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 16, // one float4: DownsampleSettings
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});
		}

		private void EnsureSampleFrameDepth(int width, int height)
		{
			if (sampleFrameDepthTexture != null)
			{
				var desc = sampleFrameDepthTexture.Description;
				if ((int)desc.Width == width && (int)desc.Height == height)
				{
					return;
				}

				sampleFrameDepthView?.Dispose();
				sampleFrameDepthTexture.Dispose();
			}

			sampleFrameDepthTexture = device.CreateTexture2D(new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.D24_UNorm_S8_UInt,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil,
			});
			sampleFrameDepthView = device.CreateDepthStencilView(sampleFrameDepthTexture);
		}

		private void DisposeSupersampleResources()
		{
			downsamplePS?.Dispose();
			downsamplePS = null;
			downsampleBuffer?.Dispose();
			downsampleBuffer = null;
			sampleFrameTarget?.Dispose();
			sampleFrameTarget = null;
			sampleFrameDepthView?.Dispose();
			sampleFrameDepthView = null;
			sampleFrameDepthTexture?.Dispose();
			sampleFrameDepthTexture = null;
		}

		// ---- INativeSceneRenderer: queuing and drawing scene meshes ----
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

			// When depth test is disabled, the caller wants this rendered as an
			// always-visible overlay (e.g., 3D control ghost pass with alpha < 255).
			bool depthTestEnabled = enableCapState.TryGetValue((int)EnableCap.DepthTest, out var d) && d;
			if (depthTestEnabled)
			{
				QueueSceneCommand(command);
			}
			else
			{
				queuedOverlayCommands.Add(command);
			}

			return true;
		}

		public bool TryRender(BedRenderCommand command)
		{
			if (activeSceneRenderContext == null
				|| command?.Mesh == null
				|| command.TopBaseTexture == null)
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

			var glMeshPlugin = MeshTrianglePlugin.Get(OwnerGl, command.Mesh);
			foreach (var subMesh in glMeshPlugin.subMeshs)
			{
				bool useTexture = subMesh.texture != null;
				if (useTexture)
				{
					var glPlugin = ImageTexturePlugin.GetImageTexturePlugin(OwnerGl, subMesh.texture, true);
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

		// ---- The scene effect pipeline the queued commands are rendered through ----
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

			// During full-frame supersample capture the scene pipeline renders at
			// supersampleScale times the logical viewport so its output matches the
			// resolution of the capture target it composites into.
			int width = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Width)) * supersampleScale;
			int height = Math.Max(1, (int)Math.Ceiling(activeSceneRenderContext.Viewport.Height)) * supersampleScale;

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

			var glMeshPlugin = MeshTrianglePlugin.Get(OwnerGl, command.Mesh);
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

			var glMeshPlugin = MeshTrianglePlugin.Get(OwnerGl, command.Mesh);
			var sceneShaderData = SceneEdgeShaderDataPlugin.Get(OwnerGl, command.Mesh, command.RenderType);
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
					var texturePlugin = ImageTexturePlugin.GetImageTexturePlugin(OwnerGl, subMesh.texture, true);
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

			// Callers pass the logical viewport size; the scene targets (and SV_POSITION
			// pixel coordinates the shaders divide by this resolution) are scaled by
			// supersampleScale during full-frame capture. The wireframe width is in
			// device pixels, so scale it to keep the same on-screen thickness.
			values[12] = width * supersampleScale;
			values[13] = height * supersampleScale;
			values[14] = SceneRenderModeUtilities.DefaultWireframeWidth * supersampleScale;
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

			// Outline width is in target pixels: scale by supersampleScale so it
			// downsamples to the same ~2 screen pixels during full-frame capture.
			values[0] = 2.0f * supersampleScale;
			values[1] = 0.35f;
			values[2] = width * supersampleScale;
			values[3] = height * supersampleScale;
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
			// renderTargetHeight is in device pixels (scaled during supersample
			// capture); the context viewport is logical, so pass the scale along.
			var viewport = SceneViewportUtilities.CreateDefaultFramebufferViewport(
				activeSceneRenderContext.Viewport, renderTargetHeight, supersampleScale);
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

			// The planner's plan holds the same commands, so clearing only the queues left the frame's
			// meshes rooted through this (process-lifetime) backend until some later frame rebuilt the
			// plan - see NativeSceneRenderPlanner.ReleasePlan.
			renderPlanner.ReleasePlan();
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
