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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The rounded field look shared by DropDownList and the themed text and number fields: a fill inside the
	/// Border band and a rounded outline stroked in exactly the pixels the square border ring would have used,
	/// so rounding a field never moves its bounds or its text.
	/// </summary>
	internal static class RoundedFieldChrome
	{
		/// <summary>
		/// Gives <paramref name="field"/> the theme's <see cref="ThemeConfig.FieldDesignHeight"/> (border included)
		/// as its minimum height and centres <paramref name="content"/> in it. Does nothing at the default of 0,
		/// so a field keeps the height its font and padding give it. Call once the field's Border is set.
		/// </summary>
		public static void ApplyFieldHeight(GuiWidget field, ThemeConfig theme, params GuiWidget[] content)
		{
			if (theme.FieldDesignHeight <= 0)
			{
				return;
			}

			foreach (var child in content)
			{
				if (child != null)
				{
					child.VAnchor = VAnchor.Center;
				}
			}

			// A widget's Border band sits outside its bounds, so the bounds are the height less that band.
			var height = theme.FieldDesignHeight * GuiWidget.DeviceScale - field.DeviceBorder.Height;
			field.MinimumSize = new VectorMath.Vector2(field.MinimumSize.X, Math.Max(field.MinimumSize.Y, height));
		}

		/// <summary>
		/// The fill's corner radius: the fill sits inside the Border band, so its corners are the outline's
		/// radius less that band.
		/// </summary>
		public static double InnerRadius(double outerRadius, double borderWidth) => Math.Max(0, outerRadius - borderWidth);

		/// <summary>Fills <paramref name="localBounds"/> with corners of <see cref="InnerRadius"/>.</summary>
		public static void DrawFill(Graphics2D graphics2D, RectangleDouble localBounds, double outerRadius, double borderWidth, Color color)
		{
			graphics2D.Render(new RoundedRect(localBounds, InnerRadius(outerRadius, borderWidth)), color);
		}

		/// <summary>
		/// Strokes the rounded outline in the Border band around <paramref name="boundsInParent"/>. Returns false,
		/// drawing nothing, when there is no band - the caller then leaves the parent's square ring to it.
		/// </summary>
		public static bool DrawRing(Graphics2D graphics2D, RectangleDouble boundsInParent, double borderWidth, double outerRadius, Color color)
		{
			if (borderWidth <= 0)
			{
				return false;
			}

			var centerline = new RectangleDouble(
				boundsInParent.Left - borderWidth / 2,
				boundsInParent.Bottom - borderWidth / 2,
				boundsInParent.Right + borderWidth / 2,
				boundsInParent.Top + borderWidth / 2);
			graphics2D.Render(new Stroke(new RoundedRect(centerline, outerRadius - borderWidth / 2), borderWidth), color);
			return true;
		}
	}
}
