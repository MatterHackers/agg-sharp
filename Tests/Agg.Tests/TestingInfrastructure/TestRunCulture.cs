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
	/// Reads <c>MATTERCAD_TEST_CULTURE</c> (a culture name such as de-DE) and makes it the culture of the whole
	/// test run, so the same tests can prove themselves under a comma-decimal or Turkish-casing culture.
	/// </summary>
	/// <remarks>
	/// Sets the default for every thread plus the calling thread's culture. The UI culture is left alone: the
	/// translations follow the Language setting, not the number format. Unset or blank leaves the run as it was.
	/// </remarks>
	public static class TestRunCulture
	{
		public const string EnvironmentVariable = "MATTERCAD_TEST_CULTURE";

		/// <summary>The culture name the run was asked for, or null when the variable is unset.</summary>
		public static string Requested
		{
			get
			{
				string name = Environment.GetEnvironmentVariable(EnvironmentVariable);
				return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
			}
		}

		public static void ApplyFromEnvironment()
		{
			string name = Requested;
			if (name == null)
			{
				return;
			}

			var culture = new CultureInfo(name);
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.CurrentCulture = culture;
		}
	}
}
