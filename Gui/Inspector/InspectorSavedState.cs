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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// What the inspector remembers between runs, as agg-gui's InspectorSavedState: each node's expansion and the
	/// selected node, both by position in the flat depth-first node list, and the properties pane's height.
	/// </summary>
	/// <remarks>
	/// Positions rather than widgets, because the next run builds new widgets. If the tree differs between runs the
	/// worst case is a few rows expanded or collapsed differently, never an error. Plain data an app can serialize.
	/// </remarks>
	public class InspectorSavedState
	{
		/// <summary>One flag per node of <see cref="InspectorModel.Nodes"/>, in order.</summary>
		public List<bool> Expanded { get; set; } = new List<bool>();

		/// <summary>The selected node's position in <see cref="InspectorModel.Nodes"/>; -1 for none.</summary>
		public int Selected { get; set; } = -1;

		/// <summary>agg-gui's props_h: the split's height above the panel bottom, in logical units.</summary>
		public double PropertiesHeight { get; set; } = InspectorPanel.DefaultPropertiesHeight;
	}
}
