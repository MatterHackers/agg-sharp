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

using MatterHackers.VectorMath;
using System;
using System.IO;
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
    public class ToolTipManager
    {
        GuiWidget toolTipWidget;
        Vector2 mousePosition;
        SystemWindow owner;
		GuiWidget widgetThatIsShowingToolTip;
		GuiWidget widgetThatWantsToShowToolTip;
        Stopwatch timeSinceLastMouseMove = new Stopwatch();
        Stopwatch timeCurrentToolTipHasBeenShowing = new Stopwatch();
		Stopwatch timeSinceMouseOver = new Stopwatch();
		Stopwatch timeSinceLastToolTipClose = new Stopwatch();

        /// <summary>
        /// Gets or sets the period of time the ToolTip remains visible if the pointer is stationary on a control with specified ToolTip text.
        /// </summary>
		public double AutoPopDelay = 5;
		private double CurrentAutoPopDelay = 5;
        /// <summary>
        /// Gets or sets the time that passes before the ToolTip appears.
        /// </summary>
        public double InitialDelay = 1;
        /// <summary>
        /// Gets or sets the length of time that must transpire before subsequent ToolTip windows appear as the pointer moves from one control to another.
        /// </summary>
        public double ReshowDelay = .2;

        internal ToolTipManager(SystemWindow owner)
        {
            this.owner = owner;
            owner.MouseMove += (sender, e) =>
            {
                mousePosition = e.Position;
                timeSinceLastMouseMove.Restart();
            };

			timeSinceLastToolTipClose.Start();
		
			// Get the an idle loop up and running
			UiThread.RunOnIdle(CheckIfNeedToDisplayToolTip, .02);
		}

        private void CheckIfNeedToDisplayToolTip()
        {
			double showDelayTime = InitialDelay;
			if (timeSinceLastToolTipClose.Elapsed.TotalSeconds < InitialDelay
				&& widgetThatWantsToShowToolTip != null
				&& widgetThatIsShowingToolTip == null)
			{
				//showDelayTime = ReshowDelay;
			}

			if (widgetThatWantsToShowToolTip != null
				&& widgetThatWantsToShowToolTip != widgetThatIsShowingToolTip
				&& timeSinceMouseOver.Elapsed.TotalSeconds > showDelayTime)
            {
                DoShowToolTip();
            }

			if (timeCurrentToolTipHasBeenShowing.Elapsed.TotalSeconds > CurrentAutoPopDelay)
            {
                RemoveToolTip();
				widgetThatIsShowingToolTip = null;
				timeCurrentToolTipHasBeenShowing.Stop();
				timeCurrentToolTipHasBeenShowing.Reset();
            }

			if (widgetThatIsShowingToolTip != null)
			{
				RectangleDouble screenBounds = widgetThatIsShowingToolTip.TransformToScreenSpace(widgetThatIsShowingToolTip.LocalBounds);
				if (!screenBounds.Contains(mousePosition))
				{
					RemoveToolTip();
					widgetThatIsShowingToolTip = null;
				}
			}

			// Call again in .1 s so that this is constantly being re-evaluated.
            UiThread.RunOnIdle(CheckIfNeedToDisplayToolTip, .05);
        }

        public void SetHoveredWidget(GuiWidget widgetToShowToolTipFor)
        {
#if __ANDROID__
            return;
#endif
            if (this.widgetThatWantsToShowToolTip != widgetToShowToolTipFor)
            {
                timeSinceMouseOver.Restart();
				this.widgetThatWantsToShowToolTip = widgetToShowToolTipFor;
            }
        }

		void DoShowToolTip()
		{
			if (widgetThatWantsToShowToolTip != null
				&& widgetThatWantsToShowToolTip != widgetThatIsShowingToolTip)
			{
				RectangleDouble screenBounds = widgetThatWantsToShowToolTip.TransformToScreenSpace(widgetThatWantsToShowToolTip.LocalBounds);
				if (screenBounds.Contains(mousePosition))
				{
					RemoveToolTip();
					widgetThatIsShowingToolTip = null;

					toolTipWidget = new FlowLayoutWidget()
					{
						BackgroundColor = RGBA_Bytes.White,
						OriginRelativeParent = new Vector2((int)mousePosition.x, (int)mousePosition.y),
						Padding = new BorderDouble(3),
						Selectable = false,
					};

					toolTipWidget.DrawAfter += (sender, drawEventHandler) =>
					{
						drawEventHandler.graphics2D.Rectangle(toolTipWidget.LocalBounds, RGBA_Bytes.Black);
					};

					// Make sure we wrap long text
					toolTipWidget.AddChild(new WrappedTextWidget(widgetThatWantsToShowToolTip.ToolTipText, 350)
					{
						HAnchor = HAnchor.FitToChildren,
					});

					// Increase the delay to make long text stay on screen long enough to read
					double RatioOfExpectedText = Math.Max(1, (widgetThatWantsToShowToolTip.ToolTipText.Length / 50.0));
					CurrentAutoPopDelay = RatioOfExpectedText * AutoPopDelay;

					owner.AddChild(toolTipWidget);
					timeCurrentToolTipHasBeenShowing.Restart();

					RectangleDouble toolTipBounds = toolTipWidget.LocalBounds;

					toolTipWidget.OriginRelativeParent = toolTipWidget.OriginRelativeParent + new Vector2(0, -toolTipBounds.Bottom - toolTipBounds.Height - 23);

					Vector2 offset = Vector2.Zero;
					RectangleDouble ownerBounds = owner.LocalBounds;
					RectangleDouble toolTipBoundsRelativeToParent = toolTipWidget.BoundsRelativeToParent;

					if (toolTipBoundsRelativeToParent.Right > ownerBounds.Right - 3)
					{
						offset.x = ownerBounds.Right - toolTipBoundsRelativeToParent.Right - 3;
					}

					if (toolTipBoundsRelativeToParent.Bottom < ownerBounds.Bottom + 3)
					{
						offset.y = ownerBounds.Bottom - toolTipBoundsRelativeToParent.Bottom + 3;
					}

					toolTipWidget.OriginRelativeParent = toolTipWidget.OriginRelativeParent + offset;

					widgetThatIsShowingToolTip = widgetThatWantsToShowToolTip;
					widgetThatWantsToShowToolTip = null;
				}
			}
		}

		static int count = 0;
        private void RemoveToolTip()
        {
			if (toolTipWidget != null
				&& toolTipWidget.Parent == owner)
			{
				widgetThatWantsToShowToolTip = null;
				timeSinceLastMouseMove.Stop();
				timeSinceLastMouseMove.Reset();
				owner.RemoveChild(toolTipWidget);
				toolTipWidget = null;
				timeSinceLastToolTipClose.Restart();
				Debug.WriteLine("RemoveToolTip {0}".FormatWith(count++));
			}
        }
    }
}