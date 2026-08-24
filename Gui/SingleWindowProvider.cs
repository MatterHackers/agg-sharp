/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;

namespace MatterHackers.Agg.UI
{
	public class SingleWindowProvider : ISystemWindowProvider
	{
		protected List<SystemWindow> _openWindows = new List<SystemWindow>();
		protected IPlatformWindow platformWindow;

		private SystemWindow _topWindow;

		// The chrome around every dialog currently nested in the hosted window. Retained (rather than left to
		// the event handlers that used to be the only reference to it) so the chrome can be rebuilt when the
		// device scale or theme changes. Entries are removed when the client window closes.
		private readonly List<NestedWindow> nestedWindows = new List<NestedWindow>();

		private static ThemeConfig theme;

		public static void SetWindowTheme(ThemeConfig theme)
		{
            SingleWindowProvider.theme = theme;
		}

		public SystemWindow TopWindow
		{
			get => _topWindow;

			private set
			{
				void MaintainSizes(object s, EventArgs e)
				{
					foreach (var window in _openWindows)
					{
						if (_topWindow != window)
						{
							window.LocalBounds = new RectangleDouble(0, 0, _topWindow.Width, _topWindow.Height);
						}
					}
				}

				if (_topWindow != null)
				{
					_topWindow.SizeChanged -= MaintainSizes;
				}

				_topWindow = value;

				if (_topWindow != null)
				{
					_topWindow.SizeChanged += MaintainSizes;
				}
			}
		}

		public IReadOnlyList<SystemWindow> OpenWindows => _openWindows;

		// Creates or connects a PlatformWindow to the given SystemWindow
		public virtual void ShowSystemWindow(SystemWindow systemWindow)
		{
			if (_openWindows.Count == 0)
			{
				this._openWindows.Add(systemWindow);
			}
			else
			{
				// Everything shown after the first window is nested: wrapped in a movable, titled
				// WindowWidget and drawn inside the window already on screen. That is how this provider
				// shows a dialog, and it is wrong for a second application shell - the result is a
				// complete second application (menus, tabs, toolbars, viewport) rendered inside the
				// first. Only one shell can be hosted, so say so rather than draw the impossible.
				if (systemWindow.IsApplicationShell)
				{
					throw new InvalidOperationException(
						$"Cannot show a second application shell window ('{systemWindow.Title}') in a single window provider "
						+ $"that is already hosting '{_openWindows.FirstOrDefault()?.Title}'. Nesting one application shell "
						+ "inside another renders an application inside an application. Close the first shell before showing "
						+ "another, or give the second one its own window provider.");
				}

				// Both production subclasses assign the one platform window to every SystemWindow they are
				// asked to show, nested dialogs included (WebGpuSingleWindowProvider, MacSingleWindowProvider,
				// both immediately after calling base). So in the running application this guard short
				// circuits a re-show of anything already shown, a nested dialog as much as a top level
				// window - showing an open dialog again brings nothing to the front, it simply returns.
				// Only a provider that leaves PlatformWindow unset (the bare base class, and the stubs the
				// tests build on it) falls through here and wraps an already nested window a second time.
				// That is existing behavior and is left as is.
				if (systemWindow.PlatformWindow != null)
				{
					return;
				}

				if (theme == null)
				{
					throw new InvalidOperationException(
						"SingleWindowProvider.theme is null. Call SingleWindowProvider.SetWindowTheme() before showing system windows.");
				}

				var overlayWindow = new SystemWindow(_openWindows.FirstOrDefault().Width, _openWindows.FirstOrDefault().Height)
				{
					PlatformWindow = platformWindow
				};

				_openWindows.FirstOrDefault().Unfocus();

				var nestedWindow = new NestedWindow(this, systemWindow, overlayWindow);

				nestedWindow.Wrap();

				nestedWindow.HookClientWindowEvents();

				this.nestedWindows.Add(nestedWindow);

				this._openWindows.Add(overlayWindow);
			}

			TopWindow = _openWindows.LastOrDefault();

			platformWindow.ShowSystemWindow(TopWindow);

			// Ensure focus is set to the new window
			systemWindow.Focus();
		}

