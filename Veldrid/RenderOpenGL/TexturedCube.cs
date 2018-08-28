using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace TexturedCube
{
	public class TexturedCube
    {
        private DeviceBuffer _projectionBuffer;
        private DeviceBuffer _viewBuffer;
        private DeviceBuffer _worldBuffer;
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        private CommandList _cl;
        private Texture _surfaceTexture;
        private TextureView _surfaceTextureView;
        private Pipeline _pipeline;
        private ResourceSet _projViewSet;
        private ResourceSet _worldTextureSet;
        private float _ticks;

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

		private static string GetExtension(GraphicsBackend backendType)
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

		protected unsafe void CreateResources()
        {
#if false
			ResourceFactory factory = VeldridGL.graphicsDevice.ResourceFactory;

			_projectionBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _viewBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));
            _worldBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

            //_vertexBuffer = factory.CreateBuffer(new BufferDescription((uint)(VertexPositionTexture.SizeInBytes * _vertices.Length), BufferUsage.VertexBuffer));
			//VeldridGL.graphicsDevice.UpdateBuffer(_vertexBuffer, 0, _vertices);

            //_indexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)_indices.Length, BufferUsage.IndexBuffer));
			//VeldridGL.graphicsDevice.UpdateBuffer(_indexBuffer, 0, _indices);

            //_surfaceTexture = _stoneTexData.CreateDeviceTexture(VeldridGL.graphicsDevice, factory, TextureUsage.Sampled);
            _surfaceTextureView = factory.CreateTextureView(_surfaceTexture);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                new[]
                {
                    LoadShader(factory, "Cube", ShaderStages.Vertex, "VS"),
                    LoadShader(factory, "Cube", ShaderStages.Fragment, "FS")
                });

            ResourceLayout projViewLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("View", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("World", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { projViewLayout, worldTextureLayout },
				VeldridGL.graphicsDevice.MainSwapchain.Framebuffer.OutputDescription));

            _projViewSet = factory.CreateResourceSet(new ResourceSetDescription(
                projViewLayout,
                _projectionBuffer,
                _viewBuffer));

            _worldTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _worldBuffer,
                _surfaceTextureView,
				VeldridGL.graphicsDevice.Aniso4xSampler));

            _cl = factory.CreateCommandList();
#endif
        }

        protected void Draw(float deltaSeconds)
        {
            _ticks += deltaSeconds * 1000f;
            _cl.Begin();

            //_cl.UpdateBuffer(_projectionBuffer, 0, Matrix4x4.CreatePerspectiveFieldOfView(
              //  1.0f,
///                (float)Window.Width / Window.Height,
                //0.5f,
                //100f));

            _cl.UpdateBuffer(_viewBuffer, 0, Matrix4x4.CreateLookAt(Vector3.UnitZ * 2.5f, Vector3.Zero, Vector3.UnitY));

            Matrix4x4 rotation =
                Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (_ticks / 1000f))
                * Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, (_ticks / 3000f));
            _cl.UpdateBuffer(_worldBuffer, 0, ref rotation);

            //_cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _vertexBuffer);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            _cl.SetGraphicsResourceSet(0, _projViewSet);
            _cl.SetGraphicsResourceSet(1, _worldTextureSet);
            _cl.DrawIndexed(36, 1, 0, 0, 0);

            _cl.End();
			//VeldridGL.graphicsDevice.SubmitCommands(_cl);
			//VeldridGL.graphicsDevice.SwapBuffers(MainSwapchain);
			//VeldridGL.graphicsDevice.WaitForIdle();
        }
    }
}
