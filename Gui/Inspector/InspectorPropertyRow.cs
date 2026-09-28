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

namespace MatterHackers.Agg.UI
{
	/// <summary>What a properties row shows, which picks agg-gui's tint for its value.</summary>
	public enum InspectorValueKind
	{
		/// <summary>No tint: a read-only value, or an editable one on <see cref="InspectorStyle.EditableValue"/>.</summary>
		Plain,

		/// <summary>A margin side, on agg-gui's orange strip.</summary>
		Margin,

		/// <summary>A padding side, read-only on agg-gui's green strip.</summary>
		Padding,

		/// <summary>An anchor, on agg-gui's blue strip.</summary>
		Anchor,

		/// <summary>A true/false flag: no strip, the value coloured green or red.</summary>
		Flag,
	}

	/// <summary>
	/// One name/value row of the inspector's properties pane, after agg-gui's PropHit (widgets/inspector_props.rs): a
	/// row with a <see cref="Field"/> is editable, and a click on its value makes an <see cref="InspectorEdit"/>.
	/// </summary>
	public sealed class InspectorPropertyRow
	{
		public InspectorPropertyRow(string name, string value, InspectorEditField? field = null, double current = 0, double step = 0)
		{
			this.Name = name;
			this.Value = value;
			this.Field = field;
			this.Current = current;
			this.Step = step;
		}

		public string Name { get; }

		public string Value { get; }

		/// <summary>What the row shows, which picks the value's tint.</summary>
		public InspectorValueKind Kind { get; init; }

		/// <summary>The field a click on the value changes; null for a read-only row.</summary>
		public InspectorEditField? Field { get; }

		/// <summary>The field's value as <see cref="InspectorEdit.Value"/> takes it.</summary>
		public double Current { get; }

		/// <summary>How far one click moves a numeric field; 0 for a field that toggles or cycles instead.</summary>
		public double Step { get; }

		/// <summary>agg-gui's margin step: half the value plus a half, between 0.5 and 4.</summary>
		public static double InsetStep(double value) => Math.Min(4, Math.Max(.5, Math.Abs(value) * .5 + .5));

		/// <summary>agg-gui's NumericStep: 5% of the value, at least 0.1.</summary>
		public static double NumericStep(double value) => Math.Max(.1, Math.Max(1, Math.Abs(value)) * .05);

		/// <summary>
		/// The edit a click on this row's value makes: a numeric field steps down when <paramref name="increase"/> is
		/// false (the value's left half) and up when true (its right half), as agg-gui's; visible and enabled toggle;
		/// an anchor moves to the next in the picker cycle. Null for a read-only row.
		/// </summary>
		public InspectorEdit EditFor(GuiWidget widget, bool increase)
		{
			switch (this.Field)
			{
				case null:
					return null;
				case InspectorEditField.HAnchor:
					return new InspectorEdit(widget, InspectorEditField.HAnchor, (int)InspectorEdit.NextHAnchor((HAnchor)(int)this.Current));
				case InspectorEditField.VAnchor:
					return new InspectorEdit(widget, InspectorEditField.VAnchor, (int)InspectorEdit.NextVAnchor((VAnchor)(int)this.Current));
				case InspectorEditField.Visible:
				case InspectorEditField.Enabled:
					return new InspectorEdit(widget, this.Field.Value, this.Current == 0 ? 1 : 0);
				default:
					return new InspectorEdit(widget, this.Field.Value, Math.Max(0, this.Current + (increase ? this.Step : -this.Step)));
			}
		}
	}
}
