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
	/// An in-window modal stack: a translucent backdrop stretched over its parent that swallows every press
	/// meant for the widgets beneath it, with dialogs (<see cref="Push"/>) centred on it one above another.
	/// Escape, or a press anywhere outside the top dialog, closes just that dialog. A port of agg-gui's demo
	/// ModalOverlay (demo-ui/src/windows/text_demos/dialogs.rs).
	/// </summary>
	/// <remarks>
	/// The overlay does not remove itself when the last dialog closes; the owner decides that from
	/// <see cref="LayersChanged"/>. Popups a dialog opens (a drop down list, say) are added to the SystemWindow
	/// above the overlay, so they keep their own input and their own Escape.
	/// </remarks>
	public class ModalOverlay : GuiWidget
	{
		private readonly List<GuiWidget> layers = new List<GuiWidget>();

		public ModalOverlay()
		{
			this.Name = "Modal Overlay";
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.BackgroundColor = DefaultBackdrop;
		}

		/// <summary>agg-gui's backdrop: black at 35%.</summary>
		public static Color DefaultBackdrop => new Color(0, 0, 0, 89);

		/// <summary>Raised after a dialog is pushed or closed.</summary>
		public event EventHandler LayersChanged;

		/// <summary>The open dialogs, bottom first.</summary>
		public IReadOnlyList<GuiWidget> Layers => this.layers;

		/// <summary>
		/// True only while a press outside the top dialog is closing it, so the dialog's Close handling can tell
		/// that press from Escape or its own close button (agg-gui's CloseReason::ClickAway).
		/// </summary>
		public bool ClosingByOutsidePress { get; private set; }

		/// <summary>The dialog that takes input, or null when none is open.</summary>
		public GuiWidget TopLayer => this.layers.Count > 0 ? this.layers[this.layers.Count - 1] : null;

		/// <summary>Opens <paramref name="dialog"/> centred above every open dialog and gives the overlay the
		/// keyboard focus, so Escape reaches it.</summary>
		public void Push(GuiWidget dialog)
		{
			dialog.HAnchor = HAnchor.Center;
			dialog.VAnchor = VAnchor.Center;
			this.layers.Add(dialog);
			this.AddChild(dialog);
			this.Focus();
			this.LayersChanged?.Invoke(this, EventArgs.Empty);
			this.Invalidate();
		}

		/// <summary>Closes the top dialog. Returns false when none was open.</summary>
		public bool CloseTop()
		{
			GuiWidget top = this.TopLayer;
			if (top == null)
			{
				return false;
			}

			this.layers.RemoveAt(this.layers.Count - 1);
			this.RemoveChild(top);
			top.Close();
			this.LayersChanged?.Invoke(this, EventArgs.Empty);
			this.Invalidate();
			return true;
		}

		/// <summary>Closes every dialog, top first.</summary>
		public void CloseAll()
		{
			while (this.CloseTop())
			{
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			GuiWidget top = this.TopLayer;
			if (top != null
				&& !top.BoundsRelativeToParent.Contains(mouseEvent.Position))
			{
				// Outside the top dialog - over the backdrop or a dialog beneath it - the press closes the top
				// one and goes no further.
				this.ClosingByOutsidePress = true;
				try
				{
					this.CloseTop();
				}
				finally
				{
					this.ClosingByOutsidePress = false;
				}

				return;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			base.OnKeyDown(keyEvent);
			if (!keyEvent.Handled
				&& keyEvent.KeyCode == Keys.Escape
				&& this.CloseTop())
			{
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}
		}
	}
}
