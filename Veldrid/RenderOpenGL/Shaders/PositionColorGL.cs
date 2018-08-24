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

using ShaderGen;
using System.Numerics;
using static ShaderGen.ShaderBuiltins;

[assembly: ShaderSet("PositionColorGL", "RenderOpenGl.Shaders.PositionColorGL.VS", "RenderOpenGl.Shaders.PositionColorGL.FS")]

namespace RenderOpenGl.Shaders
{
	public class PositionColorGL
	{
		[ResourceSet(0)]
		public Matrix4x4 Projection;
		[ResourceSet(0)]
		public Matrix4x4 View;

		[ResourceSet(1)]
		public Matrix4x4 World;

		[VertexShader]
		public FragmentInput VS(VertexInput input)
		{
			FragmentInput output;
			Vector4 worldPosition = Mul(World, new Vector4(input.Position, 1));
			Vector4 viewPosition = Mul(View, worldPosition);
			Vector4 clipPosition = Mul(Projection, viewPosition);
			output.SystemPosition = clipPosition;
			output.Color = input.Color;

			// this is the old behavior
			//output.SystemPosition = new Vector4(input.Position.X, input.Position.Y, 0, 1);

			return output;
		}

		[FragmentShader]
		public Vector4 FS(FragmentInput input)
		{
			return input.Color;
		}

		public struct VertexInput
		{
			[PositionSemantic] public Vector3 Position;
			[ColorSemantic] public Vector4 Color;
		}

		public struct FragmentInput
		{
			[SystemPositionSemantic] public Vector4 SystemPosition;
			[ColorSemantic] public Vector4 Color;
		}
	}
}
