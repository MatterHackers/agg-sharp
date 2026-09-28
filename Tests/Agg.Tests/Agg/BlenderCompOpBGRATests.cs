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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class BlenderCompOpBGRATests
	{
		/// <summary>
		/// Each operator, over a premultiplied destination, with a straight source at full and partial cover, pinned
		/// to C++ comp_op_adaptor_rgba&lt;rgba8, order_bgra&gt;::blend_pix (comp_op_rgba_minus called directly, as it
		/// is not in C++'s table). The values were traced from the reference renderer's patched agg_pixfmt_rgba.h:
		/// unpatched C++ gives SrcAtop blue 59, 85, 212 and 104 in its four translucent cases (it blended the
		/// destination's green), and ColorBurn of black over white 0, 0, 0 (it burned a full channel away).
		/// </summary>
		[Test]
		[Arguments(CompOp.Clear, 40, 120, 200, 220, 200, 100, 50, 180, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.Clear, 40, 120, 200, 220, 200, 100, 50, 180, 128, 20, 60, 100, 110)]
		[Arguments(CompOp.Clear, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.Clear, 10, 200, 60, 255, 100, 150, 250, 128, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.Clear, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.Clear, 30, 90, 150, 160, 255, 255, 255, 90, 77, 21, 63, 105, 112)]
		[Arguments(CompOp.Src, 40, 120, 200, 220, 200, 100, 50, 180, 255, 141, 71, 35, 180)]
		[Arguments(CompOp.Src, 40, 120, 200, 220, 200, 100, 50, 180, 128, 91, 95, 117, 200)]
		[Arguments(CompOp.Src, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.Src, 10, 200, 60, 255, 100, 150, 250, 128, 255, 50, 75, 125, 128)]
		[Arguments(CompOp.Src, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Src, 30, 90, 150, 160, 255, 255, 255, 90, 77, 48, 90, 132, 139)]
		[Arguments(CompOp.Dst, 40, 120, 200, 220, 200, 100, 50, 180, 255, 40, 120, 200, 220)]
		[Arguments(CompOp.Dst, 40, 120, 200, 220, 200, 100, 50, 180, 128, 40, 120, 200, 220)]
		[Arguments(CompOp.Dst, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Dst, 10, 200, 60, 255, 100, 150, 250, 128, 255, 10, 200, 60, 255)]
		[Arguments(CompOp.Dst, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.Dst, 30, 90, 150, 160, 255, 255, 255, 90, 77, 30, 90, 150, 160)]
		[Arguments(CompOp.SrcOver, 40, 120, 200, 220, 200, 100, 50, 180, 255, 153, 106, 94, 245)]
		[Arguments(CompOp.SrcOver, 40, 120, 200, 220, 200, 100, 50, 180, 128, 97, 114, 147, 232)]
		[Arguments(CompOp.SrcOver, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.SrcOver, 10, 200, 60, 255, 100, 150, 250, 128, 255, 55, 175, 155, 255)]
		[Arguments(CompOp.SrcOver, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.SrcOver, 30, 90, 150, 160, 255, 255, 255, 90, 77, 54, 107, 161, 170)]
		[Arguments(CompOp.DstOver, 40, 120, 200, 220, 200, 100, 50, 180, 255, 59, 130, 205, 245)]
		[Arguments(CompOp.DstOver, 40, 120, 200, 220, 200, 100, 50, 180, 128, 50, 125, 202, 232)]
		[Arguments(CompOp.DstOver, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.DstOver, 10, 200, 60, 255, 100, 150, 250, 128, 255, 10, 200, 60, 255)]
		[Arguments(CompOp.DstOver, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.DstOver, 30, 90, 150, 160, 255, 255, 255, 90, 77, 40, 100, 160, 170)]
		[Arguments(CompOp.SrcIn, 40, 120, 200, 220, 200, 100, 50, 180, 255, 122, 61, 30, 155)]
		[Arguments(CompOp.SrcIn, 40, 120, 200, 220, 200, 100, 50, 180, 128, 81, 91, 115, 188)]
		[Arguments(CompOp.SrcIn, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.SrcIn, 10, 200, 60, 255, 100, 150, 250, 128, 255, 50, 75, 125, 128)]
		[Arguments(CompOp.SrcIn, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.SrcIn, 30, 90, 150, 160, 255, 255, 255, 90, 77, 38, 80, 122, 129)]
		[Arguments(CompOp.DstIn, 40, 120, 200, 220, 200, 100, 50, 180, 255, 28, 85, 141, 155)]
		[Arguments(CompOp.DstIn, 40, 120, 200, 220, 200, 100, 50, 180, 128, 34, 102, 170, 188)]
		[Arguments(CompOp.DstIn, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.DstIn, 10, 200, 60, 255, 100, 150, 250, 128, 255, 5, 100, 30, 128)]
		[Arguments(CompOp.DstIn, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.DstIn, 30, 90, 150, 160, 255, 255, 255, 90, 77, 24, 72, 121, 129)]
		[Arguments(CompOp.SrcOut, 40, 120, 200, 220, 200, 100, 50, 180, 255, 19, 10, 5, 25)]
		[Arguments(CompOp.SrcOut, 40, 120, 200, 220, 200, 100, 50, 180, 128, 30, 65, 102, 122)]
		[Arguments(CompOp.SrcOut, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.SrcOut, 10, 200, 60, 255, 100, 150, 250, 128, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.SrcOut, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.SrcOut, 30, 90, 150, 160, 255, 255, 255, 90, 77, 31, 73, 115, 122)]
		[Arguments(CompOp.DstOut, 40, 120, 200, 220, 200, 100, 50, 180, 255, 12, 35, 59, 65)]
		[Arguments(CompOp.DstOut, 40, 120, 200, 220, 200, 100, 50, 180, 128, 26, 77, 129, 142)]
		[Arguments(CompOp.DstOut, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.DstOut, 10, 200, 60, 255, 100, 150, 250, 128, 255, 5, 100, 30, 127)]
		[Arguments(CompOp.DstOut, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.DstOut, 30, 90, 150, 160, 255, 255, 255, 90, 77, 27, 80, 134, 143)]
		[Arguments(CompOp.SrcAtop, 40, 120, 200, 220, 200, 100, 50, 180, 255, 133, 97, 89, 220)]
		[Arguments(CompOp.SrcAtop, 40, 120, 200, 220, 200, 100, 50, 180, 128, 87, 108, 144, 220)]
		[Arguments(CompOp.SrcAtop, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.SrcAtop, 10, 200, 60, 255, 100, 150, 250, 128, 255, 55, 175, 155, 255)]
		[Arguments(CompOp.SrcAtop, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 0)]
		[Arguments(CompOp.SrcAtop, 30, 90, 150, 160, 255, 255, 255, 90, 77, 44, 97, 151, 160)]
		[Arguments(CompOp.DstAtop, 40, 120, 200, 220, 200, 100, 50, 180, 255, 48, 94, 146, 180)]
		[Arguments(CompOp.DstAtop, 40, 120, 200, 220, 200, 100, 50, 180, 128, 44, 107, 173, 200)]
		[Arguments(CompOp.DstAtop, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.DstAtop, 10, 200, 60, 255, 100, 150, 250, 128, 255, 5, 100, 30, 128)]
		[Arguments(CompOp.DstAtop, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.DstAtop, 30, 90, 150, 160, 255, 255, 255, 90, 77, 34, 83, 131, 139)]
		[Arguments(CompOp.Xor, 40, 120, 200, 220, 200, 100, 50, 180, 255, 31, 45, 64, 89)]
		[Arguments(CompOp.Xor, 40, 120, 200, 220, 200, 100, 50, 180, 128, 36, 82, 132, 154)]
		[Arguments(CompOp.Xor, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 0)]
		[Arguments(CompOp.Xor, 10, 200, 60, 255, 100, 150, 250, 128, 255, 5, 100, 30, 127)]
		[Arguments(CompOp.Xor, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Xor, 30, 90, 150, 160, 255, 255, 255, 90, 77, 37, 91, 144, 153)]
		[Arguments(CompOp.Plus, 40, 120, 200, 220, 200, 100, 50, 180, 255, 181, 191, 235, 255)]
		[Arguments(CompOp.Plus, 40, 120, 200, 220, 200, 100, 50, 180, 128, 111, 156, 218, 255)]
		[Arguments(CompOp.Plus, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Plus, 10, 200, 60, 255, 100, 150, 250, 128, 255, 60, 255, 185, 255)]
		[Arguments(CompOp.Plus, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Plus, 30, 90, 150, 160, 255, 255, 255, 90, 77, 57, 117, 177, 187)]
		[Arguments(CompOp.Multiply, 40, 120, 200, 220, 200, 100, 50, 180, 255, 53, 78, 91, 245)]
		[Arguments(CompOp.Multiply, 40, 120, 200, 220, 200, 100, 50, 180, 128, 47, 99, 145, 232)]
		[Arguments(CompOp.Multiply, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.Multiply, 10, 200, 60, 255, 100, 150, 250, 128, 255, 7, 158, 59, 255)]
		[Arguments(CompOp.Multiply, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Multiply, 30, 90, 150, 160, 255, 255, 255, 90, 77, 40, 100, 160, 170)]
		[Arguments(CompOp.Screen, 40, 120, 200, 220, 200, 100, 50, 180, 255, 159, 158, 208, 245)]
		[Arguments(CompOp.Screen, 40, 120, 200, 220, 200, 100, 50, 180, 128, 100, 139, 204, 232)]
		[Arguments(CompOp.Screen, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Screen, 10, 200, 60, 255, 100, 150, 250, 128, 255, 58, 216, 156, 255)]
		[Arguments(CompOp.Screen, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Screen, 30, 90, 150, 160, 255, 255, 255, 90, 77, 54, 108, 161, 170)]
		[Arguments(CompOp.Overlay, 40, 120, 200, 220, 200, 100, 50, 180, 255, 75, 115, 196, 245)]
		[Arguments(CompOp.Overlay, 40, 120, 200, 220, 200, 100, 50, 180, 128, 58, 117, 198, 232)]
		[Arguments(CompOp.Overlay, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Overlay, 10, 200, 60, 255, 100, 150, 250, 128, 255, 9, 205, 89, 255)]
		[Arguments(CompOp.Overlay, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Overlay, 30, 90, 150, 160, 255, 255, 255, 90, 77, 43, 108, 161, 170)]
		[Arguments(CompOp.Darken, 40, 120, 200, 220, 200, 100, 50, 180, 255, 59, 106, 94, 245)]
		[Arguments(CompOp.Darken, 40, 120, 200, 220, 200, 100, 50, 180, 128, 50, 113, 147, 232)]
		[Arguments(CompOp.Darken, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.Darken, 10, 200, 60, 255, 100, 150, 250, 128, 255, 10, 175, 60, 255)]
		[Arguments(CompOp.Darken, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Darken, 30, 90, 150, 160, 255, 255, 255, 90, 77, 40, 100, 160, 170)]
		[Arguments(CompOp.Lighten, 40, 120, 200, 220, 200, 100, 50, 180, 255, 153, 130, 205, 245)]
		[Arguments(CompOp.Lighten, 40, 120, 200, 220, 200, 100, 50, 180, 128, 97, 125, 202, 232)]
		[Arguments(CompOp.Lighten, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Lighten, 10, 200, 60, 255, 100, 150, 250, 128, 255, 55, 200, 155, 255)]
		[Arguments(CompOp.Lighten, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Lighten, 30, 90, 150, 160, 255, 255, 255, 90, 77, 54, 108, 161, 170)]
		[Arguments(CompOp.ColorDodge, 40, 120, 200, 220, 200, 100, 50, 180, 255, 161, 185, 219, 245)]
		[Arguments(CompOp.ColorDodge, 40, 120, 200, 220, 200, 100, 50, 180, 128, 101, 153, 209, 232)]
		[Arguments(CompOp.ColorDodge, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.ColorDodge, 10, 200, 60, 255, 100, 150, 250, 128, 255, 13, 228, 158, 255)]
		[Arguments(CompOp.ColorDodge, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.ColorDodge, 30, 90, 150, 160, 255, 255, 255, 90, 77, 54, 108, 161, 170)]
		[Arguments(CompOp.ColorBurn, 40, 120, 200, 220, 200, 100, 50, 180, 255, 31, 45, 146, 245)]
		[Arguments(CompOp.ColorBurn, 40, 120, 200, 220, 200, 100, 50, 180, 128, 36, 82, 173, 232)]
		[Arguments(CompOp.ColorBurn, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.ColorBurn, 10, 200, 60, 255, 100, 150, 250, 128, 255, 5, 180, 58, 255)]
		[Arguments(CompOp.ColorBurn, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.ColorBurn, 30, 90, 150, 160, 255, 255, 255, 90, 77, 40, 100, 160, 170)]
		[Arguments(CompOp.HardLight, 40, 120, 200, 220, 200, 100, 50, 180, 255, 131, 112, 119, 245)]
		[Arguments(CompOp.HardLight, 40, 120, 200, 220, 200, 100, 50, 180, 128, 86, 116, 159, 232)]
		[Arguments(CompOp.HardLight, 255, 255, 255, 255, 0, 0, 0, 255, 255, 0, 0, 0, 255)]
		[Arguments(CompOp.HardLight, 10, 200, 60, 255, 100, 150, 250, 128, 255, 9, 205, 153, 255)]
		[Arguments(CompOp.HardLight, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.HardLight, 30, 90, 150, 160, 255, 255, 255, 90, 77, 54, 108, 161, 170)]
		[Arguments(CompOp.SoftLight, 40, 120, 200, 220, 200, 100, 50, 180, 255, 82, 127, 202, 245)]
		[Arguments(CompOp.SoftLight, 40, 120, 200, 220, 200, 100, 50, 180, 128, 64, 125, 202, 232)]
		[Arguments(CompOp.SoftLight, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.SoftLight, 10, 200, 60, 255, 100, 150, 250, 128, 255, 10, 205, 112, 255)]
		[Arguments(CompOp.SoftLight, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.SoftLight, 30, 90, 150, 160, 255, 255, 255, 90, 77, 44, 103, 163, 170)]
		[Arguments(CompOp.Difference, 40, 120, 200, 220, 200, 100, 50, 180, 255, 125, 68, 175, 245)]
		[Arguments(CompOp.Difference, 40, 120, 200, 220, 200, 100, 50, 180, 128, 82, 94, 187, 232)]
		[Arguments(CompOp.Difference, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Difference, 10, 200, 60, 255, 100, 150, 250, 128, 255, 50, 125, 125, 255)]
		[Arguments(CompOp.Difference, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Difference, 30, 90, 150, 160, 255, 255, 255, 90, 77, 51, 98, 145, 170)]
		[Arguments(CompOp.Exclusion, 40, 120, 200, 220, 200, 100, 50, 180, 255, 137, 124, 180, 245)]
		[Arguments(CompOp.Exclusion, 40, 120, 200, 220, 200, 100, 50, 180, 128, 89, 122, 190, 232)]
		[Arguments(CompOp.Exclusion, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Exclusion, 10, 200, 60, 255, 100, 150, 250, 128, 255, 56, 157, 126, 255)]
		[Arguments(CompOp.Exclusion, 0, 0, 0, 0, 200, 100, 50, 180, 200, 111, 56, 27, 141)]
		[Arguments(CompOp.Exclusion, 30, 90, 150, 160, 255, 255, 255, 90, 77, 51, 98, 145, 170)]
		[Arguments(CompOp.Minus, 40, 120, 200, 220, 200, 100, 50, 180, 255, 0, 49, 165, 245)]
		[Arguments(CompOp.Minus, 40, 120, 200, 220, 200, 100, 50, 180, 128, 0, 84, 182, 232)]
		[Arguments(CompOp.Minus, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255)]
		[Arguments(CompOp.Minus, 10, 200, 60, 255, 100, 150, 250, 128, 255, 0, 125, 0, 255)]
		[Arguments(CompOp.Minus, 0, 0, 0, 0, 200, 100, 50, 180, 200, 0, 0, 0, 141)]
		[Arguments(CompOp.Minus, 30, 90, 150, 160, 255, 255, 255, 90, 77, 3, 63, 123, 170)]
		public async Task BlendMatchesCppAgg(CompOp op, int dr, int dg, int db, int da, int sr, int sg, int sb, int sa, int cover, int er, int eg, int eb, int ea)
		{
			var buffer = new byte[4];
			buffer[ImageBuffer.OrderR] = (byte)dr;
			buffer[ImageBuffer.OrderG] = (byte)dg;
			buffer[ImageBuffer.OrderB] = (byte)db;
			buffer[ImageBuffer.OrderA] = (byte)da;

			BlenderCompOpBGRA.BlendPix(op, buffer, 0, new Color(sr, sg, sb, sa), cover);

			var result = new Color(buffer[ImageBuffer.OrderR], buffer[ImageBuffer.OrderG], buffer[ImageBuffer.OrderB], buffer[ImageBuffer.OrderA]);
			await Assert.That(result).IsEqualTo(new Color(er, eg, eb, ea));
		}

		/// <summary>A span goes through the blender's operator pixel by pixel, each at its own cover.</summary>
		[Test]
		public async Task BlendPixelsUsesTheOperatorAndEachCover()
		{
			var blender = new BlenderCompOpBGRA(CompOp.Difference);
			var buffer = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
			var colors = new[] { new Color(0, 0, 255, 255), new Color(0, 0, 255, 255) };
			var covers = new byte[] { 255, 0 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, false, 2);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(255, 255, 0, 255));
			await Assert.That(blender.PixelToColor(buffer, 4)).IsEqualTo(new Color(255, 255, 255, 255));
		}
	}
}
