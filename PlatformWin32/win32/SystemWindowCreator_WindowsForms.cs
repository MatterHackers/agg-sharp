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

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using System;

namespace MatterHackers.Agg
{
	public class SystemWindowCreator_WindowsForms : SystemWindowCreatorPlugin
	{
		private bool pendingSetInitialDesktopPosition = false;
		private Point2D InitialDesktopPosition = new Point2D();

		public override void ShowSystemWindow(SystemWindow systemWindow)
		{
			bool haveInitializedMainWindow = false;
			if (OsMappingWidgetFactory.PrimaryOsMappingWidget != null)
			{
				haveInitializedMainWindow = true;
			}

			if (!haveInitializedMainWindow)
			{
				if (systemWindow.UseOpenGL)
				{
					OsMappingWidgetFactory.SetFactory(new WindowsFormsOpenGLFactory());
				}
				else
				{
					OsMappingWidgetFactory.SetFactory(new WindowsFormsBitmapFactory());
				}
			}

			AbstractOsMappingWidget windowsFormsTopWindow = OsMappingWidgetFactory.CreateOsMappingWidget(systemWindow);

			windowsFormsTopWindow.Caption = systemWindow.Title;
			windowsFormsTopWindow.AddChild(systemWindow);
			windowsFormsTopWindow.MinimumSize = systemWindow.MinimumSize;

			systemWindow.AbstractOsMappingWidget = windowsFormsTopWindow;

			if (pendingSetInitialDesktopPosition)
			{
				pendingSetInitialDesktopPosition = false;
				systemWindow.DesktopPosition = InitialDesktopPosition;
			}

			systemWindow.AnchorAll();
			systemWindow.TitleChanged += new EventHandler(TitelChangedEventHandler);
			// and make sure the title is correct right now
			TitelChangedEventHandler(systemWindow, null);

			if (haveInitializedMainWindow)
			{
				if (systemWindow.IsModal)
				{
					windowsFormsTopWindow.ShowModal();
				}
				else
				{
					windowsFormsTopWindow.Show();
				}
			}
			else
			{
				windowsFormsTopWindow.Run();
			}
		}

		public override Point2D GetDesktopPosition(SystemWindow systemWindow)
		{
			if (systemWindow.AbstractOsMappingWidget != null)
			{
				return systemWindow.AbstractOsMappingWidget.DesktopPosition;
			}

			return new Point2D();
		}

		public override void SetDesktopPosition(SystemWindow systemWindow, Point2D position)
		{
			if (systemWindow.AbstractOsMappingWidget != null)
			{
				// Make sure the window is on screen (this logic should improve over time)
				position.x = Math.Max(0, position.x);
				position.y = Math.Max(0, position.y);

				// If it's mac make sure we are not completely under the menu bar.
				if (OsInformation.OperatingSystem == OSType.Mac)
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

		private void TitelChangedEventHandler(object sender, EventArgs e)
		{
			SystemWindow systemWindow = ((SystemWindow)sender);
			systemWindow.AbstractOsMappingWidget.Caption = systemWindow.Title;
		}
	}
}