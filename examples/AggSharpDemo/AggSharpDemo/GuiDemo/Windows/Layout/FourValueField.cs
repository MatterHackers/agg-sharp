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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>
	/// egui's Margin / CornerRadius editor (frame_demo/core.rs FourValueField): a "same" check box beside one
	/// drag value while all four are equal; unticked, the single value hides and four labelled rows show one
	/// value each. Named "&lt;name&gt; Same", "&lt;name&gt; Value" and "&lt;name&gt; &lt;label&gt;".
	/// </summary>
	internal sealed class FourValueField : FlowLayoutWidget
	{
		private readonly FrameEdges edges;
		private readonly DragValue[] parts = new DragValue[4];
		private readonly GuiWidget[] partRows = new GuiWidget[4];

		public FourValueField(string name, FrameEdges edges, string[] labels, MiscDemoKit kit)
			: base(FlowDirection.TopToBottom)
		{
			this.edges = edges;
			this.Name = name;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			FlowLayoutWidget top = kit.Row(4);
			this.SameCheckBox = kit.CheckBox($"{name} Same", "same", edges.Same, 13);
			this.SameCheckBox.Margin = new BorderDouble(0, 0, 6, 0);
			top.AddChild(this.SameCheckBox);
			this.Value = Drag($"{name} Value", edges[0], kit);
			this.Value.ValueChanged += (s, e) =>
			{
				for (int i = 0; i < 4; i++)
				{
					this.edges[i] = this.Value.Value;
				}
			};
			top.AddChild(this.Value);
			this.AddChild(top);

			for (int i = 0; i < 4; i++)
			{
				int index = i;
				FlowLayoutWidget row = kit.Row(4);
				var label = kit.Label(labels[i], 13);
				label.AutoExpandBoundsToText = false;
				label.Width = 46 * DeviceScale;
				label.Margin = new BorderDouble(20, 0, 6, 0);
				row.AddChild(label);
				this.parts[i] = Drag($"{name} {labels[i]}", edges[i], kit);
				this.parts[i].ValueChanged += (s, e) => this.edges[index] = this.parts[index].Value;
				row.AddChild(this.parts[i]);
				this.partRows[i] = row;
				this.AddChild(row);
			}

			this.SameCheckBox.CheckedStateChanged += (s, e) =>
			{
				this.edges.SetSame(this.SameCheckBox.Checked);
				this.Sync();
			};
			this.Sync();
		}

		public CheckBox SameCheckBox { get; }

		/// <summary>The one value shown while "same" is on.</summary>
		public DragValue Value { get; }

		/// <summary>The value for <paramref name="index"/> (in the order of the labels) while "same" is off.</summary>
		public DragValue Part(int index) => this.parts[index];

		private static DragValue Drag(string name, double value, MiscDemoKit kit)
		{
			var drag = new DragValue(value, 0, 100, kit.Theme) { Name = name, Decimals = 0 };
			drag.Width = System.Math.Max(drag.Width, 70 * DeviceScale);
			return drag;
		}

		/// <summary>Shows the one value or the four to match "same", each holding the current value.</summary>
		private void Sync()
		{
			bool same = this.edges.Same;
			this.Value.Value = this.edges[0];
			this.Value.Visible = same;
			for (int i = 0; i < 4; i++)
			{
				this.parts[i].Value = this.edges[i];
				this.partRows[i].Visible = !same;
			}
		}
	}
}
