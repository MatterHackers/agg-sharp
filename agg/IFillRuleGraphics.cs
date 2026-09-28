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

namespace MatterHackers.Agg
{
	/// <summary>
	/// A <see cref="Graphics2D"/> with no <see cref="ScanlineRasterizer"/> to take a fill rule (the GPU) that can
	/// still fill by one. A software surface's rule is set on its rasterizer (<c>Rasterizer.filling_rule</c>), so a
	/// caller that wants a rule on either kind of surface sets it on whichever the surface has.
	/// </summary>
	public interface IFillRuleGraphics
	{
		/// <summary>
		/// The rule every following fill uses, until set again. Starts as <see cref="Util.filling_rule_e.fill_non_zero"/>,
		/// which every other fill assumes, so a caller that sets another puts it back when done - as with the rasterizer's.
		/// </summary>
		Util.filling_rule_e FillingRule { get; set; }
	}

	/// <summary>Sets a fill rule on whichever kind of surface a <see cref="Graphics2D"/> is.</summary>
	public static class FillRuleGraphicsExtensions
	{
		/// <summary>
		/// Sets <paramref name="rule"/> on the surface's rasterizer when it has one (software), else on its
		/// <see cref="IFillRuleGraphics"/> (the GPU); a surface with neither fills non-zero regardless.
		/// </summary>
		public static void SetFillingRule(this Graphics2D graphics, Util.filling_rule_e rule)
		{
			if (graphics.Rasterizer != null)
			{
				graphics.Rasterizer.filling_rule(rule);
			}
			else if (graphics is IFillRuleGraphics fillRuleGraphics)
			{
				fillRuleGraphics.FillingRule = rule;
			}
		}
	}
}
