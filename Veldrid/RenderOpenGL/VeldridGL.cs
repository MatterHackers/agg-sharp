/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using RenderOpenGl.VertexFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Veldrid;

namespace MatterHackers.RenderOpenGl
{
	public class VeldridGL : IOpenGL
	{
		public CommandList commandList;

		public GraphicsDevice graphicsDevice;

		public DeviceBuffer indexBuffer;

		public Pipeline pipeline;

		// matrix transforms
		public DeviceBuffer projectionBuffer;

		public VertexPositionColorGL[] quadVertices;

		public DeviceBuffer vertexBuffer;

		private Dictionary<ArrayCap, bool> ArrayCapState = new Dictionary<ArrayCap, bool>()
		{
			[ArrayCap.VertexArray] = false,
			[ArrayCap.NormalArray] = false,
			[ArrayCap.ColorArray] = false,
			[ArrayCap.IndexArray] = false,
			[ArrayCap.TextureCoordArray] = false,
		};

		private Dictionary<int, byte[]> bufferData = new Dictionary<int, byte[]>();

		private int currentArrayBufferIndex = 0;

		private int currentElementArrayBufferIndex = 0;

		private ImediateMode currentImediateData = new ImediateMode();

		private ViewPortData currentViewport = new ViewPortData();

		private Dictionary<EnableCap, bool> EnableCapState = new Dictionary<EnableCap, bool>()
		{
			[EnableCap.Blend] = false,
			[EnableCap.ColorMaterial] = false,
			[EnableCap.CullFace] = false,
			[EnableCap.DepthTest] = false,
			[EnableCap.Light0] = false,
			[EnableCap.Light1] = false,
			[EnableCap.Lighting] = false,
			[EnableCap.Normalize] = false,
			[EnableCap.PolygonOffsetFill] = false,
			[EnableCap.PolygonSmooth] = false,
			[EnableCap.ScissorTest] = false,
			[EnableCap.Texture2D] = false,
		};

		private int genBuffersIndex = 1;

		private bool glHasBufferObjects = true;

		private MatrixMode matrixMode = OpenGl.MatrixMode.Modelview;

		private Stack<Matrix4X4> modelViewStack = new Stack<Matrix4X4>(new Matrix4X4[] { Matrix4X4.Identity });

		private Stack<Matrix4X4> projectionStack = new Stack<Matrix4X4>(new Matrix4X4[] { Matrix4X4.Identity });

		private Stack<AttribMask> pushedAttributStack = new Stack<AttribMask>();

		private Stack<ViewPortData> viewportStack = new Stack<ViewPortData>();

		public VeldridGL()
		{
		}

		public bool GlHasBufferObjects { get { return glHasBufferObjects; } }

		public bool HardwareAvailable { get; set; } = true;

		public void Begin(BeginMode mode)
		{
			currentImediateData.Mode = mode;
		}

		public void BindBuffer(BufferTarget target, int buffer)
		{
		}

		public void BindFramebuffer(int renderBuffer)
		{
		}

		public void BindRenderbuffer(int renderBuffer)
		{
		}

		public void BindTexture(TextureTarget target, int texture)
		{
		}

		BlendingFactorSrc blendingFactorSrc;
		BlendingFactorDest blendingFacterDest;
		public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
		{
			this.blendingFactorSrc = sfactor;
			this.blendingFacterDest = dfactor;
		}

		public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
		{
		}

		public void Clear(ClearBufferMask mask)
		{
		}

		public void ClearDepth(double depth)
		{
		}

		public void Color4(Color color)
		{
			Color4(color.red, color.green, color.blue, color.alpha);
		}

		public void Color4(int red, int green, int blue, int alpha)
		{
			Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
		}

		public void Color4(byte red, byte green, byte blue, byte alpha)
		{
			ImediateMode.currentColor[0] = (byte)red;
			ImediateMode.currentColor[1] = (byte)green;
			ImediateMode.currentColor[2] = (byte)blue;
			ImediateMode.currentColor[3] = (byte)alpha;
		}

		public void ColorMask(bool red, bool green, bool blue, bool alpha)
		{
		}

