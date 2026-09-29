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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's "Lion" window (lion.rs lion_demo): an Alpha label and slider, a wrapped note on the gestures,
	/// then the lion filling the rest.
	/// </summary>
	public class LionWindow : FlowLayoutWidget
	{
		/// <summary>lion.rs's note, word for word.</summary>
		public const string NoteText = "Left-drag or one-finger drag: rotate + scale (relative to start).  Wheel / pinch: zoom.  "
			+ "Two-finger twist: rotate.  Two-finger drag: pan.  Right-drag: skew.  MSAA is off; smooth silhouette = "
			+ "halo-AA edges; fresh tess2 every frame.";

		private const double Gap = 6;

		private readonly TextWidget alphaText;

		public LionWindow(DemoTheme demoTheme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.Padding = new BorderDouble(8);

			var label = new TextWidget("Alpha", pointSize: DemoText.Points(12), textColor: demoTheme.Palette.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(label);

			// agg-gui's slider shows its value beside the track; agg-sharp's draws only the track, so the value
			// is a label beside it.
			var sliderRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AlphaSlider = new Slider(new Vector2(0, 0), 100 * DeviceScale, 0, 1)
			{
				Name = "Lion Alpha",
				VAnchor = VAnchor.Center,
			};
			sliderRow.AddChild(this.AlphaSlider);
			this.alphaText = new TextWidget("1.00", pointSize: DemoText.Points(12), textColor: demoTheme.Palette.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 8),
			};
			sliderRow.AddChild(this.alphaText);
			this.AddChild(sliderRow);

			var note = new WrappedTextWidget(NoteText, pointSize: DemoText.Points(11), textColor: demoTheme.Palette.TextColor)
			{
				Name = "Lion Note",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: Gap),
			};
			this.AddChild(note);

			this.View = new LionView(demoTheme)
			{
				Name = "Lion View",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			this.AddChild(this.View);

			this.AlphaSlider.Value = this.View.Alpha;
			this.AlphaSlider.ValueChanged += (s, e) =>
			{
				this.View.Alpha = this.AlphaSlider.Value;
				this.alphaText.Text = this.AlphaSlider.Value.ToString("0.00");
				this.View.Invalidate();
			};

			void Recolor(object sender, EventArgs e)
			{
				label.TextColor = demoTheme.Palette.TextColor;
				this.alphaText.TextColor = demoTheme.Palette.TextColor;
				note.TextColor = demoTheme.Palette.TextColor;
			}

			demoTheme.ThemeChanged += Recolor;
			this.Closed += (s, e) => demoTheme.ThemeChanged -= Recolor;
		}

		/// <summary>The lion's opacity, 0 to 1; starts at 1 as lion.rs.</summary>
		public Slider AlphaSlider { get; }

		/// <summary>The lion itself.</summary>
		public LionView View { get; }

		public override void OnBoundsChanged(EventArgs e)
		{
			// Setting Padding in the constructor moves the bounds before the readout exists.
			if (this.alphaText == null)
			{
				base.OnBoundsChanged(e);
				return;
			}

			// The slider's bounds derive from its track length, so it cannot stretch; agg-gui's fills the column,
			// so the track follows the width less the value readout.
			double track = this.Width - this.DevicePadding.Width - this.alphaText.Width - (8 * DeviceScale) - (4 * DeviceScale);
			if (track > 20 && Math.Abs(this.AlphaSlider.TotalWidthInPixels - track) > 0.5)
			{
				this.AlphaSlider.TotalWidthInPixels = track;

				// The track length does not dirty the row's layout, so the readout would stay where the old
				// track ended.
				this.AlphaSlider.Parent?.PerformLayout();
			}

			base.OnBoundsChanged(e);
		}
	}
}
