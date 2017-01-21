﻿/*
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
using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class SystemWindow : GuiWidget
	{
		private static ISystemWindowProvider systemWindowProvider;
		public EventHandler TitleChanged;

		public AbstractOsMappingWidget AbstractOsMappingWidget { get; set; }

		public bool AlwaysOnTopOfMain { get; set; }

		public bool CenterInParent { get; set; } = true;

		public bool IsModal { get; set; }

		public bool UseOpenGL { get; set; }

		public int StencilBufferDepth { get; set; }

		private string title = "";

		public ToolTipManager ToolTipManager { get; private set; }

		public string Title
		{
			get
			{
				return title;
			}
			set
			{
				if (title != value)
				{
					title = value;
					if (TitleChanged != null)
					{
						TitleChanged(this, null);
					}
				}
			}
		}

		public enum PixelTypes { Depth24 = 24, Depth32 = 32, DepthFloat = 128 };

		private PixelTypes pixelType = PixelTypes.Depth32;

		public PixelTypes PixelType { get { return pixelType; } set { pixelType = value; } }

		public int BitDepth
		{
			get
			{
				return (int)pixelType;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			allOpenSystemWindows.Remove(this);
			base.OnClosed(e);
			if (Parent != null)
			{
				Parent.Close();
			}
		}

		static List<SystemWindow> allOpenSystemWindows = new List<SystemWindow>();
		public static List<SystemWindow> AllOpenSystemWindows
		{
			get
			{
				return allOpenSystemWindows.Where(window => window.Parent != null).ToList();
			}
		}

		public SystemWindow(double width, double height)
			: base(width, height, SizeLimitsToSet.None)
		{
			ToolTipManager = new ToolTipManager(this);
			if (systemWindowProvider == null)
			{
				systemWindowProvider = AggContext.CreateInstanceFrom<ISystemWindowProvider>(AggContext.Config.ProviderTypes.SystemWindowProvider);
				if (systemWindowProvider == null)
				{
					throw new Exception(string.Format("Unable to load the SystemWindow provider"));
				}
			}

			allOpenSystemWindows.Add(this);
		}

		public override Vector2 MinimumSize
		{
			get
			{
				return base.MinimumSize;
			}
			set
			{
				base.MinimumSize = value;
				if (Parent != null)
				{
					Parent.MinimumSize = value;
				}
			}
		}

		private Vector2 lastMousePosition;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			lastMousePosition = new Vector2(mouseEvent.X, mouseEvent.Y);
			base.OnMouseUp(mouseEvent);
		}

		public override bool GetMousePosition(out Vector2 position)
		{
			position = lastMousePosition;
			return true;
		}

		public override void BringToFront()
		{
			Parent?.BringToFront();
		}

		public void ShowAsSystemWindow()
		{
			if (Parent != null)
			{
				throw new Exception("To be a system window you cannot be a child of another widget.");
			}
			systemWindowProvider.ShowSystemWindow(this);
		}

		public Point2D DesktopPosition
		{
			get
			{
				return systemWindowProvider.GetDesktopPosition(this);
			}

			set
			{
				systemWindowProvider.SetDesktopPosition(this, value);
			}
		}

		/// <summary>
		/// If set only one os window will be created and all system windows will share it.
		/// Make sure this is set prior to creating any SystemWindows (don't change at runtime).
		/// </summary>
		public static bool ShareSingleOsWindow { get; set; }

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		public void SetHoveredWidget(GuiWidget widgetToShowToolTipFor)
		{
			ToolTipManager.SetHoveredWidget(widgetToShowToolTipFor);
		}
	}
}