		public virtual void CloseSystemWindow(SystemWindow systemWindow)
		{
			if (_openWindows.Count > 1)
			{
				if (systemWindow == _openWindows.FirstOrDefault())
				{
					foreach (var openWindow in _openWindows.Reverse<SystemWindow>())
					{
						openWindow.Close();
					}

					_openWindows.Clear();
				}

				// Find and remove the WindowContainer from the openWindows list
				_openWindows.Remove(systemWindow);
			}

			TopWindow = _openWindows.LastOrDefault();

			platformWindow.CloseSystemWindow(systemWindow);
		}

		/// <summary>
		/// Rebuilds the chrome (title bar, border, grab handles) around every open nested window at the current
		/// <see cref="GuiWidget.DeviceScale"/> and the current window theme, scaling each window's size and
		/// position by the change in device scale.
		/// </summary>
		/// <remarks>
		/// A <see cref="WindowWidget"/> takes its metrics from the scale and theme in effect when it is
		/// constructed, so a dialog that is already open when either changes keeps the old chrome until it is
		/// rebuilt. The client window is moved from the old chrome to the new one and is never closed - closing
		/// it would run the dialog's close handlers, which is how a dialog reports the user's answer to whoever
		/// opened it.
		/// </remarks>
		public void RebuildNestedChrome()
		{
			// Over a copy: a rebuild can close a window (a client that refuses to be re-parented would), and
			// that removes it from the list being walked.
			foreach (var nestedWindow in nestedWindows.ToList())
			{
				nestedWindow.RebuildChrome();
			}
		}

		/// <summary>
		/// One nested (non top level) window: the client <see cref="SystemWindow"/>, the full size overlay
		/// window it is drawn inside of, and the <see cref="WindowWidget"/> chrome that makes it movable and
		/// titled. Holding these together rather than as captured locals means the event handlers always act
		/// on the current chrome, so the chrome can be rebuilt without leaving handlers bound to a dead widget.
		/// </summary>
		private class NestedWindow
		{
			private readonly SingleWindowProvider provider;

			public NestedWindow(SingleWindowProvider provider, SystemWindow systemWindow, SystemWindow overlayWindow)
			{
				this.provider = provider;
				this.SystemWindow = systemWindow;
				this.OverlayWindow = overlayWindow;

				// Hooked here rather than in Wrap() so that rebuilding the chrome cannot subscribe a second
				// time. The handler reads Movable at invocation, so one subscription serves every chrome this
				// nested window ever has.
				OverlayWindow.BoundsChanged += this.OverlayWindow_BoundsChanged;

				OverlayWindow.DisplayScaleChanged += this.OverlayWindow_DisplayScaleChanged;
			}

			/// <summary>
			/// Gets the client window supplied by the caller - the actual dialog content.
			/// </summary>
			public SystemWindow SystemWindow { get; }

			/// <summary>
			/// Gets the window that fills the screen and hosts the chrome. This is the window that is pushed
			/// to the platform, the client window is only ever a widget within it.
			/// </summary>
			public SystemWindow OverlayWindow { get; }

			/// <summary>
			/// Gets the chrome (title bar, border, drag and resize behavior) wrapped around the client window.
			/// </summary>
			public WindowWidget Movable { get; private set; }

			/// <summary>
			/// Gets the <see cref="GuiWidget.DeviceScale"/> that was in effect when <see cref="Movable"/> was
			/// built. The chrome sizes itself from the scale at construction time, so a later scale change
			/// leaves it stale.
			/// </summary>
			public double BuiltAtDeviceScale { get; private set; }

			/// <summary>
			/// Builds the <see cref="WindowWidget"/> chrome around the client window and places it, centered,
			/// in the overlay window.
			/// </summary>
			public void Wrap()
			{
				SystemWindow.HAnchor = HAnchor.Stretch;
				SystemWindow.VAnchor = VAnchor.Stretch;

				var movable = new WindowWidget(theme, SystemWindow)
				{
					WindowBorderColor = theme.BorderColor.WithAlpha(175)
				};

				this.Movable = movable;
				this.BuiltAtDeviceScale = GuiWidget.DeviceScale;

				movable.AddTitleBar(SystemWindow.Title, () =>
				{
					SystemWindow.Close();
				});

				movable.Width = Math.Min(OverlayWindow.Width, movable.Width);
				movable.Height = Math.Min(OverlayWindow.Height, movable.Height);

				OverlayWindow.AddChild(movable);

				movable.TitleBar.BackgroundColor = theme.BackgroundColor;

				// A long standing nudge of unrecorded origin - presumably it forces a layout the sizing above
				// leaves undone. Removing it has not been tried, so it is preserved verbatim.
				movable.Width += 1;

				movable.Position = new VectorMath.Vector2((OverlayWindow.Width - movable.Width) / 2, (OverlayWindow.Height - movable.Height) / 2);
			}

