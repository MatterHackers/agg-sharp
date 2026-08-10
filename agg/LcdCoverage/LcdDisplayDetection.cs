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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// What kind of font antialiasing the desktop is configured for. The values are the Windows
	/// <c>FE_FONTSMOOTHING*</c> constants, because Windows is the only platform that reports this today.
	/// </summary>
	public enum LcdFontSmoothingStyle
	{
		/// <summary>Nothing the display told us about - treat as "not subpixel".</summary>
		Unknown = 0,

		/// <summary>FE_FONTSMOOTHINGSTANDARD: grayscale antialiasing.</summary>
		Grayscale = 1,

		/// <summary>FE_FONTSMOOTHINGCLEARTYPE: subpixel antialiasing.</summary>
		ClearType = 2,
	}

	/// <summary>
	/// Which way the panel's colour stripes run within a pixel. Values are the Windows
	/// <c>FE_FONTSMOOTHINGORIENTATION*</c> constants.
	/// </summary>
	public enum LcdStripeOrder
	{
		/// <summary>FE_FONTSMOOTHINGORIENTATIONBGR.</summary>
		Bgr = 0,

		/// <summary>FE_FONTSMOOTHINGORIENTATIONRGB.</summary>
		Rgb = 1,
	}

	/// <summary>
	/// The facts about the display that decide whether subpixel text is a good idea, gathered from the OS.
	/// Platform neutral on purpose: a provider fills it in, and <see cref="LcdDisplayDetection"/> decides
	/// from it without knowing where the values came from.
	/// </summary>
	public readonly struct LcdDisplayEnvironment
	{
		public LcdDisplayEnvironment(
			bool fontSmoothingEnabled,
			LcdFontSmoothingStyle fontSmoothingStyle,
			LcdStripeOrder stripeOrder,
			bool isRemoteSession,
			bool displayRotatedQuarterTurn)
		{
			this.FontSmoothingEnabled = fontSmoothingEnabled;
			this.FontSmoothingStyle = fontSmoothingStyle;
			this.StripeOrder = stripeOrder;
			this.IsRemoteSession = isRemoteSession;
			this.DisplayRotatedQuarterTurn = displayRotatedQuarterTurn;
		}

		/// <summary>Whether the desktop smooths font edges at all (SPI_GETFONTSMOOTHING).</summary>
		public bool FontSmoothingEnabled { get; }

		/// <summary>Grayscale or subpixel smoothing (SPI_GETFONTSMOOTHINGTYPE).</summary>
		public LcdFontSmoothingStyle FontSmoothingStyle { get; }

		/// <summary>Stripe order of the panel (SPI_GETFONTSMOOTHINGORIENTATION).</summary>
		public LcdStripeOrder StripeOrder { get; }

		/// <summary>Whether the app is being viewed over a remote desktop connection (SM_REMOTESESSION).</summary>
		public bool IsRemoteSession { get; }

		/// <summary>
		/// Whether the display is turned 90 or 270 degrees (DMDO_90 / DMDO_270), which puts the stripes on the
		/// vertical axis.
		/// </summary>
		public bool DisplayRotatedQuarterTurn { get; }
	}

	/// <summary>
	/// Reads the current display's <see cref="LcdDisplayEnvironment"/>. Implemented per platform; only
	/// Windows can answer today.
	/// </summary>
	public interface ILcdDisplayEnvironmentProvider
	{
		/// <summary>
		/// Reads the display environment, or returns false when this platform cannot say - a provider that
		/// does not know must say so rather than guess, because a wrong guess turns subpixel geometry on
		/// under a display it does not suit.
		/// </summary>
		bool TryGetEnvironment(out LcdDisplayEnvironment environment);
	}

	/// <summary>
	/// Decides whether LCD subpixel text suits the display the app is running on. Pure policy: it takes the
	/// facts an <see cref="ILcdDisplayEnvironmentProvider"/> gathered and answers yes or no, so it can be
	/// tested without a display.
	/// </summary>
	/// <remarks>
	/// This only picks a <b>default</b>. An explicit user choice always wins over it - see
	/// MatterCAD's LcdSubpixelSetting, which owns the persisted toggle.
	/// </remarks>
	public static class LcdDisplayDetection
	{
		/// <summary>
		/// Whether subpixel rendering suits <paramref name="environment"/>. Every condition has to hold; any
		/// one of them failing means grayscale is the better default.
		/// </summary>
		/// <remarks>
		/// The conditions, and why each disqualifies subpixel:
		/// <list type="bullet">
		/// <item><description>Font smoothing off - the user asked for hard edged text; adding colour fringes
		/// to it is the opposite of what they asked for.</description></item>
		/// <item><description>Smoothing style not ClearType - Windows itself decided this display should get
		/// grayscale, and it knows things we do not (it is what a user picks to turn ClearType off while
		/// keeping antialiasing).</description></item>
		/// <item><description>BGR stripe order - <b>the LcdCoverage pipeline only renders RGB order</b>. Its
		/// mask, filter and composite all assume the coverage triple maps to red, green, blue left to right
		/// (see LcdBufferGraphics2D), and nothing anywhere takes a stripe order. On a BGR panel that
		/// rendering puts the fringes on the wrong side of each stem, which looks worse than grayscale, so
		/// BGR falls back rather than rendering wrong.</description></item>
		/// <item><description>Remote session - the pixels are re-encoded and shipped over a wire, where the
		/// colour fringes both compress badly and land on a panel whose geometry we cannot know.</description></item>
		/// <item><description>Quarter turn rotation - a display on its side has its stripes running
		/// vertically, and horizontal subpixel geometry addresses the wrong axis entirely.</description></item>
		/// </list>
		/// </remarks>
		public static bool IsSubpixelAppropriate(LcdDisplayEnvironment environment)
		{
			return environment.FontSmoothingEnabled
				&& environment.FontSmoothingStyle == LcdFontSmoothingStyle.ClearType
				&& environment.StripeOrder == LcdStripeOrder.Rgb
				&& !environment.IsRemoteSession
				&& !environment.DisplayRotatedQuarterTurn;
		}

		/// <summary>
		/// Whether subpixel rendering suits the display <paramref name="provider"/> describes. False when
		/// there is no provider or it cannot read the display - "we do not know" defaults to grayscale, which
		/// is what every non-Windows platform gets.
		/// </summary>
		public static bool IsSubpixelAppropriate(ILcdDisplayEnvironmentProvider provider)
		{
			if (provider == null
				|| !provider.TryGetEnvironment(out LcdDisplayEnvironment environment))
			{
				return false;
			}

			return IsSubpixelAppropriate(environment);
		}
	}
}
