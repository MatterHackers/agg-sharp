/*
Copyright (c) 2014, Lars Brubaker
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

	public class FlowLeftRightWithWrapping : FlowLayoutWidget
	{
		protected List<GuiWidget> addedChildren = new List<GuiWidget>();

		public BorderDouble RowMargin { get; set; } = new BorderDouble(3, 0);

		public BorderDouble RowPadding { get; set; } = new BorderDouble(3);
		public double MaxLineWidth { get; private set; }

		public BorderDouble RowBoarder { get; set; }

		public Color RowBoarderColor { get; set; }

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
				Parent.BoundsChanged += Parent_BoundsChanged;
				// Make sure we always do a layout regardless of having a layout event or a draw.
				DoWrappingLayout();
			}
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);
		}

		bool doingLayout = false;
		double oldWidth = 0;

		private void Parent_BoundsChanged(object sender, EventArgs e)
		{
			var parent = Parent;
			if (parent != null
				&& parent.Width != oldWidth)
			{
				if (!doingLayout)
				{
					DoWrappingLayout();
				}

				oldWidth = parent.Width;
			}
		}

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			addedChildren.Add(childToAdd);

			return childToAdd;
		}

		private bool needAnotherLayout;

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

				// add in new row container
				FlowLayoutWidget childContainerRow = new FlowLayoutWidget()
				{
					Margin = RowMargin,
					Padding = RowPadding,
					HAnchor = HAnchor.Stretch,
				};
				base.AddChild(childContainerRow);
				var rowPaddingWidth = RowPadding.Width;

				double runningSize = 0;
				MaxLineWidth = 0;
				foreach (var child in addedChildren)
				{
					var childWidth = child.Width + child.DeviceMarginAndBorder.Width;
					if(child.HAnchor == HAnchor.Stretch)
					{
						childWidth = child.MinimumSize.X + child.DeviceMarginAndBorder.Width;
					}

					if (Parent != null
						&& (runningSize + childWidth > Parent.Width - rowPaddingWidth
							|| child is IHardBreak))
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

						childContainerRow = new FlowLayoutWidget()
						{
							Margin = RowMargin,
							Padding = RowPadding,
							HAnchor = HAnchor.Stretch,
							Border = RowBoarder,
							BorderColor = RowBoarderColor,
						};

						if (lastItemWasHorizontalSpacer)
						{
							childContainerRow.AddChild(new HorizontalSpacer());
						}

						base.AddChild(childContainerRow);
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

				doingLayout = false;
			}

			if (needAnotherLayout)
			{
				UiThread.RunOnIdle(DoWrappingLayout);
				needAnotherLayout = false;
			}

			this.PerformLayout();
		}
	}
}