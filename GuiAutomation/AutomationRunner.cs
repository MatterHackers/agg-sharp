/*
Copyright (c) 2022, Lars Brubaker
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
using System.Threading;
using System.Threading.Tasks;
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

		private const double DefaultWidgetWaitSeconds = 5.0;

		/// <summary>
		/// The number of seconds to move the mouse when going to a new position.
		/// </summary>
		public static double TimeToMoveMouse { get; set; } = .5;

		private string imageDirectory;

		public static double UpDelaySeconds { get; set; } = .2;

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
				Delay(UpDelaySeconds);
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
		/// Wait for the given condition to be satisfied. The check Interval should be nice and short to allow test to
		/// complete quickly.
		/// </summary>
		/// <param name="checkConditionSatisfied"></param>
		/// <param name="maxSeconds"></param>
		/// <param name="checkInterval"></param>
		public static bool StaticDelay(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 10)
		{
			var timer = Stopwatch.StartNew();

			while (timer.Elapsed.Seconds < maxSeconds)
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
		/// <param name="checkInterval">The frequency to recheck the condition in milliseconds</param>
		/// <returns>Returns if the condition was satisfied within maxSeconds</returns>
		public AutomationRunner WaitFor(Func<bool> checkConditionSatisfied, double maxSeconds = 5, int checkInterval = 200)
		{
			StaticDelay(checkConditionSatisfied, maxSeconds, checkInterval);

			return this;
		}

		public AutomationRunner Assert(Func<bool> checkConditionSatisfied, string errorResponse, double maxSeconds = 5, int checkInterval = 200)
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

					var mods = string.Join("", new[] {(Keys.Shift, "S"), (Keys.Control, "C")}
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
				// TODO: Widgets really shouldn't have the same ID for testing. But some cases still occur:
				//       PrinterTabRemainsAfterReloadAll: "Distance or Loops Field"
				//       AddingImageConverterWorks: "Row Item Image Converter"
				//       PulseLevelingTest: "Stop Task Button"
				//       But, well, what about common dialog box widgets?
				//if (getResults.Count > 1)
				//	throw new Exception($"Widgets have duplicate names: {widgetName}");

				this.SetTarget(getResults[0].Widget);

				containingWindow = getResults[0].ContainingSystemWindow;
				offsetHint = getResults[0].OffsetHint;

				return getResults[0].Widget;
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
			foreach(var systemWindow in SystemWindow.AllOpenSystemWindows.Reverse())
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
			WaitforDraw(containingWindow);

			if (isDoubleClick)
			{
				Thread.Sleep(150);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
				WaitforDraw(containingWindow);
			}

			Delay(UpDelaySeconds);

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
				Thread.Sleep(150);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_RIGHTDOWN, screenPosition.x, screenPosition.y, 0, 0);
				WaitforDraw(containingWindow);
			}

			Delay(UpDelaySeconds);

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

		public AutomationRunner DoubleClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			return this.ClickByName(widgetName, searchRegion, offset, origin, isDoubleClick: true);
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

			int steps = (int)(TimeToMoveMouse * 1000 / 20);
			var start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			if (origin == ClickOrigin.Center)
			{
				offset += offsetHint;
			}

			for (int i = 0; i < steps; i++)
			{
				childBounds = widget.TransformToParentSpace(containingWindow, widget.LocalBounds);

				screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

				var end = new Vector2(screenPosition.x, screenPosition.y);
				Vector2 delta = end - start;

				double ratio = i / (double)steps;
				ratio = Cubic.Out(ratio);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.X, (int)current.Y);
				Thread.Sleep(20);
			}

			inputSystem.SetCursorPosition(screenPosition.x, screenPosition.y);
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

			int steps = (int)(TimeToMoveMouse * 1000 / 20);
			for (int i = 0; i < steps; i++)
			{
				double ratio = i / (double)steps;
				ratio = Cubic.Out(ratio);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.X, (int)current.Y);
				Thread.Sleep(20);
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
					Delay(.05);
				}

				if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
				{
					return false;
				}

				return true;
			}
            catch (Exception e)
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
				Delay(.05);
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
				Delay(.05);
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

		public static Task ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, AutomationTest testMethod, double secondsToTestFailure = 30, string imagesDirectory = "", Action<AutomationRunner> closeWindow = null)
		{
			var testRunner = new AutomationRunner(InputMethod, DrawSimulatedMouse, imagesDirectory);

			var resetEvent = new AutoResetEvent(false);

			// Ignore real user input.
			IPlatformWindow.EnablePlatformWindowInput = false;

			// On load, release the reset event
			initialSystemWindow.Load += (s, e) =>
			{
				resetEvent.Set();
			};

			int testTimeout = (int)(1000 * secondsToTestFailure);

			Task delayTask = Task.Delay(testTimeout);

			// Start two tasks, the timeout and the test method. Block in the test method until the first draw
			var task = Task.WhenAny(delayTask, Task.Run(() =>
			{
				// Wait until the first system window draw before running the test method, up to the timeout
				resetEvent.WaitOne(testTimeout);
				return testMethod(testRunner);
			}));

			// Once either the timeout or the test method has completed, store if a timeout occurred and shutdown the SystemWindow
			task.ContinueWith(innerTask =>
			{
				// Invoke the callers close implementation or fall back to CloseOnIdle
				if (closeWindow != null)
				{
					closeWindow(testRunner);
				}
				else
				{
					initialSystemWindow.CloseOnIdle();
				}
			});

			// Main thread blocks here until released via CloseOnIdle above
			initialSystemWindow.ShowAsSystemWindow();

			bool timedOut = task.Result == delayTask;

			// Wait for CloseOnIdle to complete
			testRunner.WaitFor(() => initialSystemWindow.HasBeenClosed);

			if (timedOut)
			{
				// Throw an exception for test timeouts
				throw new TimeoutException("TestMethod timed out");
			}

			// After the system window is closed return the task and any exception to the calling context
			return task?.Result ?? Task.CompletedTask;
		}
	}
}