			/// <summary>
			/// Replaces the chrome with one built at the current <see cref="GuiWidget.DeviceScale"/> and window
			/// theme, keeping the same client window and the geometry the user left it at.
			/// </summary>
			/// <remarks>
			/// The client window is detached from the old chrome before that chrome is closed, because closing a
			/// widget closes its children - and closing the client window would run the dialog's close handlers,
			/// telling whoever opened it that the user had answered.
			/// </remarks>
			public void RebuildChrome()
			{
				var oldMovable = this.Movable;

				if (oldMovable == null)
				{
					// never wrapped, so there is no chrome to rebuild
					return;
				}

				// Asked before the detach, because the answer is about the chain that is about to be taken apart.
				bool clientHadFocus = SystemWindow.ContainsFocus;

				// A chrome built at scale 1 and rebuilt at scale 2 has to end up twice the size, so the window
				// occupies the same part of the screen rather than half of it.
				double scaleRatio = BuiltAtDeviceScale > 0 ? GuiWidget.DeviceScale / BuiltAtDeviceScale : 1;

				var oldSize = oldMovable.Size;
				var oldPosition = oldMovable.Position;

				SystemWindow.Parent?.RemoveChild(SystemWindow);

				// Re-adding a widget that has been removed is refused until the flag is cleared (the same dance
				// PopupWidget does when it takes over someone else's content widget).
				SystemWindow.ClearRemovedFlag();

				// Takes itself out of the overlay window on the way out.
				oldMovable.Close();

				this.Wrap();

				// Wrap() sizes the chrome to fit its content and centers it, which is right the first time a
				// window is shown and wrong for a rebuild: this window has been sized and moved since. Put it
				// back, measured in the new scale's pixels. That also drops Wrap()'s +1 width nudge, which is a
				// first-show quirk - laying out at the size the user actually left the window at is the point
				// here, and the assignment forces the same layout the nudge was there to force.
				var size = oldSize * scaleRatio;
				size.X = Math.Min(OverlayWindow.Width, size.X);
				size.Y = Math.Min(OverlayWindow.Height, size.Y);
				Movable.Size = size;

				Movable.Position = oldPosition * scaleRatio;

				ClampIntoOverlay();

				if (clientHadFocus)
				{
					RestoreFocusToClient();
				}
			}

			/// <summary>
			/// Puts the client window back on the overlay's focus chain after it has been moved to new chrome.
			/// </summary>
			/// <remarks>
			/// A key event is routed by walking down from the window on screen through the children that report
			/// <see cref="GuiWidget.ContainsFocus"/>. Re-parenting does not touch that flag: the client window and
			/// everything inside it keep theirs, but the chrome just built has never been focused, so the walk
			/// stops at the overlay and every keystroke into an open dialog is dropped until the user clicks
			/// something.
			/// <para>
			/// <see cref="GuiWidget.Focus"/> on the client marks its whole new parent chain and leaves the widget
			/// focused inside it alone - the focus walk only unfocuses widgets outside the chain it is marking,
			/// and there are none. It does nothing at all for a widget that is already focused though, which the
			/// client itself is whenever nothing inside it took the focus, so that case needs the flag dropped
			/// first. Dropping it on a focused widget returns before recursing into children, so nothing below
			/// the client is disturbed either way.
			/// </para>
			/// </remarks>
			private void RestoreFocusToClient()
			{
				if (SystemWindow.Focused)
				{
					SystemWindow.Unfocus();
				}

				SystemWindow.Focus();
			}

			/// <summary>
			/// Subscribes to the client window events that live for as long as the nested window does. Separate
			/// from <see cref="Wrap"/> so that rebuilding the chrome does not double subscribe.
			/// </summary>
			public void HookClientWindowEvents()
			{
				SystemWindow.VisibleChanged += this.SystemWindow_VisibleChanged;

				SystemWindow.Closed += this.SystemWindow_Closed;
			}

