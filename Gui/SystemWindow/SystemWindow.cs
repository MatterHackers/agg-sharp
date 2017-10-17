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
using System.Collections.Generic;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class SystemWindow : GuiWidget
	{
		public event EventHandler<ClosingEventArgs> Closing;

		public bool AlwaysOnTopOfMain { get; set; }

		public bool CenterInParent { get; set; } = true;

		public bool IsModal { get; set; }

		public bool UseOpenGL { get; set; }

		public int StencilBufferDepth { get; set; }

		public ToolTipManager ToolTipManager { get; private set; }

		private string _title;
		public string Title
		{
			get => _title;
			set
			{
				_title = value;

				if (this.PlatformWindow != null)
				{
					this.PlatformWindow.Caption = _title;
				}
			}
		}

		public enum PixelTypes { Depth24 = 24, Depth32 = 32, DepthFloat = 128 };

		public PixelTypes PixelType { get; set; } = PixelTypes.Depth32;

		public int BitDepth => (int)this.PixelType;

		public override void OnClosed(ClosedEventArgs e)
		{
			AllOpenSystemWindows.Remove(this);

			base.OnClosed(e);

			// Invoke Close on our PlatformWindow and release our reference when complete
			systemWindowProvider.CloseSystemWindow(this);
			this.PlatformWindow = null;
		}

		public virtual void OnClosing(ClosingEventArgs eventArgs)
		{
			Closing?.Invoke(this, eventArgs);
		}

		public static List<SystemWindow> AllOpenSystemWindows { get; } = new List<SystemWindow>();

		public SystemWindow(double width, double height)
			: base(width, height, SizeLimitsToSet.None)
		{
			ToolTipManager = new ToolTipManager(this);

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			AllOpenSystemWindows.Add(this);
		}

		public override void OnMinimumSizeChanged(EventArgs e)
		{
			if (PlatformWindow != null)
			{
				PlatformWindow.MinimumSize = this.MinimumSize;
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

		public override Graphics2D NewGraphics2D()
		{
			return this.PlatformWindow.NewGraphics2D();
		}

		private static ISystemWindowProvider systemWindowProvider = null;

		public void ShowAsSystemWindow()
		{
			if (Parent != null)
			{
				throw new Exception("To be a system window you cannot be a child of another widget.");
			}

			if (systemWindowProvider == null)
			{
				systemWindowProvider = AggContext.CreateInstanceFrom<ISystemWindowProvider>(AggContext.Config.ProviderTypes.SystemWindowProvider);
			}

			// Create the backing IPlatformWindow object and set its AggSystemWindow property to this new SystemWindow
			systemWindowProvider.ShowSystemWindow(this);
		}

		public virtual bool Maximized { get; set; } = false;

		public Point2D InitialDesktopPosition = new Point2D(-1, -1);

		public Point2D DesktopPosition
		{
			get
			{
				return PlatformWindow.DesktopPosition;
			}
			set
			{
				Point2D position = value;

				if (PlatformWindow != null)
				{
					// Make sure the window is on screen (this logic should improve over time)
					position.x = Math.Max(0, position.x);
					position.y = Math.Max(0, position.y);

					// If it's mac make sure we are not completely under the menu bar.
					if (AggContext.OperatingSystem == OSType.Mac)
					{
						position.y = Math.Max(5, position.y);
					}

					PlatformWindow.DesktopPosition = position;
				}
				else
				{
					InitialDesktopPosition = position;
				}
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

		public override void Invalidate(RectangleDouble rectToInvalidate)
		{
			PlatformWindow?.Invalidate(LocalBounds);
		}

		// TODO: This should become private... Callers should interact with SystemWindow proxies
		public IPlatformWindow PlatformWindow { get; set; }

		public override Keys ModifierKeys => PlatformWindow.ModifierKeys;
	}
}