		public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
		{
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
		{
		}

		public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
		{
		}

		public void CreateResources(GraphicsDevice _graphicsDevice)
		{
			graphicsDevice = _graphicsDevice;

			ResourceFactory resourceFactory = _graphicsDevice.ResourceFactory;

			projectionBuffer = resourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

			quadVertices = new[]
			{
				new VertexPositionColorGL(new System.Numerics.Vector3(-.75f, .75f, 0), RgbaFloat.Red),
				new VertexPositionColorGL(new System.Numerics.Vector3(.75f, .75f, 0), RgbaFloat.Green),
				new VertexPositionColorGL(new System.Numerics.Vector3(-.75f, -.75f, 0), RgbaFloat.Blue),
				new VertexPositionColorGL(new System.Numerics.Vector3(.75f, -.75f, 0), RgbaFloat.Yellow)
			};

			BufferDescription vbDescription = new BufferDescription(
				4 * VertexPositionColorGL.SizeInBytes,
				BufferUsage.VertexBuffer);
			vertexBuffer = resourceFactory.CreateBuffer(vbDescription);
			_graphicsDevice.UpdateBuffer(vertexBuffer, 0, quadVertices);

			{
				ushort[] quadIndices = { 0, 1, 2, 3 };
				BufferDescription ibDescription = new BufferDescription(
					4 * sizeof(ushort),
					BufferUsage.IndexBuffer);
				indexBuffer = resourceFactory.CreateBuffer(ibDescription);
				_graphicsDevice.UpdateBuffer(indexBuffer, 0, quadIndices);

				VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
					new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
					new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4));

				ShaderSetDescription shaderSetPositionTexture = new ShaderSetDescription(
					new[]
					{
					new VertexLayoutDescription(
						new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
						new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
					},
					new[]
					{
					LoadShader(resourceFactory, "PositionTextureGL", ShaderStages.Vertex, "VS"),
					LoadShader(resourceFactory, "PositionTextureGL", ShaderStages.Fragment, "FS")
					});
			}

			ShaderSetDescription shaderSetPositionColor;
			{
				shaderSetPositionColor = new ShaderSetDescription(
				new[]
				{
					new VertexLayoutDescription(
						new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
						new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4))
				},
				new[]
				{
					LoadShader(resourceFactory, "PositionColorGL", ShaderStages.Vertex, "VS"),
					LoadShader(resourceFactory, "PositionColorGL", ShaderStages.Fragment, "FS")
				});
			}

			// Create pipeline
			GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
			pipelineDescription.BlendState = BlendStateDescription.SingleAlphaBlend;
			pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
				depthTestEnabled: true,
				depthWriteEnabled: true,
				comparisonKind: ComparisonKind.LessEqual);
			pipelineDescription.RasterizerState = new RasterizerStateDescription(
				cullMode: FaceCullMode.Back,
				fillMode: PolygonFillMode.Solid,
				frontFace: Veldrid.FrontFace.Clockwise,
				depthClipEnabled: true,
				scissorTestEnabled: false);
			pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
			pipelineDescription.ShaderSet = shaderSetPositionColor;
			pipelineDescription.Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription;

			pipeline = resourceFactory.CreateGraphicsPipeline(pipelineDescription);

