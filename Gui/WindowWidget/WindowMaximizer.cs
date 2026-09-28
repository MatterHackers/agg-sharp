//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2026 Lars Brubaker
//                  larsbrubaker@gmail.com
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The maximized state of a <see cref="WindowWidget"/>: the rectangle it restores to, and while maximized
	/// the parent it fills and follows as that parent resizes. <see cref="WindowWidget.Maximized"/> is the
	/// public face; this keeps the bookkeeping out of the window.
	/// </summary>
	internal sealed class WindowMaximizer
	{
		private readonly WindowWidget window;

		/// <summary>The grab border around the visible window, in device pixels.</summary>
		private readonly Func<double> grabWidth;

		/// <summary>The parent a maximized window is following the size of; null while not maximized.</summary>
		private GuiWidget followed;

		private Vector2 restorePosition;

		private Vector2 restoreSize;

		public WindowMaximizer(WindowWidget window, Func<double> grabWidth)
		{
			this.window = window;
			this.grabWidth = grabWidth;
		}

		public bool IsMaximized { get; private set; }

		/// <summary>Maximizes (remembering where the window was) or restores it; false when nothing changed.</summary>
		public bool Set(bool maximized)
		{
			if (maximized == IsMaximized)
			{
				return false;
			}

			if (maximized)
			{
				restorePosition = window.Position;
				restoreSize = window.Size;
				IsMaximized = true;
				FollowParent();
			}
			else
			{
				IsMaximized = false;
				FollowParent();
				window.Size = restoreSize;
				window.Position = restorePosition;
			}

			return true;
		}

		/// <summary>The window's own rectangle (whole widget, in its parent), whether or not it is maximized.</summary>
		public RectangleDouble RestoreBounds
		{
			get
			{
				Vector2 position = IsMaximized ? restorePosition : window.Position;
				Vector2 size = IsMaximized ? restoreSize : window.Size;
				return new RectangleDouble(position.X, position.Y, position.X + size.X, position.Y + size.Y);
			}
		}

		/// <summary>Tracks the parent's size while maximized (and fills it now); lets go of it otherwise.
		/// Called again when the window changes parent.</summary>
		public void FollowParent()
		{
			GuiWidget follow = IsMaximized ? window.Parent : null;
			if (follow != followed)
			{
				Release();
				followed = follow;
				if (followed != null)
				{
					followed.BoundsChanged += Followed_BoundsChanged;
				}
			}

			FillParent();
		}

		/// <summary>Stops following the parent (the window closed, or it is no longer maximized).</summary>
		public void Release()
		{
			if (followed != null)
			{
				followed.BoundsChanged -= Followed_BoundsChanged;
				followed = null;
			}
		}

		private void Followed_BoundsChanged(object sender, EventArgs e) => FillParent();

		/// <summary>The visible window over the whole parent; the grab border (and shadow) falls outside it.</summary>
		private void FillParent()
		{
			if (followed == null
				|| followed.Width <= 0
				|| followed.Height <= 0)
			{
				return;
			}

			double grab = grabWidth();
			window.Position = new Vector2(-grab, -grab);
			window.Size = new Vector2(followed.Width + grab * 2, followed.Height + grab * 2);
		}
	}
}
