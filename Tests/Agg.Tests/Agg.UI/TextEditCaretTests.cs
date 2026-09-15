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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The insert caret of a text field: that it goes on blinking for as long as the field holds the keyboard,
	/// that it is drawn at the weight of the display it is on rather than at one hardware pixel, and that the
	/// field brings the text under it into view in both directions.
	/// </summary>
	/// <remarks>
	/// The tests that ask whether the caret is painted draw an empty field, so the only thing that can paint
	/// inside it is the caret - which is what makes "is the caret showing" a pixel question rather than a
	/// question about private state.
	/// </remarks>
	public class TextEditCaretTests
	{
		/// <summary>
		/// What the user does part way through a blink cycle, each of which restarts the blink.
		/// </summary>
		public enum MidCycleEvent
		{
			Nothing,
			ReFocus,
			KeyDown,
		}

		private const int ImageWidth = 120;

		private const int ImageHeight = 80;

		// where the field is placed in the image, so the caret has white on every side of it to be measured against
		private static readonly Vector2 FieldOrigin = new Vector2(20, 20);

		/// <summary>
		/// A caret that stops blinking is what a user sees when the field has quietly stopped being drawn - so
		/// the contract is not "it is showing" but "it goes off and comes back", watched across a full cycle.
		/// </summary>
		/// <param name="midCycle">
		/// What the user does part way through a blink. Both of these restart the blink - which is the point,
		/// the caret has to be showing where the typing is going - and the blink used to be driven by one chain
		/// of idle callbacks per restart, with the earlier chains left running. A stale chain firing part way
		/// into the new cycle read the elapsed time as "too early to be my own callback" and stopped the clock
		/// the caret is drawn from, freezing the caret on for as long as the field held the keyboard.
		/// </param>
		[Test]
		[Arguments(MidCycleEvent.Nothing)]
		[Arguments(MidCycleEvent.ReFocus)]
		[Arguments(MidCycleEvent.KeyDown)]
		public async Task TheCaretBlinksForAsLongAsTheFieldHoldsTheKeyboard(MidCycleEvent midCycle)
		{
			var editWidget = FocusedEmptyField(out var container);

			if (midCycle != MidCycleEvent.Nothing)
			{
				await PumpFor(TimeSpan.FromMilliseconds(200), container);

				if (midCycle == MidCycleEvent.ReFocus)
				{
					// what a reparent does - popping the editor out into a window of its own hands the keyboard
					// back to the field it took it from
					editWidget.Unfocus();
					editWidget.Focus();
				}
				else
				{
					// any key the user presses restarts the blink; Home is one that leaves the field empty, so
					// the only thing that can paint in it is still the caret
					editWidget.OnKeyDown(new KeyEventArgs(Keys.Home));
				}
			}

			var seen = await CaretStatesOverAFullCycle(editWidget, container);

			await Assert.That(seen.Any(showing => showing)).IsTrue()
				.Because("the caret has to be drawn at some point in the cycle for the user to see where they are typing");

			await Assert.That(seen.Any(showing => !showing)).IsTrue()
				.Because("a caret that is never off is not blinking; it is a line the field has been left holding");
		}

		/// <summary>
		/// The caret is the field's own piece of geometry - nothing scales it but this widget - so on a display
		/// where everything else has doubled it has to double too. It was a hardware pixel wide at every scale,
		/// which on a Retina panel is half the weight of the thinnest stroke in the text beside it.
		/// </summary>
		/// <remarks>
		/// <see cref="GuiWidget.DeviceScale"/> is process wide, so this is a keyless <c>[NotInParallel]</c> -
		/// exclusive, not merely serialized - and puts the previous value back in a finally. See
		/// <c>SliderDeviceScaleTests</c> for the same pattern.
		/// </remarks>
		[Test]
		[NotInParallel]
		public async Task TheCaretGrowsWithDeviceScale()
		{
			var atOne = CaretBoundsAtDeviceScale(1);
			var atTwo = CaretBoundsAtDeviceScale(2);

			await Assert.That(atTwo.Width).IsEqualTo(atOne.Width * 2)
				.Because($"the caret is {atOne.Width} hardware pixels wide at scale 1, so it has to be twice that"
					+ $" at scale 2 rather than the {atTwo.Width} a fixed pixel width leaves it");

			await Assert.That(atTwo.Height).IsEqualTo(atOne.Height * 2)
				.Because("the caret spans the text, and the text doubled");
		}

		/// <summary>
		/// A field nobody has given a colour to still has to show the user where they are typing. CursorColor
		/// was left at the default of a Color - transparent - so it was only ever visible on a field built
		/// through <see cref="ThemedTextEditWidget"/>, which sets TextColor and so sets the caret's colour with
		/// it. A TextEditWidget put together by hand drew its text and no caret at all.
		/// </summary>
		[Test]
		public async Task AFieldNobodyHasColouredStillDrawsACaret()
		{
			FocusedEmptyField(out var container, setTextColor: false);

			await Assert.That(CaretBounds(container).Width).IsGreaterThan(0)
				.Because("the caret has to be painted in a field that was never themed, not left transparent");
		}

		/// <summary>
		/// The same defect as the caret's, in the other colour <see cref="InternalTextEditWidget.TextColor"/>
		/// derives: HighlightColor was left at the default of a Color - transparent - so selecting text in a
		/// field that had never been themed drew no band behind it, and the user had no way to see what they
		/// had selected.
		/// </summary>
		/// <remarks>
		/// The field here is deliberately not focused, so the caret cannot paint and cannot blink: the only
		/// thing that can differ between the two pictures is the selection.
		/// </remarks>
		[Test]
		public async Task AFieldNobodyHasColouredStillDrawsItsSelection()
		{
			var editWidget = FieldInAContainer("abc", out var container, setTextColor: false);

			var withoutSelection = Render(container);

			// SetCursorPosition clears any selection, so it has to come first
			editWidget.SetCursorPosition(3);
			editWidget.SelectionIndexToStartBefore = 0;
			editWidget.Selecting = true;

			var withSelection = Render(container);

			await Assert.That(PixelsThatDiffer(withoutSelection, withSelection)).IsGreaterThan(0)
				.Because("selecting text in a field that was never themed has to paint a band behind it,"
					+ " not leave the field looking exactly as it did");
		}

		/// <summary>
		/// A single line field holding more text than it can show has to follow the caret in both directions.
		/// Walking the caret off the right edge scrolls the view right; walking it back to the front has to
		/// bring the head of the value back, or the first characters can never be read again.
		/// </summary>
		/// <param name="withKey">
		/// The key the user walks back to the front with. Home is one move; Left is the same journey taken a
		/// character at a time.
		/// </param>
		[Test]
		[Arguments(Keys.Home)]
		[Arguments(Keys.Left)]
		public async Task TheFieldFollowsTheCaretBackToTheFrontOfItsValue(Keys withKey)
		{
			var editWidget = NarrowFieldHoldingWideText(out var text);

			editWidget.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.End));

			var scrolledRight = editWidget.TopLeftOffset.X;
			await Assert.That(scrolledRight).IsNotEqualTo(0.0)
				.Because("a caret walked off the right edge has to pull the view after it");

			if (withKey == Keys.Home)
			{
				editWidget.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.Home));
			}
			else
			{
				for (int i = 0; i < text.Length; i++)
				{
					editWidget.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.Left));
				}
			}

			await Assert.That(editWidget.InternalTextEditWidget.CharIndexToInsertBefore).IsEqualTo(0)
				.Because("the caret is at the front of the text");

			await Assert.That(editWidget.TopLeftOffset.X).IsEqualTo(0.0)
				.Because($"the view has to come back to the front of the value rather than stay at {scrolledRight}");
		}

		/// <summary>
		/// A themed field is a frame around a scrolling text field, and the frame is the thing that clips. Asking
		/// the frame to fit its content used to hand Fit to the scrolling field inside it as well, so the field
		/// grew to the full width of its text instead of scrolling it - and with the frame also anchored Right,
		/// the overflow hung off the *left* edge, where it was clipped away. A radius of 2.73109 read as "73109"
		/// and no caret move could bring the "2." back, because nothing was scrolling at all.
		/// </summary>
		[Test]
		public async Task AFrameAskedToFitDoesNotPushItsFieldOffItsOwnLeftEdge()
		{
			var field = ThemedFieldInAStretchingRow(out var container);

			var frameWidth = field.Width;

			// what a value field does once it holds a plain number: cap the frame at the width it started with
			// and let it fit, so a long value scrolls inside a field that stays put
			field.Text = "2.73109";
			field.MaximumSize = new Vector2(frameWidth, field.MaximumSize.Y);
			field.HAnchor = HAnchor.Right | HAnchor.Fit;
			container.PerformLayout();

			var editWidget = field.ActualTextEditWidget;

			await Assert.That(editWidget.BoundsRelativeToParent.Left).IsGreaterThanOrEqualTo(field.LocalBounds.Left)
				.Because("the front of the value has to be inside the frame the user can see, not off its left edge");

			await Assert.That(editWidget.BoundsRelativeToParent.Right).IsLessThanOrEqualTo(field.LocalBounds.Right)
				.Because("a field held inside its frame on the left must not have been pushed out of it on the right instead");
		}

		/// <summary>
		/// The same clipping as <see cref="AFrameAskedToFitDoesNotPushItsFieldOffItsOwnLeftEdge"/>, reached from the
		/// other direction. A frame that has been stretched has stretched the field inside it; when the frame is put
		/// back to a plain anchor the field is handed a plain anchor too, and a plain anchor moves a widget without
		/// resizing it. Nothing else measures the field - so it kept the width the stretch had given it, and hung off
		/// the frame's left edge with the head of the value clipped away.
		/// </summary>
		/// <remarks>
		/// The anchor here is Right on its own rather than the Right | Fit a value field uses, because Fit hides the
		/// defect: setting Fit on the frame lays the frame out while the field inside is still stretched, and that
		/// layout pulls the field back in with the frame. A frame that neither stretches nor fits gets no such pass.
		/// </remarks>
		[Test]
		public async Task AFieldDoesNotKeepTheWidthAStretchGaveIt()
		{
			var field = ThemedFieldInAStretchingRow(out var container);

			var frameWidth = field.Width;
			var reservedWidth = field.ActualTextEditWidget.Width;

			// the frame is let off its cap and stretched across the row, the way a field holding an expression is
			field.Text = "=a+b";
			field.MaximumSize = new Vector2(double.MaxValue, field.MaximumSize.Y);
			field.HAnchor = HAnchor.Stretch;
			container.PerformLayout();

			await Assert.That(field.ActualTextEditWidget.Width).IsGreaterThan(reservedWidth)
				.Because($"the field has to have grown past the {reservedWidth} it reserved for this to be the case being tested");

			// and then put back to holding one end, at the width it started with
			field.Text = "2.73109";
			field.HAnchor = HAnchor.Right;
			field.MaximumSize = new Vector2(frameWidth, field.MaximumSize.Y);
			container.PerformLayout();

			await Assert.That(field.ActualTextEditWidget.Width).IsEqualTo(reservedWidth)
				.Because("a width the field was only given because its frame was stretched must not outlive the stretch");

			await Assert.That(field.ActualTextEditWidget.BoundsRelativeToParent.Left).IsGreaterThanOrEqualTo(field.LocalBounds.Left)
				.Because("the front of the value has to be inside the frame the user can see, not off its left edge");
		}

		/// <summary>
		/// A label refreshed while the frame is stretched must not turn the stretched width into the reserved one.
		/// Setting a label re-lays the field out to make room for itself inside it; if it works that room out from
		/// the width the field happens to have, then a field that is stretched at that moment records a width layout
		/// gave it as a width it was given outright - and hands that back when the stretch ends, which is the very
		/// thing the reserve exists to prevent. A length field reaches this every time: the frame is stretched to
		/// hold an expression, and the unit label is refreshed on the next idle.
		/// </summary>
		[Test]
		public async Task ALabelRefreshedWhileStretchedDoesNotBecomeTheReservedWidth()
		{
			var field = ThemedFieldInAStretchingRow(out var container);

			var frameWidth = field.Width;
			var reservedWidth = field.ActualTextEditWidget.Width;

			// the frame is let off its cap and stretched across the row, the way a field holding an expression is
			field.Text = "=a+b";
			field.MaximumSize = new Vector2(double.MaxValue, field.MaximumSize.Y);
			field.HAnchor = HAnchor.Stretch;
			container.PerformLayout();

			var stretchedWidth = field.ActualTextEditWidget.Width;
			await Assert.That(stretchedWidth).IsGreaterThan(reservedWidth)
				.Because($"the field has to have grown past the {reservedWidth} it reserved for this to be the case being tested");

			// and while it is stretched, its unit label is set again - same units, just refreshed
			field.TrailingLabel = "mm";

			// then the frame goes back to holding one end, at the width it started with
			field.Text = "2.73109";
			field.HAnchor = HAnchor.Right;
			field.MaximumSize = new Vector2(frameWidth, field.MaximumSize.Y);
			container.PerformLayout();

			await Assert.That(field.ActualTextEditWidget.Width).IsEqualTo(reservedWidth)
				.Because($"the width the field goes back to is the one it was given outright, not the {stretchedWidth} a stretch"
					+ " happened to leave in it when its label was refreshed");

			await Assert.That(field.ActualTextEditWidget.BoundsRelativeToParent.Left).IsGreaterThanOrEqualTo(field.LocalBounds.Left)
				.Because("the front of the value has to be inside the frame the user can see, not off its left edge");
		}

		/// <summary>
		/// A single line field a few characters wide holding a value several times that width, laid out inside a
		/// parent so its scrolling is live, and holding the keyboard.
		/// </summary>
		private static TextEditWidget NarrowFieldHoldingWideText(out string text)
		{
			text = "2.73109";

			var editWidget = new TextEditWidget(text, pixelWidth: 20, pixelHeight: 16);

			var container = new GuiWidget(ImageWidth, ImageHeight);
			container.AddChild(editWidget);
			editWidget.OriginRelativeParent = FieldOrigin;

			editWidget.Focus();

			return editWidget;
		}

		/// <summary>
		/// A themed field in a row that stretches, wearing a unit label, the way a property editor lays out a
		/// length. The unit label matters: it reserves room on the right of the text field, which is what the
		/// overflow of a field that grew with its text is pushed past.
		/// </summary>
		private static ThemedTextEditWidget ThemedFieldInAStretchingRow(out GuiWidget container)
		{
			var row = new GuiWidget() { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };

			var field = new ThemedTextEditWidget("", new ThemeConfig(), pixelWidth: 60)
			{
				TrailingLabel = "mm"
			};

			row.AddChild(field);

			container = new GuiWidget(ImageWidth * 2, ImageHeight);
			container.AddChild(row);
			container.PerformLayout();

			return field;
		}

		/// <summary>
		/// Samples whether the caret is painted, once every tenth of a second across a full on-and-off cycle,
		/// pumping the idle queue each time - the pump is what drives the blink, and what used to kill it.
		/// </summary>
		private static async Task<List<bool>> CaretStatesOverAFullCycle(InternalTextEditWidget editWidget, GuiWidget container)
		{
			// a little over the 0.6s on plus 0.6s off the widget blinks at, so a full cycle is always covered
			var cycle = TimeSpan.FromMilliseconds(1400);
			var sampleEvery = TimeSpan.FromMilliseconds(100);

			var seen = new List<bool>();
			for (var elapsed = TimeSpan.Zero; elapsed < cycle; elapsed += sampleEvery)
			{
				UiThread.InvokePendingActions();
				seen.Add(CaretBounds(container).Width > 0);
				await Task.Delay(sampleEvery);
			}

			return seen;
		}

		private static async Task PumpFor(TimeSpan duration, GuiWidget container)
		{
			var pumpEvery = TimeSpan.FromMilliseconds(20);
			for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += pumpEvery)
			{
				UiThread.InvokePendingActions();
				await Task.Delay(pumpEvery);
			}

			UiThread.InvokePendingActions();
		}

		private static RectangleInt CaretBoundsAtDeviceScale(double deviceScale)
		{
			var savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;

				FocusedEmptyField(out var container);

				return CaretBounds(container);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		/// <summary>
		/// An empty text field holding the keyboard, inside a container the caret can be measured in.
		/// </summary>
		/// <param name="setTextColor">
		/// Whether to colour the field the way <see cref="ThemedTextEditWidget"/> does. False leaves it exactly
		/// as constructed, which is the case <see cref="AFieldNobodyHasColouredStillDrawsACaret"/> is about.
		/// </param>
		private static InternalTextEditWidget FocusedEmptyField(out GuiWidget container, bool setTextColor = true)
		{
			var editWidget = FieldInAContainer("", out container, setTextColor);

			editWidget.Focus();

			return editWidget;
		}

		/// <summary>
		/// A text field placed in a container it can be drawn and measured in, left exactly as constructed
		/// unless <paramref name="setTextColor"/> asks for the colouring a themed field gets.
		/// </summary>
		private static InternalTextEditWidget FieldInAContainer(string text, out GuiWidget container, bool setTextColor)
		{
			var editWidget = new InternalTextEditWidget(text, 12, false, 0);

			if (setTextColor)
			{
				// the TextColor setter is also what gives the caret and the selection band their colours
				editWidget.TextColor = Color.Black;
			}

			container = new GuiWidget(ImageWidth, ImageHeight);
			container.AddChild(editWidget);
			editWidget.OriginRelativeParent = FieldOrigin;

			return editWidget;
		}

		/// <summary>
		/// What the field painted, which for an empty field is the caret and nothing else. An empty rectangle
		/// means the caret is in the off half of its blink.
		/// </summary>
		private static RectangleInt CaretBounds(GuiWidget container)
		{
			var image = Render(container);

			int left = int.MaxValue, bottom = int.MaxValue, right = int.MinValue, top = int.MinValue;
			for (int x = 0; x < ImageWidth; x++)
			{
				for (int y = 0; y < ImageHeight; y++)
				{
					if (image.GetPixel(x, y) != Color.White)
					{
						left = Math.Min(left, x);
						bottom = Math.Min(bottom, y);
						right = Math.Max(right, x + 1);
						top = Math.Max(top, y + 1);
					}
				}
			}

			return right == int.MinValue ? default(RectangleInt) : new RectangleInt(left, bottom, right, top);
		}

		private static ImageBuffer Render(GuiWidget container)
		{
			var image = new ImageBuffer(ImageWidth, ImageHeight);
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);

			container.OnDraw(graphics2D);

			return image;
		}

		private static int PixelsThatDiffer(ImageBuffer first, ImageBuffer second)
		{
			int differing = 0;
			for (int x = 0; x < ImageWidth; x++)
			{
				for (int y = 0; y < ImageHeight; y++)
				{
					if (first.GetPixel(x, y) != second.GetPixel(x, y))
					{
						differing++;
					}
				}
			}

			return differing;
		}
	}
}
