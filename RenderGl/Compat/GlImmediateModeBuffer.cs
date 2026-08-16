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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// The immediate mode accumulator: everything between <c>glBegin</c> and <c>glEnd</c> piles up here
	/// as parallel lists, and the flush interleaves it into one vertex buffer. Ported from the classic
	/// D3D11 context's <c>ImmediateModeData</c> plus its flush loops, so the byte layout and the flat
	/// shading color choice are identical between the two backends.
	/// </summary>
	public class GlImmediateModeBuffer
	{
		/// <summary>The color <c>glColor4</c> last set; every vertex added copies it.</summary>
		public byte[] CurrentColor { get; } = { 255, 255, 255, 255 };

		/// <summary>The primitive mode the current batch was begun with.</summary>
		public BeginMode Mode { get; set; }

		/// <summary>Accumulated positions, three floats per vertex.</summary>
		public List<float> Positions { get; private set; } = new List<float>();

		/// <summary>Accumulated colors, four bytes per vertex.</summary>
		public List<byte> Colors { get; private set; } = new List<byte>();

		/// <summary>Accumulated texture coordinates, two floats per vertex when any were supplied.</summary>
		public List<float> TexCoords { get; private set; } = new List<float>();

		/// <summary>
		/// Accumulated normals. Kept because <c>glNormal3</c> is called on this path, but note that the
		/// classic path's immediate mode flush never reads them - immediate mode geometry always goes
		/// through an unlit pipeline there, and this port keeps that behavior rather than inventing
		/// lighting the oracle does not do.
		/// </summary>
		public List<float> Normals { get; private set; } = new List<float>();

		/// <summary>Number of complete vertices accumulated.</summary>
		public int VertexCount => this.Positions.Count / 3;

		/// <summary>Starts a batch, discarding anything not yet flushed - exactly as <c>glBegin</c> does.</summary>
		/// <param name="mode">The primitive mode.</param>
		public void Begin(BeginMode mode)
		{
			this.Mode = mode;
			this.Positions.Clear();
			this.Colors.Clear();
			this.TexCoords.Clear();
			this.Normals.Clear();
		}

		/// <summary>Appends a vertex, capturing the current color with it.</summary>
		/// <param name="x">X position.</param>
		/// <param name="y">Y position.</param>
		/// <param name="z">Z position.</param>
		public void AddVertex(double x, double y, double z)
		{
			this.Positions.Add((float)x);
			this.Positions.Add((float)y);
			this.Positions.Add((float)z);

			this.Colors.Add(this.CurrentColor[0]);
			this.Colors.Add(this.CurrentColor[1]);
			this.Colors.Add(this.CurrentColor[2]);
			this.Colors.Add(this.CurrentColor[3]);
		}

		/// <summary>Sets the color subsequent vertices capture.</summary>
		/// <param name="red">Red channel.</param>
		/// <param name="green">Green channel.</param>
		/// <param name="blue">Blue channel.</param>
		/// <param name="alpha">Alpha channel.</param>
		public void SetColor(byte red, byte green, byte blue, byte alpha)
		{
			this.CurrentColor[0] = red;
			this.CurrentColor[1] = green;
			this.CurrentColor[2] = blue;
			this.CurrentColor[3] = alpha;
		}

		/// <summary>Appends a texture coordinate.</summary>
		/// <param name="u">U coordinate.</param>
		/// <param name="v">V coordinate.</param>
		public void AddTexCoord(double u, double v)
		{
			this.TexCoords.Add((float)u);
			this.TexCoords.Add((float)v);
		}

		/// <summary>Appends a normal.</summary>
		/// <param name="x">Normal X.</param>
		/// <param name="y">Normal Y.</param>
		/// <param name="z">Normal Z.</param>
		public void AddNormal(double x, double y, double z)
		{
			this.Normals.Add((float)x);
			this.Normals.Add((float)y);
			this.Normals.Add((float)z);
		}

		/// <summary>
		/// Rewrites a triangle fan as an independent triangle list. WebGPU has no fan topology - and
		/// neither does D3D11, which is why the classic path does exactly this at <c>glEnd</c> time.
		/// <para>
		/// <b><see cref="Mode"/> deliberately stays <see cref="BeginMode.TriangleFan"/>.</b> The vertices
		/// are a list, and <see cref="GlStateShadow.MapTopology"/> maps a fan to
		/// <see cref="PrimitiveTopology.TriangleList"/> for that reason, but the mode is what
		/// <see cref="ColorIndexForFlatShading"/> reads - and the classic path leaves it a fan there too,
		/// so a flat shaded fan takes each vertex's own color and renders smooth. That is GL-incorrect:
		/// real GL would give every triangle of the fan its provoking vertex's color. It is preserved
		/// anyway, because the goldens were captured from the oracle and 1:1 parity with them is worth
		/// more than correctness until the backend cutover is done. Fix it after, with new goldens.
		/// </para>
		/// </summary>
		public void ConvertTriangleFanToTriangles()
		{
			int vertexCount = this.VertexCount;
			if (vertexCount < 3)
			{
				return;
			}

			var newPositions = new List<float>();
			var newColors = new List<byte>();
			var newTexCoords = new List<float>();
			bool hasTexCoords = this.TexCoords.Count > 0;

			for (int i = 1; i < vertexCount - 1; i++)
			{
				CopyVertex(newPositions, this.Positions, 0);
				CopyVertex(newPositions, this.Positions, i);
				CopyVertex(newPositions, this.Positions, i + 1);

				CopyColor(newColors, this.Colors, 0);
				CopyColor(newColors, this.Colors, i);
				CopyColor(newColors, this.Colors, i + 1);

				if (hasTexCoords)
				{
					CopyTexCoord(newTexCoords, this.TexCoords, 0);
					CopyTexCoord(newTexCoords, this.TexCoords, i);
					CopyTexCoord(newTexCoords, this.TexCoords, i + 1);
				}
			}

			this.Positions = newPositions;
			this.Colors = newColors;
			this.TexCoords = newTexCoords;
		}

		/// <summary>
		/// Which vertex's color a vertex takes when flat shading is on: GL's provoking vertex rule, done
		/// on the CPU. Ported from the classic path's <c>GetColorIndexForFlatShading</c>, including its
		/// choices - the <em>last</em> vertex of a triangle or line provokes, a strip takes
		/// <c>i + 2</c>, and an index past the end clamps to the last vertex. A fan is not in the list, so
		/// it returns the index unchanged and flat shading has no effect on it; see
		/// <see cref="ConvertTriangleFanToTriangles"/> for why that oracle quirk is kept.
		/// </summary>
		/// <param name="mode">The primitive mode.</param>
		/// <param name="vertexIndex">Index of the vertex being written.</param>
		/// <param name="vertexCount">Number of vertices in the batch.</param>
		/// <param name="flatShading">False returns <paramref name="vertexIndex"/> unchanged.</param>
		public static int ColorIndexForFlatShading(BeginMode mode, int vertexIndex, int vertexCount, bool flatShading)
		{
			if (!flatShading)
			{
				return vertexIndex;
			}

			int colorIndex = vertexIndex;
			if (mode == BeginMode.Triangles)
			{
				colorIndex = vertexIndex - (vertexIndex % 3) + 2;
			}
			else if (mode == BeginMode.TriangleStrip)
			{
				colorIndex = vertexIndex + 2;
			}
			else if (mode == BeginMode.Lines)
			{
				colorIndex = vertexIndex - (vertexIndex % 2) + 1;
			}

			if (colorIndex >= vertexCount)
			{
				colorIndex = vertexCount - 1;
			}

			return colorIndex;
		}

		/// <summary>
		/// Interleaves position (float3) and color (float4) into the layout
		/// <see cref="GlShaderKeys.ColoredVertexLayout"/> declares.
		/// </summary>
		/// <param name="mode">The primitive mode, which decides the flat shading color choice.</param>
		/// <param name="positions">Three floats per vertex.</param>
		/// <param name="colors">Four bytes per vertex.</param>
		/// <param name="flatShading">Whether to apply the provoking vertex rule.</param>
		public static byte[] BuildColoredVertices(BeginMode mode, List<float> positions, List<byte> colors, bool flatShading)
		{
			int vertexCount = positions.Count / 3;
			int stride = (int)GlShaderKeys.ColoredVertexLayout.ArrayStride;
			var bytes = new byte[vertexCount * stride];
			var destination = bytes.AsSpan();

			for (int i = 0; i < vertexCount; i++)
			{
				int at = i * stride;
				int positionIndex = i * 3;
				int colorIndex = ColorIndexForFlatShading(mode, i, vertexCount, flatShading) * 4;

				WriteFloat(destination, at, positions[positionIndex]);
				WriteFloat(destination, at + 4, positions[positionIndex + 1]);
				WriteFloat(destination, at + 8, positions[positionIndex + 2]);
				WriteColor(destination, at + 12, colors, colorIndex);
			}

			return bytes;
		}

		/// <summary>
		/// Interleaves position (float3), texture coordinate (float2) and color (float4) into the layout
		/// <see cref="GlShaderKeys.TexturedVertexLayout"/> declares. Missing texture coordinates are
		/// written as zero rather than treated as an error, matching the classic path.
		/// </summary>
		/// <param name="mode">The primitive mode, which decides the flat shading color choice.</param>
		/// <param name="positions">Three floats per vertex.</param>
		/// <param name="colors">Four bytes per vertex.</param>
		/// <param name="texCoords">Two floats per vertex, possibly short.</param>
		/// <param name="flatShading">Whether to apply the provoking vertex rule.</param>
		public static byte[] BuildTexturedVertices(
			BeginMode mode,
			List<float> positions,
			List<byte> colors,
			List<float> texCoords,
			bool flatShading)
		{
			int vertexCount = positions.Count / 3;
			int stride = (int)GlShaderKeys.TexturedVertexLayout.ArrayStride;
			var bytes = new byte[vertexCount * stride];
			var destination = bytes.AsSpan();

			for (int i = 0; i < vertexCount; i++)
			{
				int at = i * stride;
				int positionIndex = i * 3;
				int texIndex = i * 2;
				int colorIndex = ColorIndexForFlatShading(mode, i, vertexCount, flatShading) * 4;

				WriteFloat(destination, at, positions[positionIndex]);
				WriteFloat(destination, at + 4, positions[positionIndex + 1]);
				WriteFloat(destination, at + 8, positions[positionIndex + 2]);
				WriteFloat(destination, at + 12, texIndex < texCoords.Count ? texCoords[texIndex] : 0f);
				WriteFloat(destination, at + 16, texIndex + 1 < texCoords.Count ? texCoords[texIndex + 1] : 0f);
				WriteColor(destination, at + 20, colors, colorIndex);
			}

			return bytes;
		}

		private static void WriteFloat(Span<byte> destination, int offset, float value)
			=> BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset, 4), value);

		private static void WriteColor(Span<byte> destination, int offset, List<byte> colors, int colorIndex)
		{
			// Expanded to floats rather than kept as Unorm8x4 because the classic path's vertex layout
			// is float4 and the ported shaders read a float4. Cheap to revisit later; not worth a
			// divergence now.
			WriteFloat(destination, offset, colors[colorIndex] / 255f);
			WriteFloat(destination, offset + 4, colors[colorIndex + 1] / 255f);
			WriteFloat(destination, offset + 8, colors[colorIndex + 2] / 255f);
			WriteFloat(destination, offset + 12, colors[colorIndex + 3] / 255f);
		}

		private static void CopyVertex(List<float> destination, List<float> source, int index)
		{
			destination.Add(source[index * 3]);
			destination.Add(source[(index * 3) + 1]);
			destination.Add(source[(index * 3) + 2]);
		}

		private static void CopyColor(List<byte> destination, List<byte> source, int index)
		{
			destination.Add(source[index * 4]);
			destination.Add(source[(index * 4) + 1]);
			destination.Add(source[(index * 4) + 2]);
			destination.Add(source[(index * 4) + 3]);
		}

		private static void CopyTexCoord(List<float> destination, List<float> source, int index)
		{
			destination.Add(source[index * 2]);
			destination.Add(source[(index * 2) + 1]);
		}
	}
}
