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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// Verifies that the GuiWidget base constructors no longer invoke any virtual members
	// (audit finding R5): a derived widget's overrides must not run before the derived
	// constructor body has executed, and the post-construction state must be identical
	// to the historical virtual-dispatch behavior.
	public class GuiWidgetConstructionTests
	{
		// Overrides every virtual property the GuiWidget base constructors historically
		// touched and records any invocation that happens before its own constructor
		// body has completed (i.e. during base construction).
		private class RecordingWidget : GuiWidget
		{
			// Derived field initializers run before the base constructor, so this list
			// exists while the base constructor executes.
			private readonly List<string> preConstructionCalls = new List<string>();

			private bool constructed;

			public RecordingWidget(double width, double height)
				: base(width, height)
			{
				constructed = true;
			}

			public RecordingWidget()
			{
				constructed = true;
			}

			public IReadOnlyList<string> PreConstructionCalls => preConstructionCalls;

			private void Record(string member)
			{
				if (!constructed)
				{
					preConstructionCalls.Add(member);
				}
			}

			public override RectangleDouble LocalBounds
			{
				get
				{
					Record("LocalBounds.get");
					return base.LocalBounds;
				}

				set
				{
					Record("LocalBounds.set");
					base.LocalBounds = value;
				}
			}

			public override Vector2 MinimumSize
			{
				get
				{
					Record("MinimumSize.get");
					return base.MinimumSize;
				}

				set
				{
					Record("MinimumSize.set");
					base.MinimumSize = value;
				}
			}

			public override HAnchor HAnchor
			{
				get
				{
					Record("HAnchor.get");
					return base.HAnchor;
				}

				set
				{
					Record("HAnchor.set");
					base.HAnchor = value;
				}
			}

			public override Color BackgroundColor
			{
				get
				{
					Record("BackgroundColor.get");
					return base.BackgroundColor;
				}

				set
				{
					Record("BackgroundColor.set");
					base.BackgroundColor = value;
				}
			}
		}

		// SystemWindow's constructor historically set the virtual BackgroundColor property;
		// it must now use the non-virtual initialization path.
		private class RecordingSystemWindow : SystemWindow
		{
			private readonly List<string> preConstructionCalls = new List<string>();

			private bool constructed;

			public RecordingSystemWindow()
				: base(640, 480)
			{
				constructed = true;
			}

			public IReadOnlyList<string> PreConstructionCalls => preConstructionCalls;

			public override Color BackgroundColor
			{
				get => base.BackgroundColor;

				set
				{
					if (!constructed)
					{
						preConstructionCalls.Add("BackgroundColor.set");
					}

					base.BackgroundColor = value;
				}
			}
		}

		[Test]
		public async Task SystemWindowConstructorDoesNotDispatchBackgroundColor()
		{
			var window = new RecordingSystemWindow();
			await Assert.That(window.PreConstructionCalls).IsEmpty();

			// the constructor must still establish the default background color
			await Assert.That(window.BackgroundColor.Html == "#444444FF").IsTrue();
		}

		[Test]
		public async Task BaseConstructorInvokesNoVirtualMembers()
		{
			var sized = new RecordingWidget(70, 40);
			await Assert.That(sized.PreConstructionCalls).IsEmpty();

			// state must match the historical (virtual dispatch) construction results
			await Assert.That(sized.LocalBounds == new RectangleDouble(0, 0, 70, 40)).IsTrue();
			await Assert.That(sized.MinimumSize == new Vector2(70, 40)).IsTrue();
			await Assert.That(sized.MaximumSize == new Vector2(double.MaxValue, double.MaxValue)).IsTrue();
			await Assert.That(sized.HAnchor == HAnchor.Absolute).IsTrue();
			await Assert.That(sized.VAnchor == VAnchor.Absolute).IsTrue();

			var defaulted = new RecordingWidget();
			await Assert.That(defaulted.PreConstructionCalls).IsEmpty();
			await Assert.That(defaulted.LocalBounds == new RectangleDouble(0, 0, 0, 0)).IsTrue();
			await Assert.That(defaulted.MinimumSize == new Vector2(0, 0)).IsTrue();
			await Assert.That(defaulted.HAnchor == HAnchor.Absolute).IsTrue();
			await Assert.That(defaulted.VAnchor == VAnchor.Absolute).IsTrue();
		}

		[Test]
		public async Task ConstructedStateMatchesHistoricalBehavior()
		{
			// golden values captured from the pre-change (virtual dispatch) constructors
			var sized = new GuiWidget(70, 40);
			await Assert.That(sized.LocalBounds == new RectangleDouble(0, 0, 70, 40)).IsTrue();
			await Assert.That(sized.MinimumSize == new Vector2(70, 40)).IsTrue();
			await Assert.That(sized.MaximumSize == new Vector2(double.MaxValue, double.MaxValue)).IsTrue();
			await Assert.That(sized.Width == 70).IsTrue();
			await Assert.That(sized.Height == 40).IsTrue();

			var noLimits = new GuiWidget(30, 30, SizeLimitsToSet.None);
			await Assert.That(noLimits.LocalBounds == new RectangleDouble(0, 0, 30, 30)).IsTrue();
			await Assert.That(noLimits.MinimumSize == new Vector2(0, 0)).IsTrue();
			await Assert.That(noLimits.MaximumSize == new Vector2(double.MaxValue, double.MaxValue)).IsTrue();

			var minAndMax = new GuiWidget(30, 30, SizeLimitsToSet.Minimum | SizeLimitsToSet.Maximum);
			await Assert.That(minAndMax.LocalBounds == new RectangleDouble(0, 0, 30, 30)).IsTrue();
			await Assert.That(minAndMax.MinimumSize == new Vector2(30, 30)).IsTrue();
			await Assert.That(minAndMax.MaximumSize == new Vector2(30, 30)).IsTrue();

			// fractional sizes are preserved (EnforceIntegerBounds defaults off)
			var fractional = new GuiWidget(10.5, 20.25);
			await Assert.That(fractional.LocalBounds == new RectangleDouble(0, 0, 10.5, 20.25)).IsTrue();
			await Assert.That(fractional.MinimumSize == new Vector2(10.5, 20.25)).IsTrue();
		}

		[Test]
		public async Task RealWidgetsMatchHistoricalConstructionState()
		{
			// golden values captured from the pre-change (virtual dispatch) constructors
			var button = new Button("button1");
			await Assert.That(button.LocalBounds == new RectangleDouble(0, 0, 63.3828125, 26)).IsTrue();
			await Assert.That(button.MinimumSize == new Vector2(63.3828125, 26)).IsTrue();

			var image = new ImageBuffer(8, 6);
			var responsiveImage = new ResponsiveImageWidget(image);
			await Assert.That(responsiveImage.LocalBounds == new RectangleDouble(0, 0, 0, 0)).IsTrue();
			await Assert.That(responsiveImage.MinimumSize == new Vector2(0, 0)).IsTrue();
			await Assert.That(responsiveImage.HAnchor == HAnchor.Stretch).IsTrue();

			var systemWindow = new SystemWindow(640, 480);
			await Assert.That(systemWindow.LocalBounds == new RectangleDouble(0, 0, 640, 480)).IsTrue();
			await Assert.That(systemWindow.MinimumSize == new Vector2(0, 0)).IsTrue();
			await Assert.That(systemWindow.BackgroundColor.Html == "#444444FF").IsTrue();
		}

		[Test]
		public async Task AnchoringStillWorksAfterConstruction()
		{
			// mirrors the numeric expectations of AnchorTests fit/stretch cases
			{
				var parent = new GuiWidget(10, 10);
				parent.HAnchor = HAnchor.Fit;

				var child = new GuiWidget(30, 30);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 10, 10)).IsTrue();
				parent.AddChild(child);
				await Assert.That(parent.LocalBounds == new RectangleDouble(0, 0, 30, 10)).IsTrue();
			}

			{
				var container = new GuiWidget(100, 100);
				var child = new GuiWidget(10, 10, SizeLimitsToSet.None)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
				};
				container.AddChild(child);
				await Assert.That(child.Width == 100).IsTrue();
				await Assert.That(child.Height == 100).IsTrue();

				container.LocalBounds = new RectangleDouble(0, 0, 200, 150);
				await Assert.That(child.Width == 200).IsTrue();
				await Assert.That(child.Height == 150).IsTrue();
			}
		}
	}
}
