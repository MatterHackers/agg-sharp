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
using System.Globalization;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Runs a block under a given <see cref="CultureInfo.CurrentCulture"/> and puts the previous one back when
	/// disposed, so a culture test cannot leak its culture into the next test.
	/// </summary>
	/// <remarks>
	/// The current culture flows with the async context, so a scope opened in an async test covers every await
	/// inside it. <c>includeNewThreads</c> also pins <see cref="CultureInfo.DefaultThreadCurrentCulture"/> for work
	/// that runs on threads the test did not start (the UI idle queue, for one). Both are restored to what they
	/// were - never to null - so a run under <c>MATTERCAD_TEST_CULTURE</c> keeps its culture after the scope.
	/// Culture is process-wide for any test that does not set its own, so tests using this stay [NotInParallel].
	/// </remarks>
	public sealed class CultureScope : IDisposable
	{
		private readonly CultureInfo previousCurrent;
		private readonly CultureInfo previousDefault;
		private readonly bool includeNewThreads;

		public CultureScope(string cultureName, bool includeNewThreads = false)
			: this(new CultureInfo(cultureName), includeNewThreads)
		{
		}

		public CultureScope(CultureInfo culture, bool includeNewThreads = false)
		{
			previousCurrent = CultureInfo.CurrentCulture;
			previousDefault = CultureInfo.DefaultThreadCurrentCulture;
			this.includeNewThreads = includeNewThreads;
			if (includeNewThreads)
			{
				CultureInfo.DefaultThreadCurrentCulture = culture;
			}

			CultureInfo.CurrentCulture = culture;
		}

		public void Dispose()
		{
			if (includeNewThreads)
			{
				CultureInfo.DefaultThreadCurrentCulture = previousDefault;
			}

			CultureInfo.CurrentCulture = previousCurrent;
		}
	}
}
