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

using MatterHackers.VectorMath;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MatterHackers.Agg.UI
{
	public class ToolTipManager : IDisposable
	{
		/// <summary>
		/// Gets or sets the period of time the ToolTip remains visible if the pointer is stationary on a control with specified ToolTip text.
		/// </summary>
		public const double AutoPopDelay = 5;

		/// <summary>
		/// Gets or sets the time that passes before the ToolTip appears.
		/// </summary>
		public const double InitialDelay = .6;

		/// <summary>
		/// How far, in design units, a shown tooltip's bottom sits above the mouse - a cursor's height, so the
		/// tooltip is not drawn under the cursor.
		/// </summary>
		public const double CursorClearance = 23;

		/// <summary>
		/// Gets or sets the length of time that must transpire before subsequent ToolTip windows appear as the pointer moves from one control to another.
		/// </summary>
		public const double ReshowDelay = .2;

		/// <summary>
		/// How long, in seconds, a content tooltip stays open after the mouse has left both its widget and the
		/// tooltip, so crossing the gap between them (or a momentary jump) does not flicker it closed.
		/// </summary>
		public const double ContentCloseGrace = .25;

		/// <summary>The gap, in design units, between a content tooltip and the widget it hangs under.</summary>
		private const double ContentGap = 4;

		// Per-widget tooltip options, kept here rather than on GuiWidget so a widget carries no extra fields
		// unless it asks for one. Weak keys, so a registration never keeps a closed widget alive.
		private static readonly ConditionalWeakTable<GuiWidget, ToolTipOptions> widgetOptions = new ConditionalWeakTable<GuiWidget, ToolTipOptions>();

		private class ToolTipOptions
		{
			public Func<GuiWidget> CreateContent;
			public bool AtPointer;
		}

		// The open content tooltip, if any. It lives beside the text tooltip rather than in its place - the
		// text tooltip machinery keeps running for widgets inside it, which is what shows a nested tooltip.
		private GuiWidget contentToolTip;
		private GuiWidget contentToolTipOwner;
		private readonly Stopwatch timeSinceMouseLeftContentToolTip = new Stopwatch();

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

		// 0 = not initialized, 1 = initialized. Latched with Interlocked.CompareExchange so
		// racing callers cannot double-subscribe MouseMove or leak a second interval.
		private int initialized;

		internal ToolTipManager(SystemWindow owner)
		{
			// Field initialization only. The owning SystemWindow is still constructing, so no
			// event subscriptions or UiThread scheduling may happen here - callbacks could run
			// against a partially-constructed window. Activation happens in Initialize().
			this.systemWindow = owner;
		}

		/// <summary>
		/// Activates tooltip tracking for the owning window: subscribes to MouseMove and starts
		/// the polling interval. Called once the window is being shown (or first receives mouse
		/// input). Safe to call multiple times; only the first call has an effect.
		/// </summary>
		internal void Initialize()
		{
			if (System.Threading.Interlocked.CompareExchange(ref initialized, 1, 0) != 0)
			{
				return;
			}

			// Register listeners
			systemWindow.MouseMove += this.SystemWindow_MouseMove;
			runningInterval = UiThread.SetInterval(() => CheckIfNeedToDisplayToolTip(), .05);
		}

		private void SystemWindow_MouseMove(object sender, MouseEventArgs e)
		{
			mousePosition = e.Position;
			timeSinceLastMouseMove.Restart();

			if (toolTipWidget != null
				&& widgetThatIsShowingToolTip != null
				&& GetAtPointer(widgetThatIsShowingToolTip))
			{
				PlaceAtPointer();
			}
		}

		/// <summary>
		/// Gives <paramref name="owner"/> a widget as its tooltip in place of its ToolTipText. The tooltip hangs
		/// under the owner and is interactive: the mouse can move into it without closing it, its controls take
		/// clicks, and a control inside it with its own tooltip shows that one on top. It closes once the mouse
		/// has been off both the owner and the tooltip for <see cref="ContentCloseGrace"/>.
		/// </summary>
		/// <param name="owner">The widget that shows the tooltip when hovered.</param>
		/// <param name="createContent">Builds the tooltip each time it opens, so it picks up the current theme.
		/// The widget draws its own panel (background and border). Null removes the content tooltip.</param>
		public static void SetToolTipContent(GuiWidget owner, Func<GuiWidget> createContent)
		{
			widgetOptions.GetOrCreateValue(owner).CreateContent = createContent;
		}

		/// <summary>
		/// Makes <paramref name="owner"/>'s text tooltip open beside the mouse and follow it while it moves over
		/// the owner, rather than centre on the owner (the default).
		/// </summary>
		public static void SetToolTipAtPointer(GuiWidget owner, bool atPointer)
		{
			widgetOptions.GetOrCreateValue(owner).AtPointer = atPointer;
		}

		private static Func<GuiWidget> GetContent(GuiWidget widget)
		{
			return widget != null && widgetOptions.TryGetValue(widget, out ToolTipOptions options) ? options.CreateContent : null;
		}

		private static bool GetAtPointer(GuiWidget widget)
		{
			return widget != null && widgetOptions.TryGetValue(widget, out ToolTipOptions options) && options.AtPointer;
		}

		private static bool HasToolTip(GuiWidget widget)
		{
			return !string.IsNullOrWhiteSpace(widget?.ToolTipText) || GetContent(widget) != null;
		}

		/// <summary>The open content tooltip (see <see cref="SetToolTipContent"/>), or null when none is open.</summary>
		public GuiWidget ContentToolTip => contentToolTip;

		public event EventHandler ToolTipPop;

		public event EventHandler<StringEventArgs> ToolTipShown;

		public string CurrentText => toolTipText;

		public static bool AllowToolTips { get; set; } = true;
		public static bool DebugKeepOpen { get; set; }

		private string lastTextShown = "";
		Action<GuiWidget, string> changeWidgetText;

		/// <summary>
		/// Reports the widget currently under the mouse. Pass the widget even when it has no tooltip
		/// text (or null when nothing is hovered) - that is what retires the tooltip of the widget we
		/// moved off of.
		/// </summary>
        public void SetHoveredWidget(GuiWidget widgetToShowToolTipFor)
		{
			if (!AllowToolTips)
			{
				return;
			}

			// Hovering something with no tooltip must cancel anything pending or showing. The show/remove
			// checks below are pure containment tests, so a tooltip whose widget is covered by the newly
			// hovered widget would otherwise still "contain" the mouse and stay up over the new widget.
			if (!HasToolTip(widgetToShowToolTipFor))
			{
				widgetThatWantsToShowToolTip = null;
				lastTextShown = "";

				if (widgetThatIsShowingToolTip != null)
				{
					RemoveToolTip();
					widgetThatIsShowingToolTip = null;
					timeCurrentToolTipHasBeenShowing.Stop();
					timeCurrentToolTipHasBeenShowing.Reset();
				}

				return;
			}

            if (this.widgetThatWantsToShowToolTip != widgetToShowToolTipFor)
			{
				timeSinceMouseOver.Restart();
				this.widgetThatWantsToShowToolTip = widgetToShowToolTipFor;
				this.lastTextShown = widgetToShowToolTipFor.ToolTipText ?? "";
            }
			else if (toolTipWidget != null
				&& widgetToShowToolTipFor != null
				&& lastTextShown != (widgetToShowToolTipFor.ToolTipText ?? ""))
			{
				// change the text and reset the timer
				timeSinceMouseOver.Restart();
				this.lastTextShown = widgetToShowToolTipFor.ToolTipText ?? "";
                // and set the text of the current widget
                changeWidgetText?.Invoke(toolTipWidget, widgetToShowToolTipFor.ToolTipText ?? "");
            }
        }

		private void CheckIfNeedToDisplayToolTip(bool forceRemove = false)
		{
			//DebugStopTimers();

			UpdateContentToolTip(forceRemove);

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
					// Only claim we showed a tooltip if one actually went up - otherwise this poll would
					// skip the auto-pop and mouse-leave handling below for no reason
					didShow = DoShowToolTip();
				}
			}

			if (widgetThatWasShowingToolTip != null)
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
				if (timeCurrentToolTipHasBeenShowing.Elapsed.TotalSeconds > CurrentAutoPopDelay
					|| forceRemove)
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

		/// <summary>
		/// Shows the tooltip for the widget that is waiting to show one.
		/// </summary>
		/// <returns>True if a tooltip was actually put on screen.</returns>
		private bool DoShowToolTip()
		{
			if (widgetThatWantsToShowToolTip != null
				&& widgetThatWantsToShowToolTip != widgetThatIsShowingToolTip
				&& widgetThatWasShowingToolTip != widgetThatWantsToShowToolTip)
			{
				RectangleDouble screenBoundsShowingTT = widgetThatWantsToShowToolTip.TransformToScreenSpace(widgetThatWantsToShowToolTip.LocalBounds);
				if (screenBoundsShowingTT.Contains(mousePosition))
				{
					// Check for text before tearing down the current tooltip - a widget with nothing to
					// say must not close a tooltip that is legitimately showing for another widget
					var textToShow = widgetThatWantsToShowToolTip.ToolTipText ?? "";
					Func<GuiWidget> createContent = GetContent(widgetThatWantsToShowToolTip);
					if ((textToShow.Length == 0 && createContent == null)
						|| widgetThatWantsToShowToolTip == contentToolTipOwner)
					{
						// Nothing to say, or its content tooltip is already open (the mouse came back to the
						// owner from the tooltip)
						widgetThatWantsToShowToolTip = null;
						return false;
					}

					RemoveToolTip();
					widgetThatIsShowingToolTip = null;

					if (createContent != null)
					{
						ShowContentToolTip(widgetThatWantsToShowToolTip, createContent, screenBoundsShowingTT);
						widgetThatWantsToShowToolTip = null;
						widgetThatWasShowingToolTip = null;
						return true;
					}

					toolTipText = textToShow;
					toolTipWidget = new FlowLayoutWidget()
					{
						OriginRelativeParent = new Vector2((int)mousePosition.X, (int)mousePosition.Y),
						Selectable = false,
					};

					toolTipWidget.Name = "ToolTipWidget";

					// Make sure we wrap long text
					var (widgetToShow, changeText) = CreateToolTip(toolTipText);
					changeWidgetText = changeText;
					toolTipWidget.AddChild(widgetToShow);

					// Increase the delay to make long text stay on screen long enough to read
					double ratioOfExpectedText = Math.Max(1, toolTipText.Length / 50.0);
					CurrentAutoPopDelay = ratioOfExpectedText * AutoPopDelay;

					systemWindow.AddChild(toolTipWidget);

					ToolTipShown?.Invoke(this, new StringEventArgs(CurrentText));

					// timeCurrentToolTipHasBeenShowing.Reset();
					// timeCurrentToolTipHasBeenShowingWasRunning = true;
					timeCurrentToolTipHasBeenShowing.Restart();

					if (GetAtPointer(widgetThatWantsToShowToolTip))
					{
						PlaceAtPointer();
						widgetThatIsShowingToolTip = widgetThatWantsToShowToolTip;
						widgetThatWantsToShowToolTip = null;
						widgetThatWasShowingToolTip = null;
						return true;
					}

					RectangleDouble toolTipBounds = toolTipWidget.LocalBounds;

					// Lift the tooltip clear of the cursor. The cursor is drawn by the OS at the display's
					// scale, so this clearance and the screen edge insets below are design pixels and have to
					// be scaled - a raw 23 put the tooltip under a Retina cursor's tip.
					double cursorClearance = CursorClearance * GuiWidget.DeviceScale;
					double edgeInset = 3 * GuiWidget.DeviceScale;

					// Center on the widget, not on the mouse. A tooltip anchored at the cursor sits beside the
					// thing it describes - on a toolbar that lands it under the next icon over and reads as if
					// it belonged to that one. Vertically it still hangs off the mouse, clear of the cursor.
					double centeredLeft = Math.Round(screenBoundsShowingTT.XCenter - toolTipBounds.Width / 2);

					// Then keep the cursor inside the tooltip. Two cases meet here: for a widget narrower than
					// its tooltip (a toolbar icon) the mouse is already well inside the centered tooltip and this
					// does nothing, but for a wide widget (a full width row) centering would fling the tooltip to
					// the middle of the window, far from the thing the user is pointing at - so it slides back to
					// the cursor. The margin keeps the cursor off the tooltip's own edge.
					double cursorOverlapMargin = Math.Min(10 * GuiWidget.DeviceScale, toolTipBounds.Width / 2);
					centeredLeft = Math.Min(centeredLeft, Math.Round(mousePosition.X - cursorOverlapMargin));
					centeredLeft = Math.Max(centeredLeft, Math.Round(mousePosition.X - toolTipBounds.Width + cursorOverlapMargin));

					toolTipWidget.OriginRelativeParent = new Vector2(
						centeredLeft - toolTipBounds.Left,
						toolTipWidget.OriginRelativeParent.Y - toolTipBounds.Bottom - toolTipBounds.Height - cursorClearance);

					Vector2 offset = Vector2.Zero;
					RectangleDouble systemWindowBounds = systemWindow.LocalBounds;
					RectangleDouble toolTipBoundsRelativeToParent = toolTipWidget.BoundsRelativeToParent;

					if (toolTipBoundsRelativeToParent.Right > systemWindowBounds.Right - edgeInset)
					{
						offset.X = systemWindowBounds.Right - toolTipBoundsRelativeToParent.Right - edgeInset;
					}

					// Left after right, so a tooltip too wide for the window ends up flush with the left edge
					// rather than the right - text is read from the left
					if (toolTipBoundsRelativeToParent.Left + offset.X < systemWindowBounds.Left + edgeInset)
					{
						offset.X = systemWindowBounds.Left + edgeInset - toolTipBoundsRelativeToParent.Left;
					}

					if (toolTipBoundsRelativeToParent.Bottom < systemWindowBounds.Bottom + edgeInset)
					{
						offset.Y = screenBoundsShowingTT.Top - toolTipBoundsRelativeToParent.Bottom + edgeInset;
					}

					toolTipWidget.OriginRelativeParent = toolTipWidget.OriginRelativeParent + offset;

					widgetThatIsShowingToolTip = widgetThatWantsToShowToolTip;
					widgetThatWantsToShowToolTip = null;
					widgetThatWasShowingToolTip = null;

					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Puts the text tooltip below and to the right of the mouse, clear of the cursor that hangs down and
		/// right from it (agg-gui's at_pointer placement), flipping above the mouse when there is no room below.
		/// </summary>
		private void PlaceAtPointer()
		{
			RectangleDouble bounds = toolTipWidget.LocalBounds;
			RectangleDouble windowBounds = systemWindow.LocalBounds;
			double edgeInset = 3 * GuiWidget.DeviceScale;

			double bottom = Math.Round(mousePosition.Y - CursorClearance * GuiWidget.DeviceScale - bounds.Height);
			if (bottom < windowBounds.Bottom + edgeInset)
			{
				bottom = Math.Round(mousePosition.Y + edgeInset);
			}

			double left = Math.Round(mousePosition.X);
			left = Math.Min(left, windowBounds.Right - edgeInset - bounds.Width);
			left = Math.Max(left, windowBounds.Left + edgeInset);

			toolTipWidget.OriginRelativeParent = new Vector2(left - bounds.Left, bottom - bounds.Bottom);
		}

		private void ShowContentToolTip(GuiWidget owner, Func<GuiWidget> createContent, RectangleDouble ownerScreenBounds)
		{
			CloseContentToolTip();

			contentToolTip = new GuiWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Name = "ContentToolTip",
			};
			contentToolTip.AddChild(createContent());
			systemWindow.AddChild(contentToolTip);
			contentToolTipOwner = owner;
			timeSinceMouseLeftContentToolTip.Reset();

			// Under the owner, left edges aligned, as agg-gui hangs an interactive tooltip - the mouse leaves the
			// owner downward straight into it. Above the owner when there is no room below.
			RectangleDouble bounds = contentToolTip.LocalBounds;
			RectangleDouble windowBounds = systemWindow.LocalBounds;
			double gap = ContentGap * GuiWidget.DeviceScale;
			double edgeInset = 3 * GuiWidget.DeviceScale;

			double bottom = Math.Round(ownerScreenBounds.Bottom - gap - bounds.Height);
			if (bottom < windowBounds.Bottom + edgeInset)
			{
				bottom = Math.Round(ownerScreenBounds.Top + gap);
			}

			double left = Math.Round(ownerScreenBounds.Left);
			left = Math.Min(left, windowBounds.Right - edgeInset - bounds.Width);
			left = Math.Max(left, windowBounds.Left + edgeInset);

			contentToolTip.OriginRelativeParent = new Vector2(left - bounds.Left, bottom - bounds.Bottom);
		}

		/// <summary>
		/// Keeps the content tooltip open while the mouse is over its owner or over the tooltip itself, and
		/// closes it once the mouse has been off both for <see cref="ContentCloseGrace"/>.
		/// </summary>
		private void UpdateContentToolTip(bool forceRemove)
		{
			if (contentToolTip == null)
			{
				return;
			}

			if (forceRemove
				|| contentToolTip.Parent == null
				|| contentToolTipOwner.Parent == null)
			{
				CloseContentToolTip();
				return;
			}

			bool overOwner = contentToolTipOwner.TransformToScreenSpace(contentToolTipOwner.LocalBounds).Contains(mousePosition);
			bool overToolTip = contentToolTip.BoundsRelativeToParent.Contains(mousePosition);
			if (overOwner || overToolTip)
			{
				timeSinceMouseLeftContentToolTip.Reset();
			}
			else if (!timeSinceMouseLeftContentToolTip.IsRunning)
			{
				timeSinceMouseLeftContentToolTip.Start();
			}
			else if (timeSinceMouseLeftContentToolTip.Elapsed.TotalSeconds > ContentCloseGrace)
			{
				CloseContentToolTip();
			}
		}

		private void CloseContentToolTip()
		{
			if (contentToolTip == null)
			{
				return;
			}

			// A tooltip showing (or armed) for a control inside the content tooltip goes with it
			if (widgetThatIsShowingToolTip != null && IsInside(widgetThatIsShowingToolTip, contentToolTip))
			{
				RemoveToolTip();
				widgetThatIsShowingToolTip = null;
			}

			if (widgetThatWantsToShowToolTip != null && IsInside(widgetThatWantsToShowToolTip, contentToolTip))
			{
				widgetThatWantsToShowToolTip = null;
			}

			contentToolTip.Close();
			contentToolTip = null;
			contentToolTipOwner = null;
			timeSinceMouseLeftContentToolTip.Reset();
		}

		private static bool IsInside(GuiWidget widget, GuiWidget container)
		{
			for (GuiWidget parent = widget; parent != null; parent = parent.Parent)
			{
				if (parent == container)
				{
					return true;
				}
			}

			return false;
		}

		private static (GuiWidget widgetToShow, Action<GuiWidget, string> changeWidgetText) DefaultToolTipWidget(string toolTipText)
		{
			var content = new WrappedTextWidget(toolTipText)
			{
				BackgroundColor = Color.White,
				Width = 350 * GuiWidget.DeviceScale,
				HAnchor = HAnchor.Fit,
				Padding = new BorderDouble(3),
			};

			content.AfterDraw += (sender, drawEventHandler) =>
			{
				drawEventHandler.Graphics2D.Rectangle(content.LocalBounds, Color.Black);
			};

			void ChangeWidgetText(GuiWidget widget, string newText)
			{
				var wrappedTextWidget = widget.DescendantsAndSelf<WrappedTextWidget>().FirstOrDefault();

                if (wrappedTextWidget != null)
				{
					wrappedTextWidget.Text = newText;
                }
            }

			return (content, ChangeWidgetText);
		}

		public static Func<string, (GuiWidget widgetToShow, Action<GuiWidget, string> changeWidgetText)> CreateToolTip = DefaultToolTipWidget;

		/// <summary>
		/// Takes down any tooltip, showing or merely armed. Callers are things that put something on top of
		/// the window (menus, popups) and need the area clear.
		/// </summary>
		/// <remarks>
		/// Dropping the armed widget matters as much as removing the visible one: a tooltip the mouse armed
		/// on its way to a menu item has not been drawn yet, so removing only the visible tooltip lets it pop
		/// over the menu a fraction of a second later.
		/// Setting widgetThatWasShowingToolTip is what keeps the tooltip from immediately coming back for the
		/// widget we just cleared while the mouse is still inside it - CheckIfNeedToDisplayToolTip only lets
		/// go of that widget once the mouse leaves its bounds. That suppression is why Clear() is wrong for
		/// the plain hover path (SetHoveredWidget), which must be able to re-show without a mouse exit.
		/// </remarks>
		public void Clear()
		{
			CloseContentToolTip();
			widgetThatWasShowingToolTip = widgetThatIsShowingToolTip;
			RemoveToolTip();
			widgetThatIsShowingToolTip = null;
			widgetThatWantsToShowToolTip = null;
			lastTextShown = "";
			timeCurrentToolTipHasBeenShowing.Stop();
			timeCurrentToolTipHasBeenShowing.Reset();
		}

		private void RemoveToolTip()
		{
			if (toolTipWidget != null
				&& toolTipWidget.Parent == systemWindow)
			{
				ToolTipPop?.Invoke(this, null);

				// widgetThatWantsToShowToolTip = null;
				timeSinceLastMouseMove.Stop();
				timeSinceLastMouseMove.Reset();

				if (!ToolTipManager.DebugKeepOpen)
				{
					toolTipWidget.Close();
				}

				toolTipWidget = null;
				toolTipText = "";

				// timeSinceLastToolTipClose.Reset();
				// timeSinceLastToolTipCloseWasRunning = true;
				timeSinceLastToolTipClose.Restart();

				// Debug.WriteLine("RemoveToolTip {0}".FormatWith(count++));
			}
		}

		public void Dispose()
		{
			// Unregister listeners. Idempotent and safe when Initialize() was never called
			// (unsubscribing a never-subscribed handler is a no-op, interval will be null).
			systemWindow.MouseMove -= this.SystemWindow_MouseMove;

			if (runningInterval != null)
			{
				UiThread.ClearInterval(runningInterval);
				runningInterval = null;
			}
		}
	}
}