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
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
	public class ToolTipManager : IDisposable
	{
		/// <summary>
		/// Gets or sets the period of time the ToolTip remains visible if the pointer is stationary on a control with specified ToolTip text.
		/// </summary>
		public double AutoPopDelay = 5;

		/// <summary>
		/// Gets or sets the time that passes before the ToolTip appears.
		/// </summary>
		public double InitialDelay = 1;

		/// <summary>
		/// Gets or sets the length of time that must transpire before subsequent ToolTip windows appear as the pointer moves from one control to another.
		/// </summary>
		public double ReshowDelay = .2;

		private static int count = 0;
		private double CurrentAutoPopDelay = 5;
		private Vector2 mousePosition;
		private SystemWindow systemWindow;
		private Stopwatch timeCurrentToolTipHasBeenShowing = new Stopwatch();
		private bool timeCurrentToolTipHasBeenShowingWasRunning;
		private Stopwatch timeSinceLastMouseMove = new Stopwatch();
		private bool timeSinceLastMouseMoveWasRunning;
		private Stopwatch timeSinceLastToolTipClose = new Stopwatch();
		private bool timeSinceLastToolTipCloseWasRunning;
		private Stopwatch timeSinceMouseOver = new Stopwatch();
		private bool timeSinceMouseOverWasRunning;
		private string toolTipText = "";
		private GuiWidget toolTipWidget;

		private GuiWidget widgetThatIsShowingToolTip;
		private GuiWidget widgetThatWantsToShowToolTip;
		private GuiWidget widgetThatWasShowingToolTip;
		private RunningInterval runningInterval;

		internal ToolTipManager(SystemWindow owner)
		{
			this.systemWindow = owner;

			// Register listeners
			systemWindow.MouseMove += this.SystemWindow_MouseMove;
			runningInterval = UiThread.SetInterval(CheckIfNeedToDisplayToolTip, .05);
		}

		private void SystemWindow_MouseMove(object sender, MouseEventArgs e)
		{
			mousePosition = e.Position;
			timeSinceLastMouseMove.Restart();
		}

		public event EventHandler ToolTipPop;

		public event EventHandler<StringEventArgs> ToolTipShown;

		public string CurrentText => toolTipText;

		public static bool AllowToolTips { get; set; } = true;

		public void SetHoveredWidget(GuiWidget widgetToShowToolTipFor)
		{
			if (!AllowToolTips)
			{
				return;
			}

			if (this.widgetThatWantsToShowToolTip != widgetToShowToolTipFor)
			{
				timeSinceMouseOver.Restart();
				this.widgetThatWantsToShowToolTip = widgetToShowToolTipFor;
			}
		}

		private void CheckIfNeedToDisplayToolTip()
		{
			//DebugStopTimers();

			double showDelayTime = InitialDelay;
			if ((timeSinceLastToolTipClose.IsRunning || timeSinceLastToolTipCloseWasRunning)
				&& timeSinceLastToolTipClose.Elapsed.TotalSeconds < InitialDelay
				&& widgetThatWantsToShowToolTip != null
				&& widgetThatIsShowingToolTip == null)
			{
				showDelayTime = ReshowDelay;
			}

			bool didShow = false;
			if (widgetThatWantsToShowToolTip != null
				&& widgetThatWantsToShowToolTip != widgetThatIsShowingToolTip
				&& timeSinceMouseOver.Elapsed.TotalSeconds > showDelayTime)
			{
				// And lets make sure we are still over the widget
				RectangleDouble screenBounds = widgetThatWantsToShowToolTip.TransformToScreenSpace(widgetThatWantsToShowToolTip.LocalBounds);
				if (screenBounds.Contains(mousePosition))
				{
					DoShowToolTip();
					didShow = true;
				}
			}

			if(widgetThatWasShowingToolTip != null)
			{
				RectangleDouble screenBounds = widgetThatWasShowingToolTip.TransformToScreenSpace(widgetThatWasShowingToolTip.LocalBounds);
				if (!screenBounds.Contains(mousePosition))
				{
					widgetThatWasShowingToolTip = null;
				}
			}

			if (!didShow)
			{
				bool didRemove = false;
				if (timeCurrentToolTipHasBeenShowing.Elapsed.TotalSeconds > CurrentAutoPopDelay)
				{
					RemoveToolTip();
					widgetThatWasShowingToolTip = widgetThatIsShowingToolTip;
					widgetThatIsShowingToolTip = null;
					timeCurrentToolTipHasBeenShowing.Stop();
					timeCurrentToolTipHasBeenShowing.Reset();
					didRemove = true;
				}

				if (!didRemove
					&& widgetThatIsShowingToolTip != null)
				{
					RectangleDouble screenBounds = widgetThatIsShowingToolTip.TransformToScreenSpace(widgetThatIsShowingToolTip.LocalBounds);
					if (!screenBounds.Contains(mousePosition))
					{
						RemoveToolTip();
						widgetThatIsShowingToolTip = null;
					}
				}
			}
		}

		private void DebugStartTimers()
		{
			if (timeSinceLastMouseMoveWasRunning)
				timeSinceLastMouseMove.Start();
			if (timeCurrentToolTipHasBeenShowingWasRunning)
				timeCurrentToolTipHasBeenShowing.Start();
			if (timeSinceMouseOverWasRunning)
				timeSinceMouseOver.Start();
			if (timeSinceLastToolTipCloseWasRunning)
				timeSinceLastToolTipClose.Start();
		}

		private void DebugStopTimers()
		{
			timeSinceLastMouseMoveWasRunning = timeSinceLastMouseMove.IsRunning;
			timeSinceLastMouseMove.Stop();
			timeCurrentToolTipHasBeenShowingWasRunning = timeCurrentToolTipHasBeenShowing.IsRunning;
			timeCurrentToolTipHasBeenShowing.Stop();
			timeSinceMouseOverWasRunning = timeSinceMouseOver.IsRunning;
			timeSinceMouseOver.Stop();
			timeSinceLastToolTipCloseWasRunning = timeSinceLastToolTipClose.IsRunning;
			timeSinceLastToolTipClose.Stop();
		}

		private void DoShowToolTip()
		{
			if (widgetThatWantsToShowToolTip != null
				&& widgetThatWantsToShowToolTip != widgetThatIsShowingToolTip
				&& widgetThatWasShowingToolTip != widgetThatWantsToShowToolTip)
			{
				RectangleDouble screenBoundsShowingTT = widgetThatWantsToShowToolTip.TransformToScreenSpace(widgetThatWantsToShowToolTip.LocalBounds);
				if (screenBoundsShowingTT.Contains(mousePosition))
				{
					RemoveToolTip();
					widgetThatIsShowingToolTip = null;

					toolTipText = widgetThatWantsToShowToolTip.ToolTipText;
					toolTipWidget = new FlowLayoutWidget()
					{
						BackgroundColor = Color.White,
						OriginRelativeParent = new Vector2((int)mousePosition.X, (int)mousePosition.Y),
						Padding = new BorderDouble(3),
						Selectable = false,
					};

					toolTipWidget.Name = "ToolTipWidget";

					toolTipWidget.AfterDraw += (sender, drawEventHandler) =>
					{
						drawEventHandler.Graphics2D.Rectangle(toolTipWidget.LocalBounds, Color.Black);
					};

					// Make sure we wrap long text
					toolTipWidget.AddChild(new WrappedTextWidget(toolTipText)
					{
						Width = 350 * GuiWidget.DeviceScale,
						HAnchor = HAnchor.Fit,
					});

					// Increase the delay to make long text stay on screen long enough to read
					double RatioOfExpectedText = Math.Max(1, (widgetThatWantsToShowToolTip.ToolTipText.Length / 50.0));
					CurrentAutoPopDelay = RatioOfExpectedText * AutoPopDelay;

					systemWindow.AddChild(toolTipWidget);

					ToolTipShown?.Invoke(this, new StringEventArgs(CurrentText));

					//timeCurrentToolTipHasBeenShowing.Reset();
					//timeCurrentToolTipHasBeenShowingWasRunning = true;
					timeCurrentToolTipHasBeenShowing.Restart();

					RectangleDouble toolTipBounds = toolTipWidget.LocalBounds;

					toolTipWidget.OriginRelativeParent = toolTipWidget.OriginRelativeParent + new Vector2(0, -toolTipBounds.Bottom - toolTipBounds.Height - 23);

					Vector2 offset = Vector2.Zero;
					RectangleDouble systemWindowBounds = systemWindow.LocalBounds;
					RectangleDouble toolTipBoundsRelativeToParent = toolTipWidget.BoundsRelativeToParent;

					if (toolTipBoundsRelativeToParent.Right > systemWindowBounds.Right - 3)
					{
						offset.X = systemWindowBounds.Right - toolTipBoundsRelativeToParent.Right - 3;
					}

					if (toolTipBoundsRelativeToParent.Bottom < systemWindowBounds.Bottom + 3)
					{
						offset.Y = screenBoundsShowingTT.Top - toolTipBoundsRelativeToParent.Bottom + 3;
					}

					toolTipWidget.OriginRelativeParent = toolTipWidget.OriginRelativeParent + offset;

					widgetThatIsShowingToolTip = widgetThatWantsToShowToolTip;
					widgetThatWantsToShowToolTip = null;
					widgetThatWasShowingToolTip = null;
				}
			}
		}

		public void Clear()
		{
			widgetThatWasShowingToolTip = widgetThatIsShowingToolTip;
			RemoveToolTip();
			widgetThatIsShowingToolTip = null;
			timeCurrentToolTipHasBeenShowing.Stop();
			timeCurrentToolTipHasBeenShowing.Reset();
		}

		private void RemoveToolTip()
		{
			if (toolTipWidget != null
				&& toolTipWidget.Parent == systemWindow)
			{
				ToolTipPop?.Invoke(this, null);

				//widgetThatWantsToShowToolTip = null;
				timeSinceLastMouseMove.Stop();
				timeSinceLastMouseMove.Reset();
				
				toolTipWidget.Close();
				toolTipWidget = null;
				toolTipText = "";

				//timeSinceLastToolTipClose.Reset();
				//timeSinceLastToolTipCloseWasRunning = true;
				timeSinceLastToolTipClose.Restart();

				Debug.WriteLine("RemoveToolTip {0}".FormatWith(count++));
			}
		}

		public void Dispose()
		{
			// Unregister listeners
			systemWindow.MouseMove -= this.SystemWindow_MouseMove;
			UiThread.ClearInterval(runningInterval);
		}
	}
}