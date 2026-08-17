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

using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using Microsoft.Testing.Platform.Builder;

namespace Agg.Tests
{
	/// <summary>
	/// The test process entry point, written out by hand rather than generated
	/// (<c>GenerateTestingPlatformEntryPoint</c> is off in the project file) so the main thread can be
	/// handed to <see cref="MainThreadDispatcher"/> before the test engine claims it.
	/// </summary>
	/// <remarks>
	/// The body below is what Microsoft.Testing.Platform.MSBuild generates, unchanged. The one difference
	/// is that it is run through <see cref="MainThreadDispatcher.RunHosted"/>: on macOS an NSWindow may
	/// only be created on the process main thread, and a test engine that owns <c>Main</c> and runs tests
	/// on thread pool workers leaves no thread that qualifies. Every windowed automation test aborted the
	/// whole process with <c>NSInternalInconsistencyException</c> until this existed. On every other host
	/// <c>RunHosted</c> is exactly the <c>GetAwaiter().GetResult()</c> an <c>async Main</c> already was.
	/// </remarks>
	internal sealed class Program
	{
		// TUnit0034 says not to declare a Main, because normally the build generates one. This project has
		// turned that generation off precisely so it can declare this one - see the remarks above.
#pragma warning disable TUnit0034
		public static int Main(string[] args)
#pragma warning restore TUnit0034
		{
			return MainThreadDispatcher.RunHosted(() => RunTestApplicationAsync(args));
		}

		private static async Task<int> RunTestApplicationAsync(string[] args)
		{
			ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
			builder.AddSelfRegisteredExtensions(args);

			using (ITestApplication app = await builder.BuildAsync())
			{
				return await app.RunAsync();
			}
		}
	}
}
