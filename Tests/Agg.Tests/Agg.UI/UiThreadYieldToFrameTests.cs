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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The desktop half of <see cref="UiThread.YieldToFrame"/>'s contract. Its browser half - that two idle
	/// turns really do let a frame paint - cannot be proven here: it needs a running frame loop, and the
	/// measurement behind it (nine animation frames during a 500 ms job broken into ten hops) was taken in
	/// the browser. What matters off the browser is that it costs nothing and, crucially, cannot park a
	/// caller on a queue no one is draining.
	/// </summary>
	public class UiThreadYieldToFrameTests
	{
		[Test]
		public async Task YieldToFrameIsAFreeNoOpOffTheBrowser()
		{
			if (OperatingSystem.IsBrowser())
			{
				return;
			}

			var yielded = UiThread.YieldToFrame();

			// No pump is running in this test, so anything that actually queued idle turns would hang here
			// rather than come back completed.
			await Assert.That(yielded.IsCompletedSuccessfully).IsTrue()
				.Because("a host with a separate UI thread has no frame to hand back, so this must return without touching the idle queue");

			await yielded;
		}
	}
}
