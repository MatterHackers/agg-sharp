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
*/

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// The layout of the one uniform block the canned shaders read, expressed as a name-to-offset table.
	/// <para>
	/// This is the replacement for D3D11 shader reflection. The classic path builds
	/// <c>UniformLocations[name] -&gt; offset</c> at runtime with
	/// <c>Compiler.Reflect&lt;ID3D11ShaderReflection&gt;</c>; WGSL has no runtime reflection at all, so
	/// the table is authored here as data and the WGSL that arrives in Phase 2 is written to match it.
	/// <see cref="Offsets"/> is public precisely so a shader-authoring test can assert the two agree.
	/// </para>
	/// <para>
	/// Every member is 16-byte aligned, which satisfies both WGSL's uniform address space rules and
	/// D3D's constant buffer packing, so the same table can describe either backend.
	/// </para>
	/// </summary>
	public static class GlUniformBlock
	{
		/// <summary>Byte offset of the 4x4 model-view matrix.</summary>
		public const int ModelViewMatrixOffset = 0;

		/// <summary>Byte offset of the 4x4 projection matrix, already mapped to the backend's clip space.</summary>
		public const int ProjectionMatrixOffset = 64;

		/// <summary>Byte offset of the 4x4 texture matrix.</summary>
		public const int TextureMatrixOffset = 128;

		/// <summary>Byte offset of light 0's eye-space position (w = 0 means directional).</summary>
		public const int Light0PositionOffset = 192;

		/// <summary>Byte offset of light 0's ambient color.</summary>
		public const int Light0AmbientOffset = 208;

		/// <summary>Byte offset of light 0's diffuse color.</summary>
		public const int Light0DiffuseOffset = 224;

		/// <summary>Byte offset of light 1's eye-space position.</summary>
		public const int Light1PositionOffset = 240;

		/// <summary>Byte offset of light 1's ambient color.</summary>
		public const int Light1AmbientOffset = 256;

		/// <summary>Byte offset of light 1's diffuse color.</summary>
		public const int Light1DiffuseOffset = 272;

		/// <summary>
		/// Byte offset of the flag vector: x = light 0 on, y = light 1 on, z = lighting on,
		/// w = texture environment is GL_REPLACE rather than GL_MODULATE.
		/// </summary>
		public const int FlagsOffset = 288;

		/// <summary>Total size of the block in bytes. A multiple of 16, as uniform buffers must be.</summary>
		public const int SizeInBytes = 304;

		private static readonly IReadOnlyDictionary<string, int> OffsetTable = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["modelViewMatrix"] = ModelViewMatrixOffset,
			["projectionMatrix"] = ProjectionMatrixOffset,
			["textureMatrix"] = TextureMatrixOffset,
			["light0Position"] = Light0PositionOffset,
			["light0Ambient"] = Light0AmbientOffset,
			["light0Diffuse"] = Light0DiffuseOffset,
			["light1Position"] = Light1PositionOffset,
			["light1Ambient"] = Light1AmbientOffset,
			["light1Diffuse"] = Light1DiffuseOffset,
			["flags"] = FlagsOffset,
		};

		/// <summary>
		/// Every member of the block by name. The generated-table deliverable of Phase 2 starts here:
		/// shader authoring reads this, it does not re-derive it.
		/// </summary>
		public static IReadOnlyDictionary<string, int> Offsets => OffsetTable;

		/// <summary>
		/// Writes a matrix in row-major order, which is how the classic path's <c>WriteMatrix</c> lays
		/// one out and therefore what the ported shaders expect.
		/// <para>
		/// <b>Multiplication order follows from that.</b> The rows are stored exactly as written, but a
		/// WGSL <c>mat4x4&lt;f32&gt;</c> is column-indexed, so those rows arrive in the shader as columns:
		/// the shaders must multiply <c>vec * mat</c>, not <c>mat * vec</c>. Getting this backwards
		/// transposes every transform, and there are twelve shader combinations to get it wrong in.
		/// </para>
		/// </summary>
		/// <param name="destination">The block being filled.</param>
		/// <param name="offset">Byte offset of the matrix member.</param>
		/// <param name="matrix">The matrix to write.</param>
		public static void WriteMatrix(Span<byte> destination, int offset, Matrix4X4 matrix)
		{
			WriteRow(destination, offset, matrix.Row0);
			WriteRow(destination, offset + 16, matrix.Row1);
			WriteRow(destination, offset + 32, matrix.Row2);
			WriteRow(destination, offset + 48, matrix.Row3);
		}

		/// <summary>Writes a four component float vector.</summary>
		/// <param name="destination">The block being filled.</param>
		/// <param name="offset">Byte offset of the vector member.</param>
		/// <param name="x">First component.</param>
		/// <param name="y">Second component.</param>
		/// <param name="z">Third component.</param>
		/// <param name="w">Fourth component.</param>
		public static void WriteVector4(Span<byte> destination, int offset, float x, float y, float z, float w)
		{
			BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset, 4), x);
			BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset + 4, 4), y);
			BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset + 8, 4), z);
			BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset + 12, 4), w);
		}

		/// <summary>Writes a four component float vector from an array, padding missing components.</summary>
		/// <param name="destination">The block being filled.</param>
		/// <param name="offset">Byte offset of the vector member.</param>
		/// <param name="values">Up to four components; anything shorter is zero filled.</param>
		public static void WriteVector4(Span<byte> destination, int offset, float[] values)
		{
			float Component(int index) => values != null && values.Length > index ? values[index] : 0f;

			WriteVector4(destination, offset, Component(0), Component(1), Component(2), Component(3));
		}

		/// <summary>
		/// Maps a GL projection matrix into the backend's clip space. GL's clip Z runs -w..w while both
		/// D3D and WebGPU run 0..w, so the third column is rescaled - the same correction the classic
		/// path applies in <c>UpdateTransformBuffer</c>, minus its Y flip (WebGPU's normalized device Y
		/// points up, exactly like GL's, so flipping here would render the whole UI upside down).
		/// </summary>
		/// <param name="glProjection">The projection matrix as GL code built it.</param>
		public static Matrix4X4 ToClipSpaceProjection(Matrix4X4 glProjection)
		{
			var p = glProjection;
			return new Matrix4X4(
				new Vector4(p.Row0.X, p.Row0.Y, (p.Row0.Z * 0.5) + (p.Row0.W * 0.5), p.Row0.W),
				new Vector4(p.Row1.X, p.Row1.Y, (p.Row1.Z * 0.5) + (p.Row1.W * 0.5), p.Row1.W),
				new Vector4(p.Row2.X, p.Row2.Y, (p.Row2.Z * 0.5) + (p.Row2.W * 0.5), p.Row2.W),
				new Vector4(p.Row3.X, p.Row3.Y, (p.Row3.Z * 0.5) + (p.Row3.W * 0.5), p.Row3.W));
		}

		private static void WriteRow(Span<byte> destination, int offset, Vector4 row)
			=> WriteVector4(destination, offset, (float)row.X, (float)row.Y, (float)row.Z, (float)row.W);
	}
}
