/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.GuiAutomation
{
	public delegate Task AutomationTest(AutomationRunner runner);

	public class AutomationRunner
	{
		public long MatchLimit = 50;

		private IInputMethod inputSystem;

		/// <summary>
		/// The number of seconds to move the mouse when going to a new position.
		/// </summary>
		public static double TimeToMoveMouse { get; set; } = .5;

		private string imageDirectory;
		public static double UpDelaySeconds { get; set; } = .2;

		public enum InputType { Native, Simulated, SimulatedDrawMouse };

		public static IInputMethod OverrideInputSystem = null;

		// change default to SimulatedDrawMouse
		public AutomationRunner(IInputMethod inputMethod, bool drawSimulatedMouse, string imageDirectory = "")
		{
#if !__ANDROID__
			if(OverrideInputSystem != null)
			{
				inputSystem = OverrideInputSystem;
			}
			else
			{
				inputSystem = new AggInputMethods(this, drawSimulatedMouse);
				// TODO: Consider how to set this and if needed
				//HookWindowsInputAndSendToWidget.EnableInputHook = false;
			}
#else
				inputSystem = new AggInputMethods(this, drawSimulatedMouse);
#endif
			this.imageDirectory = imageDirectory;
		}

		public enum ClickOrigin { LowerLeft, Center };

		public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

		#region Utility

		public Point2D CurrentMousePosition()
		{
			return inputSystem.CurrentMousePosition();
		}

		public ImageBuffer GetCurrentScreen()
		{
			return inputSystem.GetCurrentScreen();
		}

		#endregion Utility

		#region Mouse Functions

		#region Search By Image

		public bool ClickImage(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return ClickImage(imageToLookFor, secondsToWait, searchRegion, offset, origin, mouseButtons);
			}

			return false;
		}

		public bool ClickImage(ImageBuffer imageNeedle, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
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

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);

				inputSystem.CreateMouseEvent(GetMouseDown(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);
				Delay(UpDelaySeconds);
				inputSystem.CreateMouseEvent(GetMouseUp(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		int GetMouseDown(MouseButtons mouseButtons)
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

		int GetMouseUp(MouseButtons mouseButtons)
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

		public void Delay(double secondsToWait = .2)
		{
			Thread.Sleep((int)(secondsToWait * 1000));
		}

		/// <summary>
		/// Wait for the given condition to be satisfied. The check Interval should be nice and short to allow test to
		/// complete quickly.
		/// </summary>
		/// <param name="checkConditionSatisfied"></param>
		/// <param name="maxSeconds"></param>
		/// <param name="checkInterval"></param>
		public static void StaticDelay(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 10)
		{
			Stopwatch timer = Stopwatch.StartNew();

			while (timer.Elapsed.Seconds < maxSeconds)
			{
				if (checkConditionSatisfied())
				{
					break;
				}

				Thread.Sleep(checkInterval);
			}
		}

		/// <summary>
		/// Wait up to maxSeconds for the condition to be satisfied.
		/// </summary>
		/// <param name="checkConditionSatisfied"></param>
		/// <param name="maxSeconds"></param>
		/// <param name="checkInterval"></param>
		public void WaitFor(Func<bool> checkConditionSatisfied, double maxSeconds = 5, int checkInterval = 200)
		{
			StaticDelay(checkConditionSatisfied, maxSeconds, checkInterval);
		}

		public bool DoubleClickImage(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		public bool DragDropImage(ImageBuffer imageNeedleDrag, ImageBuffer imageNeedleDrop, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offsetDrag = default(Point2D), ClickOrigin originDrag = ClickOrigin.Center,
			Point2D offsetDrop = default(Point2D), ClickOrigin originDrop = ClickOrigin.Center)
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

		public bool DragDropImage(string imageNameDrag, string imageNameDrop, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offsetDrag = default(Point2D), ClickOrigin originDrag = ClickOrigin.Center,
			Point2D offsetDrop = default(Point2D), ClickOrigin originDrop = ClickOrigin.Center)
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
				Ellipse circle = new Ellipse(new Vector2(mousePosOnWindow.x, mousePosOnWindow.y), 10);

				if (inputSystem.LeftButtonDown)
				{
					graphics2D.Render(circle, Color.Green);

					if (inputSystem.ClickCount > 1)
					{
						graphics2D.DrawString(inputSystem.ClickCount.ToString(), mousePosOnWindow.x, mousePosOnWindow.y, 8, justification: Justification.Center, baseline: Baseline.BoundsCenter);
					}
				}

				graphics2D.Render(new Stroke(circle, 3), Color.Black);
				graphics2D.Render(new Stroke(circle, 2), Color.White);
			}
		}

		public bool DragImage(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return DragImage(imageToLookFor, secondsToWait, searchRegion, offset, origin);
			}

			return false;
		}

		public bool DragImage(ImageBuffer imageNeedle, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
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

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropImage(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return DropImage(imageToLookFor, secondsToWait, searchRegion, offset, origin);
			}

			return false;
		}

		public bool DropImage(ImageBuffer imageNeedle, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
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

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = inputSystem.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.Y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.X + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(MouseConsts.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public void ScrollIntoView(string checkBoxName)
		{
			// Find any sibling toggle switch and scroll the parent to the bottom
			var checkBox = GetWidgetByName(checkBoxName, out _, onlyVisible: false);

			if (checkBox != null)
			{
				var scrollable = checkBox.Parents<ScrollableWidget>().First();
				scrollable?.ScrollIntoView(checkBox);
			}
		}

		public bool ImageExists(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourceFolder(imageName);
			if (imageToLookFor != null)
			{
				return ImageExists(imageToLookFor, secondsToWait, searchRegion);
			}

			return false;
		}

		public bool ImageExists(ImageBuffer imageNeedle, double secondsToWait = 0, SearchRegion searchRegion = null)
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

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				return true;
			}

			return false;
		}

		public bool MoveToImage(string imageName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		private static Point2D SystemWindowToScreen(Point2D pointOnWindow, SystemWindow containingWindow)
		{
			Point2D screenPosition = new Point2D(pointOnWindow.x, (int)containingWindow.Height - pointOnWindow.y);

			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.x += mappingWidget.DesktopPosition.x;
			screenPosition.y += mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;

			return screenPosition;
		}

		public static Point2D ScreenToSystemWindow(Point2D pointOnScreen, SystemWindow containingWindow)
		{
			Point2D screenPosition = pointOnScreen;
			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.x -= mappingWidget.DesktopPosition.x;
			screenPosition.y -= (mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight);

			screenPosition.y = (int)containingWindow.Height - screenPosition.y;

			return screenPosition;
		}

		public static ScreenRectangle SystemWindowToScreen(RectangleDouble rectOnScreen, SystemWindow containingWindow)
		{
			ScreenRectangle screenPosition = new ScreenRectangle()
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
			screenPosition.Top += (mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight);
			screenPosition.Right += mappingWidget.DesktopPosition.x;
			screenPosition.Bottom += (mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight);

			return screenPosition;
		}

		private static RectangleDouble ScreenToSystemWindow(ScreenRectangle rectOnScreen, SystemWindow containingWindow)
		{
			ScreenRectangle screenPosition = new ScreenRectangle()
			{
				Left = (int)rectOnScreen.Left,
				Top = (int)rectOnScreen.Top,
				Right = (int)rectOnScreen.Right,
				Bottom = (int)rectOnScreen.Bottom,
			};

			IPlatformWindow mappingWidget = containingWindow.PlatformWindow;
			screenPosition.Left -= mappingWidget.DesktopPosition.x;
			screenPosition.Top -= (mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight);
			screenPosition.Left -= mappingWidget.DesktopPosition.x;
			screenPosition.Bottom -= (mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight);

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
			return new SearchRegion(screenImage, new ScreenRectangle()
			{
				Left = 0,
				Top = 0,
				Right = screenImage.Width,
				Bottom = screenImage.Height
			}, this);
		}

		private ImageBuffer LoadImageFromSourceFolder(string imageName)
		{
			string pathToImage = Path.Combine(imageDirectory, imageName);

			if (File.Exists(pathToImage))
			{
				ImageBuffer imageToLookFor = new ImageBuffer();

				if (AggContext.ImageIO.LoadImageData(pathToImage, imageToLookFor))
				{
					return imageToLookFor;
				}
			}

			return null;
		}

		#endregion Search By Image

		#region Search By Names

		public SearchRegion GetRegionByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			SystemWindow containingWindow;
			GuiWidget namedWidget = GetWidgetByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion);

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

		public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, double secondsToWait = 5, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			return GetWidgetByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion, onlyVisible);
		}


		GuiWidget lastWidget = null;

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

		public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, out Point2D offsetHint, double secondsToWait = 5, SearchRegion searchRegion = null, bool onlyVisible = true)
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

				return getResults[0].Widget;
			}

			return null;
		}

		public object GetObjectByName(string widgetName, out SystemWindow containingWindow, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			return GetObjectByName(widgetName, out containingWindow, out _, secondsToWait, searchRegion);
		}

		public object GetObjectByName(string widgetName, out SystemWindow containingWindow, out Point2D offsetHint, double secondsToWait = 0, SearchRegion searchRegion = null, bool onlyVisible = true)
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

		public List<GetByNameResults> GetWidgetsByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, bool onlyVisible = true)
		{
			if (secondsToWait > 0)
			{
				bool foundWidget = WaitForName(widgetName, secondsToWait, onlyVisible);
				if (!foundWidget)
				{
					return null;
				}
			}

			List<GetByNameResults> namedWidgetsInRegion = new List<GetByNameResults>();
			foreach(var systemWindow in SystemWindow.AllOpenSystemWindows.Reverse())
			{
				if (searchRegion != null) // only add the widgets that are in the screen region
				{
					var namedWidgets = systemWindow.FindDescendants(widgetName);
					foreach (GuiWidget.WidgetAndPosition widgetAndPosition in namedWidgets)
					{
						if (!onlyVisible
							|| widgetAndPosition.widget.ActuallyVisibleOnScreen())
						{
							RectangleDouble childBounds = widgetAndPosition.widget.TransformToParentSpace(systemWindow, widgetAndPosition.widget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, systemWindow);
							ScreenRectangle result;
							if (ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
							{
								namedWidgetsInRegion.Add(new GetByNameResults(widgetAndPosition.widget, widgetAndPosition.position, systemWindow, widgetAndPosition.NamedObject));
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
							|| namedWidget.widget.ActuallyVisibleOnScreen())
						{
							namedWidgetsInRegion.Add(new GetByNameResults(namedWidget.widget, namedWidget.position, systemWindow, namedWidget.NamedObject));
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
		public void ClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, bool isDoubleClick = false)
		{
			double secondsToWait = 5;

			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);

			if (widgetToClick != null)
			{
				this.ClickWidget(widgetToClick, containingWindow, origin, offset, offsetHint, isDoubleClick);

				return;
			}

			throw new Exception($"ClickByName Failed: Named GuiWidget not found [{widgetName}]");
		}

		/// <summary>
		/// Click the given widget via automation methods
		/// </summary>
		/// <param name="widget">The widget to click</param>
		public void ClickWidget(GuiWidget widget)
		{
			var systemWindow = widget.Parents<SystemWindow>().FirstOrDefault();
			var center = widget.LocalBounds.Center;

			this.ClickWidget(
				widget,
				systemWindow,
				ClickOrigin.Center,
				Point2D.Zero,
				new Point2D(center.X, center.Y));
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

			Delay(0.2);
		}

		/// <summary>
		/// Look for a widget with the given name and click it. It and all its parents must be visible and enabled.
		/// </summary>
		/// <param name="widgetName">The given widget name</param>
		/// <param name="secondsToWait">Total seconds to stay in this function waiting for the named widget to become visible.</param>
		public void RightClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, bool isDoubleClick = false)
		{
			double secondsToWait = 5;

			GuiWidget widgetToClick = GetWidgetByName(widgetName, out SystemWindow containingWindow, out Point2D offsetHint, secondsToWait, searchRegion);
			if (widgetToClick != null)
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

				return;
			}

			throw new Exception($"ClickByName Failed: Named GuiWidget not found [{widgetName}]");
		}

		public void WaitforDraw(SystemWindow containingWindow, int maxSeconds = 30)
		{
			var resetEvent = new AutoResetEvent(false);

			EventHandler<DrawEventArgs> afterDraw = (s, e) => resetEvent.Set();
			EventHandler closed = (s, e) => resetEvent.Set();

			containingWindow.AfterDraw += afterDraw;
			containingWindow.Closed += closed;

			containingWindow.Invalidate();

			resetEvent.WaitOne(maxSeconds * 1000);

			containingWindow.AfterDraw -= afterDraw;
			containingWindow.Closed -= closed;
		}

		public bool DragDropByName(string widgetNameDrag, string widgetNameDrop, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offsetDrag = default(Point2D), ClickOrigin originDrag = ClickOrigin.Center, Point2D offsetDrop = default(Point2D), ClickOrigin originDrop = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			if (DragByName(widgetNameDrag, secondsToWait, searchRegion, offsetDrag, originDrag, mouseButtons))
			{
				return DropByName(widgetNameDrop, secondsToWait, searchRegion, offsetDrop, originDrop, mouseButtons);
			}

			return false;
		}

		public bool DragByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			SystemWindow containingWindow;
			Point2D offsetHint;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, out offsetHint, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset += offsetHint;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(GetMouseDown(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, MouseButtons mouseButtons = MouseButtons.Left)
		{
			SystemWindow containingWindow;
			Point2D offsetHint;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, out offsetHint, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset += offsetHint;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				Drop(mouseButtons);

				return true;
			}

			return false;
		}

		public void Drop(MouseButtons mouseButtons = MouseButtons.Left)
		{
			Point2D screenPosition = CurrentMousePosition();
			inputSystem.CreateMouseEvent(GetMouseUp(mouseButtons), screenPosition.x, screenPosition.y, 0, 0);
		}

		public void DoubleClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			this.ClickByName(widgetName, searchRegion, offset, origin, isDoubleClick: true);
		}

		public bool MoveToByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			Point2D offsetHint;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, out offsetHint, secondsToWait, searchRegion);
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

		public bool NameExists(string widgetName, double secondsToWait = 5, bool onlyVisible = true)
		{
			return WaitForName(widgetName, secondsToWait, onlyVisible);
		}

		public bool NamedWidgetExists(string widgetName, SearchRegion searchRegion = null, bool onlyVisible = true)
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
							RectangleDouble childBounds = foundChild.widget.TransformToParentSpace(window, foundChild.widget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, window);
							ScreenRectangle result;
							if (searchRegion == null
								|| ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
							{
								if (foundChild.widget.ActuallyVisibleOnScreen())
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
						ScreenRectangle result;
						if (searchRegion == null || ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
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
		#endregion Search By Names

		public void MoveMouseToWidget(GuiWidget widget, SystemWindow containingWindow, Point2D offset, Point2D offsetHint, ClickOrigin origin, out Point2D screenPosition)
		{
			RectangleDouble childBounds = widget.TransformToParentSpace(containingWindow, widget.LocalBounds);
			screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

			int steps = (int)((TimeToMoveMouse * 1000) / 20);
			Vector2 start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			if (origin == ClickOrigin.Center)
			{
				offset += offsetHint;
			}

			for (int i = 0; i < steps; i++)
			{
				childBounds = widget.TransformToParentSpace(containingWindow, widget.LocalBounds);

				screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

				Vector2 end = new Vector2(screenPosition.x, screenPosition.y);
				Vector2 delta = end - start;

				double ratio = i / (double)steps;
				ratio = Cubic.Out(ratio);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.X, (int)current.Y);
				Thread.Sleep(20);
			}

			inputSystem.SetCursorPosition(screenPosition.x, screenPosition.y);
		}

		public void SetMouseCursorPosition(int x, int y)
		{
			Vector2 start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			Vector2 end = new Vector2(x, y);
			Vector2 delta = end - start;

			int steps = (int)((TimeToMoveMouse * 1000) / 20);
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

		public void SetMouseCursorPosition(SystemWindow systemWindow, int x, int y)
		{
			Point2D screenPosition = SystemWindowToScreen(new Point2D(x, y), systemWindow);
			SetMouseCursorPosition(screenPosition.x, screenPosition.y);
		}

		#endregion Mouse Functions

		#region Keyboard Functions

		public void KeyDown(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		public void KeyUp(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Send the string to the system window
		/// ^ will add the control key
		/// {Enter} will type the enter key
		/// {BACKSPACE} will type the backspace key
		/// </summary>
		/// <param name="textToType"></param>
		public void Type(string textToType)
		{
			inputSystem.Type(textToType);
			Delay(.2);
		}

		#endregion Keyboard Functions

		#region Time

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
			Stopwatch timeWaited = Stopwatch.StartNew();
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
		/// <param name="widgetName"></param>
		public bool WaitForName(string widgetName, double secondsToWait = 5, bool onlyVisible = true) // TODO: should have a search region
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
			while (!NamedWidgetExists(widgetName, null, onlyVisible)
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
		/// Wait up to secondsToWait for the named widget to disappear
		/// </summary>
		/// <param name="widgetName"></param>
		public bool WaitForWidgetDisappear(string widgetName, double secondsToWait) // TODO: should have a search region
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
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

		#endregion Time

		public void SelectAll()
		{
			// Type into focused widget
			this.Type("^a"); // select all
		}

		public void SelectNone()
		{
			// Type into focused widget
			this.Type(" "); // clear the selection (type a space)
		}

		public static IInputMethod InputMethod { get; set; }

		public static bool DrawSimulatedMouse { get; set; } = true;

		public static Task ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, AutomationTest testMethod, double secondsToTestFailure = 30, string imagesDirectory = "", Action closeWindow = null)
		{
			var testRunner = new AutomationRunner(InputMethod, DrawSimulatedMouse, imagesDirectory);

			var resetEvent = new AutoResetEvent(false);

			// On load, release the reset event
			initialSystemWindow.Load += (s, e) =>
			{
				resetEvent.Set();
			};

			int testTimeout = (int)(1000 * secondsToTestFailure);
			var timer = Stopwatch.StartNew();

			bool testTimedOut = false;

			// Start two tasks, the timeout and the test method. Block in the test method until the first draw
			Task<Task> task = Task.WhenAny(
				Task.Delay(testTimeout),
				Task.Run(() =>
				{
					// Wait until the first system window draw before running the test method, up to the timeout
					resetEvent.WaitOne(testTimeout);

					return testMethod(testRunner);
				}));

			// Once either the timeout or the test method has completed, store if a timeout occurred and shutdown the SystemWindow
			task.ContinueWith((innerTask) =>
			{
				long elapsedTime = timer.ElapsedMilliseconds;
				testTimedOut = elapsedTime >= testTimeout;

				// Invoke the callers close implementation or fall back to CloseOnIdle
				if (closeWindow != null)
				{
					closeWindow();
				}
				else
				{
					initialSystemWindow.CloseOnIdle();
				}
			});

			// Main thread blocks here until released via CloseOnIdle above
			initialSystemWindow.ShowAsSystemWindow();

			// Wait for CloseOnIdle to complete
			testRunner.WaitFor(() => initialSystemWindow.HasBeenClosed);

			if (testTimedOut)
			{
				// Throw an exception for test timeouts
				throw new TimeoutException("TestMethod timed out");
			}

			// After the system window is closed return the task and any exception to the calling context
			return task?.Result ?? Task.CompletedTask;
		}
	}
}