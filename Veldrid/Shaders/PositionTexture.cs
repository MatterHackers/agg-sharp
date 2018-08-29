using ShaderGen;
using System.Numerics;
using static ShaderGen.ShaderBuiltins;

[assembly: ShaderSet("PositionTexture", "MatterHackers.VeldridProvider.Shaders.PositionTexture.VS", "MatterHackers.VeldridProvider.Shaders.PositionTexture.FS")]

namespace MatterHackers.VeldridProvider.Shaders
{
	public class PositionTexture
	{
		[ResourceSet(1)]
		public Texture2DResource SurfaceTexture;
		[ResourceSet(1)]
		public SamplerResource SurfaceSampler;

		[VertexShader]
		public FragmentInput VS(VertexInput input)
		{
			FragmentInput output;
			output.SystemPosition = new Vector4(input.Position.X, input.Position.Y, 0, 1);
			output.TexCoords = input.TexCoords;

			return output;
		}

		[FragmentShader]
		public Vector4 FS(FragmentInput input)
		{
			return Sample(SurfaceTexture, SurfaceSampler, input.TexCoords);
		}

		public struct VertexInput
		{
			[PositionSemantic] public Vector2 Position;
			[TextureCoordinateSemantic] public Vector2 TexCoords;
		}

		public struct FragmentInput
		{
			[SystemPositionSemantic] public Vector4 SystemPosition;
			[TextureCoordinateSemantic] public Vector2 TexCoords;
		}
	}
}
