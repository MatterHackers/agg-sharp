/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A large choice button: an optional icon, a bold title and a muted description in a rounded card. Selected,
	/// it gets a 2 px accent edge, an accent tint fill and an accent title; unselected, a 1 px control border
	/// one pixel in from its bounds - so the card, and everything in it, stays put when the selection moves.
	/// </summary>
	/// <remarks>
	/// Named "<c>title</c> Card" for automation. A tab stop; Space or Enter selects it, and in a group the arrow
	/// keys move the selection to the previous or next card. Clicking an unselected
	/// card selects it; clicking a selected one leaves it selected, as a radio choice does. Use
	/// <see cref="SelectableCardGroup"/> to keep exactly one of several cards selected.
	/// </remarks>
	public class SelectableCard : FlowLayoutWidget
	{
		/// <summary>The design units between the card's edge and its content, not counting the border band.</summary>
		private const double DesignPadding = 14;

		/// <summary>The widest edge either state draws; the content is inset by it so it never moves.</summary>
		private const double DesignBorderBand = 2;

		private readonly ThemeConfig theme;
		private readonly TextWidget titleWidget;
		private readonly WrappedTextWidget descriptionWidget;
		private bool selected;

		public SelectableCard(string title, string description, ThemeConfig theme, ImageBuffer icon = null)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			Name = title + " Card";
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;
			Padding = new BorderDouble(DesignPadding + DesignBorderBand);
			Cursor = Cursors.Hand;
			TabStop = true;

			if (icon != null)
			{
				AddChild(new ImageWidget(icon)
				{
					HAnchor = HAnchor.Left,
					Margin = new BorderDouble(bottom: 6),
					Selectable = false,
				});
			}

			AddChild(titleWidget = new TextWidget(title, pointSize: theme.DefaultFontSize + 1, textColor: theme.SecondaryTextColor, bold: true)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(bottom: 6),
				Selectable = false,
			});

			if (!string.IsNullOrEmpty(description))
			{
				AddChild(descriptionWidget = new WrappedTextWidget(description, pointSize: theme.DefaultFontSize - 1, textColor: theme.MutedTextColor)
				{
					HAnchor = HAnchor.Stretch,
					Selectable = false,
				});
			}

			UpdateColors();
		}

		/// <summary>The group this card was added to, which the arrow keys move within; null when alone.</summary>
		internal SelectableCardGroup Group { get; set; }

		/// <summary>Raised when <see cref="Selected"/> changes, by click, keyboard or code.</summary>
		public event EventHandler SelectedChanged;

		/// <summary>Whether this card is the chosen one. Setting the same value again raises nothing.</summary>
		public bool Selected
		{
			get => selected;
			set
			{
				if (selected == value)
				{
					return;
				}

				selected = value;
				UpdateColors();
				Invalidate();
				SelectedChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>The title shown in bold.</summary>
		public override string Text
		{
			get => titleWidget.Text;
			set => titleWidget.Text = value;
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				Selected = true;
			}

			base.OnClick(mouseEvent);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			int step = keyEvent.KeyCode switch
			{
				Keys.Left or Keys.Up => -1,
				Keys.Right or Keys.Down => 1,
				_ => 0,
			};

			if (keyEvent.KeyCode == Keys.Space || keyEvent.KeyCode == Keys.Enter)
			{
				Selected = true;
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}
			else if (step != 0 && Group?.Move(this, step) == true)
			{
				// Consumed even at an end, so an arrow on the last card does not scroll an enclosing view.
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnFocusChanged(EventArgs e)
		{
			Invalidate();
			base.OnFocusChanged(e);
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var bounds = LocalBounds;
			var radius = theme.CardRadius * scale;

			// Selected: a 2 px edge on the bounds. Unselected: a 1 px margin, then a 1 px edge - the design's
			// "margin: 1px" that keeps both states the same size.
			var edge = (selected ? 2 : 1) * scale;
			var inset = selected ? 0 : scale;
			var outer = new RectangleDouble(bounds.Left + inset, bounds.Bottom + inset, bounds.Right - inset, bounds.Top - inset);
			var outerRadius = Math.Max(0, radius - inset);

			var fill = selected ? theme.AccentTintColor : theme.ResolvedControlFillColor;
			if (!selected && UnderMouseState != UnderMouseState.NotUnderMouse && Enabled)
			{
				fill = SelectionControlStyle.WithOpaqueAlpha(fill.Blend(theme.TextColor, .04));
			}

			graphics2D.Render(new RoundedRect(outer, outerRadius), fill);

			var border = selected ? theme.PrimaryAccentColor : theme.ControlBorderColorIfSet ?? SelectionControlStyle.WidgetStroke(theme);
			var centerline = new RectangleDouble(outer.Left + edge / 2, outer.Bottom + edge / 2, outer.Right - edge / 2, outer.Top - edge / 2);
			graphics2D.Render(new Stroke(new RoundedRect(centerline, Math.Max(0, outerRadius - edge / 2)), edge), border);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// Read every frame so a theme change reaches a card that is already built.
			UpdateColors();
			base.OnDraw(graphics2D);

			if (Focused)
			{
				// Inside the bounds, since drawing is clipped to them; over the edge band, as SegmentedControl does.
				var scale = DeviceScale;
				var inset = .75 * scale;
				var ring = new RoundedRect(LocalBounds.Left + inset, LocalBounds.Bottom + inset, LocalBounds.Right - inset, LocalBounds.Top - inset, theme.CardRadius * scale);
				graphics2D.Render(new Stroke(ring, 1.5 * scale), theme.EditFieldColors.Focused.BorderColor);
			}
		}

		private void UpdateColors()
		{
			titleWidget.TextColor = selected ? theme.PrimaryAccentColor : theme.SecondaryTextColor;
		}
	}

	/// <summary>
	/// Keeps at most one of a set of <see cref="SelectableCard"/>s selected: selecting one clears the rest, and
	/// the arrow keys move the selection between them like a radio group.
	/// </summary>
	public class SelectableCardGroup
	{
		private readonly List<SelectableCard> cards = new List<SelectableCard>();

		/// <summary>True while the group itself is clearing cards, so those changes are not taken as the user's.</summary>
		private bool clearing;

		/// <summary>Raised once each time <see cref="SelectedCard"/> changes, including to null.</summary>
		public event EventHandler SelectionChanged;

		/// <summary>The cards in the group, in the order they were added.</summary>
		public IReadOnlyList<SelectableCard> Cards => cards;

		/// <summary>The selected card, or null when none is.</summary>
		public SelectableCard SelectedCard { get; private set; }

		/// <summary>
		/// Adds <paramref name="card"/> to the group and returns it, so it can be added inline. A card added
		/// already selected becomes the group's selection.
		/// </summary>
		public SelectableCard Add(SelectableCard card)
		{
			cards.Add(card);
			card.Group = this;
			card.SelectedChanged += (s, e) => OnCardChanged(card);
			if (card.Selected)
			{
				OnCardChanged(card);
			}

			return card;
		}

		/// <summary>
		/// Selects the card <paramref name="step"/> places from <paramref name="from"/>, stopping at the ends, and
		/// gives it focus. Returns false when <paramref name="from"/> is not in the group.
		/// </summary>
		internal bool Move(SelectableCard from, int step)
		{
			int index = cards.IndexOf(from);
			if (index < 0)
			{
				return false;
			}

			var next = cards[Math.Clamp(index + step, 0, cards.Count - 1)];
			next.Selected = true;
			next.Focus();
			return true;
		}

		private void OnCardChanged(SelectableCard card)
		{
			if (card.Selected)
			{
				if (SelectedCard == card)
				{
					return;
				}

				SelectedCard = card;
				clearing = true;
				try
				{
					foreach (var other in cards)
					{
						if (other != card)
						{
							other.Selected = false;
						}
					}
				}
				finally
				{
					clearing = false;
				}

				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
			else if (!clearing && SelectedCard == card)
			{
				SelectedCard = null;
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}
