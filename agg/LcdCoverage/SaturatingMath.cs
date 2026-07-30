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

using System;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Saturating double-to-int conversions and integer arithmetic, shared by every place the LCD
	/// pipeline turns a geometric bound into a pixel index.
	/// </summary>
	/// <remarks>
	/// The behaviour these mirror is Rust's <c>as i32</c> / <c>saturating_mul</c>: clamp at the type's
	/// limits. Two reasons to spell it out rather than lean on a bare cast:
	/// <list type="bullet">
	/// <item><description>What the bound <i>means</i> is load bearing. An "effectively unbounded" clip
	/// rect passed as <see cref="double.MaxValue"/> has to widen the region, not invert it; a bound that
	/// came out as <see cref="int.MinValue"/> would make the caller compute an empty box and paint nothing
	/// where the reference paints the on-screen portion. Naming the clamp makes that requirement visible
	/// at the call site.</description></item>
	/// <item><description>It is not free in C#. Out-of-range <c>double</c> to <c>int</c> casts do saturate
	/// on .NET Core 3.0 and later (and NaN maps to 0), which is what the current target framework does -
	/// but that was unspecified beforehand, on x86 .NET Framework yielding <see cref="int.MinValue"/> in
	/// <b>both</b> directions. And <see cref="MultiplyBy3"/> has no such luck at all: plain
	/// <c>int * 3</c> wraps silently.</description></item>
	/// </list>
	/// Every conversion of a geometric bound to a pixel index in this namespace goes through here.
	/// </remarks>
	internal static class SaturatingMath
	{
		/// <summary>Floor of <paramref name="value"/>, saturating at the <see cref="int"/> limits.</summary>
		internal static int Floor(double value)
		{
			return ToInt(Math.Floor(value));
		}

		/// <summary>Ceiling of <paramref name="value"/>, saturating at the <see cref="int"/> limits.</summary>
		internal static int Ceiling(double value)
		{
			return ToInt(Math.Ceiling(value));
		}

		/// <summary>
		/// Clamps <paramref name="value"/> into <see cref="int"/> range and casts. NaN maps to 0, since
		/// there is no meaningful pixel index for it and 0 keeps the caller's box merely empty rather
		/// than wildly wrong.
		/// </summary>
		internal static int ToInt(double value)
		{
			if (double.IsNaN(value))
			{
				return 0;
			}

			return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
		}

		/// <summary>
		/// Saturating <c>* 3</c>, matching the reference's <c>saturating_mul(3)</c> on the clip bounds.
		/// </summary>
		internal static int MultiplyBy3(int value)
		{
			return (int)Math.Clamp((long)value * 3L, int.MinValue, int.MaxValue);
		}
	}
}
