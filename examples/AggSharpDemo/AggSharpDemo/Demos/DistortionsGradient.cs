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

using MatterHackers.Agg;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>distortions.cpp's <c>g_gradient_colors</c>: the 256-entry fire ramp its gradient circle is colored by.</summary>
	public static class DistortionsGradient
	{
		// sRGB RGBA bytes, four per entry, verbatim from distortions.cpp.
		private static readonly byte[] SrgbaBytes =
		{
			255, 255, 255, 255, 255, 255, 254, 255, 255, 255, 254, 255, 255, 255, 254, 255,
			255, 255, 253, 255, 255, 255, 253, 255, 255, 255, 252, 255, 255, 255, 251, 255,
			255, 255, 250, 255, 255, 255, 248, 255, 255, 255, 246, 255, 255, 255, 244, 255,
			255, 255, 241, 255, 255, 255, 238, 255, 255, 255, 235, 255, 255, 255, 231, 255,
			255, 255, 227, 255, 255, 255, 222, 255, 255, 255, 217, 255, 255, 255, 211, 255,
			255, 255, 206, 255, 255, 255, 200, 255, 255, 254, 194, 255, 255, 253, 188, 255,
			255, 252, 182, 255, 255, 250, 176, 255, 255, 249, 170, 255, 255, 247, 164, 255,
			255, 246, 158, 255, 255, 244, 152, 255, 254, 242, 146, 255, 254, 240, 141, 255,
			254, 238, 136, 255, 254, 236, 131, 255, 253, 234, 126, 255, 253, 232, 121, 255,
			253, 229, 116, 255, 252, 227, 112, 255, 252, 224, 108, 255, 251, 222, 104, 255,
			251, 219, 100, 255, 251, 216, 96, 255, 250, 214, 93, 255, 250, 211, 89, 255,
			249, 208, 86, 255, 249, 205, 83, 255, 248, 202, 80, 255, 247, 199, 77, 255,
			247, 196, 74, 255, 246, 193, 72, 255, 246, 190, 69, 255, 245, 187, 67, 255,
			244, 183, 64, 255, 244, 180, 62, 255, 243, 177, 60, 255, 242, 174, 58, 255,
			242, 170, 56, 255, 241, 167, 54, 255, 240, 164, 52, 255, 239, 161, 51, 255,
			239, 157, 49, 255, 238, 154, 47, 255, 237, 151, 46, 255, 236, 147, 44, 255,
			235, 144, 43, 255, 235, 141, 41, 255, 234, 138, 40, 255, 233, 134, 39, 255,
			232, 131, 37, 255, 231, 128, 36, 255, 230, 125, 35, 255, 229, 122, 34, 255,
			228, 119, 33, 255, 227, 116, 31, 255, 226, 113, 30, 255, 225, 110, 29, 255,
			224, 107, 28, 255, 223, 104, 27, 255, 222, 101, 26, 255, 221, 99, 25, 255,
			220, 96, 24, 255, 219, 93, 23, 255, 218, 91, 22, 255, 217, 88, 21, 255,
			216, 86, 20, 255, 215, 83, 19, 255, 214, 81, 18, 255, 213, 79, 17, 255,
			212, 77, 17, 255, 211, 74, 16, 255, 210, 72, 15, 255, 209, 70, 14, 255,
			207, 68, 13, 255, 206, 66, 13, 255, 205, 64, 12, 255, 204, 62, 11, 255,
			203, 60, 10, 255, 202, 58, 10, 255, 201, 56, 9, 255, 199, 55, 9, 255,
			198, 53, 8, 255, 197, 51, 7, 255, 196, 50, 7, 255, 195, 48, 6, 255,
			193, 46, 6, 255, 192, 45, 5, 255, 191, 43, 5, 255, 190, 42, 4, 255,
			188, 41, 4, 255, 187, 39, 3, 255, 186, 38, 3, 255, 185, 37, 2, 255,
			183, 35, 2, 255, 182, 34, 1, 255, 181, 33, 1, 255, 179, 32, 1, 255,
			178, 30, 0, 255, 177, 29, 0, 255, 175, 28, 0, 255, 174, 27, 0, 255,
			173, 26, 0, 255, 171, 25, 0, 255, 170, 24, 0, 255, 168, 23, 0, 255,
			167, 22, 0, 255, 165, 21, 0, 255, 164, 21, 0, 255, 163, 20, 0, 255,
			161, 19, 0, 255, 160, 18, 0, 255, 158, 17, 0, 255, 156, 17, 0, 255,
			155, 16, 0, 255, 153, 15, 0, 255, 152, 14, 0, 255, 150, 14, 0, 255,
			149, 13, 0, 255, 147, 12, 0, 255, 145, 12, 0, 255, 144, 11, 0, 255,
			142, 11, 0, 255, 140, 10, 0, 255, 139, 10, 0, 255, 137, 9, 0, 255,
			135, 9, 0, 255, 134, 8, 0, 255, 132, 8, 0, 255, 130, 7, 0, 255,
			128, 7, 0, 255, 126, 6, 0, 255, 125, 6, 0, 255, 123, 5, 0, 255,
			121, 5, 0, 255, 119, 4, 0, 255, 117, 4, 0, 255, 115, 4, 0, 255,
			113, 3, 0, 255, 111, 3, 0, 255, 109, 2, 0, 255, 107, 2, 0, 255,
			105, 2, 0, 255, 103, 1, 0, 255, 101, 1, 0, 255, 99, 1, 0, 255,
			97, 0, 0, 255, 95, 0, 0, 255, 93, 0, 0, 255, 91, 0, 0, 255,
			90, 0, 0, 255, 88, 0, 0, 255, 86, 0, 0, 255, 84, 0, 0, 255,
			82, 0, 0, 255, 80, 0, 0, 255, 78, 0, 0, 255, 77, 0, 0, 255,
			75, 0, 0, 255, 73, 0, 0, 255, 72, 0, 0, 255, 70, 0, 0, 255,
			68, 0, 0, 255, 67, 0, 0, 255, 65, 0, 0, 255, 64, 0, 0, 255,
			63, 0, 0, 255, 61, 0, 0, 255, 60, 0, 0, 255, 59, 0, 0, 255,
			58, 0, 0, 255, 57, 0, 0, 255, 56, 0, 0, 255, 55, 0, 0, 255,
			54, 0, 0, 255, 53, 0, 0, 255, 53, 0, 0, 255, 52, 0, 0, 255,
			52, 0, 0, 255, 51, 0, 0, 255, 51, 0, 0, 255, 51, 0, 0, 255,
			50, 0, 0, 255, 50, 0, 0, 255, 51, 0, 0, 255, 51, 0, 0, 255,
			51, 0, 0, 255, 51, 0, 0, 255, 52, 0, 0, 255, 52, 0, 0, 255,
			53, 0, 0, 255, 54, 1, 0, 255, 55, 2, 0, 255, 56, 3, 0, 255,
			57, 4, 0, 255, 58, 5, 0, 255, 59, 6, 0, 255, 60, 7, 0, 255,
			62, 8, 0, 255, 63, 9, 0, 255, 64, 11, 0, 255, 66, 12, 0, 255,
			68, 13, 0, 255, 69, 14, 0, 255, 71, 16, 0, 255, 73, 17, 0, 255,
			75, 18, 0, 255, 77, 20, 0, 255, 79, 21, 0, 255, 81, 23, 0, 255,
			83, 24, 0, 255, 85, 26, 0, 255, 87, 28, 0, 255, 90, 29, 0, 255,
			92, 31, 0, 255, 94, 33, 0, 255, 97, 34, 0, 255, 99, 36, 0, 255,
			102, 38, 0, 255, 104, 40, 0, 255, 107, 41, 0, 255, 109, 43, 0, 255,
			112, 45, 0, 255, 115, 47, 0, 255, 117, 49, 0, 255, 120, 51, 0, 255,
			123, 52, 0, 255, 126, 54, 0, 255, 128, 56, 0, 255, 131, 58, 0, 255,
			134, 60, 0, 255, 137, 62, 0, 255, 140, 64, 0, 255, 143, 66, 0, 255,
			145, 68, 0, 255, 148, 70, 0, 255, 151, 72, 0, 255, 154, 74, 0, 255
		};

		/// <summary>
		/// The ramp as C++ builds <c>m_gradient_colors</c>: each entry an <c>srgba8</c> converted to the demo's linear
		/// <c>rgba8</c> color type.
		/// </summary>
		public static Color[] Colors()
		{
			var colors = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				colors[i] = SrgbLut.FromSrgba8(SrgbaBytes[i * 4], SrgbaBytes[(i * 4) + 1], SrgbaBytes[(i * 4) + 2], SrgbaBytes[(i * 4) + 3]);
			}

			return colors;
		}
	}
}
