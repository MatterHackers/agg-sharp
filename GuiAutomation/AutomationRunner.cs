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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;

using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

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
		public double UpDelaySeconds = .2;

		public enum InputType { Native, Simulated, SimulatedDrawMouse };

		public AutomationRunner(string imageDirectory = "", InputType inputType = InputType.Native)
		{
#if !__ANDROID__
			if (inputType == InputType.Native)
			{
				inputSystem = new WindowsInputMethods();
			}
			else
			{
				inputSystem = new AggInputMethods(this, inputType == InputType.SimulatedDrawMouse);
				// TODO: Consider how to set this and if needed
				//HookWindowsInputAndSendToWidget.EnableInputHook = false;
			}
#else
				inputSystem = new AggInputMethods(this, inputType == InputType.SimulatedDrawMouse);
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

		public double GetInterpolatedValue(double compleatedRatio0To1, InterpolationType interpolationType)
		{
			switch (interpolationType)
			{
				case InterpolationType.LINEAR:
					return compleatedRatio0To1;

				case InterpolationType.EASE_IN:
					return Math.Pow(compleatedRatio0To1, 3);

				case InterpolationType.EASE_OUT:
					return (Math.Pow(compleatedRatio0To1 - 1, 3) + 1);

				case InterpolationType.EASE_IN_OUT:
					if (compleatedRatio0To1 < .5)
					{
						return Math.Pow(compleatedRatio0To1 * 2, 3) / 2;
					}
					else
					{
						return (Math.Pow(compleatedRatio0To1 * 2 - 2, 3) + 2) / 2;
					}

				default:
					throw new NotImplementedException();
			}
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
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + offset.x, clickYOnScreen);
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
					return NativeMethods.MOUSEEVENTF_LEFTDOWN;

				case MouseButtons.Right:
					return NativeMethods.MOUSEEVENTF_RIGHTDOWN;

				case MouseButtons.Middle:
					return NativeMethods.MOUSEEVENTF_MIDDLEDOWN;

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
					return NativeMethods.MOUSEEVENTF_LEFTUP;

				case MouseButtons.Right:
					return NativeMethods.MOUSEEVENTF_RIGHTUP;

				case MouseButtons.Middle:
					return NativeMethods.MOUSEEVENTF_MIDDLEUP;

				default:
					return 0;
			}
		}

		public void Delay(double secondsToWait = .2)
		{
			Thread.Sleep((int)(secondsToWait * 1000));
		}

		public static void StaticDelay(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 200)
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

		public void Delay(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 200)
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
					graphics2D.Render(circle, RGBA_Bytes.Green);
				}
				graphics2D.Render(new Stroke(circle, 3), RGBA_Bytes.Black);
				graphics2D.Render(new Stroke(circle, 2), RGBA_Bytes.White);
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
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

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
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + offset.y);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + offset.x, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
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
			Point2D offsetHint;
			GuiWidget namedWidget = GetWidgetByName(widgetName, out containingWindow, out offsetHint, secondsToWait, searchRegion);

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

		public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			Point2D offsetHint;
			return GetWidgetByName(widgetName, out containingWindow, out offsetHint, secondsToWait, searchRegion);
        }

        public GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow, out Point2D offsetHint, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			containingWindow = null;
			offsetHint = Point2D.Zero;

			List<GetByNameResults> getResults = GetWidgetsByName(widgetName, secondsToWait, searchRegion);
			if (getResults != null
				&& getResults.Count > 0)
			{
				containingWindow = getResults[0].containingSystemWindow;
				offsetHint = getResults[0].offsetHint;
				getResults[0].widget.DebugShowBounds = true;
				UiThread.RunOnIdle(() => getResults[0].widget.DebugShowBounds = false, 1);

				return getResults[0].widget;
			}

			return null;
		}

		public class GetByNameResults
		{
			public GuiWidget widget { get; private set; }
			public Point2D offsetHint { get; private set; }
			public SystemWindow containingSystemWindow { get; private set; }

			public GetByNameResults(GuiWidget widget, Point2D offsetHint, SystemWindow containingSystemWindow)
			{
				this.widget = widget;
				this.offsetHint = offsetHint;
				this.containingSystemWindow = containingSystemWindow;
			}
		}

		public List<GetByNameResults> GetWidgetsByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			if (secondsToWait > 0)
			{
				bool foundWidget = WaitForName(widgetName, secondsToWait);
				if (!foundWidget)
				{
					return null;
				}
			}

			List<GetByNameResults> namedWidgetsInRegion = new List<GetByNameResults>();
			for(int i=SystemWindow.AllOpenSystemWindows.Count-1; i>=0 ; i--)
			{
				SystemWindow systemWindow = SystemWindow.AllOpenSystemWindows[i];
				if (searchRegion != null) // only add the widgets that are in the screen region
				{
					List<GuiWidget.WidgetAndPosition> namedWidgets = new List<GuiWidget.WidgetAndPosition>();
					systemWindow.FindNamedChildrenRecursive(widgetName, namedWidgets);
					foreach (GuiWidget.WidgetAndPosition widgetAndPosition in namedWidgets)
					{
						if (widgetAndPosition.widget.ActuallyVisibleOnScreen())
						{
							RectangleDouble childBounds = widgetAndPosition.widget.TransformToParentSpace(systemWindow, widgetAndPosition.widget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, systemWindow);
							ScreenRectangle result;
							if (ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
							{
								namedWidgetsInRegion.Add(new GetByNameResults(widgetAndPosition.widget, widgetAndPosition.position, systemWindow));
							}
						}
					}
				}
				else // add every named widget found
				{
					List<GuiWidget.WidgetAndPosition> namedWidgets = new List<GuiWidget.WidgetAndPosition>();
					systemWindow.FindNamedChildrenRecursive(widgetName, namedWidgets);
					foreach (GuiWidget.WidgetAndPosition namedWidget in namedWidgets)
					{
						if (namedWidget.widget.ActuallyVisibleOnScreen())
						{
							namedWidgetsInRegion.Add(new GetByNameResults(namedWidget.widget, namedWidget.position, systemWindow));
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
		public void ClickByName(string widgetName, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center, double delayBeforeReturn = 0.2, bool isDoubleClick = false)
		{
			double secondsToWait = 5;

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
				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				if (isDoubleClick)
				{
					Thread.Sleep(150);
					inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
				}

				Delay(UpDelaySeconds);

				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				// After firing the click event, wait the given period of time before returning to allow MatterControl 
				// to complete the targeted action
				Delay(delayBeforeReturn);

				return;
			}

			throw new Exception($"ClickByName Failed: Named GuiWidget not found [{widgetName}]");
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

		public bool NameExists(string widgetName, double secondsToWait = 5)
		{
			return WaitForName(widgetName, secondsToWait);
		}

		public bool NamedWidgetExists(string widgetName, SearchRegion searchRegion = null)
		{
			foreach (SystemWindow window in SystemWindow.AllOpenSystemWindows)
			{
				List<GuiWidget.WidgetAndPosition> foundChildren = new List<GuiWidget.WidgetAndPosition>();
				window.FindNamedChildrenRecursive(widgetName, foundChildren);
				if (foundChildren.Count > 0)
				{
					foreach (GuiWidget.WidgetAndPosition foundChild in foundChildren) 
					{
						RectangleDouble childBounds = foundChild.widget.TransformToParentSpace(window, foundChild.widget.LocalBounds);

						ScreenRectangle screenRect = SystemWindowToScreen(childBounds, window);
						ScreenRectangle result;
						if (searchRegion == null || ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
						{
							if (foundChild.widget.ActuallyVisibleOnScreen())
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

		public void SetMouseCursorPosition(int x, int y)
		{
			Vector2 start = new Vector2(CurrentMousePosition().x, CurrentMousePosition().y);
			Vector2 end = new Vector2(x, y);
			Vector2 delta = end - start;
			int steps = (int)((TimeToMoveMouse * 1000) / 20);
			for (int i = 0; i < steps; i++)
			{
				double ratio = i / (double)steps;
				ratio = GetInterpolatedValue(ratio, InterpolationType.EASE_OUT);
				Vector2 current = start + delta * ratio;
				inputSystem.SetCursorPosition((int)current.x, (int)current.y);
				Thread.Sleep(20);
			}

			inputSystem.SetCursorPosition((int)end.x, (int)end.y);
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
		public bool WaitForName(string widgetName, double secondsToWait = 5) // TODO: should have a search region
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
			while (!NamedWidgetExists(widgetName)
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
		/// Wait up to secondsToWait for the named widget to vanish.
		/// </summary>
		/// <param name="widgetName"></param>
		public bool WaitVanishForName(string widgetName, double secondsToWait) // TODO: should have a search region
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

		#region Prior TestHarness code

		public static Task ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, AutomationTest testMethod, double secondsToTestFailure, string imagesDirectory = "", InputType inputType = InputType.Native, Action closeWindow = null)
		{
			var testRunner = new AutomationRunner(imagesDirectory, inputType);

			AutoResetEvent resetEvent = new AutoResetEvent(false);

			bool firstDraw = true;
			initialSystemWindow.AfterDraw += (sender, e) =>
			{
				if (firstDraw)
				{
					firstDraw = false;
					resetEvent.Set();
				}
			};

			int testTimeout = (int)(1000 * secondsToTestFailure);
			var timer = Stopwatch.StartNew();

			// Start two tasks, the timeout and the test method. Block in the test method until the first draw
			Task<Task> task = Task.WhenAny(
				Task.Delay(testTimeout),
				Task.Run(() =>
				{
					// Wait until the first system window draw before running the test method
					resetEvent.WaitOne();
					
					return testMethod(testRunner);
				}));

			// Once either the timeout or the test method has completed, reassign the task/result for timeout errors and shutdown the SystemWindow 
			task.ContinueWith((innerTask) =>
			{
				long elapsedTime = timer.ElapsedMilliseconds;

				// Create an exception Task for test timeouts
				if (elapsedTime >= testTimeout)
				{
					task = new Task<Task>(() => { throw new TimeoutException("TestMethod timed out"); });
					task.RunSynchronously();
				}

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

			// After the system window is closed return the task and any exception to the calling context
			return task?.Result ?? Task.CompletedTask;
		}

		#endregion
	}
}