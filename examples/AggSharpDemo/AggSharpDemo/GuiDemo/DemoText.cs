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
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The GUI demo's text metrics and default face, so it can use agg-gui's sizes as written. agg-gui sizes text
	/// as an em in pixels (a 13 is a 13px em); agg-sharp's text widgets take points, drawn at 96/72 pixels per
	/// point (<see cref="StyledTypeFace"/>). <see cref="Points"/> is the one conversion the demo uses.
	/// </summary>
	public static class DemoText
	{
		/// <summary>agg-gui's Label, CheckBox, RadioGroup, Button and TextField size: a 14px em.</summary>
		public const double BodyPixels = 14;

		/// <summary>agg-gui's Label line height as a multiple of its em (label.rs: line_h = size * 1.5), for a
		/// single line and for each line of a wrapped paragraph.</summary>
		public const double LineHeightFactor = 1.5;

		private const string RegularResource = "MatterHackers.AggSharpDemo.Fonts.Nunito-Regular.ttf";
		private const string BoldResource = "MatterHackers.AggSharpDemo.Fonts.Nunito-Bold.ttf";

		private static readonly Lazy<TypeFace> RegularInstance = new Lazy<TypeFace>(() => Load(RegularResource), LazyThreadSafetyMode.ExecutionAndPublication);
		private static readonly Lazy<TypeFace> BoldInstance = new Lazy<TypeFace>(() => Load(BoldResource), LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>Gets Nunito Regular, agg-gui's default face (system_fonts.rs), embedded in this assembly.</summary>
		public static TypeFace Nunito => RegularInstance.Value;

		/// <summary>Gets Nunito Bold, the bold face beside <see cref="Nunito"/>.</summary>
		public static TypeFace NunitoBold => BoldInstance.Value;

		/// <summary>The point size that draws agg-gui's <paramref name="aggGuiPixels"/> em (before DeviceScale).</summary>
		public static double Points(double aggGuiPixels) => aggGuiPixels * StyledTypeFace.PointsPerInch / StyledTypeFace.PixelsPerInch;

		/// <summary>
		/// Makes Nunito the process's default face, as agg-gui's demo does. It is process-wide, so a head calls it
		/// once before it builds the app, and tests (which share the process) leave the library's default alone.
		/// </summary>
		public static void UseNunitoAsDefault()
		{
			// Nunito has no symbols such as the sidebar's ▼, which Liberation Sans draws, so it falls back to
			// Liberation Sans and then to Noto Emoji.
			EmojiFont.ChainOnto(LiberationSansFont.Instance);
			EmojiFont.ChainOnto(LiberationSansBoldFont.Instance);
			if (Nunito.Fallback == null)
			{
				Nunito.Fallback = LiberationSansFont.Instance;
			}

			if (NunitoBold.Fallback == null)
			{
				NunitoBold.Fallback = LiberationSansBoldFont.Instance;
			}

			AggContext.DefaultFont = Nunito;
			AggContext.DefaultFontBold = NunitoBold;
		}

		private static TypeFace Load(string resourceName)
		{
			using var stream = typeof(DemoText).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The font resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from the Fonts folder.");
			var typeFace = new TypeFace();
			typeFace.LoadTTF(stream);
			return typeFace;
		}
	}
}
