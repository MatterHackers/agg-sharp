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

		/// <summary>
		/// The row this menu was opened from, when it is a sub menu. Null for a top level menu.
		/// </summary>
		/// <remarks>
		/// This is agg-gui's <c>open_path</c> read backwards - it is what the Left arrow backs out to.
		/// </remarks>
		internal SubMenuItemButton ParentMenuItem { get; set; }

		/// <summary>
		/// The widget the menu rows are actually parented to. Normally that is the menu itself, but
		/// <see cref="MakeMenuHaveScroll(double)"/> moves every row into a column inside a
		/// <see cref="ScrollableWidget"/>, so anything that walks the rows has to ask rather than assume.
		/// </summary>
		private GuiWidget ItemContainer
		{
			get
			{
				var scrollingWindow = this.Children.OfType<ScrollableWidget>().FirstOrDefault();

				return scrollingWindow?.ScrollArea.Children.FirstOrDefault() ?? this;
			}
		}

		/// <summary>
		/// The rows the keyboard can land on, in the order they are drawn. Separators are
		/// <see cref="HorizontalLine"/>s rather than <see cref="MenuItem"/>s so they fall out for free;
		/// disabled and hidden rows are filtered the way agg-gui's <c>step_hover</c> filters its entries.
		/// </summary>
		private List<MenuItem> NavigableItems()
		{
			return ItemContainer.Children.OfType<MenuItem>()
				.Where(item => item.Enabled && item.Visible)
				.ToList();
		}

		/// <summary>
		/// Moves the highlight <paramref name="delta"/> rows, wrapping at both ends.
		/// </summary>
		/// <remarks>
		/// agg-sharp has no separate hover index - the highlight *is* keyboard focus, so the current row is
		/// whichever row is <see cref="GuiWidget.Focused"/>. The base index of -1 for a downward step and 0
		/// for an upward one is what makes Down-from-nothing land on the first row and Up-from-nothing land
		/// on the last, exactly as agg-gui's <c>step_hover</c> does.
		/// </remarks>
		internal void MoveHighlight(int delta)
		{
			var items = NavigableItems();
			if (items.Count == 0)
			{
				return;
			}

			int current = items.FindIndex(item => item.Focused);
			int baseIndex = current >= 0 ? current : (delta > 0 ? -1 : 0);

			// rem_euclid: C#'s % keeps the sign of the dividend, so a negative step needs the extra add
			int next = ((baseIndex + delta) % items.Count + items.Count) % items.Count;

			// Scroll first: GuiWidget.CanSelect is false for a widget whose bounds clip away to nothing
			// against a parent, so Focus() is silently a no-op on a row that is currently scrolled out of
			// the menu's scroll window
			ScrollHighlightIntoView(items[next]);

			items[next].Focus();
		}

		/// <summary>
		/// Keeps the highlighted row visible in whichever scroller is clamping this menu - the one
		/// <see cref="PopupWidget"/> owns when the menu is popup hosted, or the one
		/// <see cref="MakeMenuHaveScroll(double)"/> built when it is not.
		/// </summary>
		private void ScrollHighlightIntoView(GuiWidget item)
		{
			var hostingPopup = this.Parents<PopupWidget>().FirstOrDefault();
			if (hostingPopup != null)
			{
				hostingPopup.ScrollIntoView(item);

				return;
			}

			this.Children.OfType<ScrollableWidget>().FirstOrDefault()?.ScrollIntoView(item);
		}

		/// <summary>
		/// The row of this menu whose sub menu is currently up, or null when none is.
		/// </summary>
		/// <remarks>Only one can be open at a time - this is agg-gui's <c>open_path</c>, one level deep.</remarks>
		private SubMenuItemButton OpenSubMenuRow()
		{
			return ItemContainer.Children.OfType<SubMenuItemButton>().FirstOrDefault(row => row.SubMenu != null);
		}

		/// <summary>
		/// True when the keyboard focus - which is what holds a menu chain open - is on this menu or on any
		/// sub menu opened below it.
		/// </summary>
		/// <remarks>
		/// Asking <c>ContainsFocus</c> alone is not enough for anything that decides whether a chain is still
		/// alive: a sub menu is a popup parented to the <see cref="SystemWindow"/>, not a child of the menu it
		/// hangs off, so the moment a sub menu takes the focus its parent stops containing it. The chain has
		/// moved deeper, not gone.
		/// </remarks>
		internal bool ChainContainsFocus()
		{
			if (this.ContainsFocus)
			{
				return true;
			}

			return ItemContainer.Children.OfType<SubMenuItemButton>()
				.Any(row => row.SubMenu != null
					&& !row.SubMenu.HasBeenClosed
					&& row.SubMenu.ChainContainsFocus());
		}

		/// <summary>
		/// Where the pointer was the last time it crossed onto a row of this menu, in screen space. The apex
		/// of the wedge <see cref="PointerIsAimingAtOpenSubMenu"/> tests against.
		/// </summary>
		private Vector2? lastRowHoverPosition;

		/// <summary>
		/// Drops the wedge apex, so the next hover on a row of this menu is judged on its own.
		/// </summary>
		private void ForgetRowHoverPosition()
		{
			lastRowHoverPosition = null;
		}

		/// <summary>
		/// True when the pointer is between where it last was and the near edge of <paramref name="subMenu"/> -
		/// on its way into the open sub menu rather than choosing the row it happens to be over.
		/// </summary>
		/// <remarks>
		/// This is the "safe triangle" every desktop menu needs and for the same reason: a sub menu hangs down
		/// from the row that opened it, so every row of it below the first is reached by moving down and to the
		/// right, and that path crosses the rows underneath the opening row. Windows buys the same forgiveness
		/// with a dwell timer before the crossed row takes over; a wedge does it on geometry alone, which means
		/// it is decided by where the pointer is rather than by how fast it got there - no timer to tune, and
		/// nothing that behaves differently on a loaded machine.
		/// <para>
		/// The apex is where the pointer last entered a row of this menu, so the wedge covers the paths that
		/// start on the opening row. A pointer moving along the menu instead (same x, different row) is never
		/// inside it - the wedge has no width at the apex - so hovering a sibling still closes the sub menu.
		/// </para>
		/// </remarks>
		private bool PointerIsAimingAtOpenSubMenu(Vector2 pointer, PopupMenu subMenu)
		{
			if (lastRowHoverPosition == null
				|| subMenu == null)
			{
				return false;
			}

			var subMenuBounds = subMenu.TransformToScreenSpace(subMenu.LocalBounds);
			if (subMenuBounds.Width <= 0
				|| subMenuBounds.Height <= 0)
			{
				// Queued to be shown but not laid out yet - there is nothing to aim at
				return false;
			}

			var apex = lastRowHoverPosition.Value;

			// The edge the pointer has to cross to get in. A sub menu that had to open to the left (AltMate,
			// near the right of the screen) is entered through its right edge instead.
			double edgeX = subMenuBounds.Left >= apex.X ? subMenuBounds.Left : subMenuBounds.Right;

			double toEdge = edgeX - apex.X;
			double travelled = pointer.X - apex.X;

			if (toEdge * travelled <= 0
				|| Math.Abs(travelled) > Math.Abs(toEdge))
			{
				// Not headed for the edge at all, or already past it - either way the pointer is not in transit
				return false;
			}

			return PointIsInTriangle(
				pointer,
				apex,
				new Vector2(edgeX, subMenuBounds.Bottom),
				new Vector2(edgeX, subMenuBounds.Top));
		}

		/// <summary>
		/// Standard half-plane test: the point is inside when it is on the same side of all three edges.
		/// Points on an edge count as inside, so a pointer skimming the wedge boundary is not rejected.
		/// </summary>
		private static bool PointIsInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
		{
			double Side(Vector2 from, Vector2 to)
			{
				return (to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X);
			}

			double ab = Side(a, b);
			double bc = Side(b, c);
			double ca = Side(c, a);

			return (ab >= 0 && bc >= 0 && ca >= 0)
				|| (ab <= 0 && bc <= 0 && ca <= 0);
		}

		/// <summary>
		/// The mouse has entered <paramref name="row"/>. Moves the highlight there, closes the sub menu a
		/// different row had open, and opens this row's own sub menu if it has one.
		/// </summary>
		/// <remarks>
		/// This is agg-gui's <c>update_hover</c>: a hovered row with a sub menu sets <c>open_path</c> to it,
		/// and a hovered row without one truncates <c>open_path</c> back to this level. There is no dwell
		/// timer in either implementation - the sub menu opens on the enter itself.
		/// <para>
		/// agg-sharp keeps a single highlight, and that highlight is keyboard focus, so hovering an enabled
		/// row focuses it. That is the Windows convention (Enter activates the row the mouse is pointing at)
		/// and it is what closes the sibling's sub menu for us: taking focus out of a sub menu is what every
		/// other close path in this file does, and it runs the sub menu's full teardown on idle rather than
		/// leaving a half removed popup behind.
		/// </para>
		/// <para>
		/// Disabled rows never arrive here at all - <see cref="GuiWidget.OnMouseMove"/> only routes to
		/// children that are Visible, Enabled and CanSelect - which is agg-gui's
		/// <c>disabled_rows_do_not_become_hovered</c> for free.
		/// </para>
		/// </remarks>
		internal void OnRowHover(MenuItem row, Vector2 pointerInScreenSpace)
		{
			if (!row.Enabled)
			{
				return;
			}

			// The pointer has arrived somewhere in this menu, so whatever wedge the menu above was holding
			// open for it is spent - a hover back out onto one of that menu's rows is a choice now, not transit.
			this.ParentMenuItem?.OwningMenu?.ForgetRowHoverPosition();

			var openRow = OpenSubMenuRow();

			if (openRow == row)
			{
				// The mouse came back onto the row whose sub menu is up, which is what happens on the way out
				// of that sub menu. Leave focus alone: pulling it back here would close the sub menu the user
				// is aiming at, and re-opening it is not possible - it never closed.
				lastRowHoverPosition = pointerInScreenSpace;

				return;
			}

			if (openRow != null
				&& PointerIsAimingAtOpenSubMenu(pointerInScreenSpace, openRow.SubMenu))
			{
				// Crossed on the way into the open sub menu. Leaving the highlight (and so the focus that
				// holds the sub menu up) alone is what keeps the sub menu reachable.
				return;
			}

			lastRowHoverPosition = pointerInScreenSpace;

			row.Focus();

			if (openRow != null)
			{
				// A sibling had a sub menu going, and a sub menu is *shown* from the idle queue - so one that
				// was asked for before the mouse got here is still queued, and showing it focuses it, taking
				// the highlight off this row. Claim it back once that has run (only while the mouse is still
				// here), or this row is left unhighlighted and, if it opens a sub menu of its own, that sub
				// menu's own show sees an unfocused menu and cancels itself as stale.
				UiThread.RunOnIdle(() =>
				{
					if (!row.HasBeenClosed
						&& row.ContainsFirstUnderMouseRecursive())
					{
						row.Focus();
					}
				});
			}

			if (row is SubMenuItemButton subMenuRow)
			{
				subMenuRow.OpenSubMenu();
			}
		}

		/// <summary>
		/// Takes this menu down, along with the popup hosting it and any menu it was opened from.
		/// </summary>
		/// <remarks>
		/// Every way a menu closes in agg-sharp is driven by losing focus - <c>SystemWindowExtension.ShowPopup</c>
		/// and <see cref="PopupWidget.OnContainsFocusChanged"/> both watch for it and close on idle - so this
		/// unfocuses rather than closing anything directly. What it unfocuses is the nearest
		/// <see cref="PopupWidget"/> ancestor, or this menu itself when there is none (the <c>ShowMenu</c>
		/// path parents a menu straight into the window). Either way that is the widget whose close-on-focus-
		/// lost handler owns this level, and dropping it is what takes the whole chain down: each level's
		/// Closed handler closes its parent when the parent does not contain focus, and after this nothing in
		/// the chain does.
		/// </remarks>
		public void DismissAll()
		{
			((GuiWidget)this.Parents<PopupWidget>().FirstOrDefault() ?? this).Unfocus();
		}

		/// <summary>
		/// A press of anything but the left button over an open menu dismisses it, and the row under the
		/// press never sees that press.
		/// </summary>
		/// <remarks>
		/// This is agg-gui's catch-all <c>Event::MouseDown</c> arm: the desktop convention is that a right
		/// (or middle) press closes the menu instead of leaving it hanging over the context menu that press
		/// is about to raise, and it is Consumed so the press only dismisses - it must not also activate the
		/// item underneath it. Skipping the base call is how agg-sharp consumes it: base is what routes the
		/// press to the row, and mouse dispatch hands a press to the topmost child containing the point and
		/// to nobody else, so nothing drawn under the menu sees it either.
		/// <para>
		/// DIVERGES from agg-gui: a press *outside* the menu is left to pass through to whatever is under it
		/// (it dismisses the menu too, by taking focus off it, but it is not swallowed). agg-sharp menus are
		/// dismissed by pressing the button that opened them - MatterCAD's PopupButton only toggles closed
		/// because the press reaches the button - and a right click elsewhere is expected to raise that
		/// widget's own menu in one gesture. There is no menu owned event stream here to consume from in any
		/// case: an outside press is never routed to this widget at all.
		/// </para>
		/// <para>
		/// The dismissal is queued rather than done here because it works by unfocusing, and the press is
		/// still unwinding: every widget between the window and this one finishes its own OnMouseDown by
		/// focusing itself when no child took focus (<see cref="GuiWidget.OnMouseDown"/>), so an unfocus
		/// done inline is undone on the way back out and the idle close then finds the menu focused after
		/// all. That bites the <c>PopupButton</c> shape, where a <see cref="PopupWidget"/> and its scroll
		/// window sit between the window and the menu.
		/// </para>
		/// </remarks>
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button != MouseButtons.Left)
			{
				UiThread.RunOnIdle(DismissAll);

				return;
			}

			base.OnMouseDown(mouseEvent);
		}

		/// <summary>
		/// Arrow key navigation over the menu rows.
		/// </summary>
		/// <remarks>
		/// base is called first so the focused row (and <see cref="GuiWidget"/>'s own Tab handling) gets the
		/// first say and sets <see cref="KeyEventArgs.Handled"/> for us to respect.
		/// <para>
		/// The four arrows and Escape are consumed, matching agg-gui's <c>EventResult::Consumed</c>. That is
		/// not cosmetic here: an agg-sharp menu is drawn inside an application whose root window listens for
		/// unhandled keys (MatterCAD rotates the scene on arrows and cancels the current operation on
		/// Escape), so those five leaking past an open menu would be a user visible bug.
		/// </para>
		/// <para>
		/// Only those five, though. agg-gui consumes *every* key an open menu sees; agg-sharp does not, so a
		/// key the menu has no use for - Delete and Backspace among them - still reaches the application's own
		/// shortcuts with a menu up. Widening this would need a survey of which of those are worth
		/// suppressing, so the narrower behavior is what is claimed here.
		/// </para>
		/// <para>
		/// Enter and Space are deliberately absent - <see cref="ThemedButton.OnKeyUp"/> activates the focused
		/// row on key *up*, and consuming them here would gain nothing.
		/// </para>
		/// </remarks>
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			base.OnKeyDown(keyEvent);

			if (keyEvent.Handled)
			{
				return;
			}

			switch (keyEvent.KeyCode)
			{
				case Keys.Down:
					MoveHighlight(1);
					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
					break;

				case Keys.Up:
					MoveHighlight(-1);
					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
					break;

				case Keys.Right:
					if (NavigableItems().FirstOrDefault(item => item.Focused) is SubMenuItemButton subMenuItem)
					{
						subMenuItem.OpenSubMenu();
					}

					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
					break;

				case Keys.Left:
					// Focus the opener before this menu loses focus. CreateSubMenu's Closed handler closes the
					// parent menu as well when the parent does not contain focus, and that close runs on idle
					// after this returns - so the order here is what decides whether one level closes or all
					// of them do. Nothing to back out to in a top level menu, but the key is still consumed:
					// an arrow that escapes an open menu reaches the application behind it.
					ParentMenuItem?.Focus();
					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
					break;

				case Keys.Escape:
					DismissAll();
					keyEvent.Handled = true;
					keyEvent.SuppressKeyPress = true;
					break;
			}
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

			/// <summary>The menu this row lives in, and that the sub menu is anchored beside.</summary>
			internal PopupMenu OwningMenu { get; set; }

			/// <summary>The theme <see cref="CreateSubMenu"/> was asked to build the sub menu with.</summary>
			internal ThemeConfig SubMenuTheme { get; set; }

			/// <summary>Fills in the sub menu's rows, the callback <see cref="CreateSubMenu"/> was given.</summary>
			internal Action<PopupMenu> PopulateSubMenu { get; set; }

			/// <summary>
			/// Builds, populates and shows this row's sub menu beside it. Does nothing when the sub menu is
			/// already up, so clicking (or arrowing into) an open sub menu twice cannot stack two of them.
			/// </summary>
			internal void OpenSubMenu()
			{
				if (SubMenu != null
					|| OwningMenu == null)
				{
					return;
				}

				var systemWindow = OwningMenu.Parents<SystemWindow>().FirstOrDefault();
				if (systemWindow == null)
				{
					return;
				}

				// Same as ShowMenu - a tooltip armed by whatever the mouse crossed on the way here must
				// not float over the menu we are about to open
				ClearToolTipsAbove(OwningMenu);

				var owningMenu = OwningMenu;
				var populateSubMenu = PopulateSubMenu;

				// Whether the chain was live when the sub menu was asked for. Every path that opens one -
				// hover, click, the Right arrow - leaves the opening row focused first, so this is normally
				// true; it is false only for a menu parented straight into a window rather than shown as a
				// popup, which has no focus to lose and so nothing for the check below to detect.
				bool openedFromAFocusedMenu = owningMenu.ContainsFocus;

				var subMenu = new PopupMenu(SubMenuTheme)
				{
					// Left arrow in the sub menu needs to know the row to back out to
					ParentMenuItem = this,
				};
				this.SubMenu = subMenu;

				UiThread.RunOnIdle(() =>
				{
					// The chain can be dismissed between this being queued and it running. Escape queues its
					// own close *behind* this, so nothing has been closed yet at this point and only focus
					// tells the truth. Showing the sub menu anyway would focus it, and KeepMenuOpen would then
					// hold the dismissed parent open on the strength of a sub menu the user had cancelled.
					if (this.HasBeenClosed
						|| owningMenu.HasBeenClosed
						|| (openedFromAFocusedMenu && !owningMenu.ContainsFocus))
					{
						// Let the row go of the sub menu it never opened, or it can never open another. The
						// menu itself was never populated, parented or drawn, so there is nothing to tear down.
						this.SubMenu = null;

						return;
					}

					populateSubMenu(subMenu);

					// Measure after populating - a sub menu taller than the window must be made to scroll
					// before it is positioned, or it lands (and stays) off the top of the screen
					subMenu.MakeMenuHaveScroll(systemWindow.Height - WindowEdgeInset);

					systemWindow.ShowPopup(
						owningMenu.Theme,
						new MatePoint(this)
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
					this.SubMenu = null;

					// This sub menu going away normally means the whole chain was dismissed, and the parent has
					// to follow it down. Not, though, when the focus simply moved deeper: sweeping down a column
					// of sub menu parents opens each row's sub menu in turn, and because both opening and closing
					// run from the idle queue an older sibling's sub menu can close after the newest one is up
					// and focused. Reading that as a dismissal took the entire chain down mid-sweep.
					if (!owningMenu.ChainContainsFocus())
					{
						owningMenu.Close();
					}
				};
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

			/// <summary>
			/// Whether this row's sub menu is holding the menu it lives in open.
			/// </summary>
			/// <remarks>
			/// The whole chain below the sub menu counts, not just the sub menu itself: with three levels up,
			/// the focus sits in the deepest one, and asking only about the middle level would let the root
			/// close out from under an open chain the moment a deferred ContainsFocusChanged ran.
			/// </remarks>
			public new bool KeepMenuOpen
			{
				get
				{
					if (SubMenu != null
						&& !SubMenu.HasBeenClosed)
					{
						return SubMenu.ChainContainsFocus();
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
		/// The scroll window a clamped menu keeps its rows in.
		/// </summary>
		/// <remarks>
		/// This exists only so that <see cref="ScrollableWidget.OnKeyDown"/> does not claim Up and Down to
		/// nudge the viewport 16 pixels. Once a row has keyboard focus the scroll window is on the routing
		/// path between the menu and that row, so the plain widget would swallow every arrow key before the
		/// menu could move its highlight. Inside a menu the arrows belong to the highlight, and
		/// <see cref="PopupMenu.MoveHighlight(int)"/> does the scrolling that keeps it visible.
		/// </remarks>
		internal class MenuScrollWindow : ScrollableWidget
		{
			public MenuScrollWindow()
				: base(true)
			{
			}

			public override void OnKeyDown(KeyEventArgs keyEvent)
			{
				if (keyEvent.KeyCode == Keys.Up
					|| keyEvent.KeyCode == Keys.Down)
				{
					// The focused row still gets its turn; only the base class's scroll nudge is skipped, so
					// the key arrives at the menu unclaimed.
					// Returning early also skips GuiWidget's Tab handling and its public KeyDown event for
					// these two keys. Neither matters to a menu - Tab is not Up or Down, and nothing
					// subscribes to a menu scroller's KeyDown - but a subscriber added later would silently
					// not hear the arrows.
					var childWithFocus = GetChildContainingFocus();
					if (childWithFocus != null
						&& childWithFocus.Visible
						&& childWithFocus.Enabled)
					{
						childWithFocus.OnKeyDown(keyEvent);
					}

					return;
				}

				base.OnKeyDown(keyEvent);
			}
		}

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

			var scrollingWindow = new MenuScrollWindow
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

		/// <summary>
		/// Adds an item that opens a sub menu, populated by <paramref name="populateSubMenu"/> the first time
		/// (and every time) it is opened.
		/// </summary>
		/// <returns>
		/// The button that was added, so a caller can adjust it (name, enabled state) the way the
		/// <see cref="MenuItem"/> returning creators allow. Most callers ignore it.
		/// </returns>
		public SubMenuItemButton CreateSubMenu(string menuTitle, ThemeConfig menuTheme, Action<PopupMenu> populateSubMenu, ImageBuffer icon = null)
		{
			var content = new TextWidget(menuTitle, pointSize: Theme.DefaultFontSize, textColor: Theme.TextColor)
			{
				Padding = MenuPadding,
			};

			content.Selectable = false;

			var subMenuItemButton = new SubMenuItemButton(content, Theme)
			{
				Name = menuTitle + " Menu Item",
				Image = icon,
				OwningMenu = this,
				SubMenuTheme = menuTheme,
				PopulateSubMenu = populateSubMenu,
			};

			this.AddChild(subMenuItemButton);

			// The work lives on the row so the Right arrow can open the sub menu the same way a click does
			subMenuItemButton.Click += (s, e) => subMenuItemButton.OpenSubMenu();

			return subMenuItemButton;
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

			/// <summary>
			/// Tells the owning menu the mouse arrived, so it can move the highlight and open or close sub
			/// menus. The menu decides rather than the row, because only the menu knows about the sibling
			/// whose sub menu may have to close.
			/// </summary>
			/// <remarks>
			/// The owner is looked up rather than stored: <see cref="MakeMenuHaveScroll(double)"/> reparents
			/// every row into a column inside a scroll window, so the menu is not always the row's Parent, and
			/// only <see cref="SubMenuItemButton"/> is built with an OwningMenu to ask.
			/// </remarks>
			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				base.OnMouseEnterBounds(mouseEvent);

				this.Parents<PopupMenu>().FirstOrDefault()?.OnRowHover(
					this,
					this.TransformToScreenSpace(new Vector2(mouseEvent.X, mouseEvent.Y)));
			}

			/// <summary>
			/// Reads the same as a mouse hover while this row holds keyboard focus.
			/// </summary>
			/// <remarks>
			/// Arrow key navigation moves focus from row to row, and <see cref="ThemedButton"/>'s thin focus
			/// outline is too quiet to be a menu highlight. The mouse still wins when it is over this row -
			/// otherwise the pressed shade would be lost on the row the keyboard last left behind.
			/// </remarks>
			public override Color BackgroundColor
			{
				get
				{
					if (this.Focused
						&& this.Enabled
						&& !this.ContainsFirstUnderMouseRecursive())
					{
						return this.HoverColor;
					}

					return base.BackgroundColor;
				}

				set => base.BackgroundColor = value;
			}

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
}