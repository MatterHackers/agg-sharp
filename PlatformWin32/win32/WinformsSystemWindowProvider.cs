/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
	public class WinformsSystemWindowProvider : ISystemWindowProvider
	{
		private IGuiFactory factoryToUse = null;
		private Point2D InitialDesktopPosition = new Point2D();
		private bool pendingSetInitialDesktopPosition = false;

		public Point2D GetDesktopPosition(SystemWindow systemWindow)
		{
			if (systemWindow.AbstractOsMappingWidget != null)
			{
				return systemWindow.AbstractOsMappingWidget.DesktopPosition;
			}

			if (pendingSetInitialDesktopPosition)
			{
				return InitialDesktopPosition;
			}

			return new Point2D();
		}

		public void SetDesktopPosition(SystemWindow systemWindow, Point2D position)
		{
			if (systemWindow.AbstractOsMappingWidget != null)
			{
				// Make sure the window is on screen, but allow for a small amount of negative positioning to account for Form.DesktopLocation quirks
				position.x = Math.Max(-10, position.x);
				position.y = Math.Max(-10, position.y);

				// Auto-center if set to (-1,-1)
				if (position == new Point2D(-1, -1))
				{
					Point2D desktopSize = AggContext.DesktopSize;
					position = new Point2D(
						(desktopSize.x - systemWindow.Width) / 2, 
						(desktopSize.y - systemWindow.Height - systemWindow.AbstractOsMappingWidget.TitleBarHeight) / 2);
				}

				// If it's mac make sure we are not completely under the menu bar.
				if (AggContext.OperatingSystem == OSType.Mac)
				{
					position.y = Math.Max(5, position.y);
				}

				systemWindow.AbstractOsMappingWidget.DesktopPosition = position;
			}
			else
			{
				pendingSetInitialDesktopPosition = true;
				InitialDesktopPosition = position;
			}
		}

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			bool firstWindow = false;
			if (factoryToUse == null)
			{
				if (systemWindow.UseOpenGL)
				{
					factoryToUse = new WindowsFormsOpenGLFactory();
				}
				else
				{
					factoryToUse = new WindowsFormsBitmapFactory();
				}
				firstWindow = true;

				// When our top most window closes reset this so we can make a window in the future.
				systemWindow.Closed += (sender, e) =>
				{
					factoryToUse = null;
				};
			}

			AbstractOsMappingWidget osMappingWindow = factoryToUse.CreateSurface(systemWindow);

			osMappingWindow.Caption = systemWindow.Title;
			osMappingWindow.AddChild(systemWindow);
			osMappingWindow.MinimumSize = systemWindow.MinimumSize;

			systemWindow.AbstractOsMappingWidget = osMappingWindow;

			if (pendingSetInitialDesktopPosition)
			{
				pendingSetInitialDesktopPosition = false;
				systemWindow.DesktopPosition = InitialDesktopPosition;
			}

			systemWindow.AnchorAll();
			systemWindow.TitleChanged += SystemWindow_TitleChanged;
			
			// and make sure the title is correct right now
			SystemWindow_TitleChanged(systemWindow, null);

			if (firstWindow)
			{
				osMappingWindow.Run();
			}
			else if (!SystemWindow.ShareSingleOsWindow)
			{
				if (systemWindow.IsModal)
				{
					osMappingWindow.ShowModal();
				}
				else
				{
					osMappingWindow.Show();
					osMappingWindow.BringToFront();
				}
			}
		}

		private void SystemWindow_TitleChanged(object sender, EventArgs e)
		{
			SystemWindow systemWindow = ((SystemWindow)sender);
			systemWindow.AbstractOsMappingWidget.Caption = systemWindow.Title;
		}
	}

	public class SingleWindowMappingWidget : AbstractOsMappingWidget
	{
		public SingleWindowMappingWidget(SystemWindow childSystemWindow)
			: base(childSystemWindow)
		{
			AnchorAll();
		}

		public override string Caption { get; set; }

		public override Point2D DesktopPosition { get; set; }

		public override int TitleBarHeight { get; }

		public override void Run()
		{
		}

		public override void Show()
		{
		}

		public override void ShowModal()
		{
		}
	}
}