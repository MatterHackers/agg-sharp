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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A tinted, rounded panel that sets a group of related content apart - a note, or options that belong
	/// together. Fill <see cref="ThemeConfig.AccentTintColor"/>, a 1 px <see cref="ThemeConfig.AccentTintBorderColor"/>
	/// outline, <see cref="ThemeConfig.ContainerRadius"/> corners and 14 units of padding. Children stack top to
	/// bottom; add them as to any <see cref="FlowLayoutWidget"/>.
	/// </summary>
	public class InfoBox : FlowLayoutWidget
	{
		private const double DesignPadding = 14;

		private readonly ThemeConfig theme;

		public InfoBox(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;
			Padding = new BorderDouble(DesignPadding);
		}

		/// <summary>
		/// Draws the tint and its outline straight from the theme each frame, so a theme change reaches a box
		/// that is already built.
		/// </summary>
		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			var scale = DeviceScale;
			var bounds = LocalBounds;
			var radius = theme.ContainerRadius * scale;
			graphics2D.Render(new RoundedRect(bounds, radius), theme.AccentTintColor);

			var stroke = scale;
			var centerline = new RectangleDouble(bounds.Left + stroke / 2, bounds.Bottom + stroke / 2, bounds.Right - stroke / 2, bounds.Top - stroke / 2);
			graphics2D.Render(new Stroke(new RoundedRect(centerline, System.Math.Max(0, radius - stroke / 2)), stroke), theme.AccentTintBorderColor);
		}
	}
}
