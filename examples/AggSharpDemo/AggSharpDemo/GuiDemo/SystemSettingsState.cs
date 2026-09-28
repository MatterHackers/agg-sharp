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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Platform;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The System window's settings as saved state (agg-gui state.rs font_name, lcd, hinting, gamma, ... and
	/// system_tab). The settings are process-wide statics, so they are read and written here directly and apply
	/// at start whether or not the System window is ever opened.
	/// </summary>
	/// <remarks>
	/// To persist another setting: add a nullable property, read it in <see cref="Capture"/> and write it in
	/// <see cref="Apply"/> when it is not null. Null (a file from before the setting existed) keeps the current value.
	/// </remarks>
	public class SystemSettingsState
	{
		/// <summary>A <see cref="SystemFontTab.FontOptions"/> name; null or unknown keeps the current font.</summary>
		public string Font { get; set; }

		public bool? Lcd { get; set; }

		public bool? Hinting { get; set; }

		public double? Gamma { get; set; }

		public double? PrimaryWeight { get; set; }

		/// <summary>The System window's selected tab (state.rs system_tab); out of range keeps the first.</summary>
		public int Tab { get; set; }

		/// <summary>The current process-wide settings, with <paramref name="tab"/> as the selected tab.</summary>
		public static SystemSettingsState Capture(int tab)
		{
			int font = SystemFontTab.CurrentFontIndex();
			return new SystemSettingsState
			{
				Font = font >= 0 ? SystemFontTab.FontOptions[font].Name : null,
				Lcd = LcdRenderSettings.Enabled,
				Hinting = TypeFacePrinter.SnapBaselinesToWholePixels,
				Gamma = LcdRenderSettings.Gamma,
				PrimaryWeight = LcdRenderSettings.PrimaryWeight,
				Tab = tab,
			};
		}

		/// <summary>Writes every saved setting back to the static it came from.</summary>
		public void Apply()
		{
			foreach ((string name, System.Func<TypeFace> face) in SystemFontTab.FontOptions)
			{
				if (name == this.Font)
				{
					AggContext.DefaultFont = face();
				}
			}

			if (this.Lcd is bool lcd)
			{
				LcdRenderSettings.Enabled = lcd;
			}

			if (this.Hinting is bool hinting)
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = hinting;
			}

			if (this.Gamma is double gamma)
			{
				LcdRenderSettings.Gamma = gamma;
			}

			if (this.PrimaryWeight is double primaryWeight)
			{
				LcdRenderSettings.PrimaryWeight = primaryWeight;
			}
		}
	}
}
