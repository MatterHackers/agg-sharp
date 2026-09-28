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
*/

using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Fades a <see cref="ScrollableWidget"/>'s content out towards each edge that has more content beyond it - a hint
	/// that the view scrolls. Opt-in: the view adds one the first time <see cref="ScrollableWidget.EdgeFade"/> is read, and places it
	/// above the content but below the scroll bars, so the bars are never dimmed.
	/// </summary>
	public class ScrollEdgeFade : GuiWidget
	{
		// Enough strips that the ramp reads as a gradient at the default 20 px.
		private const int Steps = 12;

		private readonly ScrollableWidget view;

		internal ScrollEdgeFade(ScrollableWidget view)
		{
			this.view = view;
			this.Selectable = false;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		/// <summary>Gets or sets how opaque the fade is at the very edge, 0..1. 0 (the default) draws nothing.</summary>
		public double Strength { get; set; }

		/// <summary>Gets or sets how far in from each edge the fade reaches, in device pixels.</summary>
		public double Size { get; set; } = 20;

		/// <summary>
		/// Gets or sets the colour the content fades to. Null (the default) takes the first opaque background of the
		/// view or its ancestors, so the fade dissolves into whatever the view sits on.
		/// </summary>
		public Color? Color { get; set; }

		/// <summary>The edges being faded right now: those with content scrolled out of view beyond them.</summary>
		public IEnumerable<string> FadedEdges()
		{
			if (Strength <= 0 || Size <= 0)
			{
				yield break;
			}

			if (view.ScrollOffsetFromTop() > 0.5)
			{
				yield return "Top";
			}

			if (view.MaxScrollFromTop() - view.ScrollOffsetFromTop() > 0.5)
			{
				yield return "Bottom";
			}

			if (view.ScrollOffsetFromLeft() > 0.5)
			{
				yield return "Left";
			}

			if (view.MaxScrollFromLeft() - view.ScrollOffsetFromLeft() > 0.5)
			{
				yield return "Right";
			}
		}

		/// <summary>The colour the fade blends to: <see cref="Color"/>, else the nearest opaque background.</summary>
		public Color ResolvedColor()
		{
			if (Color.HasValue)
			{
				return Color.Value;
			}

			for (GuiWidget widget = view; widget != null; widget = widget.Parent)
			{
				if (widget.BackgroundColor.alpha > 0)
				{
					return widget.BackgroundColor;
				}
			}

			return Agg.Color.White;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// the fade covers the viewport only - the scroll area's rect, which a solid bar is outside of
			BorderDouble margin = view.ScrollArea.DeviceMargin;
			RectangleDouble viewport = new RectangleDouble(margin.Left, margin.Bottom, view.Width - margin.Right, view.Height - margin.Top);
			Color color = ResolvedColor();
			double size = Math.Min(Size, Math.Min(viewport.Width, viewport.Height) / 2);
			double strength = Math.Clamp(Strength, 0, 1);
			foreach (string edge in FadedEdges())
			{
				for (int i = 0; i < Steps; i++)
				{
					// strip 0 touches the edge and is the most opaque
					double a = size * i / Steps;
					double b = size * (i + 1) / Steps;
					Color strip = color.WithAlpha((int)Math.Round(color.alpha * strength * (1 - (i + 0.5) / Steps)));
					switch (edge)
					{
						case "Top":
							graphics2D.FillRectangle(viewport.Left, viewport.Top - b, viewport.Right, viewport.Top - a, strip);
							break;
						case "Bottom":
							graphics2D.FillRectangle(viewport.Left, viewport.Bottom + a, viewport.Right, viewport.Bottom + b, strip);
							break;
						case "Left":
							graphics2D.FillRectangle(viewport.Left + a, viewport.Bottom, viewport.Left + b, viewport.Top, strip);
							break;
						case "Right":
							graphics2D.FillRectangle(viewport.Right - b, viewport.Bottom, viewport.Right - a, viewport.Top, strip);
							break;
					}
				}
			}

			base.OnDraw(graphics2D);
		}
	}
}
