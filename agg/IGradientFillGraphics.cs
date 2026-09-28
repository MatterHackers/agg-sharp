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

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>A gradient's distance function - the C++ AGG gradient_* class it evaluates.</summary>
	public enum GradientShape
	{
		/// <summary>gradient_x: the x coordinate.</summary>
		X,

		/// <summary>gradient_y: the y coordinate.</summary>
		Y,

		/// <summary>gradient_radial / gradient_circle / gradient_radial_d: the distance from the origin.</summary>
		Radial,

		/// <summary>gradient_diamond: the larger of |x| and |y|.</summary>
		Diamond,

		/// <summary>gradient_xy: |x| * |y| / d2.</summary>
		XY,

		/// <summary>gradient_sqrt_xy: the square root of |x| * |y|.</summary>
		SqrtXY,

		/// <summary>gradient_conic: |atan2(y, x)| * d2 / pi.</summary>
		Conic,

		/// <summary>gradient_radial_focus: a radial gradient of <see cref="GradientFill.FocusRadius"/> seen from the focal point.</summary>
		RadialFocus,
	}

	/// <summary>What a gradient does past d2 - the C++ AGG adaptor wrapped around the gradient function.</summary>
	public enum GradientSpread
	{
		/// <summary>No adaptor: the index clamps to the first and last colours.</summary>
		Pad,

		/// <summary>gradient_repeat_adaptor.</summary>
		Repeat,

		/// <summary>gradient_reflect_adaptor.</summary>
		Reflect,
	}

	/// <summary>
	/// A C++ AGG span_gradient as data: the gradient function, its d1 .. d2 range, the colour lookup and the
	/// interpolator's matrix. A surface that cannot run span generators evaluates it per pixel instead.
	/// </summary>
	public sealed class GradientFill
	{
		/// <summary>Gets or sets the gradient function.</summary>
		public GradientShape Shape { get; set; }

		/// <summary>Gets or sets the adaptor around the gradient function.</summary>
		public GradientSpread Spread { get; set; }

		/// <summary>Gets or sets span_gradient's d1: the gradient value of the first colour.</summary>
		public double D1 { get; set; }

		/// <summary>Gets or sets span_gradient's d2: the gradient value past the last colour.</summary>
		public double D2 { get; set; } = 100;

		/// <summary>
		/// Gets or sets span_interpolator_linear's matrix: the target's pixels (row 0 at the bottom) to gradient space.
		/// The current transform does not move the gradient, as it does not move a pattern fill's image.
		/// </summary>
		public Affine ScreenToGradient { get; set; } = Affine.NewIdentity();

		/// <summary>Gets or sets the colour lookup, span_gradient's colour function; every one of its entries is used.</summary>
		public IColorFunction Colors { get; set; }

		/// <summary>Gets or sets <see cref="GradientShape.RadialFocus"/>'s radius.</summary>
		public double FocusRadius { get; set; } = 100;

		/// <summary>Gets or sets <see cref="GradientShape.RadialFocus"/>'s focal point x, relative to the gradient's centre.</summary>
		public double FocusX { get; set; }

		/// <summary>Gets or sets <see cref="GradientShape.RadialFocus"/>'s focal point y, relative to the gradient's centre.</summary>
		public double FocusY { get; set; }
	}

	/// <summary>
	/// A <see cref="Graphics2D"/> that can fill a path with a gradient - C++ AGG's span_gradient fill. The software
	/// surface runs the span generators themselves; the GPU evaluates the same gradient per pixel in a shader, so a
	/// caller need not branch on which surface it has.
	/// </summary>
	public interface IGradientFillGraphics
	{
		/// <summary>
		/// Fills <paramref name="path"/> (in the current transform, like any other fill, and anti-aliased) with
		/// <paramref name="gradient"/>, source-over.
		/// </summary>
		/// <param name="path">The shape to fill.</param>
		/// <param name="gradient">The colours, as span_gradient computes them.</param>
		/// <param name="alphaGradient">When given, each pixel's alpha is replaced by this gradient's colour alpha - C++
		/// span_gradient_alpha run over span_gradient's colours (alpha_gradient's span_conv).</param>
		void FillPathWithGradient(IVertexSource path, GradientFill gradient, GradientFill alphaGradient = null);
	}
}
