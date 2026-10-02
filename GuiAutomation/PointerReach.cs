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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.GuiAutomation
{
	/// <summary>
	/// Whether a press aimed at a widget can get to it, as the mouse routes one: every ancestor is asked
	/// <see cref="GuiWidget.PositionWithinLocalBounds(double, double)"/> before the press goes into its children.
	/// </summary>
	/// <remarks>
	/// A widget can be drawn yet out of the pointer's reach - the GUI demo's closing window stays on the canvas
	/// while it fades out but lets every press through to what is under it. When several widgets share a name
	/// (each demo window has its own "Window Collapse Button") a click aimed at such a one lands on whatever is
	/// beneath and does nothing, and how long the fade lasts in frames decided whether a test met it: only a
	/// slow software-rendered CI frame left the closing windows up when the click came.
	/// </remarks>
	internal static class PointerReach
	{
		/// <summary>
		/// True when no ancestor of <paramref name="widget"/> refuses a press at its centre. The widget itself
		/// is not asked: a shaped control may answer false at its centre and still be clicked where it is drawn.
		/// </summary>
		internal static bool CanReach(GuiWidget widget)
		{
			Vector2 centre = widget.LocalBounds.Center;
			for (GuiWidget ancestor = widget.Parent; ancestor != null; ancestor = ancestor.Parent)
			{
				Vector2 inAncestor = widget.TransformToParentSpace(ancestor, centre);
				if (!ancestor.PositionWithinLocalBounds(inAncestor.X, inAncestor.Y))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// The results a click should choose among: those the pointer can reach, or all of them when it can reach
		/// none (the click then goes where it always did, rather than the lookup failing).
		/// </summary>
		internal static List<AutomationRunner.GetByNameResults> PreferReachable(List<AutomationRunner.GetByNameResults> results)
		{
			List<AutomationRunner.GetByNameResults> reachable = results.Where(result => CanReach(result.Widget)).ToList();
			return reachable.Count > 0 ? reachable : results;
		}
	}
}
