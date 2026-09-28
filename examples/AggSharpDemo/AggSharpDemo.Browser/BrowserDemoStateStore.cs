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

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using MatterHackers.AggSharpDemo.GuiDemo;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>
	/// The browser's <see cref="IDemoStateStore"/>: the GUI demo's state under one localStorage key, through
	/// <c>wwwroot/demoState.js</c>, so a refresh brings the windows back where they were.
	/// </summary>
	/// <remarks>
	/// Import the module (<see cref="ModulePath"/> as <see cref="ModuleName"/>) before the first call.
	/// <c>partial</c> because the <c>[JSImport]</c> source generator requires it.
	/// </remarks>
	[SupportedOSPlatform("browser")]
	public sealed partial class BrowserDemoStateStore : IDemoStateStore
	{
		public const string ModuleName = "aggSharpDemoState";

		/// <summary>Relative to the runtime's <c>_framework/</c> folder, as BrowserHostBootstrap's modules are.</summary>
		public const string ModulePath = "../demoState.js";

		private const string Key = "AggSharpDemo.GuiDemo.State";

		public string Load() => LoadCore(Key);

		public void Save(string json) => SaveCore(Key, json);

		public void Clear() => ClearCore(Key);

		[JSImport("load", ModuleName)]
		private static partial string LoadCore(string key);

		[JSImport("save", ModuleName)]
		private static partial void SaveCore(string key, string value);

		[JSImport("clear", ModuleName)]
		private static partial void ClearCore(string key);
	}
}
