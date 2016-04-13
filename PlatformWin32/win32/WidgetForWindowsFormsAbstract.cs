using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MatterHackers.Agg.UI
{
	public abstract class WidgetForWindowsFormsAbstract : AbstractOsMappingWidget
	{
		private WindowsFormsAbstract windowsFormsWindow;

		public WindowsFormsAbstract WindowsFormsWindow
		{
			get { return windowsFormsWindow; }
			set
			{
				windowsFormsWindow = value;
				if (mainWindowsFormsWindow == null)
				{
					mainWindowsFormsWindow = windowsFormsWindow;
					mainWindowsFormsWindow.FormClosed += (sender, e) =>
					{
						mainWindowsFormsWindow = null;
					};
				}
			}
		}

		private static WindowsFormsAbstract mainWindowsFormsWindow = null;

		public static WindowsFormsAbstract MainWindowsFormsWindow
		{
			get { return mainWindowsFormsWindow; }
		}

		public WidgetForWindowsFormsAbstract(SystemWindow childSystemWindow)
			: base(childSystemWindow)
		{
			// If no platform specific Clipboard implementation has been initialized, fallback to the default
			// WindowsForms implementation
			if (!Clipboard.IsInitialized)
			{
				Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
			}

			Focus();
		}

        public override int TitleBarHeight
        {
            get
            {
                return windowsFormsWindow.TitleBarHeight;
            }
        }

        public override Point2D DesktopPosition
		{
			get
			{
				return new Point2D(windowsFormsWindow.DesktopLocation.X, windowsFormsWindow.DesktopLocation.Y);
			}

			set
			{
				if (!windowsFormsWindow.Visible)
				{
					windowsFormsWindow.StartPosition = FormStartPosition.Manual;
				}
				windowsFormsWindow.DesktopLocation = new Point(value.x, value.y);
			}
		}

		public override Keys ModifierKeys
		{
			get
			{
				Keys modifierKeys = (MatterHackers.Agg.UI.Keys)Control.ModifierKeys;
				return modifierKeys;
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			Invalidate();
			base.OnBoundsChanged(e);
		}

		public override void BringToFront()
		{
			WindowsFormsWindow.Activate();
		}

		public override string Caption
		{
			get
			{
				return WindowsFormsWindow.Text;
			}
			set
			{
				WindowsFormsWindow.Text = value;
			}
		}

		public Rectangle GetRectangleFromRectD(RectangleDouble rectD)
		{
			Rectangle windowsRect = new Rectangle(
				(int)System.Math.Floor(rectD.Left),
				(int)System.Math.Floor(Height - rectD.Top),
				(int)System.Math.Ceiling(rectD.Width),
				(int)System.Math.Ceiling(rectD.Height));

			return windowsRect;
		}

		protected override void SetCursorOnEnter(Cursors cursorToSet)
		{
			switch (cursorToSet)
			{
				case Cursors.Arrow:
					WindowsFormsWindow.Cursor = System.Windows.Forms.Cursors.Arrow;
					break;

				case Cursors.Hand:
					WindowsFormsWindow.Cursor = System.Windows.Forms.Cursors.Hand;
					break;

				case Cursors.IBeam:
					WindowsFormsWindow.Cursor = System.Windows.Forms.Cursors.IBeam;
					break;
			}
		}

		public override void Invalidate(RectangleDouble rectToInvalidate)
		{
			rectToInvalidate.IntersectWithRectangle(LocalBounds);

			//rectToInvalidate = new rect_d(0, 0, Width, Height);

			Rectangle windowsRectToInvalidate = GetRectangleFromRectD(rectToInvalidate);
			if (WindowsFormsWindow != null)
			{
				WindowsFormsWindow.RequestInvalidate(windowsRectToInvalidate);
			}
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

				Size clientSize = new Size((int)Math.Ceiling(MinimumSize.x), (int)Math.Ceiling(MinimumSize.y));
				Size windowSize = new Size(clientSize.Width + WindowsFormsWindow.Width - WindowsFormsWindow.ClientSize.Width,
					clientSize.Height + WindowsFormsWindow.Height - WindowsFormsWindow.ClientSize.Height);

				WindowsFormsWindow.MinimumSize = windowSize;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			WindowsFormsWindow.RequestClose();
		}

		public override void Show()
		{
			// Center the window if specified on the SystemWindow
			if (mainWindowsFormsWindow != WindowsFormsWindow && childSystemWindow.CenterInParent)
			{
				Rectangle mainBounds = mainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = childSystemWindow.LocalBounds;

				windowsFormsWindow.Left = mainBounds.X + mainBounds.Width / 2 - (int) newItemBounds.Width / 2;
				windowsFormsWindow.Top = mainBounds.Y + mainBounds.Height / 2 - (int) newItemBounds.Height / 2;
			}

			if (mainWindowsFormsWindow != WindowsFormsWindow
				&& childSystemWindow.AlwaysOnTopOfMain)
			{
				WindowsFormsWindow.Show(mainWindowsFormsWindow);
			}
			else
			{
				WindowsFormsWindow.Show();
			}
		}

		public override void ShowModal()
		{
			// Release the onidle guard so that the onidle pump continues processing while we block at ShowDialog below
			Task.Run(() => windowsFormsWindow.ReleaseOnIdleGuard());

			WindowsFormsWindow.ShowDialog();
		}

		public override void Run()
		{
			Show();
			Application.Run(WindowsFormsWindow);
		}
	}
}