			private void SystemWindow_Closed(object sender, EventArgs e)
			{
				SystemWindow.VisibleChanged -= this.SystemWindow_VisibleChanged;
				OverlayWindow.BoundsChanged -= this.OverlayWindow_BoundsChanged;
				OverlayWindow.DisplayScaleChanged -= this.OverlayWindow_DisplayScaleChanged;

				// The provider holds this so the chrome can be rebuilt; a closed window has no chrome to rebuild
				// and must not be left in the list to be found by a later rebuild.
				provider.nestedWindows.Remove(this);

				OverlayWindow.Close();
			}

			private void SystemWindow_VisibleChanged(object sender, EventArgs e)
			{
				if (SystemWindow.Visible)
				{
					provider._openWindows.Add(OverlayWindow);
					provider.TopWindow = OverlayWindow;

					OverlayWindow.Visible = true;
				}
				else
				{
					provider._openWindows.Remove(OverlayWindow);
					provider.TopWindow = provider._openWindows.LastOrDefault();

					OverlayWindow.Visible = false;
				}

				provider.platformWindow.ShowSystemWindow(provider.TopWindow);
			}

			/// <summary>
			/// Passes what the monitor this overlay is on reports - its scale and how much room it has - on to
			/// the first open window, which is the application shell.
			/// </summary>
			/// <remarks>
			/// The platform host attaches itself to whichever window is on top, so while a dialog is open every
			/// OS report of a display change arrives at that dialog's overlay window. The shell is the window
			/// with the UI to rebuild, and it would otherwise hear nothing until the last dialog closed -
			/// dragging the application to a different-scale monitor with a dialog open would rescale nothing.
			/// <para>
			/// One way only. Nothing sends the shell's scale back down to the overlays, so there is no loop to
			/// break; and <see cref="SystemWindow.SetDisplayScale"/> drops a repeat of the value it last raised
			/// for, so re-reports of an unchanged scale cost the shell nothing.
			/// </para>
			/// <para>
			/// The usable size rides along here because it has no change event of its own: the hosts push it
			/// wherever they push the scale, and its consumers read it when a scale change tells them to. It is
			/// forwarded first for that reason - SetDisplayScale below is what schedules the shell handler that
			/// reads it.
			/// </para>
			/// </remarks>
			private void OverlayWindow_DisplayScaleChanged(object sender, EventArgs e)
			{
				var rootWindow = provider._openWindows.FirstOrDefault();

				if (rootWindow == null
					|| rootWindow == OverlayWindow)
				{
					return;
				}

				// A size no host has measured is Vector2.Zero, which SetDisplayUsableSize discards rather than
				// letting it overwrite the shell's last good measurement.
				rootWindow.SetDisplayUsableSize(OverlayWindow.DisplayUsableSize);

				rootWindow.SetDisplayScale(OverlayWindow.DisplayScale);
			}

			/// <summary>
			/// Keeps the chrome on screen when the overlay window shrinks. Reads <see cref="Movable"/> rather
			/// than capturing it so a rebuilt chrome is clamped instead of the one it replaced.
			/// </summary>
			private void OverlayWindow_BoundsChanged(object sender, EventArgs e)
			{
				ClampIntoOverlay();
			}

			/// <summary>
			/// Slides the chrome back inside the overlay window when it hangs off the right or the top. Does
			/// nothing before there is any chrome: the overlay's bounds are subscribed to in the constructor,
			/// which is ahead of the <see cref="Wrap"/> that builds the first <see cref="Movable"/>, so a resize
			/// arriving in between has nothing to clamp.
			/// </summary>
			private void ClampIntoOverlay()
			{
				var movable = this.Movable;

				if (movable == null)
				{
					return;
				}

				var position = movable.Position;

				// Adjust Y
				if (position.Y + movable.Height > OverlayWindow.Height)
				{
					position.Y = OverlayWindow.Height - movable.Height;
				}

				// Adjust X
				if (position.X + movable.Width > OverlayWindow.Width)
				{
					position.X = Math.Max(0, OverlayWindow.Width - movable.Width);
				}

				movable.Position = position;
			}
		}
	}
}