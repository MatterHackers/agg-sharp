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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A <see cref="ColorWheelPicker"/> in a window with a title bar and a close (×) button - agg-gui's
	/// color_wheel_picker_dialog. Push it on a <see cref="ModalOverlay"/> for a modal picker, or add it to any
	/// widget as a floating, draggable window.
	/// </summary>
	/// <remarks>
	/// Select or Cancel closes the dialog. Every other way it closes - the × button, or the overlay's Escape and
	/// press outside - counts as Cancel, so a host showing the colour live is always told to put it back
	/// (agg-gui's hosts wire on_close to the same teardown for that reason). A host that treats a press outside
	/// differently (agg-gui's rich text demo keeps a changed colour) sets <see cref="CancelOnOutsidePress"/> false
	/// and reads <see cref="ClosedByOutsidePress"/> in <see cref="GuiWidget.Closed"/>.
	/// </remarks>
	public class ColorDialog : WindowWidget
	{
		private bool finished;
		private ModalOverlay overlay;

		public ColorDialog(ColorWheelPicker picker, ThemeConfig theme, string title = "Color Picker")
			: base(theme, new GuiWidget(picker.Width, picker.Height))
		{
			Name = "Color Dialog";
			Picker = picker;
			ClientArea.AddChild(picker);
			AddTitleBar(title, CloseDialog);
			Resizable = false;
			BackgroundColor = theme.BackgroundColor;

			picker.Selected += (s, e) => Finish();
			picker.Canceled += (s, e) => Finish();
		}

		public ColorWheelPicker Picker { get; }

		/// <summary>
		/// Whether a press outside the dialog, on its <see cref="ModalOverlay"/>, cancels the picker like × and
		/// Escape do. Defaults to true; set it false to decide in <see cref="GuiWidget.Closed"/> instead.
		/// </summary>
		public bool CancelOnOutsidePress { get; set; } = true;

		/// <summary>Whether the dialog was closed by a press outside it on its <see cref="ModalOverlay"/>.</summary>
		public bool ClosedByOutsidePress { get; private set; }

		public override void OnParentChanged(EventArgs e)
		{
			// The overlay removes the dialog before closing it, so remember it to ask why it closed.
			if (Parent is ModalOverlay parentOverlay)
			{
				overlay = parentOverlay;
			}

			base.OnParentChanged(e);
		}

		public override void OnClosed(EventArgs e)
		{
			ClosedByOutsidePress = !finished && overlay?.ClosingByOutsidePress == true;

			// Closed without Select or Cancel (×, Escape, a press outside the overlay): treat it as Cancel.
			if (!finished)
			{
				finished = true;
				if (!ClosedByOutsidePress || CancelOnOutsidePress)
				{
					Picker.Cancel();
				}
			}

			base.OnClosed(e);
		}

		/// <summary>Closes the dialog, through its <see cref="ModalOverlay"/> when it is that overlay's top layer.</summary>
		public void CloseDialog()
		{
			if (Parent is ModalOverlay overlay
				&& overlay.TopLayer == this)
			{
				overlay.CloseTop();
			}
			else
			{
				Close();
			}
		}

		private void Finish()
		{
			if (!finished)
			{
				finished = true;
				CloseDialog();
			}
		}
	}
}
