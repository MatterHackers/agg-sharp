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

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// What the GUI demo remembers between runs, as agg-gui's demo-ui state.rs SavedState does: which windows are
	/// open and where, their stacking order, the theme and accent, snapping and the backend panel. Plain data;
	/// <see cref="DemoStatePersistence"/> reads it from and applies it to the page.
	/// </summary>
	/// <remarks>
	/// Stored as JSON through a source-generated context rather than reflection, so the browser's trimmed
	/// publish keeps working. Anything missing, unknown or unreadable falls back to the default, so an old or
	/// hand-edited file can never keep the demo from starting.
	/// </remarks>
	public class DemoState
	{
		public const int CurrentVersion = 1;

		public int Version { get; set; } = CurrentVersion;

		/// <summary>One entry per window the user has opened at least once; windows not listed keep their
		/// default (open-by-default, tiled).</summary>
		public List<DemoWindowState> Windows { get; set; } = new List<DemoWindowState>();

		/// <summary>The open windows' titles from back to front (state.rs z_order).</summary>
		public List<string> ZOrder { get; set; } = new List<string>();

		/// <summary>A <see cref="ThemePreference"/> name; null or unknown keeps the default.</summary>
		public string Theme { get; set; }

		/// <summary>An <see cref="AccentColor"/> name; null or unknown keeps the default.</summary>
		public string Accent { get; set; }

		public bool SnapEnabled { get; set; } = true;

		public bool BackendPanelOpen { get; set; }

		/// <summary>The inspector's tree expansion, selection and split (state.rs inspector); null keeps its defaults.</summary>
		public InspectorSavedState Inspector { get; set; }

		/// <summary>The System window's typography settings and tab; null keeps the current ones.</summary>
		public SystemSettingsState SystemSettings { get; set; }

		/// <summary>The OS window's size in agg pixels when last saved (state.rs window_w / window_h); 0 when the
		/// page was not in a desktop window. The mac head opens its window at this size.</summary>
		public double OsWindowWidth { get; set; }

		public double OsWindowHeight { get; set; }

		/// <summary>The size a desktop head opens its window at: the saved one, or the default when none was saved.
		/// A saved size smaller than a usable window is taken as no saved size.</summary>
		public static (double Width, double Height) InitialOsWindowSize(DemoState state, double defaultWidth, double defaultHeight)
		{
			return state.OsWindowWidth >= 320 && state.OsWindowHeight >= 240
				? (state.OsWindowWidth, state.OsWindowHeight)
				: (defaultWidth, defaultHeight);
		}

		public string Serialize() => JsonSerializer.Serialize(this, DemoStateJsonContext.Default.DemoState);

		/// <summary>The state in <paramref name="json"/>, or the defaults when it is missing or unreadable.</summary>
		public static DemoState Parse(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return new DemoState();
			}

			try
			{
				DemoState state = JsonSerializer.Deserialize(json, DemoStateJsonContext.Default.DemoState) ?? new DemoState();

				// An explicit null list deserialises as null rather than leaving the initialiser in place.
				state.Windows ??= new List<DemoWindowState>();
				state.Windows.RemoveAll(w => w == null || string.IsNullOrEmpty(w.Title));
				state.ZOrder ??= new List<string>();
				state.ZOrder.RemoveAll(string.IsNullOrEmpty);
				return state;
			}
			catch (JsonException)
			{
				return new DemoState();
			}
		}
	}

	/// <summary>One demo window's saved state (state.rs WindowState): open, and its visible rectangle in the
	/// canvas (y up from the canvas bottom).</summary>
	public class DemoWindowState
	{
		public string Title { get; set; }

		public bool Open { get; set; }

		public double X { get; set; }

		public double Y { get; set; }

		public double Width { get; set; }

		public double Height { get; set; }

		/// <summary>state.rs maximized: the window fills the canvas; the rectangle is the one it restores to.</summary>
		public bool Maximized { get; set; }

		/// <summary>state.rs has_valid_bounds: a zero or negative size means no rectangle was saved.</summary>
		[JsonIgnore]
		public bool HasBounds => this.Width > 0 && this.Height > 0;
	}

	// partial only because the JSON source generator fills it in.
	[JsonSourceGenerationOptions(WriteIndented = true)]
	[JsonSerializable(typeof(DemoState))]
	internal partial class DemoStateJsonContext : JsonSerializerContext
	{
	}
}
