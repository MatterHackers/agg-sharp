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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// A GUI demo window: a <see cref="WindowWidget"/> that, once closed, is still drawn while it fades out but is
	/// not there for the pointer, as agg-gui's window.rs hit_test answers only while the window is asked to be
	/// visible.
	/// </summary>
	public class DemoWindow : WindowWidget
	{
		public DemoWindow(ThemeConfig theme, RectangleDouble bounds)
			: base(theme, bounds)
		{
		}

		/// <summary>
		/// True from the moment the window is closed until it is opened again: window.rs's !requested_visible().
		/// Setting it gives up keyboard focus, as a closing agg-gui window ignores every event.
		/// </summary>
		public bool Closing
		{
			get => this.closing;
			set
			{
				this.closing = value;
				if (value)
				{
					this.Unfocus();
				}
			}
		}

		private bool closing;

		/// <summary>
		/// Every mouse route - clicks, hover and the wheel - asks this before it goes into a child, so a closing
		/// window lets them all through to what is under it. (Selectable would not do: the wheel ignores it.)
		/// </summary>
		public override bool PositionWithinLocalBounds(double x, double y)
		{
			return !this.closing && base.PositionWithinLocalBounds(x, y);
		}
	}
}
