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

using RenderOpenGl.VertexFormats;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace MatterHackers.RenderOpenGl
{
	public class VeldridGL
	{
		public static VeldridGL Instance = new VeldridGL();

		public VertexPositionColorGL[] quadVertices;
		public GraphicsDevice graphicsDevice;

		// matrix transforms
		public DeviceBuffer projectionBuffer;

		public CommandList commandList;

		public DeviceBuffer vertexBuffer;
		public DeviceBuffer indexBuffer;

		public Pipeline pipeline;

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

		public void DisposeResources()
		{
			pipeline.Dispose();
			commandList.Dispose();
			vertexBuffer.Dispose();
			indexBuffer.Dispose();

			graphicsDevice.Dispose();
		}

		public Stream OpenEmbeddedAssetStream(string name) => GetType().Assembly.GetManifestResourceStream(name);

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

		public Shader LoadShader(ResourceFactory factory, string set, ShaderStages stage, string entryPoint)
		{
			string name = $"{set}-{stage.ToString().ToLower()}.{GetExtension(factory.BackendType)}";
			return factory.CreateShader(new ShaderDescription(stage, ReadEmbeddedAssetBytes(name), entryPoint));
		}

		public void CreateResources(GraphicsDevice _graphicsDevice)
		{
			graphicsDevice = _graphicsDevice;

			ResourceFactory resourceFactory = _graphicsDevice.ResourceFactory;

			projectionBuffer = resourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

			quadVertices = new[]
			{
				new VertexPositionColorGL(new Vector3(-.75f, .75f, 0), RgbaFloat.Red),
				new VertexPositionColorGL(new Vector3(.75f, .75f, 0), RgbaFloat.Green),
				new VertexPositionColorGL(new Vector3(-.75f, -.75f, 0), RgbaFloat.Blue),
				new VertexPositionColorGL(new Vector3(.75f, -.75f, 0), RgbaFloat.Yellow)
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
				frontFace: FrontFace.Clockwise,
				depthClipEnabled: true,
				scissorTestEnabled: false);
			pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
			pipelineDescription.ShaderSet = shaderSetPositionColor;
			pipelineDescription.Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription;

			pipeline = resourceFactory.CreateGraphicsPipeline(pipelineDescription);

			commandList = resourceFactory.CreateCommandList();
		}
	}
}
