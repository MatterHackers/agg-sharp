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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The size of the box a <see cref="CheckBox"/> draws and of a <see cref="RadioButton"/>'s circle.
	/// </summary>
	/// <remarks>
	/// Opt-in: null, the default, keeps the classic 12 unit box and 10 unit circle that existing layouts were
	/// built around. An app sets it once at startup, before it builds any controls - the size is read when a
	/// check box or radio is constructed, as the room beside its label is laid out then.
	/// </remarks>
	public static class SelectionControlSize
	{
		/// <summary>The box and circle size in design units (agg-gui's is 16), or null for the classic size.</summary>
		public static double? BoxSize { get; set; }

		/// <summary>Room between the box and its label, in design units, when <see cref="BoxSize"/> is set.</summary>
		internal const double LabelGap = 6;

		/// <summary>The focus pad left of the box, in design units.</summary>
		internal const double LeadingPad = 2;
	}
}
