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

using MatterHackers.Agg.Font;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public interface IHardBreak
	{
	}

	public interface ISkipIfFirst
	{
	}

	public interface IWrapChildrenSeparatly
	{
	}

	public class HardBreak : GuiWidget, IHardBreak
	{
		public HardBreak()
		{
			Width = 1;
			Height = 1;
		}
	}

	public class SkipIfFirstSpace : TextWidget, ISkipIfFirst
	{
		public SkipIfFirstSpace(double x = 0, double y = 0, double pointSize = 12, Justification justification = Justification.Left, Color textColor = default(Color), bool ellipsisIfClipped = true, bool underline = false, Color backgroundColor = default(Color), TypeFace typeFace = null, bool bold = false)
			: base(" ", x, y, pointSize, justification, textColor, ellipsisIfClipped, underline, backgroundColor, typeFace, bold)
		{

		}
	}

	public class FlowLeftRightWithWrapping : FlowLayoutWidget
	{
		protected SafeList<GuiWidget> addedChildren = new SafeList<GuiWidget>();

		public BorderDouble RowMargin { get; set; } = new BorderDouble(3, 0);

		public BorderDouble RowPadding { get; set; } = new BorderDouble(3);
		public double MaxLineWidth { get; private set; }

		public BorderDouble RowBorder { get; set; }

		public Color RowBorderColor { get; set; }

		public bool Proportional { get; set; }

		public bool Center { get; set; }

		private HAnchor contentHAnchor = HAnchor.Left;
		public HAnchor ContentHAnchor
		{
			get => contentHAnchor;
			set
			{
				if (contentHAnchor == value)
				{
					return;
				}

				contentHAnchor = value;
				if (Parent != null)
				{
					DoWrappingLayout();
				}
			}
		}

        public FlowLeftRightWithWrapping()
			: base(FlowDirection.TopToBottom)
		{
			HAnchor = HAnchor.Stretch;
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);

			if (Parent != null)
			{
				// Make sure we always do a layout regardless of having a layout event or a draw.
				DoWrappingLayout();
			}
		}

		/// <summary>
		/// Wraps again whenever this flow's own width changes. The parent stretches the flow after its own
		/// bounds change and after the flow is parented, so wrapping on those events read a stale width.
		/// </summary>
		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);

			// The first stretch usually lands while a wrap settles (its height change lays out the parent),
			// so a wrap may start one more against the width it was just given. A flow whose width fits its
			// content could keep changing width that way, so nesting stops after one re-wrap.
			if (!doingLayout
				&& nestedWraps < 2
				&& Parent != null
				&& Width != wrappedWidth)
			{
				DoWrappingLayout();
			}
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);
		}

		bool doingLayout = false;

		// how many wraps are running, one inside another's settle
		private int nestedWraps;

		// the width the rows were last wrapped against
		private double wrappedWidth = double.NaN;

        public void AddText(string text, Color textColor, int pointSize)
        {
			var firstLine = true;
            foreach(var line in text.Split('\n'))
            {
				if (!firstLine)
                {
					this.AddChild(new HardBreak());
                }

				var firstWord = true;
				foreach(var word in line.Split(' '))
                {
					if (!firstWord)
                    {
						this.AddChild(new SkipIfFirstSpace(pointSize: pointSize, textColor: textColor));
                    }

					if (word != " ")
					{
						this.AddChild(new TextWidget(word, pointSize: pointSize, textColor: textColor));
					}
					firstWord = false;
				}

				firstLine = false;
			}
        }

        public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is IWrapChildrenSeparatly)
			{
				foreach(var child in childToAdd.Children)
                {
					addedChildren.Add(child);
                }
			}
			else
			{
				addedChildren.Add(childToAdd);
			}

			return childToAdd;
		}

		private bool needAnotherLayout;

		/// <summary>
		/// How wide one row holding every item would be, in device pixels: each item's width and margin laid end
		/// to end, without the row's own chrome. Computed from the items when asked, so it is right before the flow
		/// has ever wrapped - a host sizing its window to fit the flow on one row asks before any layout.
		/// </summary>
		public double ContentWidth => addedChildren.Sum(ItemWidth);

		/// <summary>The room <paramref name="child"/> takes in a row: a stretching item counts at its minimum.</summary>
		private static double ItemWidth(GuiWidget child)
		{
			var width = child.HAnchor == HAnchor.Stretch ? child.MinimumSize.X : child.Width;
			return width + child.DeviceMarginAndBorder.Width;
		}

		protected void DoWrappingLayout()
		{
			if (doingLayout)
			{
				needAnotherLayout = true;
				return;
			}

			using (this.LayoutLock())
			{
				doingLayout = true;
				wrappedWidth = Width;
				// remove all the children we added
				foreach (var child in addedChildren)
				{
					if (child.Parent != null)
					{
						using (child.Parent.LayoutLock())
						{
							child.Parent.RemoveChild(child);
							child.ClearRemovedFlag();
						}
					}
				}

				// close all the row containers
				this.CloseChildren();

				// add in new row container; the first row has no RowBorder, so a top-only border separates rows
				FlowLayoutWidget childContainerRow = NewRow();
				base.AddChild(childContainerRow);
				var rowPaddingWidth = RowChromeWidth(childContainerRow);

				double runningSize = 0;
				MaxLineWidth = 0;
				foreach (var child in addedChildren)
				{
					var childWidth = ItemWidth(child);

					if (runningSize + childWidth > this.Width - rowPaddingWidth
						|| child is IHardBreak)
					{
						MaxLineWidth = Math.Max(MaxLineWidth, runningSize);
						runningSize = 0;
						var lastItemWasHorizontalSpacer = false;
						if (childContainerRow != null)
						{
							childContainerRow.PerformLayout();
							if (childContainerRow.Children.LastOrDefault() is HorizontalSpacer)
							{
								lastItemWasHorizontalSpacer = true;
							}
						}

						childContainerRow = NewRow();
						childContainerRow.Border = RowBorder;
						childContainerRow.BorderColor = RowBorderColor;

						if (lastItemWasHorizontalSpacer)
						{
							childContainerRow.AddChild(new HorizontalSpacer());
						}

						base.AddChild(childContainerRow);
						rowPaddingWidth = RowChromeWidth(childContainerRow);
					}

					if (runningSize > 0
						|| !(child is ISkipIfFirst))
					{
						// add the new child to the current row
						using (childContainerRow.LayoutLock())
						{
							childContainerRow.AddChild(child);
						}

						runningSize += childWidth;
						MaxLineWidth = Math.Max(MaxLineWidth, runningSize);
					}
				}

				if (childContainerRow != null)
				{
					childContainerRow.PerformLayout();
				}

				MakeProportionalIfRequired();
				AlignRowsIfRequired();

				doingLayout = false;
			}

			if (needAnotherLayout)
			{
				UiThread.RunOnIdle(DoWrappingLayout);
				needAnotherLayout = false;
			}

			nestedWraps++;
			try
			{
				// change the size to force a recursive layout event
				this.Height--;
				this.Height++;
				this.PerformLayout();
			}
			finally
			{
				nestedWraps--;
			}
		}

		private FlowLayoutWidget NewRow()
		{
			return new FlowLayoutWidget()
			{
				Margin = RowMargin,
				Padding = RowPadding,
				HAnchor = HAnchor.Stretch,
			};
		}

		/// <summary>
		/// The device-pixel width a row's items cannot use: the row's margin, border and padding and this flow's
		/// padding. This flow's margin is outside its <see cref="GuiWidget.Width"/>, so it takes nothing.
		/// </summary>
		private double RowChromeWidth(GuiWidget row)
		{
			return row.DeviceMarginAndBorder.Width + row.DevicePadding.Width + this.DevicePadding.Width;
		}

        private void MakeProportionalIfRequired()
        {
            if (Proportional)
            {
				foreach (var row in Children)
				{
					row.PerformLayout();
					var rowChildrenCount = row.Children.Count;
					var extraWidth = this.Width - row.GetChildrenBoundsIncludingMargins().Width - RowChromeWidth(row);
					if (extraWidth > rowChildrenCount)
					{
						// distribute the extra width between each child
						var extraMargin = extraWidth / (rowChildrenCount + 1);
						using (row.LayoutLock())
						{
							for (int i = rowChildrenCount; i >= 0; i--)
							{
								// add a spacer item between every row item
								row.AddChild(new GuiWidget(extraMargin, 2), i);
							}
						}

						row.PerformLayout();
					}
				}
            }
        }

		private void AlignRowsIfRequired()
		{
			var contentHAnchor = Center ? HAnchor.Center : ContentHAnchor;
			if ((contentHAnchor & (HAnchor.Center | HAnchor.Right)) != 0)
			{
				foreach (var row in Children)
				{
					row.PerformLayout();
					var rowChildrenCount = row.Children.Count;
					var extraWidth = this.Width - row.GetChildrenBoundsIncludingMargins().Width - RowChromeWidth(row);
					if (extraWidth > rowChildrenCount)
					{
						using (row.LayoutLock())
						{
							var leadingSpace = (contentHAnchor & HAnchor.Right) == HAnchor.Right
								? extraWidth
								: extraWidth / 2;
							row.AddChild(new GuiWidget(leadingSpace, 2), 0);
						}

						row.PerformLayout();
					}
				}
			}
		}
	}
}