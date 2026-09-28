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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A demo's controls and the input routing C++ <c>platform_support</c> does around them: every mouse
	/// event is offered to the controls first, and reaches the demo's own handler only when C++ would pass
	/// it on. A demo forwards its input here and handles it itself when the call returns false.
	/// </summary>
	public class AggCtrlContainer
	{
		private readonly List<AggCtrl> ctrls = new List<AggCtrl>();

		private int current = -1;

		/// <summary>A control changed value (C++ <c>on_ctrl_change</c> + <c>force_redraw</c>).</summary>
		public event EventHandler Changed;

		public void Add(AggCtrl ctrl)
		{
			this.ctrls.Add(ctrl);
		}

		/// <summary>C++ <c>render_ctrl</c> for each control, in the order added.</summary>
		public void Render(Graphics2D graphics)
		{
			foreach (AggCtrl ctrl in this.ctrls)
			{
				ctrl.Render(graphics);
			}
		}

		/// <summary>
		/// A left press goes to the controls; one that no control takes and that is not over a control goes to
		/// the demo. Any other press goes to the demo, whatever else is held.
		/// </summary>
		/// <param name="button">The one button that went down.</param>
		/// <returns>True when the demo must not see the press.</returns>
		public bool OnMouseDown(int x, int y, AggInputFlags button)
		{
			if (button != AggInputFlags.MouseLeft)
			{
				return false;
			}

			if (this.AnyMouseButtonDown(x, y))
			{
				this.SetCurrent(x, y);
				this.OnChanged();
				return true;
			}

			if (this.InRect(x, y))
			{
				if (this.SetCurrent(x, y))
				{
					this.OnChanged();
				}

				return true;
			}

			return false;
		}

		/// <summary>A control being dragged takes the move; otherwise it reaches the demo unless it is over a
		/// control. Only a held left button drags a control.</summary>
		/// <param name="flags">Every button held during the move.</param>
		/// <returns>True when the demo must not see the move.</returns>
		public bool OnMouseMove(int x, int y, AggInputFlags flags)
		{
			bool leftHeld = flags.HasFlag(AggInputFlags.MouseLeft);
			foreach (AggCtrl ctrl in this.ctrls)
			{
				if (ctrl.OnMouseMove(x, y, leftHeld))
				{
					this.OnChanged();
					return true;
				}
			}

			return this.InRect(x, y);
		}

		/// <summary>A left release goes to every control. C++ passes every release on to the demo as well.</summary>
		/// <param name="button">The one button that came up.</param>
		public void OnMouseUp(int x, int y, AggInputFlags button)
		{
			if (button != AggInputFlags.MouseLeft)
			{
				return;
			}

			bool changed = false;
			foreach (AggCtrl ctrl in this.ctrls)
			{
				changed |= ctrl.OnMouseButtonUp(x, y);
			}

			if (changed)
			{
				this.OnChanged();
			}
		}

		/// <summary>Arrow keys go to the current control (the one last clicked).</summary>
		/// <returns>True when the control took the key.</returns>
		public bool OnKeyDown(Keys key)
		{
			bool left = key == Keys.Left;
			bool right = key == Keys.Right;
			bool down = key == Keys.Down;
			bool up = key == Keys.Up;
			if (this.current >= 0
				&& (left || right || down || up)
				&& this.ctrls[this.current].OnArrowKeys(left, right, down, up))
			{
				this.OnChanged();
				return true;
			}

			return false;
		}

		private bool AnyMouseButtonDown(double x, double y)
		{
			foreach (AggCtrl ctrl in this.ctrls)
			{
				if (ctrl.OnMouseButtonDown(x, y))
				{
					return true;
				}
			}

			return false;
		}

		private bool InRect(double x, double y)
		{
			foreach (AggCtrl ctrl in this.ctrls)
			{
				if (ctrl.InRect(x, y))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>C++ <c>ctrl_container::set_cur</c>: the control under the point becomes current.</summary>
		/// <returns>True when the current control changed.</returns>
		private bool SetCurrent(double x, double y)
		{
			for (int i = 0; i < this.ctrls.Count; i++)
			{
				if (this.ctrls[i].InRect(x, y))
				{
					if (this.current != i)
					{
						this.current = i;
						return true;
					}

					return false;
				}
			}

			if (this.current != -1)
			{
				this.current = -1;
				return true;
			}

			return false;
		}

		private void OnChanged()
		{
			this.Changed?.Invoke(this, EventArgs.Empty);
		}
	}
}
