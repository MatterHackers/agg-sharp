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

namespace MatterHackers.Agg.UI
{
	/// <summary>The colours the inspector panel draws with. The defaults read on a dark background; an app sets its
	/// own through <see cref="InspectorPanel.Style"/>.</summary>
	public class InspectorStyle
	{
		public Color Background { get; set; } = new Color(27, 27, 27);

		/// <summary>agg-gui's header bar: the panel fill darkened, to 80% on a dark theme and 94% on a light one.</summary>
		public Color HeaderBackground { get; set; } = new Color(22, 22, 22);

		public Color Text { get; set; } = new Color(220, 220, 220);

		public Color DimText { get; set; } = new Color(140, 140, 140);

		public Color HoveredRow { get; set; } = new Color(255, 255, 255, 20);

		public Color SelectedRow { get; set; } = new Color(40, 110, 210, 110);

		public Color Separator { get; set; } = new Color(60, 60, 60);

		/// <summary>The strip behind an editable value with no tint of its own (width and height).</summary>
		public Color EditableValue { get; set; } = new Color(230, 140, 25, 40);

		/// <summary>agg-gui's margin tint, rgba(0.9, 0.55, 0.1, 0.15): the strip behind an editable margin.</summary>
		public Color MarginValue { get; set; } = new Color(230, 140, 25, 38);

		/// <summary>agg-gui's padding tint, rgba(0.1, 0.75, 0.3, 0.12): the strip behind a read-only padding.</summary>
		public Color PaddingValue { get; set; } = new Color(25, 191, 77, 31);

		/// <summary>agg-gui's anchor tint, rgba(0.2, 0.4, 0.9, 0.12): the strip behind an editable anchor.</summary>
		public Color AnchorValue { get; set; } = new Color(51, 102, 230, 31);

		/// <summary>agg-gui's colour for a true flag's value, rgb(0.10, 0.52, 0.10).</summary>
		public Color TrueValue { get; set; } = new Color(26, 133, 26);

		/// <summary>agg-gui's colour for a false flag's value, rgb(0.65, 0.18, 0.18).</summary>
		public Color FalseValue { get; set; } = new Color(166, 46, 46);
	}
}
