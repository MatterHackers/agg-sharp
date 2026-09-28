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

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// The span operations of a C++ rgba32 (AGG_BGRA128) pixel format that <see cref="RendererBaseFloat"/> clips
	/// and the scanline renderers drive. The spans are not clipped here, as in C++.
	/// </summary>
	public interface IPixelFormatFloat
	{
		int Width { get; }

		int Height { get; }

		/// <summary>C++ <c>blend_hline(x, y, len, c, cover)</c>.</summary>
		void BlendHline(int x, int y, int len, ColorF c, int cover);

		/// <summary>C++ <c>blend_solid_hspan(x, y, len, c, covers)</c>.</summary>
		void BlendSolidHspan(int x, int y, int len, ColorF c, byte[] covers, int coversIndex);

		/// <summary>C++ <c>blend_color_hspan(x, y, len, colors, covers, cover)</c>: per-pixel covers when <paramref name="covers"/> is given, else one <paramref name="cover"/> for all.</summary>
		void BlendColorHspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, int cover);
	}

	/// <summary>
	/// C++ <c>pixfmt_custom_blend_rgba&lt;comp_op_adaptor_rgba&lt;rgba32, order_bgra&gt;&gt;</c>: every pixel goes through
	/// <see cref="BlenderCompOpBGRAFloat.BlendPix"/> with <see cref="Operator"/> and its cover. Unlike
	/// <see cref="PixelFormatBGRAFloat"/> there is no shortcut for opaque colors or transparent ones: an operator
	/// such as clear or src changes the destination even where the source is transparent.
	/// </summary>
	public class PixelFormatCompOpBGRAFloat : IPixelFormatFloat
	{
		private readonly ImageBufferFloat image;

		public PixelFormatCompOpBGRAFloat(ImageBufferFloat image, CompOp op = CompOp.SrcOver)
		{
			this.image = image;
			Operator = op;
		}

		/// <summary>C++ <c>comp_op()</c>: the operator every blend uses.</summary>
		public CompOp Operator { get; set; }

		public ImageBufferFloat Image => image;

		public int Width => image.Width;

		public int Height => image.Height;

		public void BlendHline(int x, int y, int len, ColorF c, int cover)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				BlenderCompOpBGRAFloat.BlendPix(Operator, p, o, c, cover);
				o += 4;
			}
			while (--len != 0);
		}

		public void BlendSolidHspan(int x, int y, int len, ColorF c, byte[] covers, int coversIndex)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				BlenderCompOpBGRAFloat.BlendPix(Operator, p, o, c, covers[coversIndex++]);
				o += 4;
			}
			while (--len != 0);
		}

		public void BlendColorHspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, int cover)
		{
			float[] p = image.GetBuffer();
			int o = image.GetBufferOffsetXY(x, y);
			do
			{
				BlenderCompOpBGRAFloat.BlendPix(Operator, p, o, colors[colorsIndex++], covers != null ? covers[coversIndex++] : cover);
				o += 4;
			}
			while (--len != 0);
		}
	}
}
