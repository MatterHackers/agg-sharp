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
using System.IO;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// Where a head keeps the GUI demo's <see cref="DemoState"/> JSON between runs (agg-gui's persistence.rs
	/// save/load): a file on the desktop, localStorage in the browser.
	/// </summary>
	public interface IDemoStateStore
	{
		/// <summary>The saved JSON, or null when nothing has been saved (or it cannot be read).</summary>
		string Load();

		void Save(string json);

		/// <summary>Forgets the saved state, so the next run starts with the defaults.</summary>
		void Clear();
	}

	/// <summary>
	/// Keeps the state in a file, by default the per-user application data folder's AggSharpDemo/state.json
	/// (~/Library/Application Support on a Mac). A file that cannot be read or written is treated as absent:
	/// losing the layout is better than a demo that will not start.
	/// </summary>
	public class FileDemoStateStore : IDemoStateStore
	{
		/// <param name="path">The file to use; null for <see cref="DefaultPath"/>.</param>
		public FileDemoStateStore(string path = null)
		{
			this.Path = path ?? DefaultPath;
		}

		public static string DefaultPath => System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
			"AggSharpDemo",
			"state.json");

		public string Path { get; }

		public string Load()
		{
			try
			{
				return File.Exists(this.Path) ? File.ReadAllText(this.Path) : null;
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
				return null;
			}
		}

		public void Save(string json)
		{
			try
			{
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.Path));

				// Written aside and moved over, so a run killed mid-write leaves the last good state.
				string temporary = this.Path + ".tmp";
				File.WriteAllText(temporary, json);
				File.Move(temporary, this.Path, overwrite: true);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
			}
		}

		public void Clear()
		{
			try
			{
				File.Delete(this.Path);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
			{
			}
		}
	}
}
