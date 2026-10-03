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

using System;
using System.Collections.Generic;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The list a <see cref="TextSuggestionController"/> shows below the word being completed: label on the left, detail right
	/// aligned and dimmer, the highlighted row's description in a footer line, and at most
	/// <see cref="MaxVisibleRows"/> rows with the rest reached by the highlight or the wheel.
	/// </summary>
	/// <remarks>
	/// It draws its own rows rather than holding a widget per row in a <see cref="ScrollableWidget"/>: nothing in
	/// it may take the focus, and the keys that move through it arrive at the field, never here.
	/// <para>
	/// A click on it leaves the focus in the field because of where it lives: it is a child of the outermost
	/// window, which has no parent and so never grabs the focus for itself, and a press one child accepts is not
	/// offered to its siblings, so the branch holding the field is never unfocused. Parented anywhere lower, the
	/// press would unfocus the field (firing its EditComplete) before the click could accept.
	/// </para>
	/// </remarks>
	public class TextSuggestionPopup : GuiWidget
	{
		public const int MaxVisibleRows = 8;

		private readonly TextSuggestionController controller;
		private readonly ThemeConfig theme;
		private readonly AnchoredPopup placement = new AnchoredPopup() { Gap = 2 };
		private readonly List<GuiWidget> trackedAncestors = new List<GuiWidget>();
		private GuiWidget trackedHost;
		private int firstVisibleRow;

		internal TextSuggestionPopup(TextSuggestionController controller, ThemeConfig theme)
		{
			this.controller = controller;
			this.theme = theme;
			this.Name = "Text Suggestions";
			this.Visible = false;
		}

		/// <summary>The name a row showing <paramref name="label"/> answers to in a widget search: "[red] Suggestion".</summary>
		public static string RowName(string label) => label + " Suggestion";

		/// <summary>Never focusable, so a press on the list does not take the focus off the field.</summary>
		public override bool CanFocus => false;

		public int VisibleRowCount => Math.Min(MaxVisibleRows, this.controller.Suggestions.Suggestions.Count);

		private double RowHeight => this.theme.MenuRowHeight;

		private double Pad => 3 * DeviceScale;

		private double PointSize => this.theme.DefaultFontSize;

		private bool HasFooter => !string.IsNullOrEmpty(this.HighlightedDescription);

		private string HighlightedDescription
		{
			get
			{
				var suggestions = this.controller.Suggestions.Suggestions;
				int index = this.controller.HighlightIndex;
				return index >= 0 && index < suggestions.Count ? suggestions[index].Description : null;
			}
		}

		/// <summary>The bounds of suggestion <paramref name="index"/>'s row in this widget's coordinates; empty when scrolled out of view.</summary>
		public RectangleDouble RowBounds(int index)
		{
			int slot = index - this.firstVisibleRow;
			if (slot < 0 || slot >= this.VisibleRowCount)
			{
				return default;
			}

			double top = this.LocalBounds.Top - this.Pad - (slot * this.RowHeight);
			return new RectangleDouble(this.Pad, top - this.RowHeight, this.Width - this.Pad, top);
		}

		/// <summary>Parents the list to the field's window, topmost, and places it.</summary>
		internal void Show(TextEditWidget field)
		{
			GuiWidget host = field.PopupHostWindow();
			if (host == null)
			{
				// Not in a SystemWindow (a headless tree): the outermost widget is the whole space there is.
				host = field;
				while (host.Parent != null)
				{
					host = host.Parent;
				}
			}

			// Last child draws on top and is offered presses first.
			if (this.Parent != host
				|| host.Children[host.Children.Count - 1] != this)
			{
				this.Parent?.RemoveChild(this);
				host.AddChild(this);
			}

			this.firstVisibleRow = 0;
			this.Visible = true;
			this.TrackField(field, host);
			this.Place();
		}

		/// <summary>Hides the list and stops following the field.</summary>
		internal void Hide()
		{
			this.Untrack();
			this.Visible = false;
		}

		/// <summary>
		/// Nothing moves the list with the field - it is the window's child, not the field's - so it watches what can
		/// move the field. A window resize (which in y-up agg moves every top-anchored field) or any ancestor moving
		/// places it again. A scroll of a <see cref="ScrollableWidget"/> above the field closes it instead: the list
		/// is drawn over the whole window, unclipped by the scroll area, so following the field out of view would
		/// leave it floating over unrelated content.
		/// </summary>
		private void TrackField(GuiWidget field, GuiWidget host)
		{
			this.Untrack();
			this.trackedHost = host;
			host.BoundsChanged += this.Tracked_Moved;
			for (var widget = field; widget != null && widget != host; widget = widget.Parent)
			{
				this.trackedAncestors.Add(widget);
				widget.PositionChanged += this.Tracked_Moved;
				if (widget is ScrollableWidget scrollable)
				{
					scrollable.ScrollPositionChanged += this.Tracked_Scrolled;
				}
			}
		}

		private void Untrack()
		{
			if (this.trackedHost != null)
			{
				this.trackedHost.BoundsChanged -= this.Tracked_Moved;
				this.trackedHost = null;
			}

			foreach (var widget in this.trackedAncestors)
			{
				widget.PositionChanged -= this.Tracked_Moved;
				if (widget is ScrollableWidget scrollable)
				{
					scrollable.ScrollPositionChanged -= this.Tracked_Scrolled;
				}
			}

			this.trackedAncestors.Clear();
		}

		private void Tracked_Moved(object sender, EventArgs e)
		{
			if (this.Visible)
			{
				this.Place();
			}
		}

		private void Tracked_Scrolled(object sender, EventArgs e)
		{
			this.controller.Close();
		}

		internal void ScrollHighlightIntoView()
		{
			int highlight = this.controller.HighlightIndex;
			if (highlight < this.firstVisibleRow)
			{
				this.firstVisibleRow = highlight;
			}
			else if (highlight >= this.firstVisibleRow + this.VisibleRowCount)
			{
				this.firstVisibleRow = highlight - this.VisibleRowCount + 1;
			}

			// The footer comes and goes with the highlighted row's description.
			this.Place();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var bounds = this.LocalBounds;
			double radius = this.theme.MenuRowRadius;
			graphics2D.Render(new RoundedRect(bounds, radius), this.theme.BackgroundColor);
			graphics2D.Render(new Stroke(new RoundedRect(bounds, radius), DeviceScale), this.theme.TextColor.WithAlpha(60));

			var suggestions = this.controller.Suggestions.Suggestions;
			Color dim = this.theme.TextColor.WithAlpha(150);
			for (int i = this.firstVisibleRow; i < this.firstVisibleRow + this.VisibleRowCount; i++)
			{
				var row = this.RowBounds(i);
				if (i == this.controller.HighlightIndex)
				{
					graphics2D.Render(new RoundedRect(row, radius), this.theme.AccentMimimalOverlay);
				}

				double baseline = row.Bottom + (row.Height - (this.PointSize * DeviceScale)) / 2;
				var label = new TypeFacePrinter(suggestions[i].Label, this.PointSize * DeviceScale, new Vector2(row.Left + this.Pad, baseline));
				label.Render(graphics2D, this.theme.TextColor);
				if (!string.IsNullOrEmpty(suggestions[i].Detail))
				{
					double detailWidth = Measure(suggestions[i].Detail, this.PointSize);
					double detailLeft = row.Right - this.Pad - detailWidth;

					// A detail that would run into the label is left out rather than drawn over it.
					if (detailLeft > row.Left + this.Pad + label.LocalBounds.Width + this.Pad)
					{
						new TypeFacePrinter(suggestions[i].Detail, this.PointSize * DeviceScale, new Vector2(detailLeft, baseline)).Render(graphics2D, dim);
					}
				}
			}

			if (this.HasFooter)
			{
				double footerTop = this.Pad + this.RowHeight;
				graphics2D.Line(this.Pad, footerTop, this.Width - this.Pad, footerTop, this.theme.TextColor.WithAlpha(40));
				double baseline = this.Pad + (this.RowHeight - (this.PointSize * DeviceScale)) / 2;
				new TypeFacePrinter(this.HighlightedDescription, this.PointSize * DeviceScale, new Vector2(2 * this.Pad, baseline)).Render(graphics2D, dim);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			for (int i = this.firstVisibleRow; i < this.firstVisibleRow + this.VisibleRowCount; i++)
			{
				if (this.RowBounds(i).Contains(mouseEvent.Position))
				{
					this.controller.Accept(i);
					return;
				}
			}
		}

		/// <summary>
		/// Reports each row in view under its <see cref="RowName"/>, positioned at the row's center: the rows are
		/// drawn rather than widgets, and this is how an automation test finds one to click.
		/// </summary>
		public override List<WidgetAndPosition> FindDescendants(IEnumerable<string> widgetNames, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType searchType, bool allowDisabledOrHidden = true)
		{
			if (this.Visible)
			{
				var suggestions = this.controller.Suggestions.Suggestions;
				for (int i = this.firstVisibleRow; i < this.firstVisibleRow + this.VisibleRowCount; i++)
				{
					string rowName = RowName(suggestions[i].Label);
					foreach (var widgetName in widgetNames)
					{
						if (searchType == SearchType.Exact ? rowName == widgetName : rowName.Contains(widgetName))
						{
							var row = this.RowBounds(i);

							// IntersectWithRectangle clips the rectangle it is called on, so it gets a copy.
							var touching = touchingBounds;
							if (touching.IntersectWithRectangle(row))
							{
								foundChildren.Add(new WidgetAndPosition(this, new Point2D((int)row.Center.X, (int)row.Center.Y), rowName, suggestions[i]));
							}

							break;
						}
					}
				}
			}

			return base.FindDescendants(widgetNames, foundChildren, touchingBounds, searchType, allowDisabledOrHidden);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			int maxFirst = this.controller.Suggestions.Suggestions.Count - this.VisibleRowCount;
			this.firstVisibleRow = Math.Max(0, Math.Min(maxFirst, this.firstVisibleRow - Math.Sign(mouseEvent.WheelDelta) * 3));

			// Enter, Tab and the footer act on the highlighted row, so it comes along to the nearest row in view
			// rather than staying behind, out of sight.
			int highlight = this.controller.HighlightIndex;
			int lastVisibleRow = this.firstVisibleRow + this.VisibleRowCount - 1;
			int clamped = Math.Max(this.firstVisibleRow, Math.Min(highlight, lastVisibleRow));
			if (clamped != highlight)
			{
				// in view already, so this only re-places for the footer
				this.controller.MoveHighlight(clamped - highlight, wrap: false);
			}

			this.Invalidate();
			mouseEvent.WheelDelta = 0;
		}

		private static double Measure(string text, double pointSize)
		{
			return new TypeFacePrinter(text, pointSize * DeviceScale).LocalBounds.Width;
		}

		/// <summary>Sizes the list for what it shows and hangs it below the text it replaces, clamped into the window.</summary>
		internal void Place()
		{
			if (this.Parent == null)
			{
				return;
			}

			this.Resize();
			this.placement.Anchor = this.controller.ReplaceStartBounds(this.Parent);
			this.placement.Size = this.LocalBounds.Size;
			var rect = this.placement.Rect(this.Parent.LocalBounds.Size);
			this.OriginRelativeParent = new Vector2(rect.Left, rect.Bottom);
			this.Invalidate();
		}

		private void Resize()
		{
			var suggestions = this.controller.Suggestions.Suggestions;
			double widest = 0;
			for (int i = 0; i < suggestions.Count; i++)
			{
				double rowWidth = Measure(suggestions[i].Label, this.PointSize);
				if (!string.IsNullOrEmpty(suggestions[i].Detail))
				{
					rowWidth += (6 * this.Pad) + Measure(suggestions[i].Detail, this.PointSize);
				}

				widest = Math.Max(widest, rowWidth);
			}

			if (this.HasFooter)
			{
				widest = Math.Max(widest, Measure(this.HighlightedDescription, this.PointSize));
			}

			double width = Math.Min(Math.Max(widest + (4 * this.Pad), 150 * DeviceScale), 480 * DeviceScale);
			double height = (this.VisibleRowCount + (this.HasFooter ? 1 : 0)) * this.RowHeight + (2 * this.Pad);
			this.LocalBounds = new RectangleDouble(0, 0, width, height);
		}
	}
}
