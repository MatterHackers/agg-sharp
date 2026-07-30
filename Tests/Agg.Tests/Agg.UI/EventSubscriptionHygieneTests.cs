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
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// Verifies the widget lifecycle hygiene fixes: widgets that subscribe to events on
	// externally owned objects (ImageBuffer, ImageSequence, static TextEditWidget events)
	// must unsubscribe in OnClosed, and animation intervals must not be registered with
	// UiThread until the widget has actually loaded.
	public class EventSubscriptionHygieneTests
	{
		private static int SubscriberCount(Type declaringType, object instance, string eventFieldName)
		{
			var field = declaringType.GetField(
				eventFieldName,
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

			var invocationTarget = (Delegate)field.GetValue(instance);
			return invocationTarget?.GetInvocationList().Length ?? 0;
		}

		private static string[] SubscriberMethodNames(Type declaringType, object instance, string eventFieldName)
		{
			var field = declaringType.GetField(
				eventFieldName,
				BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

			var invocationTarget = (Delegate)field.GetValue(instance);
			return invocationTarget?.GetInvocationList().Select(d => d.Method.Name).ToArray() ?? new string[0];
		}

		private static int UiThreadIntervalCount()
		{
			var field = typeof(UiThread).GetField("intervalActions", BindingFlags.NonPublic | BindingFlags.Static);
			return ((ICollection)field.GetValue(null)).Count;
		}

		[Test]
		public async Task ImageWidgetUnsubscribesFromImageBufferOnClose()
		{
			var imageBuffer = new ImageBuffer(10, 10);
			var widget = new ImageWidget(imageBuffer, true);

			// listening: the widget is subscribed and reacts to image changes
			await Assert.That(SubscriberCount(typeof(ImageBuffer), imageBuffer, "ImageChanged")).IsEqualTo(1);

			imageBuffer.CopyFrom(new ImageBuffer(20, 20));
			await Assert.That(widget.Width).IsEqualTo(20.0);

			widget.Close();

			// closed: no subscription remains and further changes have no effect
			await Assert.That(SubscriberCount(typeof(ImageBuffer), imageBuffer, "ImageChanged")).IsEqualTo(0);

			imageBuffer.CopyFrom(new ImageBuffer(30, 30));
			await Assert.That(widget.Width).IsEqualTo(20.0);
		}

		[Test]
		public async Task ResponsiveImageWidgetUnsubscribesFromImageBufferOnClose()
		{
			var imageBuffer = new ImageBuffer(10, 10);
			var widget = new ResponsiveImageWidget(imageBuffer);

			await Assert.That(SubscriberCount(typeof(ImageBuffer), imageBuffer, "ImageChanged")).IsEqualTo(1);

			widget.Close();

			await Assert.That(SubscriberCount(typeof(ImageBuffer), imageBuffer, "ImageChanged")).IsEqualTo(0);
		}

		[Test]
		[NotInParallel]
		public async Task ImageSequenceWidgetDefersAnimationStartUntilLoad()
		{
			int baselineIntervals = UiThreadIntervalCount();

			// the constructor sets RunAnimation = true, but no UiThread interval may be
			// registered until the widget loads
			var widget = new ImageSequenceWidget(10, 10);
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals);

			// the property still reports the requested state
			await Assert.That(widget.RunAnimation).IsTrue();

			widget.OnLoad(null);
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals + 1);

			widget.Close();
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals);
		}

		[Test]
		[NotInParallel]
		public async Task ImageSequenceWidgetCloseBeforeLoadDoesNotLeakInterval()
		{
			int baselineIntervals = UiThreadIntervalCount();

			var widget = new ImageSequenceWidget(10, 10)
			{
				ImageSequence = new ImageSequence(new ImageBuffer(10, 10))
			};

			// never loaded - closing must not throw and must not leave anything registered
			widget.Close();

			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals);
			await Assert.That(widget.RunAnimation).IsFalse();
		}

		[Test]
		[NotInParallel]
		public async Task ResponsiveImageSequenceWidgetDefersAnimationStartUntilLoad()
		{
			int baselineIntervals = UiThreadIntervalCount();

			var widget = new ResponsiveImageSequenceWidget(new ImageSequence(new ImageBuffer(8, 8)));
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals);
			await Assert.That(widget.AnimationRunning).IsTrue();

			widget.OnLoad(null);
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals + 1);

			widget.Close();
			await Assert.That(UiThreadIntervalCount()).IsEqualTo(baselineIntervals);
		}

		[Test]
		public async Task ResponsiveImageSequenceWidgetSubscribesEachHandlerExactlyOnce()
		{
			var sequence = new ImageSequence(new ImageBuffer(8, 8));
			var widget = new ResponsiveImageSequenceWidget(sequence);

			var methodNames = SubscriberMethodNames(typeof(ImageSequence), sequence, "Invalidated");

			// exactly one subscription per handler (previously ImageChanged could end up
			// subscribed independently of the ImageSequence property setter)
			await Assert.That(methodNames.Length).IsEqualTo(2);
			await Assert.That(methodNames.Count(name => name == "ResetImageIndex")).IsEqualTo(1);
			await Assert.That(methodNames.Count(name => name == "ImageChanged")).IsEqualTo(1);

			widget.Close();

			// closed: both handlers removed from the externally owned sequence
			await Assert.That(SubscriberCount(typeof(ImageSequence), sequence, "Invalidated")).IsEqualTo(0);
		}

		[Test]
		public async Task ImageSequenceWidgetUnsubscribesFromSequenceOnClose()
		{
			var sequence = new ImageSequence(new ImageBuffer(10, 10));
			var widget = new ImageSequenceWidget(sequence);

			await Assert.That(SubscriberCount(typeof(ImageSequence), sequence, "Invalidated")).IsEqualTo(1);

			widget.Close();

			await Assert.That(SubscriberCount(typeof(ImageSequence), sequence, "Invalidated")).IsEqualTo(0);
		}

		[Test]
		[NotInParallel]
		public async Task SoftKeyboardContentOffsetUnsubscribesStaticEventsOnClose()
		{
			int baselineShow = SubscriberCount(typeof(TextEditWidget), null, "ShowSoftwareKeyboard");
			int baselineCollapsed = SubscriberCount(typeof(TextEditWidget), null, "KeyboardCollapsed");

			var widget = new SoftKeyboardContentOffset(new GuiWidget());

			await Assert.That(SubscriberCount(typeof(TextEditWidget), null, "ShowSoftwareKeyboard")).IsEqualTo(baselineShow + 1);
			await Assert.That(SubscriberCount(typeof(TextEditWidget), null, "KeyboardCollapsed")).IsEqualTo(baselineCollapsed + 1);

			widget.Close();

			await Assert.That(SubscriberCount(typeof(TextEditWidget), null, "ShowSoftwareKeyboard")).IsEqualTo(baselineShow);
			await Assert.That(SubscriberCount(typeof(TextEditWidget), null, "KeyboardCollapsed")).IsEqualTo(baselineCollapsed);
		}
	}
}
