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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class PopupMenu : FlowLayoutWidget, IIgnoredPopupChild
	{
		public ThemeConfig Theme { get; private set; }

		public static BorderDouble MenuPadding => new BorderDouble(40, 8, 20, 8);

		public static Color DisabledTextColor { get; set; } = Color.Gray;

		public PopupMenu(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.Theme = theme;
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
			this.BackgroundColor = theme.BackgroundColor;
		}

		public HorizontalLine CreateSeparator(double height = 1)
		{
			var line = new HorizontalLine(Theme.BorderColor20)
			{
				Margin = new BorderDouble(8, 1),
				BackgroundColor = Theme.RowBorder,
				Height = height * DeviceScale,
			};

			this.AddChild(line);

			return line;
		}

		public MenuItem CreateMenuItem(string name, ImageBuffer icon = null, string shortCut = null)
		{
			GuiWidget content;

			var textWidget = new TextWidget(name, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
			{
				Padding = MenuPadding,
			};

			if (shortCut != null)
			{
				content = new GuiWidget()
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};

				content.AddChild(new TextWidget(shortCut, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
				{
					HAnchor = HAnchor.Right
				});

				content.AddChild(textWidget);
			}
			else
			{
				content = textWidget;
			}

			content.Selectable = false;

			var menuItem = new MenuItem(content, Theme)
			{
				Name = name + " Menu Item",
				Image = icon
			};

			menuItem.Click += (s, e) =>
			{
				Unfocus();
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public class SubMenuItemButton : MenuItem, IIgnoredPopupChild
		{
			public PopupMenu SubMenu { get; set; }

			public SubMenuItemButton(GuiWidget content, ThemeConfig theme) : base(content, theme)
			{
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				// Draw the right arrow. Its offsets are the shape we want at 1:1, so they are device pixels
				// only at that scale - without DeviceScale the arrow stays a 6x10 speck on a Retina panel
				// beside a row that has doubled.
				var x = this.LocalBounds.Right - this.LocalBounds.Height / 2;
				var y = this.Size.Y / 2 + 2 * DeviceScale;

				var arrow = new VertexStorage();
				arrow.MoveTo(x + 3 * DeviceScale, y);
				arrow.LineTo(x - 3 * DeviceScale, y + 5 * DeviceScale);
				arrow.LineTo(x - 3 * DeviceScale, y - 5 * DeviceScale);

				graphics2D.Render(arrow, theme.TextColor);
			}

			public new bool KeepMenuOpen
			{
				get
				{
					if (SubMenu != null)
					{
						return SubMenu.ContainsFocus;
					}

					return false;
				}
			}
		}

		public class CheckboxMenuItem : MenuItem, IIgnoredPopupChild, ICheckbox
		{
			private bool _checked;

			private ImageBuffer faChecked;

			private double faCheckedScale;

			public CheckboxMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
			}

			/// <summary>
			/// Loads the check mark, or leaves it alone if it is already the size the current display wants.
			/// </summary>
			/// <remarks>
			/// <see cref="StaticData.LoadIcon(string, int, int, bool, Func{ImageBuffer, ValueTuple{ImageBuffer, string}})"/>
			/// bakes <see cref="GuiWidget.DeviceScale"/> into the pixels, and the scale can change under a live
			/// widget, so the check has to be reloaded when it does. LoadIcon caches by device size, so this
			/// costs a dictionary lookup and a recolor rather than a decode.
			/// </remarks>
			private void EnsureCheckIcon()
			{
				if (faChecked != null
					&& faCheckedScale == GuiWidget.DeviceScale)
				{
					return;
				}

				faChecked = StaticData.Instance.LoadIcon("fa-check_16.png", 16, 16).GrayToColor(theme.TextColor);
				faCheckedScale = GuiWidget.DeviceScale;

				this.Image = _checked ? faChecked : null;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				// OnLoad fires once per widget, but the display scale can move after that
				EnsureCheckIcon();

				base.OnDraw(graphics2D);
			}

			public override void OnLoad(EventArgs args)
			{
				// Icon load is deferred to OnLoad (disk I/O + recolor), matching RadioMenuItem
				EnsureCheckIcon();

				this.Image = _checked ? faChecked : null;
				base.OnLoad(args);
			}

			public new bool KeepMenuOpen => false;

			public bool Checked
			{
				get => _checked;
				set
				{
					if (_checked != value)
					{
						_checked = value;
						this.Image = _checked ? faChecked : null;

						this.CheckedStateChanged?.Invoke(this, null);
						this.Invalidate();
					}
				}
			}

			public event EventHandler CheckedStateChanged;
		}

		public class RadioMenuItem : MenuItem, IIgnoredPopupChild, IRadioButton
		{
			private bool _checked;

			private ImageBuffer radioIconChecked;

			private ImageBuffer radioIconUnchecked;

			private double radioIconScale;

			public RadioMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
			}

			public ImageBuffer SetPreMultiply(ImageBuffer sourceImage)
			{
				sourceImage.SetRecieveBlender(new BlenderPreMultBGRA());

				return sourceImage;
			}

			/// <summary>
			/// Rasterizes the two radio circles, or leaves them alone if they are already the size the
			/// current display wants.
			/// </summary>
			/// <remarks>
			/// The icons are rasterized at <see cref="GuiWidget.DeviceScale"/>, and that can change under a
			/// live widget (the window moved to a display of another scale), so building them once and only
			/// checking for null would leave the circle at half or double the size of the text beside it.
			/// </remarks>
			private void EnsureRadioIcons()
			{
				if (radioIconChecked != null
					&& radioIconScale == GuiWidget.DeviceScale)
				{
					return;
				}

				var size = (int)Math.Round(16 * GuiWidget.DeviceScale);
				radioIconChecked = SetPreMultiply(new ImageBuffer(size, size));
				radioIconUnchecked = SetPreMultiply(new ImageBuffer(size, size));
				radioIconScale = GuiWidget.DeviceScale;

				var rect = new RectangleDouble(0, 0, size, size);

				RadioImage.DrawCircle(
					radioIconChecked.NewGraphics2D(),
					rect.Center,
					theme.TextColor,
					isChecked: true,
					isActive: false);

				RadioImage.DrawCircle(
					radioIconUnchecked.NewGraphics2D(),
					rect.Center,
					theme.TextColor,
					isChecked: false,
					isActive: false);

				this.Image = _checked ? radioIconChecked : radioIconUnchecked;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				// Rebuild here rather than only in OnLoad - OnLoad fires once per widget, and the display
				// scale can move after that (a comparison of two doubles per draw, and nothing else when
				// the scale has not changed)
				EnsureRadioIcons();

				base.OnDraw(graphics2D);
			}

			public override void OnLoad(EventArgs args)
			{
				// Icon rasterization is deferred to OnLoad, matching CheckboxMenuItem
				EnsureRadioIcons();

				this.Image = _checked ? radioIconChecked : radioIconUnchecked;

				this.Invalidate();

				if (!this.SiblingRadioButtonList.Contains(this))
				{
					this.SiblingRadioButtonList.Add(this);
				}

				base.OnLoad(args);
			}

			public bool KeepMenuOpen => false;

			public IList<GuiWidget> SiblingRadioButtonList { get; set; }

			public bool Checked
			{
				get => _checked;
				set
				{
					if (_checked != value)
					{
						_checked = value;

						this.Image = _checked ? radioIconChecked : radioIconUnchecked;

						if (_checked)
						{
							this.UncheckSiblings();
						}

						this.CheckedStateChanged?.Invoke(this, null);

						this.Invalidate();
					}
				}
			}

			public event EventHandler CheckedStateChanged;
		}

		/// <summary>
		/// The gap left between a clamped menu and the window edge, so the menu border does not draw on the
		/// window boundary. Matches the inset <see cref="PopupLayoutEngine"/> uses for drop downs.
		/// </summary>
		internal const double WindowEdgeInset = 5;

		/// <summary>
		/// Constrain this menu to <paramref name="maxHeight"/>, moving its items into a scrolling area when
		/// they do not fit. This is not the same operation as the same named
		/// <see cref="PopupWidget.MakeMenuHaveScroll(double)"/>: that one resizes a scroll window the popup
		/// already owns, while this one has no scroll window to start with and so reparents the menu items
		/// into one it creates.
		/// </summary>
		/// <param name="maxHeight">The tallest this menu is allowed to be, typically the window height.</param>
		/// <remarks>
		/// Sub menus are anchored to the item that opened them (see <see cref="CreateSubMenu"/>) rather than
		/// going through <see cref="PopupWidget"/>, so they get none of the clamp-and-scroll behavior that
		/// drop downs have. Without this a tall menu (a 20 entry recent files list, say) is simply drawn off
		/// the top of the window where most of its items can never be reached.
		/// Only call this once the menu has been populated - the height of an empty menu tells us nothing.
		/// </remarks>
		internal void MakeMenuHaveScroll(double maxHeight)
		{
			if (maxHeight <= 0
				|| this.Height <= maxHeight)
			{
				return;
			}

			// Capture the laid out width before the items leave - a Fit menu collapses without them
			var contentWidth = this.Width;

			var items = this.Children.ToList();
			this.RemoveChildren();

			var contentColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Left | HAnchor.Fit,
				VAnchor = VAnchor.Fit,
			};

			foreach (var item in items)
			{
				// Children remember that they were removed, which would keep them from laying out again
				item.ClearRemovedFlag();
				contentColumn.AddChild(item);
			}

			var scrollingWindow = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Height = maxHeight,
			};
			scrollingWindow.ScrollArea.VAnchor = VAnchor.Fit;
			scrollingWindow.AddChild(contentColumn);

			// Fit anchoring would size us to the scroll window's content, which is the very thing we are
			// trying to escape, so take explicit control of both axes
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute;

			// Widen for the scroll bar so it does not cover the item text (matches PopupWidget)
			this.Width = contentWidth + 15 * DeviceScale;
			this.Height = maxHeight;

			this.AddChild(scrollingWindow);
		}

		/// <summary>
		/// Takes down any tooltip, shown or armed, on every SystemWindow above <paramref name="widget"/> before
		/// a menu is opened over that area.
		/// </summary>
		/// <remarks>
		/// This deliberately clears all of them rather than picking one. SystemWindows nest in single window
		/// mode, and each one runs its own ToolTipManager over the same mouse position, so the tooltip that
		/// would draw over the menu can belong to either. The two menu paths used to disagree about which one
		/// to ask (CreateSubMenu took the innermost window, ShowMenu the outermost), which meant the submenu
		/// path could clear an inner manager while an outer one still held the tooltip. Clearing an inner
		/// window's manager when it holds nothing is free, so there is nothing to gain by guessing.
		/// Note the window each path uses to *host* the popup still differs - that is a placement concern and
		/// is unrelated to which manager owns the tooltip.
		/// </remarks>
		internal static void ClearToolTipsAbove(GuiWidget widget)
		{
			foreach (var window in widget.Parents<SystemWindow>())
			{
				window.ToolTipManager.Clear();
			}
		}

		public void CreateSubMenu(string menuTitle, ThemeConfig menuTheme, Action<PopupMenu> populateSubMenu, ImageBuffer icon = null)
		{
			var content = new TextWidget(menuTitle, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
			{
				Padding = MenuPadding,
			};

			content.Selectable = false;

			var subMenuItemButton = new SubMenuItemButton(content, Theme)
			{
				Name = menuTitle + " Menu Item",
				Image = icon
			};

			this.AddChild(subMenuItemButton);

			subMenuItemButton.Click += (s, e) =>
			{
				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				if (systemWindow == null)
				{
					return;
				}

				// Same as ShowMenu - a tooltip armed by whatever the mouse crossed on the way here must
				// not float over the menu we are about to open
				ClearToolTipsAbove(this);

				var subMenu = new PopupMenu(menuTheme);
				subMenuItemButton.SubMenu = subMenu;

				UiThread.RunOnIdle(() =>
				{
					populateSubMenu(subMenu);

					// Measure after populating - a sub menu taller than the window must be made to scroll
					// before it is positioned, or it lands (and stays) off the top of the screen
					subMenu.MakeMenuHaveScroll(systemWindow.Height - WindowEdgeInset);

					systemWindow.ShowPopup(
                        Theme,
						new MatePoint(subMenuItemButton)
						{
							Mate = new MateOptions(MateEdge.Right, MateEdge.Top),
							AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
						},
						new MatePoint(subMenu)
						{
							Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
							AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
						});
				});

				subMenu.Closed += (s1, e1) =>
				{
					subMenu.ClearRemovedFlag();
					subMenuItemButton.SubMenu = null;
					if (!this.ContainsFocus)
					{
						this.Close();
					}
				};
			};
		}

		public MenuItem CreateBoolMenuItem(string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var textWidget = new TextWidget(name, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
			{
				Padding = MenuPadding,
			};

			return this.CreateBoolMenuItem(textWidget, name, getter, setter, useRadioStyle, siblingRadioButtonList);
		}

		public MenuItem CreateBoolMenuItem(string name, ImageBuffer icon, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var row = new FlowLayoutWidget()
			{
				Selectable = false
			};
			row.AddChild(new ThemedIconButton(icon, Theme));

			var textWidget = new TextWidget(name, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
			{
				Padding = MenuPadding,
				VAnchor = VAnchor.Center
			};
			row.AddChild(textWidget);

			return this.CreateBoolMenuItem(row, name, getter, setter, useRadioStyle, siblingRadioButtonList);
		}

		public MenuItem CreateBoolMenuItem(GuiWidget guiWidget, string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			bool isChecked = getter?.Invoke() == true;

			MenuItem menuItem;

			if (useRadioStyle)
			{
				menuItem = new RadioMenuItem(guiWidget, Theme)
				{
					Name = name + " Menu Item",
					Checked = isChecked,
					SiblingRadioButtonList = siblingRadioButtonList
				};
			}
			else
			{
				menuItem = new CheckboxMenuItem(guiWidget, Theme)
				{
					Name = name + " Menu Item",
					Checked = isChecked
				};
			}

			menuItem.Click += (s, e) =>
			{
				if (menuItem is RadioMenuItem radioMenu)
				{
					// Do nothing on reclick of active radio menu
					if (radioMenu.Checked)
					{
						return;
					}

					isChecked = radioMenu.Checked = !radioMenu.Checked;
				}
				else if (menuItem is CheckboxMenuItem checkboxMenu)
				{
					isChecked = checkboxMenu.Checked = !isChecked;
				}

				setter?.Invoke(isChecked);
			};

			this.AddChild(menuItem);

			return menuItem;
		}


		public MenuItem CreateMenuItem(GuiWidget guiWidget, string name, ImageBuffer icon = null)
		{
			var menuItem = new MenuItem(guiWidget, Theme)
			{
				Text = name,
				Name = name + " Menu Item",
				Image = icon
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public bool KeepMenuOpen => false;

		public class MenuItem : ThemedButton
		{
			private GuiWidget content;

			public MenuItem(GuiWidget content, ThemeConfig theme)
				: base(theme)
			{
				// Inflate padding to match the target (MenuGutterWidth) after scale operation in assignment
				this.Padding = new BorderDouble(left: Math.Ceiling(theme.MenuGutterWidth / DeviceScale), right: 15);
				this.HAnchor = HAnchor.MaxFitOrStretch;
				this.VAnchor = VAnchor.Fit;
				this.MinimumSize = new Vector2(150 * GuiWidget.DeviceScale, theme.ButtonHeight);
				this.content = content;
				this.GutterWidth = theme.MenuGutterWidth;
				this.HoverColor = theme.AccentMimimalOverlay;

				content.VAnchor = VAnchor.Center;
				content.HAnchor |= HAnchor.Left;

				this.AddChild(content);
			}

			public double GutterWidth { get; set; }

			private ImageBuffer _image;

			public ImageBuffer Image
			{
				get => _image;
				set
				{
					_image = value;

					// The faded copy is derived from this image, so it is stale the moment the image changes -
					// which happens both when the icon is rebuilt for a new DeviceScale and when a checked
					// state swaps one icon for another.
					_disabledImage = null;
				}
			}

			private ImageBuffer _disabledImage;

			public ImageBuffer DisabledImage
			{
				get
				{
					// Lazy construct on first access
					if (this.Image != null &&
						_disabledImage == null)
					{
						_disabledImage = this.Image.AjustAlpha(0.2);
					}

					return _disabledImage;
				}
			}

			public override bool Enabled
			{
				get => base.Enabled;
				set
				{
					if (content is TextWidget textWidget)
					{
						textWidget.Enabled = value;
					}

					base.Enabled = value;
				}
			}

			public bool KeepMenuOpen => false;

			public override void OnDraw(Graphics2D graphics2D)
			{
				if (this.Image != null)
				{
					var x = this.LocalBounds.Left + (this.GutterWidth / 2 - this.Image.Width / 2);
					var y = this.Size.Y / 2 - this.Image.Height / 2;

					graphics2D.Render(this.Enabled ? this.Image : this.DisabledImage, (int)x, (int)y);
				}

				base.OnDraw(graphics2D);
			}
		}

		public static Vector2 GetYAnchor(MateOptions anchor, MateOptions popup, GuiWidget popupWidget, RectangleDouble bounds)
		{
			if (anchor.Top && popup.Bottom)
			{
				return new Vector2(0, bounds.Height);
			}
			else if (anchor.Top && popup.Top)
			{
				return new Vector2(0, popupWidget.Height - bounds.Height) * -1;
			}
			else if (anchor.Bottom && popup.Top)
			{
				return new Vector2(0, -popupWidget.Height);
			}

			return Vector2.Zero;
		}

		public static Vector2 GetXAnchor(MateOptions anchor, MateOptions popup, GuiWidget popupWidget, RectangleDouble bounds)
		{
			if (anchor.Right && popup.Left)
			{
				return new Vector2(bounds.Width, 0);
			}
			else if (anchor.Left && popup.Right)
			{
				return new Vector2(-popupWidget.Width, 0);
			}
			else if (anchor.Right && popup.Right)
			{
				return new Vector2(popupWidget.Width - bounds.Width, 0) * -1;
			}

			return Vector2.Zero;
		}
	}

	public static class PopupMenuExtensions
	{
		public static void ShowMenu(this PopupMenu popupMenu, GuiWidget anchorWidget, MouseEventArgs mouseEvent)
		{
			popupMenu.ShowMenu(anchorWidget, mouseEvent.Position);
		}

		public static void ShowMenu(this PopupMenu popupMenu, GuiWidget anchorWidget, Vector2 menuPosition)
		{
			var systemWindow = anchorWidget.Parents<SystemWindow>().LastOrDefault();
			PopupMenu.ClearToolTipsAbove(anchorWidget);

			// The menu is fully populated by the time it is shown, so this is the point at which we can tell
			// whether it fits. A tall right click menu (MatterCAD's scene menu) would otherwise be positioned
			// off the top of the window with no way to scroll to the items that ran off.
			popupMenu.MakeMenuHaveScroll(systemWindow.Height - PopupMenu.WindowEdgeInset);

			systemWindow.ShowPopup(
				popupMenu.Theme,
				new MatePoint(anchorWidget)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new MatePoint(popupMenu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
				},
				altBounds: new RectangleDouble(menuPosition.X + 1, menuPosition.Y + 1, menuPosition.X + 1, menuPosition.Y + 1));
		}
	}

	[Flags]
	public enum MateEdge
	{
		Top = 1,
		Bottom = 2,
		Left = 4,
		Right = 8
	}

	public class MateOptions
	{
		public MateOptions(MateEdge horizontalEdge = MateEdge.Left, MateEdge verticalEdge = MateEdge.Bottom)
		{
			this.HorizontalEdge = horizontalEdge;
			this.VerticalEdge = verticalEdge;
		}

		public MateEdge HorizontalEdge { get; set; }

		public MateEdge VerticalEdge { get; set; }

		public bool Top => this.VerticalEdge.HasFlag(MateEdge.Top);

		public bool Bottom => this.VerticalEdge.HasFlag(MateEdge.Bottom);

		public bool Left => this.HorizontalEdge.HasFlag(MateEdge.Left);

		public bool Right => this.HorizontalEdge.HasFlag(MateEdge.Right);
	}

	public class MatePoint
	{
		public MateOptions Mate { get; set; } = new MateOptions();

		public MateOptions AltMate { get; set; } = new MateOptions();

		public GuiWidget Widget { get; set; }

		public MatePoint()
		{
		}

		public MatePoint(GuiWidget widget)
		{
			this.Widget = widget;
		}

		public RectangleDouble Offset { get; set; }
	}

	public interface IOverrideAutoClose
	{
		bool AllowAutoClose { get; }
	}

	public static class SystemWindowExtension
	{
		private static void RightHorizontalSplitPopup(SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left for right aligned split
			Vector2 popupPosition = new Vector2(systemWindow.Width - popup.Widget.Width, 0);

			Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);

			popup.Widget.Height = anchorLeft.Y;

			popup.Widget.Position = popupPosition;
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, BestPopupPosition);
		}

		public static void ShowRightSplitPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, RightHorizontalSplitPopup);
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds, int borderWidth, Action<SystemWindow, MatePoint, MatePoint, RectangleDouble> layoutHelper)
		{
			var hookedParents = new HashSet<GuiWidget>();

			List<IIgnoredPopupChild> ignoredWidgets = popup.Widget.Children.OfType<IIgnoredPopupChild>().ToList();

			void Widget_Draw(object sender, DrawEventArgs e)
			{
				if (borderWidth > 0)
				{
					e.Graphics2D.Render(
						new Stroke(
							new RoundedRect(popup.Widget.LocalBounds, 0),
							borderWidth * 2),
						theme.PopupBorderColor);
				}
			}

			void WidgetRelativeTo_PositionChanged(object sender, EventArgs e)
			{
				if (anchor.Widget?.Parent != null)
				{
					layoutHelper.Invoke(systemWindow, anchor, popup, altBounds);
				}
			}

			void CloseMenu()
			{
				popup.Widget.AfterDraw -= Widget_Draw;

				popup.Widget.Close();

				anchor.Widget.Closed -= Anchor_Closed;

				// Unbind callbacks on parents for position_changed if we're closing
				foreach (GuiWidget widget in hookedParents)
				{
					widget.PositionChanged -= WidgetRelativeTo_PositionChanged;
					widget.BoundsChanged -= WidgetRelativeTo_PositionChanged;
				}

				// Long lived originating item must be unregistered
				anchor.Widget.Closed -= Anchor_Closed;

				// Restore focus to originating widget on close
				if (anchor.Widget?.HasBeenClosed == false)
				{
					anchor.Widget.Focus();
				}
			}

			void FocusChanged(object s, EventArgs e)
			{
				UiThread.RunOnIdle(() =>
				{
					// Fired any time focus changes. Traditionally we closed the menu if we weren't focused.
					// To accommodate children (or external widgets) having focus we also query for and consider special cases
					bool specialChildHasFocus = ignoredWidgets.Any(w => w.ContainsFocus || w.Focused || w.KeepMenuOpen);
					bool descendantIsHoldingOpen = popup.Widget.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
						&& ignoredPopupChild.KeepMenuOpen);

					// If the focused changed and we've lost focus and no special cases permit, close the menu
					if (!popup.Widget.ContainsFocus
						&& !specialChildHasFocus
						&& !descendantIsHoldingOpen
						&& !PopupWidget.DebugKeepOpen)
					{
						CloseMenu();
					}
				});
			}

			void Anchor_Closed(object sender, EventArgs e)
			{
				// If the owning widget closed, so should we
				CloseMenu();
			}

			foreach (var ancestor in anchor.Widget.Parents<GuiWidget>().Where(p => p != systemWindow))
			{
				if (hookedParents.Add(ancestor))
				{
					ancestor.PositionChanged += WidgetRelativeTo_PositionChanged;
					ancestor.BoundsChanged += WidgetRelativeTo_PositionChanged;
				}
			}

			popup.Widget.ContainsFocusChanged += FocusChanged;
			popup.Widget.AfterDraw += Widget_Draw;

			WidgetRelativeTo_PositionChanged(anchor.Widget, null);
			anchor.Widget.Closed += Anchor_Closed;

			// When the widgets position changes, sync the popup position
			systemWindow?.AddChild(popup.Widget);

			popup.Widget.Closed += (s, e) =>
			{
				Console.WriteLine();
			};

			popup.Widget.Focus();

			popup.Widget.Invalidate();
		}

		private static void BestPopupPosition(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left aligned screen space position (using widgetRelativeTo.parent)
			Vector2 anchorLeft = anchor.Widget.Parent.TransformToParentSpace(systemWindow, anchor.Widget.Position);
			anchorLeft += new Vector2(altBounds.Left, altBounds.Bottom);

			Vector2 popupPosition = anchorLeft;

			var bounds = altBounds == default(RectangleDouble) ? anchor.Widget.LocalBounds : altBounds;

			Vector2 xPosition = PopupMenu.GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			Vector2 screenPosition;

			screenPosition = anchorLeft + xPosition;

			// Constrain
			if (screenPosition.X + popup.Widget.Width > systemWindow.Width
				|| screenPosition.X < 0)
			{
				xPosition = PopupMenu.GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += xPosition;

			Vector2 yPosition = PopupMenu.GetYAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			screenPosition = anchorLeft + yPosition;

			// Constrain
			if (anchor.AltMate != null
				&& (screenPosition.Y + popup.Widget.Height > systemWindow.Height
					|| screenPosition.Y < 0))
			{
				yPosition = PopupMenu.GetYAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += yPosition;

			// Flipping to the alt mate does not guarantee an on screen result - several mate combinations
			// (anchor bottom to popup bottom, for one) resolve to no offset at all, leaving the popup exactly
			// where it did not fit. Clamp vertically so the content stays reachable. Horizontal placement is
			// left as the mate flip above decided it, as callers rely on that alignment.
			double topAlignedY = systemWindow.Height - popup.Widget.Height;
			if (popup.Widget.Height > systemWindow.Height)
			{
				// Nothing can show all of a popup that is taller than the window (menus avoid this by
				// scrolling first, see PopupMenu.MakeMenuHaveScroll). Show its top - that is where the items
				// a user is looking for are; bottom aligning it would push them above the top of the window.
				popupPosition.Y = topAlignedY;
			}
			else
			{
				popupPosition.Y = Math.Max(0, Math.Min(popupPosition.Y, topAlignedY));
			}

			popup.Widget.Position = popupPosition;
		}
	}
}