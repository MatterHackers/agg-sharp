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
	/// <summary>
	/// A snapshot of one widget for the inspector, after agg-gui's InspectorNode (widget/tree_inspector.rs): what it
	/// is, where it is, and how it is laid out. Taken by <see cref="InspectorModel.Collect"/>; it does not follow the
	/// widget afterwards - <see cref="InspectorModel.Refresh"/> takes a new one.
	/// </summary>
	public sealed class InspectorNode
	{
		public InspectorNode(GuiWidget widget, GuiWidget root, int depth, int[] path)
		{
			this.Widget = widget;
			this.TypeName = widget.GetType().Name;
			this.Name = widget.Name ?? "";
			this.ScreenBounds = widget.TransformToParentSpace(root, widget.LocalBounds);
			this.Margin = widget.Margin;
			this.Padding = widget.Padding;
			this.DeviceMargin = widget.DeviceMargin;
			this.DevicePadding = widget.DevicePadding;
			this.HAnchor = widget.HAnchor;
			this.VAnchor = widget.VAnchor;
			this.Visible = widget.Visible;
			this.Enabled = widget.Enabled;
			this.ChildCount = widget.Children.Count;
			this.Depth = depth;
			this.Path = path;
		}

		/// <summary>The live widget the snapshot was taken of.</summary>
		public GuiWidget Widget { get; }

		/// <summary>The widget's class name, e.g. "TextWidget".</summary>
		public string TypeName { get; }

		/// <summary>The widget's <see cref="GuiWidget.Name"/>; empty when it has none.</summary>
		public string Name { get; }

		/// <summary>The widget's bounds in the inspected root's coordinates (y up, device pixels): where the overlay
		/// outlines it.</summary>
		public RectangleDouble ScreenBounds { get; }

		public BorderDouble Margin { get; }

		public BorderDouble Padding { get; }

		/// <summary>The margin in device pixels, the unit of <see cref="ScreenBounds"/>.</summary>
		public BorderDouble DeviceMargin { get; }

		/// <summary>The padding in device pixels, the unit of <see cref="ScreenBounds"/>.</summary>
		public BorderDouble DevicePadding { get; }

		public HAnchor HAnchor { get; }

		public VAnchor VAnchor { get; }

		/// <summary>The widget's own Visible flag (an ancestor may still hide it).</summary>
		public bool Visible { get; }

		public bool Enabled { get; }

		public int ChildCount { get; }

		/// <summary>0 for the root, 1 for its children, and so on.</summary>
		public int Depth { get; }

		/// <summary>Child indices from the root down to the widget; empty for the root.</summary>
		public int[] Path { get; }

		/// <summary>How the tree row reads: the type, then the name in quotes when there is one.</summary>
		public string Label => string.IsNullOrEmpty(this.Name) ? this.TypeName : $"{this.TypeName} \"{this.Name}\"";

		/// <summary>Whether <paramref name="other"/> shows the same widget with nothing the inspector displays changed.</summary>
		public bool SameAs(InspectorNode other)
		{
			return other != null
				&& other.Widget == this.Widget
				&& other.Name == this.Name
				&& other.ScreenBounds.Equals(this.ScreenBounds)
				&& other.Visible == this.Visible
				&& other.Enabled == this.Enabled
				&& other.ChildCount == this.ChildCount
				&& other.Depth == this.Depth
				&& other.Margin.Equals(this.Margin)
				&& other.Padding.Equals(this.Padding)
				&& other.HAnchor == this.HAnchor
				&& other.VAnchor == this.VAnchor;
		}
	}
}
