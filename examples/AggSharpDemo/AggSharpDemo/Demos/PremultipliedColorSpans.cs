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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// Takes color spans of premultiplied colors - as a renderer draws through C++'s <c>pixfmt_*_pre</c> - and hands
	/// them on as the straight colors a plain blend expects, so a surface that blends straight (a
	/// <see cref="Graphics2DSpanImage"/>) lands on the pixels a premultiplied blender would. Only the color spans
	/// are converted; solid colors pass through, which is exact for the opaque ones.
	/// </summary>
	public class PremultipliedColorSpans : ImageProxy
	{
		private Color[] straight = new Color[0];

		public PremultipliedColorSpans(IImageByte linkedImage)
			: base(linkedImage)
		{
		}

		public override void blend_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			base.blend_color_hspan(x, y, len, this.Demultiply(colors, colorsIndex, len), 0, covers, coversIndex, firstCoverForAll);
		}

		public override void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			base.blend_color_vspan(x, y, len, this.Demultiply(colors, colorsIndex, len), 0, covers, coversIndex, firstCoverForAll);
		}

		private Color[] Demultiply(Color[] colors, int colorsIndex, int len)
		{
			if (this.straight.Length < len)
			{
				this.straight = new Color[len];
			}

			for (int i = 0; i < len; i++)
			{
				Color color = colors[colorsIndex + i];
				int a = color.alpha;
				if (a != 0 && a != 255)
				{
					// Rounded to nearest; a channel above its alpha (not a premultiplied color) clamps.
					color.red = (byte)Math.Min(255, ((color.red * 255) + (a / 2)) / a);
					color.green = (byte)Math.Min(255, ((color.green * 255) + (a / 2)) / a);
					color.blue = (byte)Math.Min(255, ((color.blue * 255) + (a / 2)) / a);
				}

				this.straight[i] = color;
			}

			return this.straight;
		}
	}
}
