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

				// draw the right arrow
				var x = this.LocalBounds.Right - this.LocalBounds.Height / 2;
				var y = this.Size.Y / 2 + 2;

				var arrow = new VertexStorage();
				arrow.MoveTo(x + 3, y);
				arrow.LineTo(x - 3, y + 5);
				arrow.LineTo(x - 3, y - 5);

				graphics2D.Render(arrow, theme.TextColor);
			}

			public bool KeepMenuOpen
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

			public CheckboxMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
				faChecked = StaticData.Instance.LoadIcon("fa-check_16.png", 16, 16).SetToColor(theme.TextColor);
			}

			public override void OnLoad(EventArgs args)
			{
				this.Image = _checked ? faChecked : null;
				base.OnLoad(args);
			}

			public bool KeepMenuOpen => false;

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

			public RadioMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
			}

			public ImageBuffer SetPreMultiply(ImageBuffer sourceImage)
			{
				sourceImage.SetRecieveBlender(new BlenderPreMultBGRA());

				return sourceImage;
			}

			public override void OnLoad(EventArgs args)
			{
				// Init static radio icons if null
				if (radioIconChecked == null)
				{
					var size = (int)Math.Round(16 * GuiWidget.DeviceScale);
					radioIconChecked = SetPreMultiply(new ImageBuffer(size, size));
					radioIconUnchecked = SetPreMultiply(new ImageBuffer(size, size));

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
				}

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

				var subMenu = new PopupMenu(menuTheme);
				subMenuItemButton.SubMenu = subMenu;

				UiThread.RunOnIdle(() =>
				{
					populateSubMenu(subMenu);

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

			public ImageBuffer Image { get; set; }

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
			systemWindow.ToolTipManager.Clear();
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
			Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);
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

			popup.Widget.Position = popupPosition;
		}
	}
}