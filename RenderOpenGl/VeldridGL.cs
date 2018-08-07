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

using System.IO;
using Veldrid;

namespace MatterHackers.RenderOpenGl
{
	public struct VertexPositionColor
	{
		public const uint SizeInBytes = 24;
		public System.Numerics.Vector2 Position;
		public RgbaFloat Color;
		public VertexPositionColor(System.Numerics.Vector2 position, RgbaFloat color)
		{
			Position = position;
			Color = color;
		}
	}

	public static class VeldridGL
	{
		public static VertexPositionColor[] quadVertices;
		public static GraphicsDevice _graphicsDevice;
		public static CommandList _commandList;
		public static DeviceBuffer _vertexBuffer;
		public static DeviceBuffer _indexBuffer;
		public static Shader _vertexShader;
		public static Shader _fragmentShader;
		public static Pipeline _pipeline;

		public static Shader LoadShader(ShaderStages stage)
		{
			string extension = null;
			switch (_graphicsDevice.BackendType)
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
			return _graphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(stage, shaderBytes, entryPoint));
		}

		public static void DisposeResources()
		{
			_pipeline.Dispose();
			_vertexShader.Dispose();
			_fragmentShader.Dispose();
			_commandList.Dispose();
			_vertexBuffer.Dispose();
			_indexBuffer.Dispose();
			_graphicsDevice.Dispose();
		}

		public static void CreateResources(GraphicsDevice _graphicsDevice)
		{
			VeldridGL._graphicsDevice = _graphicsDevice;

			ResourceFactory factory = _graphicsDevice.ResourceFactory;

			quadVertices = new[]
			{
				new VertexPositionColor(new System.Numerics.Vector2(-.75f, .75f), RgbaFloat.Red),
				new VertexPositionColor(new System.Numerics.Vector2(.75f, .75f), RgbaFloat.Green),
				new VertexPositionColor(new System.Numerics.Vector2(-.75f, -.75f), RgbaFloat.Blue),
				new VertexPositionColor(new System.Numerics.Vector2(.75f, -.75f), RgbaFloat.Yellow)
			};

			BufferDescription vbDescription = new BufferDescription(
				4 * VertexPositionColor.SizeInBytes,
				BufferUsage.VertexBuffer);
			_vertexBuffer = factory.CreateBuffer(vbDescription);
			_graphicsDevice.UpdateBuffer(_vertexBuffer, 0, quadVertices);

			ushort[] quadIndices = { 0, 1, 2, 3 };
			BufferDescription ibDescription = new BufferDescription(
				4 * sizeof(ushort),
				BufferUsage.IndexBuffer);
			_indexBuffer = factory.CreateBuffer(ibDescription);
			_graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);

			VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
				new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
				new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4));

			_vertexShader = LoadShader(ShaderStages.Vertex);
			_fragmentShader = LoadShader(ShaderStages.Fragment);

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
			pipelineDescription.ShaderSet = new ShaderSetDescription(
				vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
				shaders: new Shader[] { _vertexShader, _fragmentShader });
			pipelineDescription.Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription;

			_pipeline = factory.CreateGraphicsPipeline(pipelineDescription);

			_commandList = factory.CreateCommandList();
		}
	}
}
