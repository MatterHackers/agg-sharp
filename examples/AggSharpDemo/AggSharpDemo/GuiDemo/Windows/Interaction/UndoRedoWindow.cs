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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction
{
	/// <summary>The whole Undo Redo demo state, as egui's undo_redo::State { toggle_value, text }.</summary>
	public readonly record struct UndoRedoState(bool Toggle, string Text);

	/// <summary>
	/// agg-gui's "Undo Redo" window (text_demos/dialogs/basic.rs undo_redo): a checkbox and a text field whose
	/// combined state one <see cref="Undoer{TState}"/> snapshots, so Undo and Redo revert both together; rapid
	/// edits coalesce into one undo point once the state holds still.
	/// </summary>
	public class UndoRedoWindow : FlowLayoutWidget
	{
		public const string InitialText = "Text with undo/redo";

		private const double Gap = 12;

		/// <summary>How soon to look again while a change is settling (agg-gui's request_draw_after(120 ms)).</summary>
		private const double FluxPollSeconds = 0.12;

		private readonly Stopwatch clock = Stopwatch.StartNew();
		private bool applying;
		private bool pollPending;
		private bool closed;

		public UndoRedoWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(16);
			this.BackgroundColor = demoTheme.Palette.PanelFill;

			var kit = new MiscDemoKit(demoTheme);
			GuiWidget Spaced(GuiWidget widget)
			{
				widget.Margin = new BorderDouble(bottom: Gap);
				return widget;
			}

			this.AddChild(Spaced(kit.Label("Undo Redo", 13)));

			this.CheckBox = kit.CheckBox("Undo Redo Checkbox", "Checkbox with undo/redo", false, 13);
			this.AddChild(Spaced(this.CheckBox));

			this.TextField = new ThemedTextEditWidget(InitialText, kit.Theme, pixelWidth: 180 * GuiWidget.DeviceScale)
			{
				Name = "Undo Redo Text",
				HAnchor = HAnchor.Stretch,
			};
			this.AddChild(Spaced(this.TextField));

			FlowLayoutWidget buttons = kit.Row(8);
			buttons.Margin = new BorderDouble(bottom: Gap);
			// agg-gui prefixes ⟲ / ⟳, which no text font the demo loads carries; Font Awesome's undo / redo arrows stand in.
			ImageBuffer Glyph(string glyph) => GlyphIcon.Render(glyph, IconFont.TypeFace, kit.Theme.TextColor, (int)Math.Round(kit.FontSize(13) * 96 / 72 * DeviceScale));
			ThemedTextIconButton GlyphTextButton(string name, string text, string glyph) =>
				new ThemedTextIconButton(text, Glyph(glyph), kit.Theme) { Name = name, DrawIconOverlayOnDisabled = true };
			this.UndoButton = GlyphTextButton("Undo Redo Undo", "Undo", IconFont.Undo);
			this.UndoButton.Margin = new BorderDouble(right: 8);
			this.UndoButton.Click += (s, e) => UiThread.RunOnIdle(this.Undo);
			buttons.AddChild(this.UndoButton);
			this.RedoButton = GlyphTextButton("Undo Redo Redo", "Redo", IconFont.Redo);
			this.RedoButton.Click += (s, e) => UiThread.RunOnIdle(this.Redo);
			buttons.AddChild(this.RedoButton);
			this.AddChild(buttons);

			var separator = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = Math.Max(1, Math.Round(DeviceScale)),
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(separator);

			WrappedTextWidget note = kit.Wrapped(
				"One shared Undoer snapshots the whole state {toggle, text} with time-based coalescing (rapid edits collapse into a single undo point). Undo and Redo revert both controls together, matching egui's Undoer<State>.",
				11);
			note.HAnchor = HAnchor.Stretch;
			this.AddChild(note);

			this.CheckBox.CheckedStateChanged += (s, e) => this.Feed();
			this.TextField.TextChanged += (s, e) => this.Feed();

			void Recolor(object sender, EventArgs e)
			{
				DemoPalette palette = demoTheme.Palette;
				this.BackgroundColor = palette.PanelFill;
				kit.Recolor();
				separator.BackgroundColor = palette.Separator;
				this.TextField.ActualTextEditWidget.TextColor = kit.Theme.TextColor;
				this.TextField.BackgroundColor = palette.WidgetBackground;
				foreach ((ThemedTextIconButton button, string glyph) in new[] { (this.UndoButton, IconFont.Undo), (this.RedoButton, IconFont.Redo) })
				{
					button.BackgroundColor = kit.Theme.ButtonBackgroundColor;
					button.HoverColor = kit.Theme.SlightShade;
					button.MouseDownColor = kit.Theme.MinimalShade;
					button.SetIcon(Glyph(glyph));

					// ThemedTextIconButton keeps its label's colour from construction and does not expose it.
					foreach (TextWidget label in button.Descendants<TextWidget>())
					{
						label.TextColor = kit.Theme.TextColor;
					}
				}
			}

			Recolor(null, null);
			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) =>
			{
				this.closed = true;
				demoTheme.ThemeChanged -= Recolor;
			};

			// The first feed is the baseline Undo returns to.
			this.Feed();
		}

		public CheckBox CheckBox { get; }

		public ThemedTextEditWidget TextField { get; }

		public ThemedTextIconButton UndoButton { get; }

		public ThemedTextIconButton RedoButton { get; }

		/// <summary>The shared history of <see cref="State"/>.</summary>
		public Undoer<UndoRedoState> Undoer { get; } = new Undoer<UndoRedoState>();

		/// <summary>What the two controls show now.</summary>
		public UndoRedoState State => new UndoRedoState(this.CheckBox.Checked, this.TextField.Text);

		/// <summary>Reverts both controls to the previous undo point, if there is one.</summary>
		public void Undo()
		{
			if (this.Undoer.Undo(this.State, out UndoRedoState previous))
			{
				this.Apply(previous);
			}
		}

		/// <summary>Re-applies the state last undone, if nothing changed since.</summary>
		public void Redo()
		{
			if (this.Undoer.Redo(this.State, out UndoRedoState next))
			{
				this.Apply(next);
			}
		}

		/// <summary>
		/// Hands the undoer the current state, as agg-gui does every layout pass, and keeps looking while it
		/// settles so an undo point is kept even after the user stops typing.
		/// </summary>
		private void Feed()
		{
			// Mid-apply the controls hold half the new state; feeding that would wipe the redo history.
			if (this.applying || this.closed)
			{
				return;
			}

			this.Undoer.FeedState(this.clock.Elapsed.TotalSeconds, this.State);
			this.UpdateButtons();
			if (this.Undoer.IsInFlux && !this.pollPending)
			{
				this.pollPending = true;
				UiThread.RunOnIdle(
					() =>
					{
						this.pollPending = false;
						this.Feed();
					},
					FluxPollSeconds);
			}
		}

		private void Apply(UndoRedoState state)
		{
			this.applying = true;
			this.CheckBox.Checked = state.Toggle;
			this.TextField.Text = state.Text;
			this.applying = false;
			this.Feed();
		}

		private void UpdateButtons()
		{
			UndoRedoState state = this.State;
			this.UndoButton.Enabled = this.Undoer.HasUndo(state);
			this.RedoButton.Enabled = this.Undoer.HasRedo(state);
		}
	}
}
