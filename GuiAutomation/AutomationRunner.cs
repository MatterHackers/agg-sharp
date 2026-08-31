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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using static MatterHackers.Agg.UI.ScrollableWidget;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.GuiAutomation
{
	public delegate Task AutomationTest(AutomationRunner runner);

	public class AutomationRunner
	{
		public long MatchLimit = 50;

		private IInputMethod inputSystem;

		private const double DefaultWidgetWaitSeconds = 2.0;

		/// <summary>
		/// IMPORTANT: Every automation test MUST call <see cref="MarkTestComplete()"/>
		/// as its last action. This is how we verify that tests execute all the way
		/// to their final statement. If a test exits early — due to an exception,
		/// a silent WaitFor timeout, or an accidental early return — MarkTestComplete()
		/// is never called and the framework reports the test as failed.
		///
		/// If you are adding a new test that calls ShowWindowAndExecuteTests directly
		/// (rather than through a wrapper like RunTest or NewPartTabTest), you must
		/// call testRunner.MarkTestComplete() as the last line of your test lambda.
		///
		/// Tests going through MatterCADUtilities.RunTest or NewPartTabTest get this
		/// automatically — the wrapper calls MarkTestComplete() after your lambda returns.
		/// </summary>
		public bool RequireTestCompletion { get; set; } = true;

		/// <summary>
		/// Indicates whether the test called MarkTestComplete() before returning.
		/// </summary>
		public bool TestWasCompleted { get; private set; }

		/// <summary>
		/// Signals that the test executed all the way to its final statement.
		/// Must be the last call in every automation test lambda. The framework
		/// verifies this was called; if not, the test is reported as failed.
		/// </summary>
		public void MarkTestComplete()
		{
			TestWasCompleted = true;
		}

		/// <summary>
		/// The longest a single mouse move may take, in seconds.
		/// </summary>
		/// <remarks>
		/// This is a ceiling, not a target. A mouse move is paced by the UI thread - each intermediate
		/// position waits only until the UI has actually taken the move event that was just sent - so on a
		/// responsive UI a move finishes well inside this budget. It only comes into play when the UI thread
		/// is busy doing real work, where it stops the runner waiting indefinitely; the queued moves are
		/// still delivered in order once the UI gets back to its idle pump.
		/// </remarks>
		public static double TimeToMoveMouse { get; set; } = .1;

		/// <summary>
		/// How many intermediate positions a mouse move is broken into.
		/// </summary>
		/// <remarks>
		/// The intermediate <c>OnMouseMove</c> events are load bearing - hover highlighting, drag tracking
		/// and tooltips all key off seeing the pointer travel rather than teleport - so the count is fixed
		/// here rather than derived from <see cref="TimeToMoveMouse"/>. It used to be derived, which meant
		/// that asking for a slower mouse silently asked for a differently shaped gesture as well.
		/// </remarks>
		public static int MouseMoveSteps { get; set; } = 5;

		private string imageDirectory;

		/// <summary>
		/// The longest a simulated mouse button may be held down before it is released, in seconds.
		/// </summary>
		/// <remarks>
		/// Like <see cref="TimeToMoveMouse"/> this is a ceiling rather than a target. What the release
		/// actually needs is for the press to have reached the widgets, so the hold lasts until the UI thread
		/// has taken the press and no longer; the ceiling only bounds how long that is waited for.
		/// </remarks>
		public static double UpDelaySeconds { get; set; } = .1;

		/// <summary>
		/// The default ceiling for a single <see cref="WaitForPendingUiWork(int)"/>. The RunOnIdle pump
		/// ticks every 10ms, so this is 25 pump intervals: long past the point where the UI thread is merely
		/// idle and into "it is inside real work". Queued input is delivered in order whenever it does come
		/// back, so waiting longer than this only costs test time.
		/// </summary>
		private const int DefaultUiWorkWaitMilliseconds = 250;

		/// <summary>
		/// The ceiling for the pump wait between polls of the widget tree. Never longer than the fixed 50ms
		/// sleep this replaced, so a dead pump degrades to exactly the old polling rate rather than stalling.
		/// </summary>
		private const int WidgetPollWaitMilliseconds = 50;

		public enum InputType
		{
			Native,
			Simulated,
			SimulatedDrawMouse
		}

		public static IInputMethod OverrideInputSystem = null;

		// change default to SimulatedDrawMouse
		public AutomationRunner(IInputMethod inputMethod, bool drawSimulatedMouse, string imageDirectory = "")
		{
#if !__ANDROID__
			if (OverrideInputSystem != null)
			{
				inputSystem = OverrideInputSystem;
			}
			else
			{
				inputSystem = new AggInputMethods(this, drawSimulatedMouse);
				// TODO: Consider how to set this and if needed
				// HookWindowsInputAndSendToWidget.EnableInputHook = false;
			}
#else
				inputSystem = new AggInputMethods(this, drawSimulatedMouse);
#endif
			this.imageDirectory = imageDirectory;
		}

		public enum ClickOrigin
		{
			LowerLeft,
			Center
		}

		public enum InterpolationType
		{
			LINEAR,
			EASE_IN,
			EASE_OUT,
			EASE_IN_OUT
		}

		[Flags]
		public enum ModifierKeys
		{
			None = 0,
			Shift = 0x1,
			Control = 0x2,
			Alt = 0x4
		}

		public Point2D CurrentMousePosition()
		{
			return inputSystem.CurrentMousePosition();
		}

		public ImageBuffer GetCurrentScreen()
		{
			return inputSystem.GetCurrentScreen();
		}

		public bool ClickImage(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return ClickImage(imageToLookFor, secondsToWait, searchRegion, offset, origin, mouseButtons);
			}

			return false;
		}

		public bool ClickImage(ImageBuffer imageNeedle, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			if (origin == ClickOrigin.Center)
			{
				offset.x += imageNeedle.Width / 2;
				offset.y += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out Vector2 matchPosition, out _, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				var screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);

				inputSystem.CreateMouseEvent(GetMouseDown(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);
				HoldButton();
				inputSystem.CreateMouseEvent(GetMouseUp(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		private int GetMouseDown(MouseButtons mouseButtons)
		{
			switch (mouseButtons)
			{
				case MouseButtons.None:
					return 0;

				case MouseButtons.Left:
					return MouseConsts.MOUSEEVENTF_LEFTDOWN;

				case MouseButtons.Right:
					return MouseConsts.MOUSEEVENTF_RIGHTDOWN;

				case MouseButtons.Middle:
					return MouseConsts.MOUSEEVENTF_MIDDLEDOWN;

				default:
					return 0;
			}
		}

		private int GetMouseUp(MouseButtons mouseButtons)
		{
			switch (mouseButtons)
			{
				case MouseButtons.None:
					return 0;

				case MouseButtons.Left:
					return MouseConsts.MOUSEEVENTF_LEFTUP;

				case MouseButtons.Right:
					return MouseConsts.MOUSEEVENTF_RIGHTUP;

				case MouseButtons.Middle:
					return MouseConsts.MOUSEEVENTF_MIDDLEUP;

				default:
					return 0;
			}
		}

		public AutomationRunner Delay(double secondsToWait = .2)
		{
			Thread.Sleep((int)(secondsToWait * 1000));

			return this;
		}

		/// <summary>
		/// Blocks until the UI thread has run everything that was queued before this call.
		/// </summary>
		/// <remarks>
		/// This is the signal that replaces the fixed sleeps that used to pad simulated input. Simulated
		/// mouse events are handed to the widgets through <see cref="UiThread.RunOnIdle(Action)"/>, and the
		/// pump drains that queue in the order it was filled, so a sentinel queued now having run means every
		/// event queued before it has already been delivered. Waiting on that is both faster than a fixed
		/// sleep (the pump ticks every 10ms) and more honest than one, which could expire while the UI thread
		/// was still busy.
		/// </remarks>
		/// <param name="maxMilliseconds">How long to wait before giving up and letting the test carry on.</param>
		/// <returns>True if the UI thread reached the sentinel within the time allowed.</returns>
		public bool WaitForPendingUiWork(int maxMilliseconds = DefaultUiWorkWaitMilliseconds)
		{
			if (maxMilliseconds <= 0)
			{
				return false;
			}

			// Deliberately never disposed. The UI thread can reach the sentinel after this method has given
			// up on it, and setting a disposed event throws - on the UI thread, where it would surface as a
			// test failure caused by the waiting rather than by the test. Let the GC collect it instead.
			var pumped = new ManualResetEventSlim(false);

			UiThread.RunOnIdle(() => pumped.Set());

			// wasm has one thread and cannot block it, so the sentinel could never be reached from here -
			// this reports the same "gave up waiting" the timeout path does. UI automation is a desktop
			// feature; nothing in the browser head drives an AutomationRunner.
			if (OperatingSystem.IsBrowser())
			{
				return false;
			}

			return pumped.Wait(maxMilliseconds);
		}

		/// <summary>
		/// Wait for the given condition to be satisfied. The check Interval should be nice and short to allow test to
		/// complete quickly.
		/// </summary>
		/// <param name="checkConditionSatisfied"></param>
		/// <param name="maxSeconds"></param>
		/// <param name="checkInterval"></param>
		public static bool StaticDelay(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 10)
		{
			var timer = Stopwatch.StartNew();

			// TotalSeconds, not Seconds: Seconds is the 0-59 component of the elapsed time, so any wait of a
			// minute or more never expired - it silently became an infinite loop.
			while (timer.Elapsed.TotalSeconds < maxSeconds)
			{
				if (checkConditionSatisfied())
				{
					return true;
				}

				Thread.Sleep(checkInterval);
			}

			return false;
		}

		/// <summary>
		/// Wait up to maxSeconds for the condition to be satisfied.
		/// </summary>
		/// <param name="checkConditionSatisfied">The condition to wait for</param>
		/// <param name="maxSeconds">The maximum amount of time to wait</param>
		/// <param name="checkInterval">
		/// The frequency to recheck the condition in milliseconds. Matches <see cref="StaticDelay"/>'s own
		/// default: it used to be 200 here, which meant a condition that became true in 5ms still cost the
		/// test a fifth of a second, multiplied by every wait in every test.
		/// </param>
		/// <returns>Returns if the condition was satisfied within maxSeconds</returns>
		public AutomationRunner WaitFor(Func<bool> checkConditionSatisfied, double maxSeconds = 5, int checkInterval = 10)
		{
			StaticDelay(checkConditionSatisfied, maxSeconds, checkInterval);

			return this;
		}

		public AutomationRunner Assert(Func<bool> checkConditionSatisfied, string errorResponse, double maxSeconds = 5, int checkInterval = 10)
		{
			var satisfied = StaticDelay(checkConditionSatisfied, maxSeconds, checkInterval);

			if (!satisfied)
			{
				throw new Exception($"Require Failed: {errorResponse}");
			}

			return this;
		}

		public bool DoubleClickImage(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		public bool DragDropImage(ImageBuffer imageNeedleDrag,
			ImageBuffer imageNeedleDrop,
			double secondsToWait = DefaultWidgetWaitSeconds,
			SearchRegion searchRegion = null,
			Point2D offsetDrag = default(Point2D),
			ClickOrigin originDrag = ClickOrigin.Center,
			Point2D offsetDrop = default(Point2D),
			ClickOrigin originDrop = ClickOrigin.Center)
		{
			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (DragImage(imageNeedleDrag, secondsToWait, searchRegion, offsetDrag, originDrag))
			{
				return DropImage(imageNeedleDrop, secondsToWait, searchRegion, offsetDrop, originDrop);
			}

			return false;
		}

		public bool DragDropImage(string imageNameDrag,
			string imageNameDrop,
			double secondsToWait = DefaultWidgetWaitSeconds,
			SearchRegion searchRegion = null,
			Point2D offsetDrag = default(Point2D),
			ClickOrigin originDrag = ClickOrigin.Center,
			Point2D offsetDrop = default(Point2D),
			ClickOrigin originDrop = ClickOrigin.Center)
		{
			ImageBuffer imageNeedleDrag = LoadImageFromSourceFolder(imageNameDrag);
			if (imageNeedleDrag != null)
			{
				ImageBuffer imageNeedleDrop = LoadImageFromSourceFolder(imageNameDrop);
				if (imageNeedleDrop != null)
				{
					return DragDropImage(imageNeedleDrag, imageNeedleDrop, secondsToWait, searchRegion, offsetDrag, originDrag, offsetDrop, originDrop);
				}
			}

			return false;
		}

		public void RenderMouse(GuiWidget targetWidget, Graphics2D graphics2D)
		{
			GuiWidget parentSystemWindow = targetWidget;
			while (parentSystemWindow != null
				&& parentSystemWindow as SystemWindow == null)
			{
				parentSystemWindow = parentSystemWindow.Parent;
			}

			if (parentSystemWindow != null)
			{
				Point2D mousePosOnWindow = ScreenToSystemWindow(inputSystem.CurrentMousePosition(), (SystemWindow)parentSystemWindow);
				var circle = new Ellipse(new Vector2(mousePosOnWindow.x, mousePosOnWindow.y), 10);

				if (inputSystem.LeftButtonDown)
				{
					graphics2D.Render(circle, Color.Green);

					var mods = string.Join("", new[] { (Keys.Shift, "S"), (Keys.Control, "C") }
						.Select(x => Keyboard.IsKeyDown(x.Item1) ? x.Item2 : "")
						.Where(v => !string.IsNullOrEmpty(v)));

					if (inputSystem.ClickCount > 1)
					{
						mods += inputSystem.ClickCount.ToString();
					}

					if (!string.IsNullOrEmpty(mods))
					{
						graphics2D.DrawString(mods, mousePosOnWindow.x, mousePosOnWindow.y, 8, justification: Justification.Center, baseline: Baseline.BoundsCenter);
					}
				}

				graphics2D.Render(new Stroke(circle, 3), Color.Black);
				graphics2D.Render(new Stroke(circle, 2), Color.White);
			}
		}

		public bool DragImage(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return DragImage(imageToLookFor, secondsToWait, searchRegion, offset, origin);
			}

			return false;
		}

		public bool DragImage(ImageBuffer imageNeedle, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			if (origin == ClickOrigin.Center)
			{
				offset.x += imageNeedle.Width / 2;
				offset.y += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out Vector2 matchPosition, out _, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				var screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropImage(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return DropImage(imageToLookFor, secondsToWait, searchRegion, offset, origin);
			}

			return false;
		}

		public bool DropImage(ImageBuffer imageNeedle, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			if (origin == ClickOrigin.Center)
			{
				offset.x += imageNeedle.Width / 2;
				offset.y += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out Vector2 matchPosition, out _, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				var screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public AutomationRunner ScrollIntoView(string widgetName, ScrollAmount scrollAmount = ScrollAmount.Minimum)
		{
			// Find any sibling toggle switch and scroll the parent to the bottom
			var widgets = GetWidgetsByName(widgetName, onlyVisible: false);

			IEnumerable<(GuiWidget widget, int index)> widgetsByDepth = widgets.Select(w => (w.Widget, w.Widget.Parents<GuiWidget>().Where(p => p.ActuallyVisibleOnScreen()).Count()));

			var widget = widgetsByDepth.OrderBy(wbd => wbd.index).FirstOrDefault().widget;

			if (widget != null)
			{
				var parents = widget.Parents<ScrollableWidget>();
				var scrollable = parents.FirstOrDefault();
				if (scrollable != null)
				{
					scrollable.ScrollIntoView(widget);
					scrollable.ScrollArea.Width = scrollable.ScrollArea.Width + 1;
					scrollable.Width = scrollable.Width + 1;
				}
			}

			return this;
		}

		public bool ImageExists(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return ImageExists(imageToLookFor, secondsToWait, searchRegion);
			}

			return false;
		}

		public bool ImageExists(ImageBuffer imageNeedle, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null)
		{
			if (secondsToWait > 0)
			{
				bool foundImage = WaitForImage(imageNeedle, secondsToWait, searchRegion);
				if (!foundImage)
				{
					return false;
				}
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out _, out _, MatchLimit))
			{
				return true;
			}

			return false;
		}

		public bool MoveToImage(string imageName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		private static Point2D SystemWindowToScreen(Point2D pointOnWindow, SystemWindow containingWindow)
		{
			var screenPosition = new Point2D(pointOnWindow.x, (int)containingWindow.Height - pointOnWindow.y);

			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			if (mappingWidget != null)
			{
				screenPosition.x += mappingWidget.DesktopPosition.x;
				screenPosition.y += mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;
			}

			return screenPosition;
		}

		public static Point2D ScreenToSystemWindow(Point2D pointOnScreen, SystemWindow containingWindow)
		{
			Point2D screenPosition = pointOnScreen;
			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.x -= mappingWidget.DesktopPosition.x;
			screenPosition.y -= mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;

			screenPosition.y = (int)containingWindow.Height - screenPosition.y;

			return screenPosition;
		}

		public static ScreenRectangle SystemWindowToScreen(RectangleDouble rectOnScreen, SystemWindow containingWindow)
		{
			var screenPosition = new ScreenRectangle()
			{
				Left = (int)rectOnScreen.Left,
				Top = (int)rectOnScreen.Top,
				Right = (int)rectOnScreen.Right,
				Bottom = (int)rectOnScreen.Bottom,
			};

			screenPosition.Top = (int)containingWindow.Height - screenPosition.Top;
			screenPosition.Bottom = (int)containingWindow.Height - screenPosition.Bottom;

			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.Left += mappingWidget.DesktopPosition.x;
			screenPosition.Top += mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;
			screenPosition.Right += mappingWidget.DesktopPosition.x;
			screenPosition.Bottom += mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;

			return screenPosition;
		}

		private static RectangleDouble ScreenToSystemWindow(ScreenRectangle rectOnScreen, SystemWindow containingWindow)
		{
			var screenPosition = new ScreenRectangle()
			{
				Left = (int)rectOnScreen.Left,
				Top = (int)rectOnScreen.Top,
				Right = (int)rectOnScreen.Right,
				Bottom = (int)rectOnScreen.Bottom,
			};

			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.Left -= mappingWidget.DesktopPosition.x;
			screenPosition.Top -= mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;
			screenPosition.Left -= mappingWidget.DesktopPosition.x;
			screenPosition.Bottom -= mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;

			screenPosition.Top = (int)containingWindow.Height - screenPosition.Top;
			screenPosition.Bottom = (int)containingWindow.Height - screenPosition.Bottom;

			return new RectangleDouble()
			{
				Left = screenPosition.Left,
				Bottom = screenPosition.Bottom,
				Right = screenPosition.Right,
				Top = screenPosition.Top,
			};
		}

		private SearchRegion GetScreenRegion()
		{
			ImageBuffer screenImage = inputSystem.GetCurrentScreen();
			return new SearchRegion(screenImage,
				new ScreenRectangle()
				{
					Left = 0,
					Top = 0,
					Right = screenImage.Width,
					Bottom = screenImage.Height
				},
				this);
		}

		private ImageBuffer LoadImageFromSourceFolder(string imageName)
		{
			string pathToImage = Path.Combine(imageDirectory, imageName);

			if (File.Exists(pathToImage))
			{
				var imageToLookFor = new ImageBuffer();

				if (ImageIO.LoadImageData(pathToImage, imageToLookFor))
				{
					return imageToLookFor;
				}
			}

			return null;
		}

		public SearchRegion GetRegionByName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null)
		{
			GuiWidget namedWidget = GetWidgetByName(widgetName, out SystemWindow containingWindow, out _, secondsToWait, searchRegion);

			if (namedWidget != null)
			{
				RectangleDouble childBounds = namedWidget.TransformToParentSpace(containingWindow, namedWidget.LocalBounds);

				ScreenRectangle screenPosition = SystemWindowToScreen(childBounds, containingWindow);

				return new SearchRegion(this)
				{
					ScreenRect = screenPosition,
				};
			}

			return null;
		}

		public AutomationRunner GetWidgetByName(string widgetName, out GuiWidget widget, out SystemWindow containingWindow, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			widget = GetWidgetByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion, onlyVisible);
			return this;
		}

		public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			return GetWidgetByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion, onlyVisible);
		}

		private GuiWidget lastWidget = null;

		private void SetTarget(GuiWidget guiWidget)
		{
			if (lastWidget != null)
			{
				lastWidget.DebugShowBounds = false;
			}

			lastWidget = guiWidget;
			lastWidget.DebugShowBounds = true;

			UiThread.RunOnIdle(() => guiWidget.DebugShowBounds = false, 1);
		}

		public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, out Point2D offsetHint, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			containingWindow = null;
			offsetHint = Point2D.Zero;

			List<GetByNameResults> getResults = GetWidgetsByName(widgetName, secondsToWait, searchRegion, onlyVisible);
			if (getResults != null
				&& getResults.Count > 0)
			{
				// When multiple widgets share the same name, prefer the one with the
				// largest clipped visible area — it is most likely the interactive one.
				var best = getResults[0];
				if (getResults.Count > 1)
				{
					double bestArea = 0;
					foreach (var result in getResults)
					{
						var clipped = result.Widget.ClippedOnScreenBounds();
						double area = clipped.Width * clipped.Height;
						if (area > bestArea)
						{
							bestArea = area;
							best = result;
						}
					}
				}

				this.SetTarget(best.Widget);

				containingWindow = best.ContainingSystemWindow;
				offsetHint = best.OffsetHint;

				return best.Widget;
			}

			return null;
		}

		public object GetObjectByName(string widgetName, out SystemWindow containingWindow, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null)
		{
			return GetObjectByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion);
		}

		public object GetObjectByName(string widgetName, out SystemWindow containingWindow, out Point2D offsetHint, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			containingWindow = null;
			offsetHint = Point2D.Zero;

			List<GetByNameResults> getResults = GetWidgetsByName(widgetName, secondsToWait, searchRegion, onlyVisible);
			if (getResults != null
				&& getResults.Count > 0)
			{
				this.SetTarget(getResults[0].Widget);

				containingWindow = getResults[0].ContainingSystemWindow;
				offsetHint = getResults[0].OffsetHint;

				return getResults[0].NamedObject;
			}

			return null;
		}

		public class GetByNameResults
		{
			public GuiWidget Widget { get; private set; }

			public Point2D OffsetHint { get; private set; }

			public SystemWindow ContainingSystemWindow { get; private set; }

			public object NamedObject { get; private set; }

			public GetByNameResults(GuiWidget widget, Point2D offsetHint, SystemWindow containingSystemWindow, object namedItem)
			{
				this.Widget = widget;
				this.OffsetHint = offsetHint;
				this.ContainingSystemWindow = containingSystemWindow;
				this.NamedObject = namedItem;
			}
		}

		public List<GetByNameResults> GetWidgetsByName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			if (secondsToWait > 0)
			{
				bool foundWidget = WaitForName(widgetName, secondsToWait, onlyVisible);
				if (!foundWidget)
				{
					return null;
				}
			}

			var namedWidgetsInRegion = new List<GetByNameResults>();
			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows.Reverse())
			{
				if (searchRegion != null) // only add the widgets that are in the screen region
				{
					var namedWidgets = systemWindow.FindDescendants(widgetName);
					foreach (GuiWidget.WidgetAndPosition widgetAndPosition in namedWidgets)
					{
						if (!onlyVisible
							|| widgetAndPosition.Widget.ActuallyVisibleOnScreen())
						{
							RectangleDouble childBounds = widgetAndPosition.Widget.TransformToParentSpace(systemWindow, widgetAndPosition.Widget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, systemWindow);
							if (ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out ScreenRectangle result))
							{
								namedWidgetsInRegion.Add(new GetByNameResults(widgetAndPosition.Widget, widgetAndPosition.Position, systemWindow, widgetAndPosition.NamedObject));
							}
						}
					}
				}
				else // add every named widget found
				{
					var namedWidgets = systemWindow.FindDescendants(widgetName);
					foreach (GuiWidget.WidgetAndPosition namedWidget in namedWidgets)
					{
						if (!onlyVisible
							|| namedWidget.Widget.ActuallyVisibleOnScreen())
						{
							namedWidgetsInRegion.Add(new GetByNameResults(namedWidget.Widget, namedWidget.Position, systemWindow, namedWidget.NamedObject));
						}
					}
				}
			}

			return namedWidgetsInRegion;
		}

		/// <summary>
		/// Look for a widget with the given name and click it. It and all its parents must be visible and enabled.
		/// </summary>
		/// <param name="widgetName">The given widget name</param>
		/// <param name="secondsToWait">Total seconds to stay in this function waiting for the named widget to become visible.</param>
		/// <returns>The current AutomationRunner so commands can be issued in sequence.</returns>
		public AutomationRunner ClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, bool isDoubleClick = false, double secondsToWait = DefaultWidgetWaitSeconds)
		{
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);

			if (widgetToClick != null)
			{
				this.ClickWidget(widgetToClick, containingWindow, origin, offset, offsetHint, isDoubleClick);

				return this;
			}

            // If we see "Queue... Menu" we should be changing the test to look for a different source. There is no longer a queue menu.
            throw new Exception($"ClickByName Failed: Named GuiWidget not found [{widgetName}]");
		}

		/// <summary>
		/// Click the given widget via automation methods
		/// </summary>
		/// <param name="widget">The widget to click</param>
		/// <param name="isDoubleClick">Set to true to simulate a double-click</param>
		public AutomationRunner ClickWidget(GuiWidget widget, bool isDoubleClick = false)
		{
			var systemWindow = widget.Parents<SystemWindow>().FirstOrDefault();
			var center = widget.LocalBounds.Center;

			ClickWidget(
				widget,
				systemWindow,
				ClickOrigin.Center,
				Point2D.Zero,
				new Point2D(center.X, center.Y),
				isDoubleClick);

			return this;
		}

		private void ClickWidget(GuiWidget widget, SystemWindow containingWindow, ClickOrigin origin, Point2D offset, Point2D offsetHint, bool isDoubleClick = false)
		{
			MoveMouseToWidget(widget, containingWindow, offset, offsetHint, origin, out Point2D screenPosition);
			inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

			if (!isDoubleClick)
			{
				// Only a single click can afford to settle here; for a double click this frame would be
				// spent out of the 550ms the two presses have to share (see below).
				WaitforDraw(containingWindow);
			}

			if (isDoubleClick)
			{
				// A real double click is two complete press/release pairs - down(1) up down(2) up -
				// with only the second DOWN reporting a click count of 2 (WinForms semantics; ups
				// always report 1). The click number is stated explicitly on the second down rather
				// than inferred from event spacing.
				//
				// Stating the count is necessary but not sufficient: GuiWidget.IsDoubleClick also
				// requires the two DOWNs to be processed within 550ms of each other, and a widget that
				// asks during its own OnMouseDown (ListViewItemBase does) is comparing against the
				// FIRST down. So nothing may be waited on in here - no UpDelay, no draw - or a loaded
				// machine spends the whole window on the intervening frames and the widget correctly
				// concludes it received two single clicks. Two single clicks on a library folder row
				// select it twice and open nothing, silently: no drill-in, no event, no error, and a
				// test that then waits for content that will never load. Issue the three events
				// back-to-back and let the draws happen after the pair has been delivered.
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 2, 0);
			}

			HoldButton();

			inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

			WaitforDraw(containingWindow);

			// One wait just isn't enough sometimes. Maybe there's some more deferred processing going on.
			// ValidateDoUndoTranslateXY appears to be more sensitive to this timing.
			WaitforDraw(containingWindow);

			Delay(0.2);
		}

		/// <summary>
		/// Look for a widget with the given name and click it. It and all its parents must be visible and enabled.
		/// </summary>
		/// <param name="widgetName">The given widget name</param>
		/// <param name="secondsToWait">Total seconds to stay in this function waiting for the named widget to become visible.</param>
		public AutomationRunner RightClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, bool isDoubleClick = false)
		{
			double secondsToWait = DefaultWidgetWaitSeconds;

			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RightClickWidget(widgetToClick, containingWindow, origin, offset, offsetHint, isDoubleClick);
				return this;
			}

			throw new Exception($"ClickByName Failed: Named GuiWidget not found [{widgetName}]");
		}

		public AutomationRunner RightClickWidget(GuiWidget widget)
		{
			var systemWindow = widget.Parents<SystemWindow>().FirstOrDefault();
			var center = widget.LocalBounds.Center;

			RightClickWidget(
				widget,
				systemWindow,
				ClickOrigin.Center,
				Point2D.Zero,
				new Point2D(center.X, center.Y));
			return this;
		}

		private void RightClickWidget(GuiWidget widgetToClick, SystemWindow containingWindow, ClickOrigin origin, Point2D offset, Point2D offsetHint, bool isDoubleClick = false)
		{
			MoveMouseToWidget(widgetToClick, containingWindow, offset, offsetHint, origin, out Point2D screenPosition);
			inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_RIGHTDOWN, screenPosition.x, screenPosition.y, 0, 0);
			WaitforDraw(containingWindow);

			if (isDoubleClick)
			{
				// Same authentic double-click shape as ClickWidget: two full press/release pairs,
				// the second down carrying the click count of 2.
				HoldButton();
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_RIGHTUP, screenPosition.x, screenPosition.y, 0, 0);
				WaitforDraw(containingWindow);

				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_RIGHTDOWN, screenPosition.x, screenPosition.y, 2, 0);
				WaitforDraw(containingWindow);
			}

			HoldButton();

			inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_RIGHTUP, screenPosition.x, screenPosition.y, 0, 0);

			WaitforDraw(containingWindow);

			Delay(0.2);
		}

		public AutomationRunner WaitforDraw(SystemWindow containingWindow, int maxSeconds = 30)
		{
			var resetEvent = new AutoResetEvent(false);

			void afterDraw(object s, DrawEventArgs e) => resetEvent.Set();
			void closed(object s, EventArgs e) => resetEvent.Set();

			UiThread.RunOnIdle(() =>
			{
				// The window appears to be reliably closed already in the SoftwareLevelingTest test.
				if (containingWindow.HasBeenClosed)
					resetEvent.Set();
				else
				{
					containingWindow.AfterDraw += afterDraw;
					containingWindow.Closed += closed;
					containingWindow.Invalidate();
				}
			});

			resetEvent.WaitOne(maxSeconds);

			containingWindow.AfterDraw -= afterDraw;
			containingWindow.Closed -= closed;

			return this;
		}

		public AutomationRunner DragDropByName(string widgetNameDrag, string widgetNameDrop, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offsetDrag = default(Point2D), ClickOrigin originDrag = ClickOrigin.Center, Point2D offsetDrop = default(Point2D), ClickOrigin originDrop = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			DragByName(widgetNameDrag, secondsToWait, searchRegion, offsetDrag, originDrag, mouseButtons);
			DropByName(widgetNameDrop, secondsToWait, searchRegion, offsetDrop, originDrop, mouseButtons);

			return this;
		}

		public AutomationRunner DragByName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);
			DragStart(widgetToClick, containingWindow, origin, offset, offsetHint, mouseButtons);

			return this;
		}

		public AutomationRunner DragWidget(GuiWidget widget, Point2D travel, MouseButtons mouseButtons = MouseButtons.Left)
		{
			var systemWindow = widget.Parents<SystemWindow>().FirstOrDefault();
			var center = widget.LocalBounds.Center;

			var start = DragStart(
				widget,
				systemWindow,
				ClickOrigin.Center,
				Point2D.Zero,
				new Point2D(center.X, center.Y),
				mouseButtons);
			var screenPosition = new Point2D(start.x + travel.x, start.y + travel.y);
			SetMouseCursorPosition(screenPosition.x, screenPosition.y);

			return this;
		}

		public AutomationRunner DragToPosition(SystemWindow containingWindow, int x, int y)
		{
			var screenPosition = CurrentMousePosition();
			inputSystem.CreateMouseEvent(GetMouseDown(MouseButtons.Left), screenPosition.x, screenPosition.y, 0, 0);
			SetMouseCursorPosition(containingWindow, x, y);

			return this;
		}

		private Point2D DragStart(GuiWidget widgetToClick, SystemWindow containingWindow, ClickOrigin origin, Point2D offset, Point2D offsetHint, MouseButtons mouseButtons)
		{
			RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

			if (origin == ClickOrigin.Center)
			{
				offset += offsetHint;
			}

			var screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
			SetMouseCursorPosition(screenPosition.x, screenPosition.y);
			WaitforDraw(containingWindow);
			inputSystem.CreateMouseEvent(GetMouseDown(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);
			WaitforDraw(containingWindow);

			return screenPosition;
		}

		public AutomationRunner DropByName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);

			RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

			if (origin == ClickOrigin.Center)
			{
				offset += offsetHint;
			}

			Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
			SetMouseCursorPosition(screenPosition.x, screenPosition.y);
			WaitforDraw(containingWindow);
			Drop(mouseButtons);
			WaitforDraw(containingWindow);

			return this;
		}

		public AutomationRunner Drop(MouseButtons mouseButtons = MouseButtons.Left)
		{
			Point2D screenPosition = CurrentMousePosition();
			inputSystem.CreateMouseEvent(GetMouseUp(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);
			return this;
		}

		public AutomationRunner DoubleClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, double secondsToWait = 2)
		{
			return this.ClickByName(widgetName, searchRegion, offset, origin, isDoubleClick: true, secondsToWait);
		}

		public bool MoveToByName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset += offsetHint;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);

				return true;
			}

			return false;
		}

		public bool NameExists(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, bool onlyVisible = true)
		{
			return WaitForName(widgetName, secondsToWait, onlyVisible);
		}

		public bool NamedWidgetExists(string widgetName,
			SearchRegion searchRegion = null,
			bool onlyVisible = true,
			Func<GuiWidget, bool> predicate = null)
		{
			// Ignore SystemWindows with null PlatformWindow members - SystemWindow constructed but not yet shown
			foreach (SystemWindow window in SystemWindow.AllOpenSystemWindows.ToArray())
			{
				var foundChildren = window.FindDescendants(widgetName);
				if (foundChildren.Count > 0)
				{
					foreach (GuiWidget.WidgetAndPosition foundChild in foundChildren)
					{
						if (onlyVisible)
						{
							RectangleDouble childBounds = foundChild.Widget.TransformToParentSpace(window, foundChild.Widget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, window);
							if (searchRegion == null
								|| ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out _))
							{
								if (foundChild.Widget.ActuallyVisibleOnScreen()
									&& (predicate == null || predicate(foundChild.Widget)))
								{
									return true;
								}
							}
						}
						else
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool ChildExists<T>(SearchRegion searchRegion = null) where T : GuiWidget
		{
			// Ignore SystemWindows with null PlatformWindow members - SystemWindow constructed but not yet shown
			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows.ToArray())
			{
				// Get either the topmost or active SystemWindow
				var window = systemWindow.Parents<GuiWidget>().LastOrDefault() as SystemWindow ?? systemWindow;

				// Single window implementation requires both windows to be checked
				var foundChildren = window.Children<T>().Concat(systemWindow.Children<T>());
				if (foundChildren.Count() > 0)
				{
					foreach (var foundChild in foundChildren)
					{
						RectangleDouble childBounds = foundChild.TransformToParentSpace(window, foundChild.LocalBounds);

						ScreenRectangle screenRect = SystemWindowToScreen(childBounds, window);
						if (searchRegion == null || ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out _))
						{
							if (foundChild.ActuallyVisibleOnScreen())
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		private void MoveMouseToWidget(GuiWidget widget, SystemWindow containingWindow, Point2D offset, Point2D offsetHint, ClickOrigin origin, out Point2D screenPosition)
		{
			RectangleDouble childBounds = widget.TransformToParentSpace(containingWindow, widget.LocalBounds);
			screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

			var start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			if (origin == ClickOrigin.Center)
			{
				offset += offsetHint;
			}

			var moveTimer = Stopwatch.StartNew();

			for (int i = 0; i < MouseMoveSteps; i++)
			{
				childBounds = widget.TransformToParentSpace(containingWindow, widget.LocalBounds);

				screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

				var end = new Vector2(screenPosition.x, screenPosition.y);
				Vector2 delta = end - start;

				double ratio = i / (double)MouseMoveSteps;
				ratio = Cubic.Out(ratio);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.X, (int)current.Y);
				PaceMouseMove(moveTimer);
			}

			inputSystem.SetCursorPosition(screenPosition.x, screenPosition.y);
		}

		/// <summary>
		/// Waits between the steps of an interpolated mouse move.
		/// </summary>
		/// <remarks>
		/// The position that was just sent is queued for the UI thread, so what the next step actually needs
		/// is for the UI to have taken it - not for a fixed slice of the clock to pass. This used to be a
		/// flat 20ms sleep per step, which made every mouse move cost the same 100ms whether the UI was idle
		/// or hadn't finished the previous move yet. <see cref="TimeToMoveMouse"/> caps the whole move so a
		/// UI that is busy inside real work cannot stall the test; the moves queued behind it still arrive
		/// in order once its pump runs again.
		/// </remarks>
		/// <param name="moveTimer">Running since the first step of this move, so the cap covers the move as a whole.</param>
		private void PaceMouseMove(Stopwatch moveTimer)
		{
			int remainingMilliseconds = (int)((TimeToMoveMouse * 1000) - moveTimer.Elapsed.TotalMilliseconds);

			WaitForPendingUiWork(remainingMilliseconds);
		}

		/// <summary>
		/// Holds a simulated mouse button down between the press and the release.
		/// </summary>
		/// <remarks>
		/// The press is queued for the UI thread, and the only thing the release actually depends on is the
		/// press having got there - a widget that sees them in one batch has no press to match the release
		/// against. So the hold waits for the UI thread rather than sleeping <see cref="UpDelaySeconds"/>
		/// flat, which cost every click a tenth of a second whether the UI was ready in 2ms or not ready at
		/// all. Where the caller has already waited for a draw the press is long since delivered and this
		/// returns immediately.
		/// </remarks>
		private void HoldButton()
		{
			WaitForPendingUiWork((int)(UpDelaySeconds * 1000));
		}

		public void SetMouseCursorPosition(SystemWindow systemWindow, int x, int y)
		{
			Point2D screenPosition = SystemWindowToScreen(new Point2D(x, y), systemWindow);
			SetMouseCursorPosition(screenPosition.x, screenPosition.y);
		}

		public void SetMouseCursorPosition(int x, int y)
		{
			var start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			var end = new Vector2(x, y);
			Vector2 delta = end - start;

			var moveTimer = Stopwatch.StartNew();

			for (int i = 0; i < MouseMoveSteps; i++)
			{
				double ratio = i / (double)MouseMoveSteps;
				ratio = Cubic.Out(ratio);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.X, (int)current.Y);
				PaceMouseMove(moveTimer);
			}

			inputSystem.SetCursorPosition((int)end.X, (int)end.Y);
		}

		public void Dispose()
		{
			inputSystem.Dispose();
			inputSystem = null;
		}

		public void KeyDown(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		public void KeyUp(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Send modifier key presses to the system window. Modifiers may be combined
		/// as in Control+Shift.
		/// </summary>
		/// <param name="modifierKeys">Modifier keys to be pressed</param>
		/// <returns>The automation runner to allow call chaining</returns>
		public AutomationRunner PressModifierKeys(ModifierKeys modifierKeys)
		{
			if (modifierKeys == ModifierKeys.None)
			{
				return this;
			}

			var keys = Keys.None;
			if ((modifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
			{
				keys = (Keys)((uint)keys | (uint)Keys.ShiftKey | (uint)Keys.Shift);
			}
			if ((modifierKeys & ModifierKeys.Control) == ModifierKeys.Control)
			{
				keys = (Keys)((uint)keys | (uint)Keys.ControlKey | (uint)Keys.Control);
			}
			if ((modifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt)
			{
				keys = (Keys)((uint)keys | (uint)Keys.Menu | (uint)Keys.Alt);
			}

			inputSystem.PressModifierKeys(keys);
			Delay(.2);

			return this;
		}

		/// <summary>
		/// Release modifier keys that were previously pressed.
		/// </summary>
		/// <param name="modifierKeys">Modifier keys to be released</param>
		/// <returns>The automation runner to allow call chaining</returns>
		public AutomationRunner ReleaseModifierKeys(ModifierKeys modifierKeys)
		{
			if (modifierKeys == ModifierKeys.None)
			{
				return this;
			}

			var keys = Keys.None;
			if ((modifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
			{
				keys = (Keys)((uint)keys | (uint)Keys.ShiftKey);
			}
			if ((modifierKeys & ModifierKeys.Control) == ModifierKeys.Control)
			{
				keys = (Keys)((uint)keys | (uint)Keys.ControlKey);
			}
			if ((modifierKeys & ModifierKeys.Alt) == ModifierKeys.Alt)
			{
				keys = (Keys)((uint)keys | (uint)Keys.Menu);
			}

			inputSystem.ReleaseModifierKeys(keys);
			Delay(.2);

			return this;
		}

		/// <summary>
		/// Send the string to the system window
		/// ^ will add the control key
		/// {Enter} will type the enter key
		/// {BACKSPACE} will type the backspace key
		/// </summary>
		/// <param name="textToType"></param>
		public AutomationRunner Type(string textToType)
		{
			inputSystem.Type(textToType);
			Delay(.2);

			return this;
		}

		public bool WaitForImage(string imageName, double secondsToWait, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return WaitForImage(imageToLookFor, secondsToWait, searchRegion);
			}

			return false;
		}

		public bool WaitForImage(ImageBuffer imageNeedle, double secondsToWait, SearchRegion searchRegion = null)
		{
			var timeWaited = Stopwatch.StartNew();
			while (!ImageExists(imageNeedle)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				Delay(.05);
			}

			if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Wait up to secondsToWait for the named widget to exist and be visible.
		/// </summary>
		/// <param name="widgetName">The name of the widget to wait for</param>
		/// <returns></returns>
		public bool WaitForName(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds, bool onlyVisible = true, Func<GuiWidget, bool> predicate = null)
		{
			try
			{
				// TODO: should have a search region
				var timeWaited = Stopwatch.StartNew();
				while (!NamedWidgetExists(widgetName, null, onlyVisible, predicate)
					&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
				{
					// The widget tree only changes on the UI thread, so asking again before it has run again
					// can only give the same answer. Waiting on its pump rechecks the moment there is
					// something new to see, instead of on a fixed 50ms tick.
					WaitForPendingUiWork(WidgetPollWaitMilliseconds);
				}

				if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
				{
					return false;
				}

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Wait up to secondsToWait for the named widget to disappear
		/// </summary>
		/// <param name="widgetName"></param>
		public bool WaitForWidgetDisappear(string widgetName, double secondsToWait) // TODO: should have a search region
		{
			var timeWaited = Stopwatch.StartNew();
			while (NamedWidgetExists(widgetName)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				// Waiting on the UI thread's queue rather than the clock - the widget can only go away as a
				// result of UI thread work, so that is the only thing worth waiting for.
				WaitForPendingUiWork(WidgetPollWaitMilliseconds);
			}

			if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
			{
				return false;
			}

			return true;
		}

		public AutomationRunner WaitForWidgetEnabled(string widgetName, double secondsToWait = DefaultWidgetWaitSeconds) // TODO: should have a search region
		{
			// This can be called after a Reload All. Wait for the next draw in the hope that the UI will sort itself out in time.
			// Otherwise, the next `GetWidgetByName` call might pick up an orphaned (closed) widget.
			var widget = this.GetWidgetByName(widgetName, out SystemWindow window);
			WaitforDraw(window);

			var timeWaited = Stopwatch.StartNew();
			while (!NamedWidgetExists(widgetName)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				// Same reasoning as WaitForName - the tree only moves when the UI thread does.
				WaitForPendingUiWork(WidgetPollWaitMilliseconds);
			}

			widget = this.GetWidgetByName(widgetName, out SystemWindow _);
			if (widget == null
				|| this.WaitFor(() => widget.ActuallyVisibleOnScreen() && widget.Enabled,
				secondsToWait - timeWaited.Elapsed.TotalSeconds) == null)
			{
				throw new Exception($"WaitForWidgetEnabled Failed: Named GuiWidget not found [{widgetName}]");
			}

			if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
			{
				throw new Exception($"WaitForWidgetEnabled Failed: Time elapsed [{secondsToWait}] seconds");
			}

			return this;
		}

		public AutomationRunner SelectAll()
		{
			// Type into focused widget
			return this.Type("^a"); // select all
		}

		public AutomationRunner SelectNone()
		{
			// Type into focused widget
			return this.Type(" "); // clear the selection (type a space)
		}

		public static IInputMethod InputMethod { get; set; }

		public static bool DrawSimulatedMouse { get; set; } = true;

		/// <summary>
		/// Gets or sets how long the close phase of a test may take before the window is force closed.
		/// An application that cancels its close (e.g. to show a "save changes?" dialog) would otherwise
		/// leave the main thread blocked in the message pump forever. Tests may shorten this.
		/// </summary>
		public static double CloseWindowTimeoutSeconds { get; set; } = 15;

		/// <summary>
		/// Posts the action to the platform window's UI thread. Reflection is used because GuiAutomation does
		/// not reference WinForms; on Windows this reaches Control.BeginInvoke, which posts a real window
		/// message and therefore still runs when the RunOnIdle timer pump is not running.
		/// </summary>
		/// <returns>True when the action was successfully posted.</returns>
		private static bool TryBeginInvokeOnPlatformWindow(object platformWindow, Action action)
		{
			if (platformWindow == null)
			{
				return false;
			}

			try
			{
				var beginInvoke = platformWindow.GetType().GetMethod("BeginInvoke", new[] { typeof(Delegate) });
				if (beginInvoke == null)
				{
					return false;
				}

				beginInvoke.Invoke(platformWindow, new object[] { action });

				return true;
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Could not marshal the force close to the platform window: {ex.Message}");

				return false;
			}
		}

		/// <summary>
		/// Closes the platform window itself (Form.Close on Windows) so the message loop exits even when the
		/// agg SystemWindow is already marked closed. Must be called on the UI thread.
		/// </summary>
		private static void ClosePlatformWindow(object platformWindow)
		{
			if (platformWindow == null)
			{
				return;
			}

			try
			{
				platformWindow.GetType().GetMethod("Close", System.Type.EmptyTypes)?.Invoke(platformWindow, null);
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Could not close the platform window: {ex.Message}");
			}
		}

		/// <summary>
		/// Traces one milestone of the run's close timeline.
		/// </summary>
		/// <remarks>
		/// Deliberately not <see cref="DebugLogger.LogMessage"/>, which is [Conditional("DEBUG")] and so
		/// compiles the whole timeline out of the Release build that CI runs - which is exactly how a hung
		/// shutdown came to print nothing at all between the watchdog latching and the process finally
		/// draining minutes later. Calling Log directly keeps these lines in Release, where
		/// <see cref="DebugLogger.EchoToConsole"/> (set at test start) puts them on stdout for the TRX.
		/// </remarks>
		private static void LogClosePhase(string message)
		{
			DebugLogger.Log("AutomationRunner", message, DebugLevel.Message);
		}

		/// <remarks>
		/// Everything up to and including <c>ShowAsSystemWindow</c> runs synchronously on the calling
		/// thread - that thread has to become the platform message loop, so this method cannot yield
		/// before then. The first await is only reached once the loop has exited.
		/// </remarks>
		/// <param name="timeoutIsTheExpectedOutcome">
		/// True for a test that is <em>about</em> the timeout - one asserting that a run which overstays is
		/// cut off - rather than one that would only time out if something were wrong. The load watchdog is
		/// then not armed: its thread dump is diagnostic evidence for a window that unexpectedly never
		/// painted, and a test whose whole point is the deadline would produce that evidence on every run,
		/// costing a capture and an artifact each time and burying the one dump somebody actually needs.
		/// </param>
		public static async Task ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, AutomationTest testMethod, double secondsToTestFailure = 30, string imagesDirectory = "", Action<AutomationRunner> closeWindow = null, bool timeoutIsTheExpectedOutcome = false)
		{
			// Enable debug logging for AutomationRunner
			DebugLogger.EnableFilter("AutomationRunner");

			// A test host's console is what the TRX records, so this is the only sink a build server can read
			// back. Without it the close timeline below exists only in a debugger nobody is attached to.
			DebugLogger.EchoToConsole = true;

			// The thread that gets here is the thread that will call ShowAsSystemWindow below and become the
			// message loop, so name it now: after it is stuck there is no way to ask it who it is.
			ThreadStackDump.RegisterCurrentThread("<<< UI THREAD (message pump)");

			LogClosePhase($"=== TEST START === WindowTitle: {initialSystemWindow.Title}");

			var testRunner = new AutomationRunner(InputMethod, DrawSimulatedMouse, imagesDirectory);

			var resetEvent = new AutoResetEvent(false);
			var uiThreadExceptionSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var uiThreadExceptionLock = new object();
			ExceptionDispatchInfo capturedUiThreadException = null;

			int closeRequested = 0;
			int closePhaseTimedOut = 0;

			// Set once the platform message loop has actually exited. HasBeenClosed is NOT a usable exit
			// condition on its own: a Close() issued from a background thread can mark the agg window closed
			// while the platform window stays up, and then the main thread sits in the pump forever.
			int showCompleted = 0;

			void RequestWindowClose()
			{
				if (Interlocked.Exchange(ref closeRequested, 1) != 0)
				{
					return;
				}

				LogClosePhase("REQUESTING WINDOW CLOSE");

				// The close budget must not be spent by the application's own shutdown work: MatterCAD's
				// closeWindow callback can legitimately take ~10 seconds before it even asks the window to
				// close. So the watchdog only starts counting once the callback has returned, tracked here.
				var closeWatch = Stopwatch.StartNew();
				int closeCallbackReturned = 0;
				double closeCallbackReturnedSeconds = 0;

				// Watch the close itself. If the application cancels the close (a "save changes?" dialog is
				// the usual culprit) or the close never reaches the platform window, the main thread stays
				// parked in ShowAsSystemWindow forever, so force the window closed and remember that we had
				// to - the run must fail rather than hang or pass silently.
				Task.Run(async () =>
				{
					try
					{
						// Remember the platform window while it is still reachable: SystemWindow.OnClosed nulls
						// PlatformWindow, and the platform window is the only handle we have on the message loop.
						object platformWindow = initialSystemWindow.PlatformWindow;

						while (true)
						{
							if (Volatile.Read(ref showCompleted) == 1)
							{
								return;
							}

							platformWindow = initialSystemWindow.PlatformWindow ?? platformWindow;

							// Recomputed every pass because the callback can return while we are waiting. If it
							// never returns (a hung shutdown) fall back to a hard cap so this still bails out.
							double deadlineSeconds = Volatile.Read(ref closeCallbackReturned) == 1
								? Volatile.Read(ref closeCallbackReturnedSeconds) + CloseWindowTimeoutSeconds
								: CloseWindowTimeoutSeconds * 3;

							if (closeWatch.Elapsed.TotalSeconds >= deadlineSeconds)
							{
								break;
							}

							await Task.Delay(50);
						}

						// Re-check immediately before forcing - a window that closed just in time is not a failure.
						if (Volatile.Read(ref showCompleted) == 1)
						{
							return;
						}

						DebugLogger.LogError(
							"AutomationRunner",
							"WINDOW FAILED TO CLOSE - forcing close. Either the UI thread never returned to its pump, or the "
							+ "pump is idle and nothing is waking it. "
							+ (IdlePumpPolicy.DescribeDriver?.Invoke() ?? "idle pump: no host published a driver."));
						Interlocked.Exchange(ref closePhaseTimedOut, 1);

						// Only on this path, and only once the run is already failing: the capture costs a
						// minidump of the whole process, which no healthy close should ever pay for. Taken
						// before the force close so the stacks show what the UI thread was stuck in while it
						// was ignoring the close, rather than whatever it moved on to afterwards.
						ThreadStackDump.WriteToConsole($"close watchdog latched - window still open {closeWatch.Elapsed.TotalSeconds:0.0} s after close was requested");

						void ForceCloseNow()
						{
							try
							{
								if (!initialSystemWindow.HasBeenClosed)
								{
									initialSystemWindow.ForceClose();
								}
							}
							catch (Exception ex)
							{
								// ForceClose runs the whole widget close cascade; a throw in there must neither
								// vanish nor stop us from closing the platform window below - that close is the
								// only thing that actually releases the parked message loop.
								DebugLogger.LogError("AutomationRunner", $"ForceClose threw while forcing the test window closed: {ex}");
							}
							finally
							{
								// Always close the platform window as well. When the agg window was already marked
								// closed the ForceClose above is a no-op, and only closing the platform window can
								// still get the message loop to exit.
								ClosePlatformWindow(platformWindow);
							}
						}

						// Marshal through the platform window first - that posts a real window message, so it
						// works even when the RunOnIdle pump is dead, which is one of the ways a run hangs here.
						// RunOnIdle is only the fallback.
						TryBeginInvokeOnPlatformWindow(platformWindow, ForceCloseNow);
						UiThread.RunOnIdle(ForceCloseNow);

						// Give the force close a grace period. If the loop still has not exited there is nothing
						// more this side can do, so say so loudly instead of hanging silently.
						var forceWatch = Stopwatch.StartNew();
						while (forceWatch.Elapsed.TotalSeconds < CloseWindowTimeoutSeconds)
						{
							if (Volatile.Read(ref showCompleted) == 1)
							{
								return;
							}

							await Task.Delay(50);
						}

						// One last look: the pump can exit in the window between the final poll and here.
						if (Volatile.Read(ref showCompleted) == 1)
						{
							return;
						}

						string stillBlocked = $"FORCED CLOSE DID NOT RELEASE THE MESSAGE LOOP after {CloseWindowTimeoutSeconds} seconds - the test process is hung in the platform window pump.";
						DebugLogger.LogError("AutomationRunner", stillBlocked);
						Console.WriteLine(stillBlocked);

						// Second capture, and worth its cost: the first one showed the UI thread ignoring the
						// close request, this one shows whether the posted force close moved it at all.
						ThreadStackDump.WriteToConsole("forced close did not release the message loop");
					}
					catch (Exception ex)
					{
						// Nothing observes this task, so an escaping exception (e.g. from touching
						// PlatformWindow during teardown) would silently kill the watchdog - the one thing
						// standing between a stuck close and a hung test run.
						DebugLogger.LogError("AutomationRunner", $"CLOSE WATCHDOG FAULTED: {ex}");
					}
				});

				try
				{
					if (closeWindow != null)
					{
						closeWindow(testRunner);
					}
					else
					{
						initialSystemWindow.CloseOnIdle();
					}
				}
				catch (Exception ex)
				{
					// A faulting shutdown callback would otherwise never reach a close request, and the run
					// would be misreported as a close hang instead of the real failure.
					DebugLogger.LogError("AutomationRunner", $"closeWindow callback threw: {ex}");

					// CaptureUiThreadException calls back into RequestWindowClose, which is already latched,
					// so close the window explicitly here.
					CaptureUiThreadException(ex);
					initialSystemWindow.CloseOnIdle();
				}
				finally
				{
					// Start the watchdog's real countdown now that the application's shutdown work is done.
					Volatile.Write(ref closeCallbackReturnedSeconds, closeWatch.Elapsed.TotalSeconds);
					Volatile.Write(ref closeCallbackReturned, 1);
				}
			}

			void CaptureUiThreadException(Exception exception)
			{
				var exceptionToCapture = exception ?? new Exception("Unhandled UI thread exception.");
				bool shouldSignal = false;

				lock (uiThreadExceptionLock)
				{
					if (capturedUiThreadException == null)
					{
						capturedUiThreadException = ExceptionDispatchInfo.Capture(exceptionToCapture);
						shouldSignal = true;
					}
				}

				if (!shouldSignal)
				{
					return;
				}

				DebugLogger.LogError("AutomationRunner", $"UI THREAD EXCEPTION: {exceptionToCapture}");
				uiThreadExceptionSignal.TrySetResult(true);
				RequestWindowClose();
			}

			System.Threading.ThreadExceptionEventHandler threadExceptionHandler = (s, e) =>
			{
				CaptureUiThreadException(e.Exception);
			};
			Action<Exception> uiThreadUnhandledExceptionHandler = CaptureUiThreadException;
			System.Reflection.EventInfo threadExceptionEvent = null;
			Delegate threadExceptionDelegate = null;

			UiThread.UnhandledException += uiThreadUnhandledExceptionHandler;

			try
			{
				var applicationType = System.Type.GetType("System.Windows.Forms.Application, System.Windows.Forms");
				var unhandledExceptionModeType = System.Type.GetType("System.Windows.Forms.UnhandledExceptionMode, System.Windows.Forms");
				if (applicationType != null
					&& unhandledExceptionModeType != null)
				{
					var catchExceptionMode = Enum.Parse(unhandledExceptionModeType, "CatchException");
					applicationType.GetMethod("SetUnhandledExceptionMode", new[] { unhandledExceptionModeType })
						?.Invoke(null, new[] { catchExceptionMode });
				}

				threadExceptionEvent = applicationType?.GetEvent("ThreadException");
				if (threadExceptionEvent != null)
				{
					threadExceptionDelegate = Delegate.CreateDelegate(
						threadExceptionEvent.EventHandlerType,
						threadExceptionHandler.Target,
						threadExceptionHandler.Method);
					threadExceptionEvent.AddEventHandler(null, threadExceptionDelegate);
				}
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Failed to register UI thread exception handler: {ex.Message}");
			}

			// Cancelled the moment the window loads, which is what makes the load watchdog below free on a
			// healthy run: it never wakes up at all.
			var windowLoaded = new CancellationTokenSource();

			// On load, release the reset event
			initialSystemWindow.Load += (s, e) =>
			{
				DebugLogger.LogMessage("AutomationRunner", $"LOAD EVENT FIRED - Setting resetEvent");
				resetEvent.Set();
				windowLoaded.Cancel();
			};

			// Puts a thread dump where the CI artifact upload will find it, and answers with the path. Never
			// throws: it decorates a failure that has to be reported whether or not this works.
			static string WriteDumpBeside(string dump, string reason)
			{
				try
				{
					// TestResults is what the workflow uploads on always(); a dump anywhere else is only
					// readable by someone sitting at the machine, which on CI is nobody.
					string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "TestResults");
					System.IO.Directory.CreateDirectory(directory);

					string path = System.IO.Path.Combine(
						directory,
						$"threadstacks-{reason}-{DateTime.Now:HHmmss}-{Environment.ProcessId}.txt");

					System.IO.File.WriteAllText(path, dump ?? "(the dump was empty)");

					return path;
				}
				catch (Exception ex)
				{
					return $"(could not be written: {ex.GetType().Name}: {ex.Message})";
				}
			}

			// What the render backend says about itself, for a diagnostic that has to work off any host.
			// RenderStatusReport is declared per platform window rather than on IPlatformWindow, so it is
			// read by name - the same way this class already reaches WinForms' Application. A window that
			// never painted answers "webgpu not initialized"; one whose device came up answers with the
			// backend, the adapter and how many frames it has presented, which is the difference between
			// "the GPU never started" and "the GPU is fine and nobody asked it to draw".
			static string DescribeRenderStatus(SystemWindow window)
			{
				try
				{
					object platformWindow = window?.PlatformWindow;
					if (platformWindow == null)
					{
						return "render status: no platform window";
					}

					// Bounded for the same reason DescribeIdleDriver's TryEnter is: this only ever runs when
					// something has already gone wrong, possibly on a UI thread that is wedged, and a host's
					// status property is free to touch state that thread owns. Every implementation today
					// reads cached fields and cannot block - but a diagnostic that can outlive the failure it
					// came to explain is not one worth having, and the next host need not know that rule.
					string status = null;
					var read = Task.Run(() =>
					{
						var property = platformWindow.GetType().GetProperty("RenderStatusReport");
						status = property?.GetValue(platformWindow)?.ToString() ?? "(not reported by this host)";
					});

					if (!read.Wait(TimeSpan.FromMilliseconds(500)))
					{
						return "render status: could not be read (the host's status property did not return)";
					}

					return $"render status: {status}";
				}
				catch (Exception ex)
				{
					return $"render status: could not be read ({ex.GetType().Name})";
				}
			}

			int testTimeout = (int)(1000 * secondsToTestFailure);
			Task delayTask = Task.Delay(testTimeout);
			Task uiExceptionTask = uiThreadExceptionSignal.Task;

			// Baseline for the timeout diagnostic below. GuiWidget.DrawCount is process-wide, so only the
			// change across this window's life means anything: zero says nothing painted at all (no frames -
			// look at the device and the pump), non-zero says painting happened but this window's tree was
			// not what got drawn (look at its size and its platform window).
			int drawCountAtShow = GuiWidget.DrawCount;

			// Start two tasks, the timeout and the test method. Block in the test method until the first draw
			// The load watchdog. It exists because the diagnostics in the timeout branch below cannot be
			// trusted to run: that branch begins at the same instant delayTask completes, so the WhenAny it
			// belongs to has already returned and the runner is on its way to teardown - measured, with a
			// capture that started, wrote nothing and left no file. This fires two seconds earlier, on a task
			// the runner awaits, so the window is still standing and nothing is racing it.
			//
			// What it is for: a window whose Show() has not returned. Load is raised by the first draw, so a
			// reset event that never fires means nothing painted, and the UI thread's own frames are the only
			// thing that says why. If the next reader is looking at that dump, the three unbounded calls on
			// the UI thread inside Show()/OnLoad are the shortlist to check first - the form's own handle
			// creation, Screen.FromControl(this).WorkingArea in PushDisplayUsableSize, and the first touch of
			// ApplicationIcon.Value - none of which is bounded the way device creation now is.
			int loadWatchdogDelay = Math.Max(10, testTimeout - 2000);
			var loadWatchdog = Task.Run(async () =>
			{
				if (timeoutIsTheExpectedOutcome)
				{
					// This run is supposed to end in a timeout, so a window that never painted is the result
					// being asserted rather than a symptom. Capturing it would spend the time and leave the
					// artifact on every single run, for a dump nobody will ever read.
					return;
				}

				try
				{
					await Task.Delay(loadWatchdogDelay, windowLoaded.Token);
				}
				catch (OperationCanceledException)
				{
					// The window loaded. Nothing to report, and nothing was spent.
					return;
				}

				DebugLogger.LogError(
					"AutomationRunner",
					$"LOAD WATCHDOG - the window has not painted {loadWatchdogDelay} ms after being shown;"
					+ " capturing the UI thread's stack while it is still up.");

				try
				{
					string dumpPath = WriteDumpBeside(
						ThreadStackDump.Capture("the window had not painted when the load watchdog fired"),
						"load-watchdog");

					DebugLogger.LogError("AutomationRunner", $"LOAD WATCHDOG - stacks written to {dumpPath}");
				}
				catch (Exception ex)
				{
					DebugLogger.LogError(
						"AutomationRunner",
						$"LOAD WATCHDOG - stack capture failed: {ex.GetType().Name}: {ex.Message}");
				}
			});

			var task = Task.WhenAny(delayTask, Task.Run(() =>
			{
				DebugLogger.LogMessage("AutomationRunner", "TASK STARTED - Waiting for resetEvent");
				// Wait until the first system window draw before running the test method, up to the timeout
				bool eventSet = resetEvent.WaitOne(testTimeout);
				DebugLogger.LogMessage("AutomationRunner", $"RESET EVENT RESULT - EventSet: {eventSet}");

				if (eventSet)
				{
					DebugLogger.LogMessage("AutomationRunner", "EXECUTING TEST METHOD");
					var result = testMethod(testRunner);
					DebugLogger.LogMessage("AutomationRunner", "TEST METHOD COMPLETED");
					return result;
				}
				else
				{
					// The reset event is set from the window's Load, and GuiWidget raises Load from its first
					// OnDraw - so this timeout does not mean "the window never got focus" or "the test never
					// started"; it means the window never painted. Everything below separates the ways that
					// can happen, because the message alone has sent an investigation the wrong way before.
					DebugLogger.LogError(
						"AutomationRunner",
						$"TIMEOUT - Reset event never set. The window's Load never fired, and Load is raised by the"
						+ $" first draw, so nothing painted. window={initialSystemWindow.Width}x{initialSystemWindow.Height}"
						+ $", hasBeenClosed={initialSystemWindow.HasBeenClosed}"
						+ $", platformWindow={(initialSystemWindow.PlatformWindow == null ? "null" : "present")}"
						+ $", widgetDrawsDuringThisWindow={GuiWidget.DrawCount - drawCountAtShow}"
						+ $", {DescribeRenderStatus(initialSystemWindow)}"
						+ $", {IdlePumpPolicy.DescribeDriver?.Invoke() ?? "idle pump: no host published a driver."}");

					// The fields above say what state the window is in; they cannot say what the UI thread is
					// doing, and that has been the missing half for several rounds of this. If Show() has not
					// returned, this dump names the frame it is stuck in - which is the difference between
					// guessing at the init path and reading it. The close watchdog has always dumped; this
					// path never did, and a window that never opened is at least as worth a dump as one that
					// never closed.
					// Through DebugLogger rather than ThreadStackDump.WriteToConsole: this runs inside the
					// test's own console capture, where a lone Console.WriteLine of a report this size was
					// observed to vanish while the LogError above - same thread, same instant - came through.
					// LogError is not [Conditional] and echoes to the console, so it is the sink with a track
					// record of reaching a CI log. Capture can throw; this must not, because it is decorating
					// a failure that has to be reported either way.
					// Skipped for a run whose timeout is the expected outcome, same reasoning as the load
					// watchdog above: there the timeout is a result being asserted, not evidence of anything.
					if (!timeoutIsTheExpectedOutcome)
					{
						try
						{
							// Inline, like the close watchdog's capture, which is the one that demonstrably reaches a
							// CI log. Written to a file rather than logged: a report this size did not survive the log
							// from here under either reporter, and TestResults is what the workflow uploads on always().
							//
							// Known limit, measured and not yet solved: everything after this point races the test's own
							// teardown. The outer Task.WhenAny returns the instant delayTask completes - the same instant
							// this branch starts - so on a short run the process can exit before a capture finishes. The
							// short log line above always lands; this may not. It costs nothing when it fails.
							string dumpPath = WriteDumpBeside(
								ThreadStackDump.Capture("the window's first draw never happened (reset event timed out)"),
								"load-timeout");

							DebugLogger.LogError("AutomationRunner", $"UI thread stacks written to {dumpPath}");
					}
					catch (Exception dumpException)
					{
						DebugLogger.LogError(
							"AutomationRunner",
							$"Thread stack dump failed: {dumpException.GetType().Name}: {dumpException.Message}");
					}
					}

					throw new TimeoutException("Reset event timed out");
				}
			}), uiExceptionTask);

			// Once either the timeout or the test method has completed, store if a timeout occurred and shutdown the SystemWindow
			task.ContinueWith(innerTask =>
			{
				LogClosePhase("CLEANUP - Task completed, calling CloseOnIdle");
				RequestWindowClose();
			});

			// Main thread blocks here until released via CloseOnIdle above
			bool originalAllowDropState = SystemWindow.EnableAllowDrop;
			SystemWindow.EnableAllowDrop = false;

			try
			{
				LogClosePhase("CALLING ShowAsSystemWindow");

				// This thread is about to become the message loop, so it is the thread await has to come back
				// to. Installing here rather than relying on the platform host means the context is in place
				// before the window's first idle tick, and covers hosts that never reach an idle timer. Scoped
				// because this thread belongs to the test harness, which reuses it once the loop has exited.
				using (MainLoopSynchronizationContext.InstallForScope())
				{
					initialSystemWindow.ShowAsSystemWindow();
				}
			}
			catch (Exception ex)
			{
				DebugLogger.LogError("AutomationRunner", $"ShowAsSystemWindow failed: {ex.Message}");
				SystemWindow.EnableAllowDrop = originalAllowDropState;
				throw;
			}
			finally
			{
				// The message loop has exited - this, not HasBeenClosed, is what the close watchdog waits for.
				Interlocked.Exchange(ref showCompleted, 1);

				UiThread.UnhandledException -= uiThreadUnhandledExceptionHandler;
				threadExceptionEvent?.RemoveEventHandler(null, threadExceptionDelegate);
			}

			// On the nominal, timeout and test-threw paths this task is already complete, because the only
			// thing that asks the window to close is its own ContinueWith above - the close cannot even
			// have been requested until it finished. The await is then a synchronous pass-through and the
			// cleanup below stays on the thread that ran the message loop.
			//
			// The UI-exception path can genuinely race. uiThreadExceptionSignal is created with
			// RunContinuationsAsynchronously, so CaptureUiThreadException's TrySetResult only *queues* the
			// WhenAny completion to the pool while it goes on to drive the close on this thread. Under pool
			// starvation the close can win, and then this await really does suspend and the cleanup resumes
			// on a pool thread. That is safe: the cleanup is thread-agnostic, and UiThread.InvokePendingActions
			// sees IsUiThread false, so it will not re-latch the UI thread id onto a pool thread - it just
			// skips a last-gasp drain in a run that is already failing on a UI thread exception. It also
			// cannot deadlock, because SynchronizationContext.Current is null here: the InstallForScope
			// around ShowAsSystemWindow has been disposed.
			//
			// That last point is the standing requirement. If a caller ever has MainLoopSynchronizationContext
			// current at this await, its Post enqueues to RunOnIdle - and nothing pumps RunOnIdle once
			// ShowAsSystemWindow has returned, so the continuation would never run.
			Task completedTask = await task;

			// Awaited here, before any teardown, which is the whole point of it being a task of its own: a
			// capture the runner waits for cannot be cut off by the close. On a healthy run it was cancelled
			// at Load and completed long ago, so this costs nothing.
			windowLoaded.Cancel();
			await loadWatchdog;
			windowLoaded.Dispose();

			bool timedOut = completedTask == delayTask;
			bool uiThreadFaulted = completedTask == uiExceptionTask;
			LogClosePhase($"SHOW COMPLETED - message loop exited - TimedOut: {timedOut}");

			// Wait for CloseOnIdle to complete
			testRunner.WaitFor(() => initialSystemWindow.HasBeenClosed);
			LogClosePhase($"WINDOW CLOSED - HasBeenClosed: {initialSystemWindow.HasBeenClosed}");

			// Restore the original EnableAllowDrop state
			SystemWindow.EnableAllowDrop = originalAllowDropState;

			// Reset the static window provider and firstWindow flag for the next test
			SystemWindow.ResetSystemWindowProvider();
			try
			{
				// Use reflection to call the static method since we can't directly reference the platform-specific class
				var winformsType = System.Type.GetType("MatterHackers.Agg.UI.WinformsSystemWindow, agg_platform_win32");
				var resetMethod = winformsType?.GetMethod("ResetFirstWindowFlag", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				resetMethod?.Invoke(null, null);
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Failed to reset WinformsSystemWindow state: {ex.Message}");
			}

			// Reset EnablePlatformWindowInput for the next test - this is critical for tests to receive input events
			try
			{
				// Use reflection to access the static property
				var platformWindowType = System.Type.GetType("MatterHackers.Agg.UI.IPlatformWindow, agg");
				var enableInputProperty = platformWindowType?.GetProperty("EnablePlatformWindowInput");
				if (enableInputProperty != null)
				{
					enableInputProperty.SetValue(null, true);
				}
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Failed to reset EnablePlatformWindowInput: {ex.Message}");
			}

			// IMPORTANT: Reset UiThread LAST after window is fully closed to avoid clearing CloseOnIdle actions
			try
			{
				// Let any remaining RunOnIdle actions complete first, still under the main loop context so a
				// continuation queued by this final drain lands in the same queue we are draining.
				using (MainLoopSynchronizationContext.InstallForScope())
				{
					UiThread.InvokePendingActions();
				}

				// Now reset UiThread static state for the next test
				UiThread.ResetForTests();
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Failed to reset UiThread state: {ex.Message}");
			}

			// Reset Keyboard static state for the next test  
			try
			{
				Keyboard.Clear();
			}
			catch (Exception ex)
			{
				DebugLogger.LogWarning("AutomationRunner", $"Failed to reset Keyboard state: {ex.Message}");
			}

			if (timedOut)
			{
				DebugLogger.LogError("AutomationRunner", "TEST TIMED OUT");
				throw new TimeoutException("TestMethod timed out");
			}

			if (uiThreadFaulted && capturedUiThreadException != null)
			{
				DebugLogger.LogError("AutomationRunner", $"TEST FAULTED ON UI THREAD: {capturedUiThreadException.SourceException.Message}");
				capturedUiThreadException.Throw();
			}

			// If the test task threw an exception, propagate it before checking
			// MarkTestComplete. The original exception is more useful than the
			// generic "did not call MarkTestComplete" message.
			if (completedTask != null && completedTask.IsFaulted)
			{
				DebugLogger.LogError("AutomationRunner", $"TEST FAULTED: {completedTask.Exception?.InnerException?.Message}");
				await completedTask; // throws the original exception
			}

			// Checked after the test body results so a real test failure still wins - but before
			// RequireTestCompletion, because a blocked shutdown is the more useful diagnosis.
			if (Volatile.Read(ref closePhaseTimedOut) == 1)
			{
				// Two different failures reach here and the dump tells them apart, so the message must not
				// pick one. A UI thread stopped *inside* shutdown work (device teardown, an uncancellable
				// render, a lock) never returns to dispatch. A UI thread asleep in WaitMessage is the
				// opposite: the pump is healthy and simply has nothing to process, because whatever should
				// have been waking it - the idle pump's driver, on this window's own thread - is not.
				// An earlier version of this message asserted the first shape; it sent an investigation of
				// the second one down the wrong path for a day. It says what it knows now, and names the
				// idle driver, which is the fact that separates the two.
				throw new TimeoutException(
					$"Test window failed to close within {CloseWindowTimeoutSeconds} seconds after the test completed. "
					+ "The posted force-close was never processed. Read the UI thread's frame in the ALL MANAGED THREAD "
					+ "STACKS dump above: inside shutdown work means it never got back to dispatching, while WaitMessage "
					+ "(or any idle pump frame) means the loop is alive and nothing is waking it - idle turns and posted "
					+ "messages are not arriving, so suspect the idle-pump driver. "
					+ (IdlePumpPolicy.DescribeDriver?.Invoke() ?? "idle pump: no host published a driver."));
			}

			// When RequireTestCompletion is set, verify the test signaled
			// completion by calling MarkTestComplete(). This catches tests that
			// exit early (e.g., silent return) without reaching their final statement.
			if (testRunner.RequireTestCompletion && !testRunner.TestWasCompleted)
			{
				DebugLogger.LogError("AutomationRunner", "TEST DID NOT CALL MarkTestComplete() - test may have exited early");
				throw new Exception("Test did not call MarkTestComplete(). The test may have exited before reaching its last statement.");
			}

			LogClosePhase("=== TEST COMPLETE ===");

			// The winning task is already complete; awaiting it hands its outcome - a cancellation, say -
			// to the calling context exactly as returning it used to.
			if (completedTask != null)
			{
				await completedTask;
			}
		}
	}
}
