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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
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
		public double TimeToMoveMouse = .5;

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
				HookWindowsInputAndSendToWidget.EnableInputHook = false;
			}
#else
				inputSystem = new AggInputMethods(this, inputType == InputType.SimulatedDrawMouse);
#endif

			this.imageDirectory = imageDirectory;
		}

		public enum ClickOrigin { LowerLeft, Center };

		public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

		private List<TestResult> results = new List<TestResult>();

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
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
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
				switch (mouseButtons)
				{
					case MouseButtons.None:
						break;

					case MouseButtons.Left:
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(UpDelaySeconds);
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);
						break;

					case MouseButtons.Right:
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_RIGHTDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(UpDelaySeconds);
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_RIGHTUP, screenPosition.x, screenPosition.y, 0, 0);
						break;

					case MouseButtons.Middle:
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_MIDDLEDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(UpDelaySeconds);
						inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_MIDDLEUP, screenPosition.x, screenPosition.y, 0, 0);
						break;

					default:
						break;
				}

				return true;
			}

			return false;
		}

		public void WaitUntil(Func<bool> checkConditionSatisfied, double maxSeconds, int checkInterval = 200)
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
			ImageBuffer imageNeedleDrag = LoadImageFromSourcFolder(imageNameDrag);
			if (imageNeedleDrag != null)
			{
				ImageBuffer imageNeedleDrop = LoadImageFromSourcFolder(imageNameDrop);
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
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
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
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
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
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
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

			AbstractOsMappingWidget mappingWidget = containingWindow.Parent as AbstractOsMappingWidget;
			screenPosition.x += mappingWidget.DesktopPosition.x;
			screenPosition.y += mappingWidget.DesktopPosition.y + mappingWidget.TitleBarHeight;

			return screenPosition;
		}

		public static Point2D ScreenToSystemWindow(Point2D pointOnScreen, SystemWindow containingWindow)
		{
			Point2D screenPosition = pointOnScreen;
			AbstractOsMappingWidget mappingWidget = containingWindow.Parent as AbstractOsMappingWidget;
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

			AbstractOsMappingWidget mappingWidget = containingWindow.Parent as AbstractOsMappingWidget;
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

			AbstractOsMappingWidget mappingWidget = containingWindow.Parent as AbstractOsMappingWidget;
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

		private ImageBuffer LoadImageFromSourcFolder(string imageName)
		{
			string pathToImage = Path.Combine(imageDirectory, imageName);

			if (File.Exists(pathToImage))
			{
				ImageBuffer imageToLookFor = new ImageBuffer();

				if (ImageIO.LoadImageData(pathToImage, imageToLookFor))
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
			GuiWidget namedWidget = GetWidgetByName(widgetName, out containingWindow, secondsToWait, searchRegion);

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
			containingWindow = null;

			List<GetResults> getResults = GetWidgetsByName(widgetName, secondsToWait, searchRegion);
			if (getResults != null
				&& getResults.Count > 0)
			{
				containingWindow = getResults[0].containingSystemWindow;
				return getResults[0].widget;
			}

			return null;
		}

		public class GetResults
		{
			public GuiWidget widget;
			public SystemWindow containingSystemWindow;
		}

		public List<GetResults> GetWidgetsByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null)
		{
			if (secondsToWait > 0)
			{
				bool foundWidget = WaitForName(widgetName, secondsToWait);
				if (!foundWidget)
				{
					return null;
				}
			}

			List<GetResults> namedWidgetsInRegion = new List<GetResults>();
			foreach (SystemWindow systemWindow in SystemWindow.AllOpenSystemWindows)
			{
				if (searchRegion != null) // only add the widgets that are in the screen region
				{
					List<GuiWidget> namedWidgets = new List<GuiWidget>();
					systemWindow.FindNamedChildrenRecursive(widgetName, namedWidgets);
					foreach (GuiWidget namedWidget in namedWidgets)
					{
						if (namedWidget.ActuallyVisibleOnScreen())
						{
							RectangleDouble childBounds = namedWidget.TransformToParentSpace(systemWindow, namedWidget.LocalBounds);

							ScreenRectangle screenRect = SystemWindowToScreen(childBounds, systemWindow);
							ScreenRectangle result;
							if (ScreenRectangle.Intersection(searchRegion.ScreenRect, screenRect, out result))
							{
								namedWidgetsInRegion.Add(new GetResults()
								{
									widget = namedWidget,
									containingSystemWindow = systemWindow,
								});
							}
						}
					}
				}
				else // add every named widget found
				{
					List<GuiWidget> namedWidgets = new List<GuiWidget>();
					systemWindow.FindNamedChildrenRecursive(widgetName, namedWidgets);
					foreach (GuiWidget namedWidget in namedWidgets)
					{
						if (namedWidget.ActuallyVisibleOnScreen())
						{
							namedWidgetsInRegion.Add(new GetResults()
							{
								widget = namedWidget,
								containingSystemWindow = systemWindow,
							});
						}
					}
				}
			}

			return namedWidgetsInRegion;
		}

		/// <summary>
		/// Look for a widget with the given name and click it. It and all its parents must be visible and enabled.
		/// </summary>
		/// <param name="widgetName"></param>
		/// <param name="origin"></param>
		/// <param name="secondsToWait">Total seconds to stay in this function waiting for the named widget to become visible.</param>
		/// <returns></returns>
		public bool ClickByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset.x += (int)childBounds.Width / 2;
					offset.y += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);

				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				Wait(UpDelaySeconds);

				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DragDropByName(string widgetNameDrag, string widgetNameDrop, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offsetDrag = default(Point2D), ClickOrigin originDrag = ClickOrigin.Center, Point2D offsetDrop = default(Point2D), ClickOrigin originDrop = ClickOrigin.Center)
		{
			if (DragByName(widgetNameDrag, secondsToWait, searchRegion, offsetDrag, originDrag))
			{
				return DropByName(widgetNameDrop, secondsToWait, searchRegion, offsetDrop, originDrop);
			}

			return false;
		}

		public bool DragByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset.x += (int)childBounds.Width / 2;
					offset.y += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset.x += (int)childBounds.Width / 2;
					offset.y += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				Drop();

				return true;
			}

			return false;
		}

		public void Drop(Point2D offset = default(Point2D))
		{
			Point2D screenPosition = CurrentMousePosition() + offset;
			inputSystem.CreateMouseEvent(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);
		}

		public bool DoubleClickByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		public bool MoveToByName(string widgetName, double secondsToWait = 0, SearchRegion searchRegion = null, Point2D offset = default(Point2D), ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow, secondsToWait, searchRegion);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					offset.x += (int)childBounds.Width / 2;
					offset.y += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = SystemWindowToScreen(new Point2D(childBounds.Left + offset.x, childBounds.Bottom + offset.y), containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);

				return true;
			}

			return false;
		}

		public bool NameExists(string widgetName, SearchRegion searchRegion = null)
		{
			foreach (SystemWindow window in SystemWindow.AllOpenSystemWindows)
			{
				List<GuiWidget> foundChildren = new List<GuiWidget>();
				window.FindNamedChildrenRecursive(widgetName, foundChildren);
				if (foundChildren.Count > 0)
				{
					foreach (GuiWidget foundChild in foundChildren)
					{
						if (foundChild.ActuallyVisibleOnScreen())
						{
							return true;
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
			Wait(.2);
		}

		#endregion Keyboard Functions

		#region Time

		public void Wait(double secondsToWait)
		{
			Thread.Sleep((int)(secondsToWait * 1000));
		}

		public bool WaitForImage(string imageName, double secondsToWait, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
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
				Wait(.05);
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
		public bool WaitForName(string widgetName, double secondsToWait = 1) // TODO: should have a search region
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
			while (!NameExists(widgetName)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				Wait(.05);
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
		public bool WaitVanishForName(string widgetName, double secondsToWait) // TODO: should have a search regoin
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
			while (NameExists(widgetName)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				Wait(.05);
			}

			if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
			{
				return false;
			}

			return true;
		}

		#endregion Time

		#region Prior TestHarness code
		public void AddTestResult(bool passed, string resultDescription = "")
		{
			var testResult = new TestResult()
			{
				Passed = passed,
				Description = resultDescription,
			};

			results.Add(testResult);

			Console.WriteLine(
				" {0} {1}",
				passed ? "-" : "!",
				testResult.ToString());
		}

		public bool AllTestsPassed(int expectedCount)
		{
			return expectedCount == results.Count
				&& results.TrueForAll(testResult => testResult.Passed);
		}

		internal class TestResult
		{
			internal bool Passed { get; set; }
			internal string Description { get; set; }

			public override string ToString()
			{
				string status = Passed ? "Passed" : "Failed";
				return $"Test {status}: {Description}";
			}
		}

		public static Task ShowWindowAndExecuteTests(SystemWindow initialSystemWindow, AutomationTest testMethod, double secondsToTestFailure, string imagesDirectory = "", InputType inputType = InputType.Native)
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

				initialSystemWindow.CloseOnIdle();
			});

			// Main thread blocks here until released via CloseOnIdle above
			initialSystemWindow.ShowAsSystemWindow();

			// After the system window is closed return the task and any exception to the calling context
			return task?.Result ?? Task.FromResult(0);
		}

		#endregion
	}
}