			commandList = resourceFactory.CreateCommandList();
		}

		public void CullFace(CullFaceMode mode)
		{
		}

		public void DeleteBuffers(int n, ref int buffers)
		{
		}

		public void DeleteFramebuffers(int n, ref int frameBuffers)
		{
		}

		public void DeleteRenderbuffers(int n, ref int renderBuffers)
		{
		}

		public void DeleteTextures(int n, ref int textures)
		{
		}

		public void DepthFunc(DepthFunction func)
		{
		}

		public void DepthMask(bool flag)
		{
		}

		public void Disable(EnableCap cap)
		{
			EnableCapState[cap] = false;
		}

		public void DisableClientState(ArrayCap array)
		{
			ArrayCapState[array] = false;
		}

		public void DisableGlBuffers()
		{
			glHasBufferObjects = false;
		}

		public void DisposeResources()
		{
			pipeline?.Dispose();
			commandList?.Dispose();
			vertexBuffer?.Dispose();
			indexBuffer?.Dispose();
		}

		public void DrawArrays(BeginMode mode, int first, int count)
		{
		}

		public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
		{
		}

		public void Enable(EnableCap cap)
		{
			EnableCapState[cap] = true;
		}

		public void EnableClientState(ArrayCap arrayCap)
		{
			ArrayCapState[arrayCap] = true;
		}

		public void End()
		{
			switch (currentImediateData.Mode)
			{
				case BeginMode.Lines:
					{
						GL.EnableClientState(ArrayCap.ColorArray);
						GL.EnableClientState(ArrayCap.VertexArray);

						float[] v = currentImediateData.positions3f.Array;
						byte[] c = currentImediateData.color4b.Array;
						// pin the data, so that GC doesn't move them, while used
						// by native code
						unsafe
						{
							fixed (float* pv = v)
							{
								fixed (byte* pc = c)
								{
									GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
									GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
									GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
								}
							}
						}
						GL.DisableClientState(ArrayCap.VertexArray);
						GL.DisableClientState(ArrayCap.ColorArray);
					}
					break;

				case BeginMode.TriangleFan:
				case BeginMode.Triangles:
				case BeginMode.TriangleStrip:
					{
						GL.EnableClientState(ArrayCap.ColorArray);
						GL.EnableClientState(ArrayCap.VertexArray);
						GL.EnableClientState(ArrayCap.TextureCoordArray);

						float[] v = currentImediateData.positions3f.Array;
						byte[] c = currentImediateData.color4b.Array;
						float[] t = currentImediateData.textureCoords2f.Array;
						// pin the data, so that GC doesn't move them, while used
						// by native code
						unsafe
						{
							fixed (float* pv = v, pt = t)
							{
								fixed (byte* pc = c)
								{
									GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, new IntPtr(pc));
									GL.VertexPointer(currentImediateData.vertexCount, VertexPointerType.Float, 0, new IntPtr(pv));
									GL.TexCoordPointer(2, TexCordPointerType.Float, 0, new IntPtr(pt));
									GL.DrawArrays(currentImediateData.Mode, 0, currentImediateData.positions3f.Count / currentImediateData.vertexCount);
								}
							}
						}
						GL.DisableClientState(ArrayCap.VertexArray);
						GL.DisableClientState(ArrayCap.TextureCoordArray);
						GL.DisableClientState(ArrayCap.ColorArray);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public void Finish()
		{
		}

		public void FramebufferRenderbuffer(int renderBuffer)
		{
		}

		public void FrontFace(FrontFaceDirection mode)
		{
		}

		// start at 1 so we can use 0 as a not initialize tell.
		public void GenBuffers(int n, out int buffers)
		{
			buffers = -1;
		}

		public void GenFramebuffers(int n, out int frameBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenRenderbuffers(int n, out int renderBuffers)
		{
			throw new NotImplementedException();
		}

		public void GenTextures(int n, out int textureHandle)
		{
			textureHandle = -1;
		}

		public ErrorCode GetError()
		{
			throw new NotImplementedException();
		}

		public string GetString(StringName name)
		{
			return "";
		}

		public void Light(LightName light, LightParameter pname, float[] param)
		{
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
		}

		public Shader LoadShader(ShaderStages stage)
		{
			string extension = null;
			switch (graphicsDevice.BackendType)
			{
				case GraphicsBackend.Direct3D11:
					extension = "hlsl.bytes";
					break;

				case GraphicsBackend.Vulkan:
					extension = "spv";
					break;

				case GraphicsBackend.OpenGL:
					extension = "glsl";
					break;

				case GraphicsBackend.Metal:
					extension = "metallib";
					break;

				default: throw new System.InvalidOperationException();
			}

			string entryPoint = stage == ShaderStages.Vertex ? "VS" : "FS";
			string path = Path.Combine(System.AppContext.BaseDirectory, "Shaders", $"{stage.ToString()}.{extension}");
			byte[] shaderBytes = File.ReadAllBytes(path);
			return graphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(stage, shaderBytes, entryPoint));
		}

		public Shader LoadShader(ResourceFactory factory, string set, ShaderStages stage, string entryPoint)
		{
			string name = $"{set}-{stage.ToString().ToLower()}.{GetExtension(factory.BackendType)}";
			return factory.CreateShader(new ShaderDescription(stage, ReadEmbeddedAssetBytes(name), entryPoint));
		}

		public void MatrixMode(MatrixMode mode)
		{
			matrixMode = mode;
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

		public void Normal3(double x, double y, double z)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
		{
		}

		public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
		{
		}

		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			var ortho = Matrix4X4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar);

			if (matrixMode == OpenGl.MatrixMode.Modelview)
			{
				modelViewStack.Push(modelViewStack.Pop() * ortho);
			}
			else
			{
				projectionStack.Push(projectionStack.Pop() * ortho);
			}
		}

		public void PolygonOffset(float factor, float units)
		{
		}

		public void PopAttrib()
		{
		}

		public void PopMatrix()
		{
		}

		public void PushAttrib(AttribMask mask)
		{
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

		public void ReadBuffer()
		{
		}

		public byte[] ReadEmbeddedAssetBytes(string name)
		{
			using (Stream stream = OpenEmbeddedAssetStream(name))
			{
				byte[] bytes = new byte[stream.Length];
				using (MemoryStream ms = new MemoryStream(bytes))
				{
					stream.CopyTo(ms);
					return bytes;
				}
			}
		}

		public void ReadPixels(int x, int y, int width, int height, OpenGl.PixelFormat pixelFormat, PixelType pixelType, byte[] buffer)
		{
		}

		public void Rotate(double angle, double x, double y, double z)
		{
		}

		public void Scale(double x, double y, double z)
		{
		}

		public void Scissor(int x, int y, int width, int height)
		{
		}

		public void ShadeModel(ShadingModel model)
		{
		}

		public void TexCoord2(Vector2 uv)
		{
			TexCoord2(uv.X, uv.Y);
		}

		public void TexCoord2(double x, double y)
		{
		}

		public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
		{
		}

		public void TexImage2D(TextureTarget target, int level,
			PixelInternalFormat internalFormat,
			int width, int height, int border,
			OpenGl.PixelFormat format,
			PixelType type,
			Byte[] pixels)
		{
		}

		public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
		{
		}

		public void Translate(MatterHackers.VectorMath.Vector3 vector)
		{
			if (HardwareAvailable)
			{
				Translate(vector.X, vector.Y, vector.Z);
			}
		}

		public void Translate(double x, double y, double z)
		{
		}

		public void Vertex2(Vector2 position)
		{
			Vertex2(position.X, position.Y);
		}

		public void Vertex2(double x, double y)
		{
			currentImediateData.vertexCount = 2;
			currentImediateData.positions3f.Add((float)x);
			currentImediateData.positions3f.Add((float)y);

			currentImediateData.color4b.add(ImediateMode.currentColor[0]);
			currentImediateData.color4b.add(ImediateMode.currentColor[1]);
			currentImediateData.color4b.add(ImediateMode.currentColor[2]);
			currentImediateData.color4b.add(ImediateMode.currentColor[3]);
		}

		public void Vertex3(Vector3 position)
		{
			Vertex3(position.X, position.Y, position.Z);
		}

		public void Vertex3(double x, double y, double z)
		{
		}

		public void VertexPointer(int size, VertexPointerType type, int stride, float[] pointer)
		{
			unsafe
			{
				fixed (float* pArray = pointer)
				{
					VertexPointer(size, type, stride, new IntPtr(pArray));
				}
			}
		}

		public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
		{
		}

		public void Viewport(int x, int y, int width, int height)
		{
			currentViewport = new ViewPortData(x, y, width, height);
		}

		private string GetExtension(GraphicsBackend backendType)
		{
			bool isMacOS = RuntimeInformation.OSDescription.Contains("Darwin");

			return (backendType == GraphicsBackend.Direct3D11)
				? "hlsl.bytes"
				: (backendType == GraphicsBackend.Vulkan)
					? "450.glsl.spv"
					: (backendType == GraphicsBackend.Metal)
						? isMacOS ? "metallib" : "ios.metallib"
						: (backendType == GraphicsBackend.OpenGL)
							? "330.glsl"
							: "300.glsles";
		}

		private Stream OpenEmbeddedAssetStream(string name) => GetType().Assembly.GetManifestResourceStream(name);

		internal struct ViewPortData
		{
			internal int height;
			internal int width;
			internal int x;
			internal int y;

			public ViewPortData(int x, int y, int width, int height)
			{
				// TODO: Complete member initialization
				this.x = x;
				this.y = y;
				this.width = width;
				this.height = height;
			}
		}

		internal class ImediateMode
		{
			public static byte[] currentColor = new byte[4];
			internal VectorPOD<byte> color4b = new VectorPOD<byte>();
			internal VectorPOD<float> positions3f = new VectorPOD<float>();
			internal VectorPOD<float> textureCoords2f = new VectorPOD<float>();
			internal int vertexCount;
			private BeginMode mode;

			internal BeginMode Mode
			{
				get
				{
					return mode;
				}
				set
				{
					mode = value;
					positions3f.Clear();
					color4b.Clear();
					textureCoords2f.Clear();
				}
			}
		}
	}
}