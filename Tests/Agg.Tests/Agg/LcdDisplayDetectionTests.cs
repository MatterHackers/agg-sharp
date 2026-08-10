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

using System.Threading.Tasks;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdDisplayDetection"/>: the policy that picks a default for subpixel text from what
	/// the OS says about the display. Pure logic over injected facts - no P/Invoke, no display - so the
	/// cases a developer can never reproduce locally (BGR panel, remote session, rotated monitor) are all
	/// testable.
	/// </summary>
	public class LcdDisplayDetectionTests
	{
		/// <summary>
		/// The one combination that says yes: smoothing on, ClearType, RGB stripes, local session, upright
		/// display. This is the ordinary desktop, and getting it wrong would leave 95% of users on grayscale.
		/// </summary>
		[Test]
		public async Task AnOrdinaryClearTypeDesktopGetsSubpixel()
		{
			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(OrdinaryDesktop())).IsTrue();
		}

		[Test]
		public async Task FontSmoothingOffMeansGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: false,
				fontSmoothingStyle: LcdFontSmoothingStyle.ClearType,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: false,
				displayRotatedQuarterTurn: false);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse()
				.Because("a user who turned font smoothing off asked for hard edges, not coloured ones");
		}

		/// <summary>
		/// Grayscale smoothing is what a user picks to keep antialiasing but turn ClearType off, so it is an
		/// explicit "no subpixel" rather than an absence of information.
		/// </summary>
		[Test]
		public async Task GrayscaleSmoothingStyleMeansGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.Grayscale,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: false,
				displayRotatedQuarterTurn: false);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse();
		}

		[Test]
		public async Task AnUnknownSmoothingStyleMeansGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.Unknown,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: false,
				displayRotatedQuarterTurn: false);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse()
				.Because("nothing said this display is subpixel, and a guess would be visible on every glyph");
		}

		/// <summary>
		/// The LcdCoverage pipeline renders RGB stripe order only - see
		/// <see cref="LcdDisplayDetection.IsSubpixelAppropriate(LcdDisplayEnvironment)"/> - so a BGR panel
		/// would get its fringes on the wrong side of every stem. Grayscale is the honest fallback.
		/// </summary>
		[Test]
		public async Task BgrStripeOrderFallsBackToGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.ClearType,
				stripeOrder: LcdStripeOrder.Bgr,
				isRemoteSession: false,
				displayRotatedQuarterTurn: false);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse()
				.Because("the renderer has no BGR path, so rendering RGB anyway would look worse than grayscale");
		}

		[Test]
		public async Task ARemoteSessionFallsBackToGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.ClearType,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: true,
				displayRotatedQuarterTurn: false);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse()
				.Because("the pixels are re-encoded on the way to a panel whose geometry we cannot know");
		}

		[Test]
		public async Task AQuarterTurnedDisplayFallsBackToGrayscale()
		{
			var environment = new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.ClearType,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: false,
				displayRotatedQuarterTurn: true);

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(environment)).IsFalse()
				.Because("a monitor on its side has vertical stripes, and the subpixel geometry is horizontal");
		}

		/// <summary>
		/// No provider at all is every non-Windows platform, and a provider that cannot read the display is a
		/// stripped or headless Windows host. Both mean "we do not know", which must never render subpixel.
		/// </summary>
		[Test]
		public async Task AMissingOrSilentProviderMeansGrayscale()
		{
			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate((ILcdDisplayEnvironmentProvider)null)).IsFalse();

			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(new StubProvider(false, OrdinaryDesktop()))).IsFalse()
				.Because("a provider that returned false has told us nothing, whatever is in its out parameter");
		}

		[Test]
		public async Task AProviderThatCanReadTheDisplayDecidesFromWhatItRead()
		{
			await Assert.That(LcdDisplayDetection.IsSubpixelAppropriate(new StubProvider(true, OrdinaryDesktop()))).IsTrue();
		}

		private static LcdDisplayEnvironment OrdinaryDesktop()
		{
			return new LcdDisplayEnvironment(
				fontSmoothingEnabled: true,
				fontSmoothingStyle: LcdFontSmoothingStyle.ClearType,
				stripeOrder: LcdStripeOrder.Rgb,
				isRemoteSession: false,
				displayRotatedQuarterTurn: false);
		}

		private class StubProvider : ILcdDisplayEnvironmentProvider
		{
			private readonly bool canRead;
			private readonly LcdDisplayEnvironment environment;

			public StubProvider(bool canRead, LcdDisplayEnvironment environment)
			{
				this.canRead = canRead;
				this.environment = environment;
			}

			public bool TryGetEnvironment(out LcdDisplayEnvironment environment)
			{
				environment = this.environment;
				return this.canRead;
			}
		}
	}
}
