/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// A run of related formatting buttons drawn as one segmented control: a rounded outline in the theme's control
	/// border colour around them all, and a divider of the same colour in the one-unit gap between each pair. The
	/// buttons themselves draw no outline, so no edge is ever drawn twice; their fills are square inside and
	/// rounded only on the group's outer corners. The toolbar's wrapping strip holds each group as one item, so it
	/// wraps a group whole.
	/// </summary>
	public class MarkdownFormatGroup : FlowLayoutWidget
	{
		/// <summary>The outline's corner radius, in design units.</summary>
		public const double DesignRadius = 7;

		/// <summary>The room between two groups, in design units (half of it on each side of a group).</summary>
		public const double DesignSpacing = 6;

		private readonly ThemeConfig theme;

		public MarkdownFormatGroup(ThemeConfig theme)
			: base(FlowDirection.LeftToRight)
		{
			this.theme = theme;
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit | VAnchor.Center;
			Margin = new BorderDouble(DesignSpacing / 2, 0);

			// The outline is drawn inside the bounds, one unit wide; the padding keeps the buttons' fills inside it.
			Padding = new BorderDouble(1);
			BackgroundOutlineWidth = 1;
			BackgroundRadius = DesignRadius * DeviceScale;
		}

		/// <summary>The group's buttons, left to right.</summary>
		public IEnumerable<MarkdownFormatButton> Buttons => Children.OfType<MarkdownFormatButton>();

		/// <summary>
		/// The outline and divider colour: the theme's control border, or agg-gui's widget stroke when the theme
		/// sets none - the colour the size list's ring falls back to (SelectionControlStyle.WidgetStroke, internal
		/// to Gui), so the groups and the list read as one set of controls. Read at draw time, so it follows a theme
		/// changed in place.
		/// </summary>
		public override Color BorderColor
		{
			get => theme.ControlBorderColorIfSet ?? (theme.IsDarkTheme
				? new ColorF(.60, .60, .65, .60).ToColor()
				: new ColorF(.75, .76, .78).ToColor());
			set { }
		}

		/// <summary>
		/// Adds <paramref name="button"/> at the right end, a one-unit divider gap after the one before it, and
		/// rounds the end buttons' fills to the outline's inner corners.
		/// </summary>
		public MarkdownFormatButton AddButton(MarkdownFormatButton button)
		{
			if (Children.Count > 0)
			{
				button.Margin = new BorderDouble(1, 0, 0, 0);
			}

			AddChild(button);

			var buttons = Buttons.ToList();
			double inner = (DesignRadius - 1) * DeviceScale;
			for (int i = 0; i < buttons.Count; i++)
			{
				double left = i == 0 ? inner : 0;
				double right = i == buttons.Count - 1 ? inner : 0;
				buttons[i].BackgroundRadius = new RadiusCorners(ne: right, nw: left, sw: left, se: right);
			}

			return button;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// A divider fills each gap between two buttons, from the outline's inside top to its inside bottom.
			var color = BorderColor;
			GuiWidget previous = null;
			foreach (var child in Children)
			{
				if (previous != null)
				{
					var before = previous.BoundsRelativeToParent;
					var gap = new RectangleDouble(before.Right, before.Bottom, child.BoundsRelativeToParent.Left, before.Top);
					if (gap.Width > 0)
					{
						graphics2D.FillRectangle(gap, color);
					}
				}

				previous = child;
			}
		}
	}
}
