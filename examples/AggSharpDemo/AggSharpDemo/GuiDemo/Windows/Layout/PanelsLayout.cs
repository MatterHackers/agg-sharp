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
using MatterHackers.Agg;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>Which separator of the Panels window is being dragged.</summary>
	public enum PanelDrag
	{
		Top,
		Left,
		Right,
	}

	/// <summary>
	/// The Panels window's geometry (interaction.rs PanelsLayout), in design units with y up: a full-width top
	/// panel, left and right panels below it, and between them a fixed-height bottom panel under the central
	/// panel, all 4 apart. The top height and side widths are what the separators drag.
	/// </summary>
	public sealed class PanelsLayout
	{
		public const double Gap = 4;
		public const double TopMinimumHeight = 32;
		public const double SideMinimumWidth = 80;
		public const double SideMaximumWidth = 200;
		public const double BottomHeight = 52;

		/// <summary>The order of <see cref="Rects"/>, as egui adds the panels.</summary>
		public static readonly string[] Titles = { "Expandable Upper Panel", "Left Panel", "Right Panel", "Bottom Panel", "Central Panel" };

		public double TopHeight { get; set; } = 112;

		public double LeftWidth { get; set; } = 150;

		public double RightWidth { get; set; } = 150;

		/// <summary>Keeps the top panel and both sides inside a <paramref name="width"/> by <paramref name="height"/> area.</summary>
		public void Clamp(double width, double height)
		{
			width = Math.Max(0, width);
			height = Math.Max(0, height);
			double sideMaximum = Math.Max(SideMinimumWidth, Math.Min(SideMaximumWidth, Math.Max(0, width - Gap * 2) * .45));
			this.TopHeight = Math.Clamp(this.TopHeight, TopMinimumHeight, Math.Max(TopMinimumHeight, height - BottomHeight - Gap * 2));
			this.LeftWidth = Math.Clamp(this.LeftWidth, SideMinimumWidth, sideMaximum);
			this.RightWidth = Math.Clamp(this.RightWidth, SideMinimumWidth, sideMaximum);
		}

		/// <summary>The five panels in <see cref="Titles"/> order for a <paramref name="width"/> by <paramref name="height"/> area.</summary>
		public RectangleDouble[] Rects(double width, double height)
		{
			double bottom = Math.Min(BottomHeight, Math.Max(0, height - this.TopHeight - Gap * 2));
			double middleWidth = Math.Max(0, width - this.LeftWidth - this.RightWidth - Gap * 2);
			double middleHeight = this.MiddleHeight(height);
			double centerHeight = Math.Max(0, middleHeight - bottom - Gap);
			double middleLeft = this.LeftWidth + Gap;
			return new[]
			{
				Rect(0, height - this.TopHeight, width, this.TopHeight),
				Rect(0, 0, this.LeftWidth, middleHeight),
				Rect(width - this.RightWidth, 0, this.RightWidth, middleHeight),
				Rect(middleLeft, 0, middleWidth, bottom),
				Rect(middleLeft, bottom + Gap, middleWidth, centerHeight),
			};
		}

		/// <summary>The gap <paramref name="target"/>'s separator covers.</summary>
		public RectangleDouble Separator(PanelDrag target, double width, double height)
		{
			double middleHeight = this.MiddleHeight(height);
			switch (target)
			{
				case PanelDrag.Top:
					return Rect(0, height - this.TopHeight - Gap, width, Gap);
				case PanelDrag.Left:
					return Rect(this.LeftWidth, 0, Gap, middleHeight);
				default:
					return Rect(width - this.RightWidth - Gap, 0, Gap, middleHeight);
			}
		}

		/// <summary>
		/// Moves <paramref name="target"/>'s separator to the pointer at <paramref name="x"/>, <paramref name="y"/>:
		/// the top edge follows y, a side follows x, each held to its limits.
		/// </summary>
		public void Resize(PanelDrag target, double x, double y, double width, double height)
		{
			width = Math.Max(0, width);
			height = Math.Max(0, height);
			switch (target)
			{
				case PanelDrag.Top:
					this.TopHeight = Math.Clamp(height - y - Gap, TopMinimumHeight, Math.Max(TopMinimumHeight, height - BottomHeight - Gap * 2));
					break;
				case PanelDrag.Left:
					this.LeftWidth = Math.Clamp(x, SideMinimumWidth, SideMaximumWidth);
					break;
				case PanelDrag.Right:
					this.RightWidth = Math.Clamp(width - x - Gap, SideMinimumWidth, SideMaximumWidth);
					break;
			}
		}

		private double MiddleHeight(double height) => Math.Max(0, height - this.TopHeight - Gap);

		private static RectangleDouble Rect(double x, double y, double width, double height) => new RectangleDouble(x, y, x + width, y + height);
	}
}
