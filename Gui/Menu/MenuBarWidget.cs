/*
Copyright (c) 2026, Lars Brubaker
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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A horizontal bar of top level menu titles, each opening a <see cref="PopupMenu"/> built from a
	/// <see cref="MenuItemModel"/>. The in-app equivalent of a platform menu bar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Ported from agg-gui's <c>MenuBar</c> (<c>agg-gui/src/widgets/menu/widget/mod.rs</c>) and follows its
	/// desktop semantics: pressing a title opens that menu, pressing the title of the menu already open
	/// closes it, Escape closes, and a press in neutral space dismisses.
	/// </para>
	/// <para>
	/// The titles are deliberately <see cref="GuiWidget.Selectable"/> = false, so the bar - not the buttons -
	/// does the hit testing over their bounds. That is agg-gui's <c>menu_at(pos)</c>, and it keeps the whole
	/// open/close state machine in one place instead of spread over per button Click handlers that cannot
	/// see each other.
	/// </para>
	/// </remarks>
	public class MenuBarWidget : FlowLayoutWidget
	{
		private readonly List<MenuItemModel> barMenus = new List<MenuItemModel>();
		private readonly List<ThemedTextButton> titles = new List<ThemedTextButton>();
		private readonly ThemeConfig theme;

		private int openIndex = -1;
		private int hoverIndex = -1;
		private int suppressHoverFor = -1;
		private PopupMenu openPopup;

		/// <summary>
		/// The title whose menu <see cref="OpenMenu"/> last declined to open because its model came back
		/// empty, or -1 for none.
		/// </summary>
		/// <remarks>
		/// A decline leaves <see cref="openIndex"/> pointing at whatever was already open, so without this the
		/// hover switch would re-attempt the empty title on every mouse move over it - and each attempt runs
		/// the application's model builder, which for an app menu can walk the whole scene. Remembered only
		/// while the cursor stays on that title (see <see cref="SetHoverIndex"/>), since moving away and back
		/// is a fresh ask and the model may have something in it by then.
		/// </remarks>
		private int declinedHoverIndex = -1;

		/// <summary>
		/// Whether the button that is currently down opened a menu, and so whether letting it go chooses
		/// whatever the drag has reached. agg-gui's <c>arm_mouse_up_activation</c>.
		/// </summary>
		private bool activateOnRelease;

		/// <summary>
		/// Builds a bar with one title per entry in <paramref name="menus"/>.
		/// </summary>
		/// <param name="menus">
		/// The top level menus. Each one's <see cref="MenuItemModel.SubMenuItems"/> is invoked every time
		/// the menu opens, never here, so contents that depend on application state stay current.
		/// </param>
		/// <param name="theme">The theme the titles and the popups they open are drawn with.</param>
		public MenuBarWidget(IReadOnlyList<MenuItemModel> menus, ThemeConfig theme)
			: base(FlowDirection.LeftToRight)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;

			// Match the compact bar height the app chrome already uses, rather than the taller
			// theme.ButtonHeight a ThemedTextButton gives itself.
			this.Height = theme.MicroButtonHeight;

			foreach (var menu in menus ?? new List<MenuItemModel>())
			{
				if (menu == null
					|| menu.IsVisible?.Invoke() == false)
				{
					continue;
				}

				var title = new ThemedTextButton(menu.Text, theme)
				{
					// This exact name is what automation and product tours already search for
					Name = $"{menu.Text} Menu",
					Height = theme.MicroButtonHeight,
					VAnchor = VAnchor.Center,
					BackgroundColor = Color.Transparent,

					// The bar owns the hit testing - see the class remarks
					Selectable = false,
					TabStop = false,
				};

				barMenus.Add(menu);
				titles.Add(title);
				this.AddChild(title);
			}
		}

		/// <summary>
		/// Gets the index of the menu currently showing its popup, or null when none is.
		/// </summary>
		/// <remarks>
		/// Cleared when the popup actually closes, which for a dismissal happens on the next idle pass
		/// rather than inside the press that caused it. Code reading this from within a mouse handler is
		/// therefore looking at the state before that press resolves.
		/// </remarks>
		public int? OpenMenuIndex => openIndex >= 0 ? openIndex : (int?)null;

		/// <summary>Gets a value indicating whether any top level menu is showing its popup.</summary>
		public bool AnyMenuOpen => openIndex >= 0;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Snapshot which menu was open *before* base.OnMouseDown, because taking the focus there is
			// what starts the popup's close-on-focus-lost. Checking afterwards would see a menu that is
			// already on its way out and reopen the one the user just asked to close. Same reason
			// MatterCAD's PopupButton snapshots its menu visibility at mouse down.
			int wasOpen = openIndex;
			int pressed = MenuAt(mouseEvent.Position);

			base.OnMouseDown(mouseEvent);

			if (pressed >= 0
				&& pressed != wasOpen)
			{
				OpenMenu(pressed);

				// agg-gui's open_menu_for_drag_release: a press that opens a menu also starts the
				// press-drag-release gesture, so the user can go straight from here to a row without
				// letting go. Only when the menu really opened - OpenMenu declines an empty one.
				activateOnRelease = openIndex == pressed;
			}
			else
			{
				// A press that closes rather than opens ends any gesture with it
				activateOnRelease = false;

				// Either the toggle (pressed the open title) or a press in the bar's neutral space. Both
				// close, and both close the same way: the bar has just taken the focus, so the popup's own
				// close-on-focus-lost runs it out on the next idle pass. Closing it here as well would run
				// the popup's teardown twice.
				openIndex = -1;
				SuppressHoverOnTheTitleUnderTheCursor();
				UpdateTitleColors();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			int hovered = MenuAt(mouseEvent.Position);

			SetHoverIndex(hovered);

			// Desktop hover switching: once a menu is open, sliding across the bar walks the open popup along
			// with the cursor. Deliberately only while something is open - hover on its own never opens the
			// first menu, so a cursor merely crossing the bar cannot drop a panel over the app.
			if (hovered >= 0
				&& openIndex >= 0
				&& hovered != openIndex
				&& hovered != declinedHoverIndex)
			{
				// The incoming popup is opened without first closing the outgoing one. Taking the focus is
				// what tells the outgoing popup to go, and it tears itself down on the next idle pass; each
				// popup's close is closed over its own widget, so the one being opened here is not caught up
				// in it. See the OpenMenu remarks.
				//
				// DIVERGES from agg-gui only in bookkeeping: there the switch resets the popup's state and so
				// has to re-arm mouse-up activation explicitly. Here the flag lives on the bar and outlives
				// the popup it was raised for, so a drag that switches menus stays armed for free.
				OpenMenu(hovered);

				// It may have declined - an empty menu is not opened. Remember that so the moves that follow
				// while the cursor sits here do not build the same empty model over and over.
				if (openIndex != hovered)
				{
					declinedHoverIndex = hovered;
				}
			}

			if (activateOnRelease
				&& hovered < 0
				&& openPopup?.HasBeenClosed == false)
			{
				// The press was on the bar, so agg-sharp routes this move to the bar and to nothing else -
				// the popup the drag has moved over cannot see it. Hand it the position so its rows
				// highlight and its sub menus open exactly as they would under a free cursor.
				openPopup.DragOver(this.TransformToScreenSpace(mouseEvent.Position));
			}

			base.OnMouseMove(mouseEvent);
		}

		/// <summary>
		/// Ends a press-drag-release: over a row it chooses it, in neutral space it dismisses, and on a title
		/// it does neither.
		/// </summary>
		/// <remarks>
		/// This only runs for a release the bar's own press armed (agg-gui's <c>is_mouse_up_activation_armed</c>),
		/// so an ordinary click - press and release on the same title without moving - still just leaves the
		/// menu open, and the toggle that closes it stays where it belongs, on the press.
		/// <para>
		/// Neutral space is agg-gui's <c>body_contains</c> read inside out: off the titles and off the open
		/// menu. Only the bar knows where its titles are and only the menu knows where its panels are, which
		/// is why the test needs both halves.
		/// </para>
		/// </remarks>
		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool activating = activateOnRelease;
			bool overATitle = MenuAt(mouseEvent.Position) >= 0;
			var screenPosition = this.TransformToScreenSpace(mouseEvent.Position);

			// The gesture is over however it resolves
			activateOnRelease = false;

			// Before acting, so the capture is already given up by the time an activation runs the menu's
			// teardown
			base.OnMouseUp(mouseEvent);

			if (!activating
				|| overATitle
				|| openPopup?.HasBeenClosed != false)
			{
				return;
			}

			if (openPopup.BodyContains(screenPosition))
			{
				openPopup.ActivateRowAt(screenPosition);

				return;
			}

			// Neutral space. Nothing else will close the menu for us - a release moves no focus, which is
			// what every other dismissal here rides on - so it is asked to go directly.
			openIndex = -1;
			UpdateTitleColors();
			openPopup.DismissAll();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			SetHoverIndex(-1);

			base.OnMouseLeaveBounds(mouseEvent);
		}

		/// <summary>
		/// The index of the title containing <paramref name="position"/>, or -1 for none. agg-gui's
		/// <c>menu_at</c>.
		/// </summary>
		private int MenuAt(Vector2 position)
		{
			for (int i = 0; i < titles.Count; i++)
			{
				if (titles[i].Visible
					&& titles[i].BoundsRelativeToParent.Contains(position))
				{
					return i;
				}
			}

			return -1;
		}

		private void SetHoverIndex(int index)
		{
			if (hoverIndex != index)
			{
				// The cursor has left the title that declined, so the next visit asks its model again
				declinedHoverIndex = -1;
			}

			// The suppression lasts exactly as long as the cursor stays on the title it was raised for.
			// Compared against the incoming hover rather than the stored one so that leaving the bar entirely
			// clears it too.
			bool suppressionChanged = suppressHoverFor != index
				&& suppressHoverFor != -1;

			if (suppressionChanged)
			{
				suppressHoverFor = -1;
			}

			if (hoverIndex == index
				&& !suppressionChanged)
			{
				return;
			}

			hoverIndex = index;
			UpdateTitleColors();
		}

		/// <summary>
		/// Stops the title the cursor is sitting on from re-reading as hovered after the menu it owns has
		/// just been dismissed.
		/// </summary>
		/// <remarks>
		/// Closing a menu by clicking its own title leaves the cursor exactly where it was, so the hover
		/// highlight would come straight back and the dismissed menu would still look selected. agg-gui's
		/// <c>suppress_hover_for</c>. It clears the moment the cursor moves to another title or off the bar.
		/// </remarks>
		private void SuppressHoverOnTheTitleUnderTheCursor()
		{
			suppressHoverFor = hoverIndex;
		}

		/// <summary>
		/// Moves the open menu <paramref name="delta"/> titles along the bar, wrapping at both ends. agg-gui's
		/// <c>switch_open_menu</c>.
		/// </summary>
		/// <remarks>
		/// Reached from the open popup rather than from a key handler here - see
		/// <see cref="PopupMenu.TopLevelArrowKey"/> for why. The switch is the same open-then-close
		/// <see cref="OnMouseMove"/> does for a hover, so the outgoing popup goes down on its own once the
		/// incoming one has taken the focus.
		/// </remarks>
		private void SwitchOpenMenu(int delta)
		{
			if (openIndex < 0
				|| barMenus.Count == 0)
			{
				return;
			}

			// rem_euclid: C#'s % keeps the sign of the dividend, so stepping left off the first title needs
			// the extra add to come out on the last one
			int next = ((openIndex + delta) % barMenus.Count + barMenus.Count) % barMenus.Count;

			OpenMenu(next);
		}

		/// <summary>
		/// Builds and shows the popup for <paramref name="index"/>, replacing whatever was open.
		/// </summary>
		/// <remarks>
		/// Switching is open-then-close, not close-then-open. Showing the new popup focuses it, which is the
		/// signal the outgoing one closes on, and that close runs from the idle queue. The two never get
		/// crossed because each popup's close-on-focus-lost is closed over its own widget (see
		/// <c>PopupPlacement.ShowPopup</c>) and this bar's own Closed handler only clears
		/// <see cref="openIndex"/> for the popup that is still the current one. Closing the outgoing popup
		/// here instead would tear it down twice, once directly and once from the queued handler.
		/// </remarks>
		/// <param name="index">Which top level menu to open.</param>
		private void OpenMenu(int index)
		{
			var systemWindow = this.Parents<SystemWindow>().LastOrDefault();
			if (systemWindow == null)
			{
				return;
			}

			var popupMenu = new PopupMenu(theme)
			{
				// The popup holds the keyboard focus for as long as it is up, so the bar never sees a key
				// itself. This is how the two arrows the menu cannot use get back to it.
				TopLevelArrowKey = SwitchOpenMenu,
			};

			MenuModelPopupBuilder.AddItems(popupMenu, barMenus[index].SubMenuItems?.Invoke(), theme);

			if (popupMenu.Children.Count == 0)
			{
				// Nothing to show. Leave whatever was open to close on its own rather than swapping it for
				// an empty panel.
				popupMenu.Close();
				return;
			}

			PopupMenu.ClearToolTipsAbove(this);

			// The menu is fully populated by now, so this is the point at which we can tell whether it fits
			popupMenu.MakeMenuHaveScroll(systemWindow.Height - PopupMenu.WindowEdgeInset);

			openPopup = popupMenu;
			openIndex = index;

			// An open title paints as open, so whatever an earlier dismissal suppressed is moot
			suppressHoverFor = -1;

			popupMenu.Closed += (s, e) =>
			{
				// Only if it is still the current one - switching menus closes the outgoing popup after the
				// incoming one has already claimed these fields.
				if (openPopup == popupMenu)
				{
					openPopup = null;
					openIndex = -1;

					// Escape, an outside click and choosing an item all land here with the cursor possibly
					// still on the title - same stale highlight as the click-to-close toggle
					SuppressHoverOnTheTitleUnderTheCursor();
					UpdateTitleColors();
				}
			};

			// Hang the panel off the bottom of its title. The alt mate is the fallback on both axes, and
			// BestPopupPosition picks it per axis, so this one pair covers two independent flips: right
			// aligned when a menu near the right edge would run off the window, and hung *above* the title
			// when there is no room below it. The bar is not always at the top of the window - MatterCAD
			// docks one at the bottom of the Variable Sheet editor - and without the vertical flip that menu
			// was only clamped back on screen, landing over the bar rather than above it.
			systemWindow.ShowPopup(
				theme,
				new MatePoint(titles[index])
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
				},
				new MatePoint(popupMenu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
				});

			UpdateTitleColors();
		}

		private void UpdateTitleColors()
		{
			for (int i = 0; i < titles.Count; i++)
			{
				Color background;

				if (i == openIndex)
				{
					background = theme.AccentMimimalOverlay;
				}
				else if (i == hoverIndex
					&& i != suppressHoverFor)
				{
					background = theme.ToolbarButtonHover;
				}
				else
				{
					background = Color.Transparent;
				}

				if (titles[i].BackgroundColor != background)
				{
					titles[i].BackgroundColor = background;
					titles[i].Invalidate();
				}
			}
		}
	}
}
