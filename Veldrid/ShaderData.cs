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

using MatterHackers.Agg.Image;
using MatterHackers.VeldirdProvider.VertexFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace MatterHackers.VeldridProvider
{
	public class ShaderData
	{
		public static ShaderData Instance = new ShaderData();

		public Vector4[] quadVerts;

		public VertexPositionColor[] vertexPositionColor;
		public VertexPositionTexture[] vertexPositionTexture;

		public GraphicsDevice GraphicsDevice;

		public CommandList CommandList;

		public DeviceBuffer TextureVertexBuffer;
		public DeviceBuffer TextureIndexBuffer;

		public DeviceBuffer VertexBufferPositionColor;
		public DeviceBuffer IndexBufferPositionColor;

		public Pipeline StandardPipeline;

		public Pipeline TextureGraphicsPipeline { get; private set; }

		public Shader LoadShader(ShaderStages stage)
		{
			string extension = null;
			switch (this.GraphicsDevice.BackendType)
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
			return this.GraphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(stage, shaderBytes, entryPoint));
		}

		public unsafe Texture CreateTexture(GraphicsDevice gd, ResourceFactory rf, TextureUsage usage, ImageBuffer image)
		{
			uint MipLevels = 1;
			uint Width = (uint)image.Width;
			uint Height = (uint)image.Height;
			uint Depth = 1; // (uint)image.BitDepth;
			uint ArrayLayers = 1;
			var Format = PixelFormat.B8_G8_R8_A8_UNorm;

			Texture texture = rf.CreateTexture(new TextureDescription(
				Width, Height, Depth, MipLevels, ArrayLayers, Format, usage, TextureType.Texture2D));

			Texture staging = rf.CreateTexture(new TextureDescription(
				Width, Height, Depth, MipLevels, ArrayLayers, Format, TextureUsage.Staging, TextureType.Texture2D));

			ulong offset = 0;
			fixed (byte* texDataPtr = &image.GetBuffer()[0])
			{
				for (uint level = 0; level < MipLevels; level++)
				{
					uint mipWidth = GetDimension(Width, level);
					uint mipHeight = GetDimension(Height, level);
					uint mipDepth = GetDimension(Depth, level);
					uint subresourceSize = mipWidth * mipHeight * mipDepth * GetFormatSize(Format);

					for (uint layer = 0; layer < ArrayLayers; layer++)
					{
						gd.UpdateTexture(
							staging, (IntPtr)(texDataPtr + offset), subresourceSize,
							0, 0, 0, mipWidth, mipHeight, mipDepth,
							level, layer);
						offset += subresourceSize;
					}
				}
			}

			CommandList cl = rf.CreateCommandList();
			cl.Begin();
			cl.CopyTexture(staging, texture);
			cl.End();
			gd.SubmitCommands(cl);

			return texture;
		}

		public unsafe Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory rf, TextureUsage usage, ImageBuffer image)
		{
			uint MipLevels = 1;
			uint Width = (uint)image.Width;
			uint Height = (uint)image.Height;
			uint Depth = (uint)image.BitDepth;
			uint ArrayLayers = 1;
			var Format = PixelFormat.B8_G8_R8_A8_UNorm;
			Texture texture = rf.CreateTexture(new TextureDescription(
				Width, Height, Depth, MipLevels, ArrayLayers, Format, TextureUsage.Sampled, TextureType.Texture2D));

			Texture staging = rf.CreateTexture(new TextureDescription(
				Width, Height, Depth, MipLevels, ArrayLayers, Format, TextureUsage.Staging, TextureType.Texture2D));

			ulong offset = 0;
			fixed (byte* texDataPtr = &image.GetBuffer()[0])
			{
				for (uint level = 0; level < MipLevels; level++)
				{
					uint mipWidth = GetDimension(Width, level);
					uint mipHeight = GetDimension(Height, level);
					uint mipDepth = GetDimension(Depth, level);
					uint subresourceSize = mipWidth * mipHeight * mipDepth * GetFormatSize(Format);

					for (uint layer = 0; layer < ArrayLayers; layer++)
					{
						gd.UpdateTexture(
							staging, (IntPtr)(texDataPtr + offset), subresourceSize,
							0, 0, 0, mipWidth, mipHeight, mipDepth,
							level, layer);
						offset += subresourceSize;
					}
				}
			}

			CommandList cl = rf.CreateCommandList();
			cl.Begin();
			cl.CopyTexture(staging, texture);
			cl.End();
			gd.SubmitCommands(cl);

			return texture;
		}

		private uint GetFormatSize(PixelFormat format)
		{
			switch (format)
			{
				case PixelFormat.R8_G8_B8_A8_UNorm:
					return 4;
				case PixelFormat.B8_G8_R8_A8_UNorm:
					return 4;
				case PixelFormat.BC3_UNorm:
					return 1;
				default:
					throw new NotImplementedException();
			}
		}

		public static uint GetDimension(uint largestLevelDimension, uint mipLevel)
		{
			uint ret = largestLevelDimension;
			for (uint i = 0; i < mipLevel; i++)
			{
				ret /= 2;
			}

			return Math.Max(1, ret);
		}

		public void DisposeResources()
		{
			this.StandardPipeline.Dispose();
			this.TextureGraphicsPipeline.Dispose();

			this.CommandList?.Dispose();
			this.VertexBufferPositionColor.Dispose();
			this.IndexBufferPositionColor.Dispose();

			this.GraphicsDevice.Dispose();
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
			this.GraphicsDevice = _graphicsDevice;

			ResourceFactory resourceFactory = this.GraphicsDevice.ResourceFactory;

			this.StandardPipeline = this.CreateStandardPipeline(resourceFactory);
			this.TextureGraphicsPipeline = this.CreateTexturePipeline(resourceFactory);

			this.CommandList = resourceFactory.CreateCommandList();
		}

		private Pipeline CreateStandardPipeline(ResourceFactory resourceFactory)
		{
			vertexPositionColor = new[]
			{
				new VertexPositionColor(new Vector2(-.75f, .75f), RgbaFloat.Red),
				new VertexPositionColor(new Vector2(.75f, .75f), RgbaFloat.Green),
				new VertexPositionColor(new Vector2(-.75f, -.75f), RgbaFloat.Blue),
				new VertexPositionColor(new Vector2(.75f, -.75f), RgbaFloat.Yellow)
			};

			this.VertexBufferPositionColor = resourceFactory.CreateBuffer(
				new BufferDescription(
					4 * VertexPositionColor.SizeInBytes,
					BufferUsage.VertexBuffer));

			this.GraphicsDevice.UpdateBuffer(VertexBufferPositionColor, 0, vertexPositionColor);

			this.IndexBufferPositionColor = resourceFactory.CreateBuffer(
				new BufferDescription(
					4 * sizeof(ushort),
					BufferUsage.IndexBuffer));

			this.GraphicsDevice.UpdateBuffer(this.IndexBufferPositionColor, 0, new ushort[] { 0, 1, 2, 3 });

			// Create pipeline
			return resourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription
			{
				BlendState = BlendStateDescription.SingleAlphaBlend,
				DepthStencilState = new DepthStencilStateDescription(
				depthTestEnabled: true,
				depthWriteEnabled: true,
				comparisonKind: ComparisonKind.LessEqual),
				RasterizerState = new RasterizerStateDescription(
					cullMode: FaceCullMode.Back,
					fillMode: PolygonFillMode.Solid,
					frontFace: FrontFace.Clockwise,
					depthClipEnabled: true,
					scissorTestEnabled: false),
				PrimitiveTopology = PrimitiveTopology.TriangleStrip,
				ResourceLayouts = Array.Empty<ResourceLayout>(),
				ShaderSet = new ShaderSetDescription(
				new[]
				{
					new VertexLayoutDescription(
						new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
						new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4))
				},
				new[]
				{
					LoadShader(resourceFactory, "PositionColor", ShaderStages.Vertex, "VS"),
					LoadShader(resourceFactory, "PositionColor", ShaderStages.Fragment, "FS")
				}),
				Outputs = this.GraphicsDevice.SwapchainFramebuffer.OutputDescription
			});
		}

		private Pipeline CreateTexturePipeline(ResourceFactory resourceFactory)
		{
			quadVerts = new[]
						{
				new Vector4(-1, 1, 0, 1),
				new Vector4(1, 1, 1, 1),
				new Vector4(1, -1, 1, 0),
				new Vector4(-1, -1, 0, 0),
			};

			this.TextureVertexBuffer = resourceFactory.CreateBuffer(new BufferDescription(16 * 4, BufferUsage.VertexBuffer));
			this.TextureIndexBuffer = resourceFactory.CreateBuffer(new BufferDescription(2 * 6, BufferUsage.IndexBuffer));

			ushort[] indices = { 0, 1, 2, 0, 2, 3 };

			this.GraphicsDevice.UpdateBuffer(this.TextureVertexBuffer, 0, quadVerts);
			this.GraphicsDevice.UpdateBuffer(this.TextureIndexBuffer, 0, indices);

			// Create texture pipeline
			return resourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
				BlendStateDescription.SingleAlphaBlend,
				DepthStencilStateDescription.Disabled,
				new RasterizerStateDescription(FaceCullMode.Back, PolygonFillMode.Solid, FrontFace.Clockwise, true, false),
				PrimitiveTopology.TriangleList,
				// Shader definition
#if true
				new ShaderSetDescription(
					new[]
					{
						new VertexLayoutDescription(
							new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
							new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
					},
					new[]
					{
						LoadShader(resourceFactory, "PositionTexture", ShaderStages.Vertex, "VS"),
						LoadShader(resourceFactory, "PositionTexture", ShaderStages.Fragment, "FS")
					}),
#else
				new ShaderSetDescription(
					new VertexLayoutDescription[]
					{
						new VertexLayoutDescription(
							new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
							new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
					},
					new[]
					{
						resourceFactory.CreateShader(
							new ShaderDescription(
								ShaderStages.Vertex,
								ReadEmbeddedAssetBytes($"Vertex.{GetExtension(resourceFactory.BackendType)}"),
								"VS")),
						resourceFactory.CreateShader(
							new ShaderDescription(
								ShaderStages.Fragment,
								ReadEmbeddedAssetBytes($"Fragment.{GetExtension(resourceFactory.BackendType)}"),
								"FS"))
					}),
#endif
				new[]
				{
					// The ResourceLayout expected by the shaders
					resourceFactory.CreateResourceLayout(
						new ResourceLayoutDescription(
							new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
							new ResourceLayoutElementDescription("SS", ResourceKind.Sampler, ShaderStages.Fragment)))
				},
				this.GraphicsDevice.SwapchainFramebuffer.OutputDescription));
		}
	}
}
