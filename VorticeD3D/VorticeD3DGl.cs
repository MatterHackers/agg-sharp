/*
Copyright (c) 2025, Lars Brubaker
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
using System.Reflection;
using System.Runtime.InteropServices;
using MatterHackers.Agg;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MatterHackers.RenderOpenGl
{
	public class VorticeD3DGl : IOpenGL
	{
		private ID3D11Device device;
		private ID3D11DeviceContext context;
		private IDXGISwapChain swapChain;
		private ID3D11RenderTargetView renderTargetView;
		private ID3D11Texture2D currentBackBuffer;
		private ID3D11DepthStencilView depthStencilView;
		private ID3D11Texture2D depthStencilBuffer;

		// Shaders for position+color rendering
		private ID3D11VertexShader posColorVS;
		private ID3D11PixelShader posColorPS;
		private ID3D11InputLayout posColorInputLayout;

		// Shaders for position+texture rendering
		private ID3D11VertexShader posTexVS;
		private ID3D11PixelShader posTexPS;
		private ID3D11InputLayout posTexInputLayout;

		// Constant buffer for transforms
		private ID3D11Buffer transformBuffer;

		// Dynamic vertex buffer for immediate mode
		private ID3D11Buffer dynamicVertexBuffer;
		private const int MaxVertices = 65536;

		// Blend states
		private ID3D11BlendState blendStateEnabled;
		private ID3D11BlendState blendStateDisabled;

		// Depth stencil states
		private ID3D11DepthStencilState depthTestEnabled;
		private ID3D11DepthStencilState depthTestDisabled;

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
		private bool scissorEnabled = false;
		private Color4 clearColor = new Color4(0, 0, 0, 1);

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
		private int currentBoundTexture = 0;
		private bool texture2DEnabled = false;

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

		// VAO management
		private int nextVaoId = 1;

		// Viewport
		private int viewportX, viewportY, viewportWidth, viewportHeight;

		public bool GlHasBufferObjects => true;

		public VorticeD3DGl()
		{
		}

		public void Initialize(ID3D11Device device, ID3D11DeviceContext context, IDXGISwapChain swapChain)
		{
			this.device = device;
			this.context = context;
			this.swapChain = swapChain;

			CreateRenderTarget();
			CreateShaders();
			CreateStates();
			CreateDynamicVertexBuffer();
			CreateTransformBuffer();
		}

		private void CreateRenderTarget()
		{
			currentBackBuffer?.Dispose();
			currentBackBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
			renderTargetView = device.CreateRenderTargetView(currentBackBuffer);

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
			currentBackBuffer?.Dispose();
			currentBackBuffer = null;
			depthStencilView?.Dispose();
			depthStencilBuffer?.Dispose();

			swapChain.ResizeBuffers(0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None);

			CreateRenderTarget();
		}

		private void CreateShaders()
		{
			// Position+Color shader
			{
				string hlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.PositionColor.hlsl");
				byte[] vsByteCode = Compiler.Compile(hlsl, "VS", "PositionColor.hlsl", "vs_5_0").ToArray();
				byte[] psByteCode = Compiler.Compile(hlsl, "PS", "PositionColor.hlsl", "ps_5_0").ToArray();

				posColorVS = device.CreateVertexShader(vsByteCode);
				posColorPS = device.CreatePixelShader(psByteCode);

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

				posTexVS = device.CreateVertexShader(vsByteCode);
				posTexPS = device.CreatePixelShader(psByteCode);

				var inputElements = new[]
				{
					new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
					new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 20, 0),
				};

				posTexInputLayout = device.CreateInputLayout(inputElements, vsByteCode);
			}
		}

		private void CreateStates()
		{
			// Blend states
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
				blendStateEnabled = device.CreateBlendState(desc);

				desc.RenderTarget[0].BlendEnable = false;
				blendStateDisabled = device.CreateBlendState(desc);
			}

			// Depth stencil states
			{
				depthTestEnabled = device.CreateDepthStencilState(new DepthStencilDescription
				{
					DepthEnable = true,
					DepthWriteMask = DepthWriteMask.All,
					DepthFunc = ComparisonFunction.Less,
				});

				depthTestDisabled = device.CreateDepthStencilState(new DepthStencilDescription
				{
					DepthEnable = false,
					DepthWriteMask = DepthWriteMask.All,
				});
			}

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
			context.OMSetDepthStencilState(depthTestDisabled);
			context.OMSetBlendState(blendStateDisabled);
		}

		private void CreateDynamicVertexBuffer()
		{
			int vertexSize = 7 * sizeof(float); // position(3) + color(4)
			var desc = new BufferDescription
			{
				ByteWidth = (uint)(MaxVertices * vertexSize),
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.VertexBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			};
			dynamicVertexBuffer = device.CreateBuffer(desc);
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
			var mv = modelViewStack.Peek();
			// Apply Z correction: map OpenGL clip-space Z [-1,1] to D3D11 [0,1]
			// Post-multiply projection by the Z correction matrix:
			// z_new = z_old * 0.5 + w_old * 0.5, w_new = w_old
			// This modifies column 2 of each row
			var p = projectionStack.Peek();
			var proj = new Matrix4X4(
				new Vector4(p.Row0.X, p.Row0.Y, p.Row0.Z * 0.5 + p.Row0.W * 0.5, p.Row0.W),
				new Vector4(p.Row1.X, p.Row1.Y, p.Row1.Z * 0.5 + p.Row1.W * 0.5, p.Row1.W),
				new Vector4(p.Row2.X, p.Row2.Y, p.Row2.Z * 0.5 + p.Row2.W * 0.5, p.Row2.W),
				new Vector4(p.Row3.X, p.Row3.Y, p.Row3.Z * 0.5 + p.Row3.W * 0.5, p.Row3.W));

			// Log the first time we see a non-ortho projection (perspective)
			if (!diagLoggedProjection && Math.Abs(p.Row2.W) > 0.5)
			{
				diagLoggedProjection = true;
				DiagLog($"Perspective projection BEFORE correction: Row2=[{p.Row2.X:F3},{p.Row2.Y:F3},{p.Row2.Z:F3},{p.Row2.W:F3}] Row3=[{p.Row3.X:F3},{p.Row3.Y:F3},{p.Row3.Z:F3},{p.Row3.W:F3}]");
				DiagLog($"Perspective projection AFTER  correction: Row2=[{proj.Row2.X:F3},{proj.Row2.Y:F3},{proj.Row2.Z:F3},{proj.Row2.W:F3}] Row3=[{proj.Row3.X:F3},{proj.Row3.Y:F3},{proj.Row3.Z:F3},{proj.Row3.W:F3}]");
			}

			var mapped = context.Map(transformBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				WriteMatrix(ptr, mv);
				WriteMatrix(ptr + 16, proj);
			}
			context.Unmap(transformBuffer, 0);
		}

		private bool diagLoggedProjection;

		private static unsafe void WriteMatrix(float* dest, Matrix4X4 m)
		{
			float[] arr = m.GetAsFloatArray();
			for (int i = 0; i < 16; i++)
				dest[i] = arr[i];
		}

		private void FlushImmediateMode()
		{
			int vertexCount = immediateData.Positions.Count / 3;
			if (vertexCount == 0) return;

			bool hasTexCoords = immediateData.TexCoords.Count > 0 && texture2DEnabled && currentBoundTexture != 0;

			if (hasTexCoords)
			{
				FlushTexturedVertices(vertexCount);
			}
			else
			{
				FlushColoredVertices(vertexCount);
			}
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
						int ci = (offset + i) * 4;

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
				context.VSSetShader(posColorVS);
				context.PSSetShader(posColorPS);
				context.VSSetConstantBuffer(0, transformBuffer);

				ApplyRenderState();

				context.Draw((uint)count, 0);

				offset += count;
			}
		}

		private void FlushTexturedVertices(int vertexCount)
		{
			int stride = 9 * sizeof(float); // pos(3) + texcoord(2) + color(4)

			// Create or reuse a texture vertex buffer
			var desc = new BufferDescription
			{
				ByteWidth = (uint)(vertexCount * stride),
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.VertexBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			};

			using var texVertexBuffer = device.CreateBuffer(desc);

			var mapped = context.Map(texVertexBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				for (int i = 0; i < vertexCount; i++)
				{
					int vi = i * 3;
					int ti = i * 2;
					int ci = i * 4;

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
			context.Unmap(texVertexBuffer, 0);

			UpdateTransformBuffer();

			context.IASetInputLayout(posTexInputLayout);
			context.IASetVertexBuffer(0, texVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(immediateData.Mode));
			context.VSSetShader(posTexVS);
			context.PSSetShader(posTexPS);
			context.VSSetConstantBuffer(0, transformBuffer);

			if (textures.TryGetValue(currentBoundTexture, out var texInfo) && texInfo.ShaderResourceView != null)
			{
				context.PSSetShaderResource(0, texInfo.ShaderResourceView);
				context.PSSetSampler(0, texInfo.Sampler ?? defaultSampler);
			}

			ApplyRenderState();

			context.Draw((uint)vertexCount, 0);
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

		private void ApplyRenderState()
		{
			bool blendEnabled = enableCapState.TryGetValue((int)EnableCap.Blend, out var b) && b;
			context.OMSetBlendState(blendEnabled ? blendStateEnabled : blendStateDisabled);

			bool depthEnabled = enableCapState.TryGetValue((int)EnableCap.DepthTest, out var d) && d;
			context.OMSetDepthStencilState(depthEnabled ? depthTestEnabled : depthTestDisabled);

			bool cullEnabled = enableCapState.TryGetValue((int)EnableCap.CullFace, out var c) && c;
			if (scissorEnabled)
				context.RSSetState(rasterizerScissor);
			else if (cullEnabled)
				context.RSSetState(rasterizerCullBack);
			else
				context.RSSetState(rasterizerNoCull);
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

		// --- IOpenGL implementation ---

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

			immediateData.Colors.Add(ImmediateModeData.CurrentColor[0]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[1]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[2]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[3]);
		}

		public void Vertex3(double x, double y, double z)
		{
			immediateData.Positions.Add((float)x);
			immediateData.Positions.Add((float)y);
			immediateData.Positions.Add((float)z);

			immediateData.Colors.Add(ImmediateModeData.CurrentColor[0]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[1]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[2]);
			immediateData.Colors.Add(ImmediateModeData.CurrentColor[3]);
		}

		public void Color4(byte red, byte green, byte blue, byte alpha)
		{
			ImmediateModeData.CurrentColor[0] = red;
			ImmediateModeData.CurrentColor[1] = green;
			ImmediateModeData.CurrentColor[2] = blue;
			ImmediateModeData.CurrentColor[3] = alpha;
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

		private static readonly string diagPath = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "d3d11_diag.log");
		private int drawCallNumber;
		private int litDrawCallsLogged;

		private void DiagLog(string msg)
		{
			try { System.IO.File.AppendAllText(diagPath, msg + "\n"); } catch { }
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

			drawCallNumber++;

			if (hasVertexPointer && vertexPointerData.pointer != IntPtr.Zero)
			{
				bool useTexture = hasTexCoordPointer && texture2DEnabled && currentBoundTexture != 0
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
				r = ImmediateModeData.CurrentColor[0] / 255f;
				g = ImmediateModeData.CurrentColor[1] / 255f;
				b = ImmediateModeData.CurrentColor[2] / 255f;
				a = ImmediateModeData.CurrentColor[3] / 255f;
			}
		}

		private void ApplyLighting(ref float r, ref float g, ref float b, float nx, float ny, float nz, bool light0On, bool light1On)
		{
			// Simple Lambert lighting on CPU
			float lr = 0, lg = 0, lb = 0;

			void AddLight(LightData light)
			{
				float dx = light.Position[0], dy = light.Position[1], dz = light.Position[2];
				float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
				if (len > 0) { dx /= len; dy /= len; dz /= len; }

				float ndotl = nx * dx + ny * dy + nz * dz;
				if (ndotl < 0) ndotl = 0;

				lr += light.Ambient[0] + light.Diffuse[0] * ndotl;
				lg += light.Ambient[1] + light.Diffuse[1] * ndotl;
				lb += light.Ambient[2] + light.Diffuse[2] * ndotl;
			}

			if (light0On) AddLight(lights[0]);
			if (light1On) AddLight(lights[1]);

			r = Math.Min(1.0f, r * lr);
			g = Math.Min(1.0f, g * lg);
			b = Math.Min(1.0f, b * lb);
		}

		private bool diagLoggedVertexTransform;

		private void DrawArraysColored(BeginMode mode, int first, int totalCount, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			int stride = 7 * sizeof(float);

			UpdateTransformBuffer();

			if (!diagLoggedVertexTransform && lightingOn && totalCount > 10)
			{
				diagLoggedVertexTransform = true;
				var mv = modelViewStack.Peek();
				var p = projectionStack.Peek();
				DiagLog($"DrawArraysColored DIAG: count={totalCount}, first={first}");
				DiagLog($"  MV Row0=[{mv.Row0.X:F3},{mv.Row0.Y:F3},{mv.Row0.Z:F3},{mv.Row0.W:F3}]");
				DiagLog($"  MV Row3=[{mv.Row3.X:F3},{mv.Row3.Y:F3},{mv.Row3.Z:F3},{mv.Row3.W:F3}]");
				unsafe
				{
					float* srcVert = (float*)vertexPointerData.pointer;
					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					for (int vi = 0; vi < Math.Min(3, totalCount); vi++)
					{
						int idx = (first + vi) * vertStride;
						double vx = srcVert[idx], vy = srcVert[idx + 1], vz = srcVert[idx + 2];
						double ex = vx * mv.Row0.X + vy * mv.Row1.X + vz * mv.Row2.X + mv.Row3.X;
						double ey = vx * mv.Row0.Y + vy * mv.Row1.Y + vz * mv.Row2.Y + mv.Row3.Y;
						double ez = vx * mv.Row0.Z + vy * mv.Row1.Z + vz * mv.Row2.Z + mv.Row3.Z;
						DiagLog($"  V{vi}: model=[{vx:F3},{vy:F3},{vz:F3}] eye=[{ex:F3},{ey:F3},{ez:F3}]");
					}
				}
				bool depthOn = enableCapState.TryGetValue((int)EnableCap.DepthTest, out var dep) && dep;
				bool blendOn = enableCapState.TryGetValue((int)EnableCap.Blend, out var bl) && bl;
				DiagLog($"  depth={depthOn} blend={blendOn} depthMask={depthMaskEnabled}");
			}

			context.IASetInputLayout(posColorInputLayout);
			context.IASetVertexBuffer(0, dynamicVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetShader(posColorVS);
			context.PSSetShader(posColorPS);
			context.VSSetConstantBuffer(0, transformBuffer);
			ApplyRenderState();

			int offset = 0;
			while (offset < totalCount)
			{
				int batchCount = Math.Min(totalCount - offset, MaxVertices);

				var mapped = context.Map(dynamicVertexBuffer, MapMode.WriteDiscard);
				unsafe
				{
					float* dest = (float*)mapped.DataPointer;
					float* srcVert = (float*)vertexPointerData.pointer;
					float* srcNormal = (hasNormalPointer && normalPointerData.pointer != IntPtr.Zero)
						? (float*)normalPointerData.pointer : null;

					int vertStride = vertexPointerData.stride > 0 ? vertexPointerData.stride / sizeof(float) : vertexPointerData.size;
					int normStride = normalPointerData.stride > 0 ? normalPointerData.stride / sizeof(float) : 3;

					for (int i = 0; i < batchCount; i++)
					{
						int globalIdx = first + offset + i;
						int srcIdx = globalIdx * vertStride;
						dest[i * 7 + 0] = srcVert[srcIdx];
						dest[i * 7 + 1] = srcVert[srcIdx + 1];
						dest[i * 7 + 2] = vertexPointerData.size >= 3 ? srcVert[srcIdx + 2] : 0;

						GetVertexColor(i, globalIdx, hasColorPointer, out float r, out float g, out float b, out float a);

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
				context.Unmap(dynamicVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		private void DrawArraysTextured(BeginMode mode, int first, int totalCount, bool hasColorPointer, bool hasNormalPointer, bool lightingOn, bool light0On, bool light1On)
		{
			int stride = 9 * sizeof(float); // pos(3) + texcoord(2) + color(4)

			int bufferSize = Math.Min(totalCount, MaxVertices);
			var desc = new BufferDescription
			{
				ByteWidth = (uint)(bufferSize * stride),
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.VertexBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			};

			using var texVertexBuffer = device.CreateBuffer(desc);

			UpdateTransformBuffer();
			context.IASetInputLayout(posTexInputLayout);
			context.IASetVertexBuffer(0, texVertexBuffer, (uint)stride);
			context.IASetPrimitiveTopology(GetTopology(mode));
			context.VSSetShader(posTexVS);
			context.PSSetShader(posTexPS);
			context.VSSetConstantBuffer(0, transformBuffer);

			if (textures.TryGetValue(currentBoundTexture, out var texInfo) && texInfo.ShaderResourceView != null)
			{
				context.PSSetShaderResource(0, texInfo.ShaderResourceView);
				context.PSSetSampler(0, texInfo.Sampler ?? defaultSampler);
			}

			ApplyRenderState();

			int offset = 0;
			while (offset < totalCount)
			{
				int batchCount = Math.Min(totalCount - offset, MaxVertices);

				var mapped = context.Map(texVertexBuffer, MapMode.WriteDiscard);
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

						if (lightingOn && srcNormal != null)
						{
							int ni = globalIdx * normStride;
							ApplyLighting(ref r, ref g, ref b, srcNormal[ni], srcNormal[ni + 1], srcNormal[ni + 2], light0On, light1On);
						}

						dest[i * 9 + 5] = r;
						dest[i * 9 + 6] = g;
						dest[i * 9 + 7] = b;
						dest[i * 9 + 8] = a;
					}
				}
				context.Unmap(texVertexBuffer, 0);

				context.Draw((uint)batchCount, 0);

				offset += batchCount;
			}
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
			// Simplified: treat as DrawArrays for now
		}

		public void DrawElements(int mode, int count, int elementType, IntPtr indices)
		{
			// Simplified: will be implemented for VBO path
		}

		// --- State management ---

		public void Enable(int cap)
		{
			enableCapState[cap] = true;
			if (cap == (int)EnableCap.Texture2D) texture2DEnabled = true;
			if (cap == (int)EnableCap.ScissorTest) scissorEnabled = true;
		}

		public void Disable(int cap)
		{
			enableCapState[cap] = false;
			if (cap == (int)EnableCap.Texture2D) texture2DEnabled = false;
			if (cap == (int)EnableCap.ScissorTest) scissorEnabled = false;
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
			arrayCapState[arrayCap] = true;
		}

		public void DisableClientState(ArrayCap array)
		{
			arrayCapState[array] = false;
		}

		private Dictionary<(int src, int dst), ID3D11BlendState> blendStateCache = new Dictionary<(int, int), ID3D11BlendState>();

		public void BlendFunc(int sfactor, int dfactor)
		{
			blendSrcFactor = sfactor;
			blendDstFactor = dfactor;

			var key = (sfactor, dfactor);
			if (!blendStateCache.ContainsKey(key))
			{
				var desc = new BlendDescription();
				desc.RenderTarget[0] = new RenderTargetBlendDescription
				{
					BlendEnable = true,
					SourceBlend = MapBlendFactor(sfactor),
					DestinationBlend = MapBlendFactor(dfactor),
					BlendOperation = BlendOperation.Add,
					SourceBlendAlpha = Blend.One,
					DestinationBlendAlpha = Blend.InverseSourceAlpha,
					BlendOperationAlpha = BlendOperation.Add,
					RenderTargetWriteMask = ColorWriteEnable.All,
				};
				blendStateCache[key] = device.CreateBlendState(desc);
			}

			blendStateEnabled = blendStateCache[key];
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

		public void DepthFunc(int func) { }

		public void DepthMask(bool flag)
		{
			depthMaskEnabled = flag;
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha) { }

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode) { }

		public void CullFace(CullFaceMode mode) { }

		public void FrontFace(FrontFaceDirection mode) { }

		public void ShadeModel(ShadingModel model) { }

		public void PolygonOffset(float factor, float units) { }

		public void Light(LightName light, LightParameter pname, float[] param)
		{
			int idx = light == LightName.Light0 ? 0 : 1;
			if (idx >= lights.Length || param == null) return;

			switch (pname)
			{
				case LightParameter.Ambient: Array.Copy(param, lights[idx].Ambient, Math.Min(param.Length, 4)); break;
				case LightParameter.Diffuse: Array.Copy(param, lights[idx].Diffuse, Math.Min(param.Length, 4)); break;
				case LightParameter.Specular: Array.Copy(param, lights[idx].Specular, Math.Min(param.Length, 4)); break;
				case LightParameter.Position: Array.Copy(param, lights[idx].Position, Math.Min(param.Length, 4)); break;
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
		}

		public void MultMatrix(float[] m)
		{
			var matrix = new Matrix4X4(m);
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(modelViewStack.Pop() * matrix);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * matrix);
			}
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
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			// Standard OpenGL orthographic projection (Z maps to [-1,1]).
			// Z correction to D3D11 [0,1] is applied in UpdateTransformBuffer.
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
				modelViewStack.Push(modelViewStack.Pop() * ortho);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * ortho);
			}
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
				modelViewStack.Push(modelViewStack.Pop() * translation);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * translation);
			}
		}

		public void Rotate(double angle, double x, double y, double z)
		{
			double radians = VectorMath.MathHelper.DegreesToRadians(angle);
			var axis = new Vector3(x, y, z);
			var rotation = Matrix4X4.CreateRotation(axis, radians);

			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(modelViewStack.Pop() * rotation);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * rotation);
			}
		}

		public void Scale(double x, double y, double z)
		{
			var scale = Matrix4X4.CreateScale(x, y, z);
			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(modelViewStack.Pop() * scale);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * scale);
			}
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
				context.ClearRenderTargetView(renderTargetView, clearColor);
			}
			if ((mask & 0x00000100) != 0) // GL_DEPTH_BUFFER_BIT
			{
				context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
			}
		}

		public void ClearDepth(double depth) { }

		public void ClearColor(double r, double g, double b, double a)
		{
			clearColor = new Color4((float)r, (float)g, (float)b, (float)a);
		}

		private (int x, int y, int w, int h) currentViewport;
		private int viewportChangeCount;

		public void Viewport(int x, int y, int width, int height)
		{
			viewportX = x;
			viewportY = y;
			viewportWidth = width;
			viewportHeight = height;
			currentViewport = (x, y, width, height);
			viewportChangeCount++;

			if (viewportChangeCount <= 20)
			{
				DiagLog($"Viewport #{viewportChangeCount}: x={x} y={y} w={width} h={height}");
			}

			context.RSSetViewport(x, y, width, height);
		}

		public void Scissor(int x, int y, int width, int height)
		{
			context.RSSetScissorRect(x, viewportHeight - y - height, x + width, viewportHeight - y);
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
			currentBoundTexture = texture;
		}

		public void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, byte[] pixels)
		{
			if (currentBoundTexture <= 0 || !textures.ContainsKey(currentBoundTexture)) return;

			var texInfo = textures[currentBoundTexture];
			texInfo.ShaderResourceView?.Dispose();
			texInfo.Texture?.Dispose();

			// Determine the correct D3D11 format based on the OpenGL pixel format
			// 0x1908 = GL_RGBA, 0x80E1 = GL_BGRA
			var d3dFormat = format == 0x80E1 ? Format.B8G8R8A8_UNorm : Format.R8G8B8A8_UNorm;

			var texDesc = new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = d3dFormat,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.ShaderResource,
			};

			if (pixels != null)
			{
				unsafe
				{
					fixed (byte* ptr = pixels)
					{
						var initData = new SubresourceData((IntPtr)ptr, (uint)(width * 4));
						texInfo.Texture = device.CreateTexture2D(texDesc, new[] { initData });
					}
				}
			}
			else
			{
				texInfo.Texture = device.CreateTexture2D(texDesc);
			}

			texInfo.ShaderResourceView = device.CreateShaderResourceView(texInfo.Texture);
			texInfo.Width = width;
			texInfo.Height = height;
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
			if (currentBoundTexture <= 0 || !textures.TryGetValue(currentBoundTexture, out var texInfo)) return;

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
		public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param) { }
		public void ActiveTexture(int texture) { }

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

		public void LinkProgram(int id)
		{
			if (!shaderPrograms.TryGetValue(id, out var prog)) return;

			if (prog.VertexShaderId > 0 && shaderObjects.TryGetValue(prog.VertexShaderId, out var vs) && vs.ByteCode != null)
			{
				prog.VertexShader = device.CreateVertexShader(vs.ByteCode);
			}

			if (prog.FragmentShaderId > 0 && shaderObjects.TryGetValue(prog.FragmentShaderId, out var fs) && fs.ByteCode != null)
			{
				prog.PixelShader = device.CreatePixelShader(fs.ByteCode);
			}
		}

		public void UseProgram(int program)
		{
			currentProgram = program;
			if (program == 0)
			{
				// Restore default shaders
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

		public void DetachShader(int id, int shader) { }

		public int GetUniformLocation(int program, string name)
		{
			// Simple hash-based uniform location
			return name.GetHashCode() & 0x7FFFFFFF;
		}

		public void Uniform1i(int location, int v0) { }
		public void Uniform1f(int location, float v0) { }
		public void UniformMatrix4fv(int location, int count, int transpose, float[] value) { }
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
			if (buffer == 0)
			{
				context.OMSetRenderTargets(renderTargetView, depthStencilView);
			}
		}

		public int GenFramebuffer()
		{
			return nextFramebufferId++;
		}

		public void GenFramebuffers(int v, out int fbo)
		{
			fbo = nextFramebufferId++;
		}

		public void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level) { }

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
		public string GetString(StringName name) => "Vortice Direct3D 11";
		public void Finish() { }

		public void Present()
		{
			swapChain.Present(1, PresentFlags.None);

			// With FlipDiscard, the back buffer changes after Present.
			// Re-acquire the render target view for the new back buffer.
			// Keep a strong reference to the back buffer texture to prevent
			// the RTV from pointing to a released resource.
			renderTargetView?.Dispose();
			currentBackBuffer?.Dispose();
			currentBackBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
			renderTargetView = device.CreateRenderTargetView(currentBackBuffer);
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
		}

		public void Dispose()
		{
			foreach (var buf in buffers.Values) buf?.Dispose();
			foreach (var tex in textures.Values)
			{
				tex.ShaderResourceView?.Dispose();
				tex.Texture?.Dispose();
			}

			foreach (var bs in blendStateCache.Values) bs?.Dispose();
			blendStateCache.Clear();

			dynamicVertexBuffer?.Dispose();
			transformBuffer?.Dispose();
			posColorVS?.Dispose();
			posColorPS?.Dispose();
			posColorInputLayout?.Dispose();
			posTexVS?.Dispose();
			posTexPS?.Dispose();
			posTexInputLayout?.Dispose();
			blendStateEnabled?.Dispose();
			blendStateDisabled?.Dispose();
			depthTestEnabled?.Dispose();
			depthTestDisabled?.Dispose();
			rasterizerNoCull?.Dispose();
			rasterizerCullBack?.Dispose();
			rasterizerCullFront?.Dispose();
			rasterizerScissor?.Dispose();
			defaultSampler?.Dispose();
			depthStencilView?.Dispose();
			depthStencilBuffer?.Dispose();
			renderTargetView?.Dispose();
			currentBackBuffer?.Dispose();
		}

		// --- Helper classes ---

		private class ImmediateModeData
		{
			public static byte[] CurrentColor = new byte[] { 255, 255, 255, 255 };
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
		}

		private class ShaderProgramInfo
		{
			public int VertexShaderId;
			public int FragmentShaderId;
			public ID3D11VertexShader VertexShader;
			public ID3D11PixelShader PixelShader;
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
	}
}
