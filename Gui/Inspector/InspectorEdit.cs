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
	/// <summary>A field of a widget the inspector's properties pane can change.</summary>
	public enum InspectorEditField
	{
		Width,
		Height,
		MarginLeft,
		MarginBottom,
		MarginRight,
		MarginTop,
		PaddingLeft,
		PaddingBottom,
		PaddingRight,
		PaddingTop,
		HAnchor,
		VAnchor,
		Visible,
		Enabled,
	}

	/// <summary>
	/// One queued change to an inspected widget, after agg-gui's WidgetBaseEdit / InspectorEdit
	/// (widget/tree_inspector.rs). The properties pane makes these on a click; the panel queues them and applies them
	/// on the UI thread's next idle, not in the middle of the mouse event that made them, then refreshes the tree.
	/// </summary>
	/// <remarks>
	/// agg-gui names the target by its child-index path because Rust cannot hold a reference into the tree; here the
	/// edit holds the widget itself, so a tree that changed shape before the edit lands still edits the right widget.
	/// </remarks>
	public sealed class InspectorEdit
	{
		/// <param name="value">The new value: logical units for margin and padding, device pixels for width and
		/// height, the enum value for an anchor, and 0 or 1 for visible and enabled.</param>
		public InspectorEdit(GuiWidget widget, InspectorEditField field, double value)
		{
			this.Widget = widget;
			this.Field = field;
			this.Value = value;
		}

		public GuiWidget Widget { get; }

		public InspectorEditField Field { get; }

		public double Value { get; }

		/// <summary>The anchor after <paramref name="anchor"/> in agg-gui's picker cycle - Fit, Stretch, Left, Center,
		/// Right - then Absolute (which agg-gui does not have) and back to Fit.</summary>
		public static HAnchor NextHAnchor(HAnchor anchor)
		{
			switch (anchor)
			{
				case HAnchor.Fit: return HAnchor.Stretch;
				case HAnchor.Stretch: return HAnchor.Left;
				case HAnchor.Left: return HAnchor.Center;
				case HAnchor.Center: return HAnchor.Right;
				case HAnchor.Right: return HAnchor.Absolute;
				default: return HAnchor.Fit;
			}
		}

		/// <summary>The anchor after <paramref name="anchor"/> in agg-gui's picker cycle - Fit, Stretch, Bottom,
		/// Center, Top - then Absolute (which agg-gui does not have) and back to Fit.</summary>
		public static VAnchor NextVAnchor(VAnchor anchor)
		{
			switch (anchor)
			{
				case VAnchor.Fit: return VAnchor.Stretch;
				case VAnchor.Stretch: return VAnchor.Bottom;
				case VAnchor.Bottom: return VAnchor.Center;
				case VAnchor.Center: return VAnchor.Top;
				case VAnchor.Top: return VAnchor.Absolute;
				default: return VAnchor.Fit;
			}
		}

		/// <summary>Sets the field on the widget and lays its parent out again. Sizes, margins and paddings never go
		/// below zero. Returns false, changing nothing, when the widget has been closed since the edit was made.</summary>
		public bool Apply()
		{
			GuiWidget widget = this.Widget;
			if (widget == null || widget.HasBeenClosed)
			{
				return false;
			}

			double size = System.Math.Max(0, this.Value);
			BorderDouble margin = widget.Margin;
			BorderDouble padding = widget.Padding;
			switch (this.Field)
			{
				case InspectorEditField.Width: widget.Width = size; break;
				case InspectorEditField.Height: widget.Height = size; break;
				case InspectorEditField.MarginLeft: widget.Margin = new BorderDouble(margin, left: size); break;
				case InspectorEditField.MarginBottom: widget.Margin = new BorderDouble(margin, bottom: size); break;
				case InspectorEditField.MarginRight: widget.Margin = new BorderDouble(margin, right: size); break;
				case InspectorEditField.MarginTop: widget.Margin = new BorderDouble(margin, top: size); break;
				case InspectorEditField.PaddingLeft: widget.Padding = new BorderDouble(padding, left: size); break;
				case InspectorEditField.PaddingBottom: widget.Padding = new BorderDouble(padding, bottom: size); break;
				case InspectorEditField.PaddingRight: widget.Padding = new BorderDouble(padding, right: size); break;
				case InspectorEditField.PaddingTop: widget.Padding = new BorderDouble(padding, top: size); break;
				case InspectorEditField.HAnchor: widget.HAnchor = (HAnchor)(int)this.Value; break;
				case InspectorEditField.VAnchor: widget.VAnchor = (VAnchor)(int)this.Value; break;
				case InspectorEditField.Visible: widget.Visible = this.Value != 0; break;
				case InspectorEditField.Enabled: widget.Enabled = this.Value != 0; break;
			}

			// a margin, anchor or size moves the widget within its parent, so the parent lays out again
			widget.Parent?.PerformLayout();
			widget.Invalidate();
			return true;
		}
	}
}
