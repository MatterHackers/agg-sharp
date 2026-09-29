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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo remembering its windows and settings between runs (agg-gui's state.rs / persistence.rs).
	// Builds every demo window, About included: new DemoTheme() writes ThemeConfig.Current and the About
	// window's MarkdownWidget writes MarkdownWidget.Theme.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoStatePersistenceTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		/// <summary>A shell in a page (by default 1000 x 700, so its canvas is 780 x 674), laid out.</summary>
		private static GuiDemoShell LaidOutShell(IDemoStateStore store, int width = 1000, int height = 700)
		{
			var page = new GuiWidget(width, height);
			var shell = new GuiDemoShell(new DemoTheme(), store);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		[Test]
		public async Task StateSurvivesASerializeRoundTrip()
		{
			var state = new DemoState
			{
				Theme = "Light",
				Accent = "Teal",
				SnapEnabled = false,
				BackendPanelOpen = true,
				ZOrder = new List<string> { "Sliders", "TextEdit" },
				Windows = new List<DemoWindowState>
				{
					new DemoWindowState { Title = "Sliders", Open = true, X = 10.5, Y = 20, Width = 300, Height = 200 },
				},
			};

			DemoState back = DemoState.Parse(state.Serialize());

			await Assert.That(back.Version).IsEqualTo(DemoState.CurrentVersion);
			await Assert.That(back.Theme).IsEqualTo("Light");
			await Assert.That(back.Accent).IsEqualTo("Teal");
			await Assert.That(back.SnapEnabled).IsFalse();
			await Assert.That(back.BackendPanelOpen).IsTrue();
			await Assert.That(back.ZOrder).IsEquivalentTo(new[] { "Sliders", "TextEdit" }, CollectionOrdering.Matching);
			await Assert.That(back.Windows.Count).IsEqualTo(1);
			DemoWindowState window = back.Windows[0];
			await Assert.That(window.Title).IsEqualTo("Sliders");
			await Assert.That(window.Open).IsTrue();
			await Assert.That((window.X, window.Y, window.Width, window.Height)).IsEqualTo((10.5, 20.0, 300.0, 200.0));
		}

		[Test]
		public async Task CorruptOrPartialJsonFallsBackToDefaults()
		{
			foreach (string corrupt in new[] { null, "", "   ", "{not json", "[1,2]", "{\"SnapEnabled\":\"yes\"}" })
			{
				DemoState state = DemoState.Parse(corrupt);
				await Assert.That(state.SnapEnabled).IsTrue();
				await Assert.That(state.Theme).IsNull();
				await Assert.That(state.Windows.Count).IsEqualTo(0);
			}

			// Only what is there is taken; the rest keeps its default, and null lists become empty.
			DemoState partial = DemoState.Parse("{\"Theme\":\"Dark\",\"Windows\":null,\"ZOrder\":[null,\"\"],\"Extra\":1}");
			await Assert.That(partial.Theme).IsEqualTo("Dark");
			await Assert.That(partial.SnapEnabled).IsTrue();
			await Assert.That(partial.Windows.Count).IsEqualTo(0);
			await Assert.That(partial.ZOrder.Count).IsEqualTo(0);

			// A shell given garbage starts as if nothing had been saved.
			GuiDemoShell shell = LaidOutShell(new MemoryStore("{garbage"));
			await Assert.That(shell.Windows.ZOrder.Select(s => s.Title).ToList()).IsEquivalentTo(shell.Windows.DefaultStacking().Select(s => s.Title).ToList(), CollectionOrdering.Matching);
			await Assert.That(shell.DemoTheme.Preference).IsEqualTo(ThemePreference.System);
		}

		[Test]
		public async Task ARestoredPageReopensItsWindowsWhereTheyWereWithTheSameThemeAndOrder()
		{
			// Wide enough that every window stays on the canvas, so restoring has nothing to clamp.
			var store = new MemoryStore();
			GuiDemoShell first = LaidOutShell(store, 1800, 1000);
			DemoSpec sliders = Spec("Sliders");
			DemoSpec textEdit = Spec("TextEdit");
			DemoSpec closedDefault = GuiDemoSpecs.All.First(s => s.OpenByDefault);

			first.Windows.SetOpen(sliders, true);
			first.Windows.SetOpen(textEdit, true);
			first.Windows.SetOpen(closedDefault, false);
			first.Windows.GetWindow(sliders).Position = new Vector2(111, 99);
			first.Windows.Raise(sliders);
			first.DemoTheme.SetPreference(ThemePreference.Light);
			first.DemoTheme.SetAccent(AccentColor.Green);
			first.TopBar.SetSnapEnabled(false);
			first.TopBar.SetBackendPanelOpen(true);
			RectangleDouble slidersRect = first.Windows.GetVisibleRect(sliders).Value;
			RectangleDouble textEditRect = first.Windows.GetVisibleRect(textEdit).Value;
			var zOrder = first.Windows.ZOrder.ToList();

			// Closing the page saves it.
			first.Close();
			await Assert.That(store.SaveCount).IsGreaterThan(0);

			GuiDemoShell second = LaidOutShell(store, 1800, 1000);
			await Assert.That(second.Windows.IsOpen(sliders)).IsTrue();
			await Assert.That(second.Windows.IsOpen(textEdit)).IsTrue();
			await Assert.That(second.Windows.IsOpen(closedDefault)).IsFalse();
			await Assert.That(second.Windows.GetVisibleRect(sliders).Value).IsEqualTo(slidersRect);
			await Assert.That(second.Windows.GetVisibleRect(textEdit).Value).IsEqualTo(textEditRect);
			await Assert.That(second.Windows.ZOrder).IsEquivalentTo(zOrder, CollectionOrdering.Matching);
			await Assert.That(second.Windows.ZOrder.Last()).IsEqualTo(sliders);
			await Assert.That(second.DemoTheme.Preference).IsEqualTo(ThemePreference.Light);
			await Assert.That(second.DemoTheme.Accent).IsEqualTo(AccentColor.Green);
			await Assert.That(second.TopBar.SnapEnabled).IsFalse();
			await Assert.That(second.Windows.Snap.Enabled).IsFalse();
			await Assert.That(second.TopBar.BackendPanelOpen).IsTrue();
			second.Close();
		}

		[Test]
		public async Task AMaximizedWindowComesBackMaximizedAndRestoresToItsOwnRectangle()
		{
			var store = new MemoryStore();
			GuiDemoShell first = LaidOutShell(store, 1800, 1000);
			DemoSpec sliders = Spec("Sliders");
			first.Windows.SetOpen(sliders, true);
			WindowWidget window = first.Windows.GetWindow(sliders);
			await Assert.That(window.Maximizable).IsTrue();
			RectangleDouble ownRect = first.Windows.GetVisibleRect(sliders).Value;
			window.FindDescendant("Window Maximize Button").InvokeClick();
			await Assert.That(first.Windows.GetVisibleRect(sliders).Value).IsEqualTo(ownRect);
			first.Close();

			GuiDemoShell second = LaidOutShell(store, 1800, 1000);
			await Assert.That(second.Windows.IsMaximized(sliders)).IsTrue();
			second.Windows.SetMaximized(sliders, false);
			await Assert.That(second.Windows.GetVisibleRect(sliders).Value).IsEqualTo(ownRect);
			second.Close();
		}

		[Test]
		public async Task ChangesSaveOnceAfterTheDelay()
		{
			var store = new MemoryStore();
			GuiDemoShell shell = LaidOutShell(null);
			var scheduled = new List<(Action Action, double Delay)>();
			var persistence = new DemoStatePersistence(shell, store, (action, delay) => scheduled.Add((action, delay)));
			persistence.Start();

			// A burst of changes - open, drag, theme, snapping - waits on one save.
			DemoSpec sliders = Spec("Sliders");
			shell.Windows.SetOpen(sliders, true);
			for (int i = 0; i < 5; i++)
			{
				shell.Windows.GetWindow(sliders).Position = new Vector2(100 + i, 100);
			}

			shell.DemoTheme.SetAccent(AccentColor.Red);
			shell.TopBar.SetSnapEnabled(false);
			await Assert.That(scheduled.Count).IsEqualTo(1);
			await Assert.That(scheduled[0].Delay).IsEqualTo(DemoStatePersistence.SaveDelaySeconds);
			await Assert.That(store.SaveCount).IsEqualTo(0);

			scheduled[0].Action();
			await Assert.That(store.SaveCount).IsEqualTo(1);
			await Assert.That(DemoState.Parse(store.Json).Accent).IsEqualTo("Red");

			// The next change schedules the next save.
			shell.Windows.Raise(sliders);
			await Assert.That(scheduled.Count).IsEqualTo(2);

			// Stopping (the page closing) saves at once, and the late timer then has nothing to write.
			persistence.Stop();
			await Assert.That(store.SaveCount).IsEqualTo(2);
			scheduled[1].Action();
			await Assert.That(store.SaveCount).IsEqualTo(2);
		}

		[Test]
		public async Task UnknownTitlesAreIgnored()
		{
			var saved = new DemoState
			{
				ZOrder = new List<string> { "Sliders", "No Such Window" },
				Windows = new List<DemoWindowState>
				{
					new DemoWindowState { Title = "No Such Window", Open = true, X = 1, Y = 1, Width = 100, Height = 100 },
					new DemoWindowState { Title = "Sliders", Open = true },
				},
			};

			GuiDemoShell shell = LaidOutShell(new MemoryStore(saved.Serialize()));
			await Assert.That(shell.Windows.IsOpen(Spec("Sliders"))).IsTrue();
			await Assert.That(shell.Windows.ZOrder.Last()).IsEqualTo(Spec("Sliders"));
		}

		[Test]
		public async Task OffCanvasRectanglesAreClampedIntoTheCanvas()
		{
			await Assert.That(DemoWindowHost.ClampToCanvas(new RectangleDouble(5000, -300, 5300, -100), 780, 674))
				.IsEqualTo(new RectangleDouble(480, 0, 780, 200));
			await Assert.That(DemoWindowHost.ClampToCanvas(new RectangleDouble(-50, 10, 1950, 110), 780, 674))
				.IsEqualTo(new RectangleDouble(0, 10, 780, 110));

			// Restored before the canvas has a size, and clamped once it has one.
			var saved = new DemoState
			{
				Windows = new List<DemoWindowState>
				{
					new DemoWindowState { Title = "Sliders", Open = true, X = 5000, Y = -300, Width = 300, Height = 200 },
				},
			};
			GuiDemoShell shell = LaidOutShell(new MemoryStore(saved.Serialize()));
			await Assert.That(shell.Canvas.Width).IsEqualTo(780);
			await Assert.That(shell.Windows.GetVisibleRect(Spec("Sliders")).Value).IsEqualTo(new RectangleDouble(480, 0, 780, 200));
		}

		[Test]
		public async Task FileStoreSavesLoadsAndClears()
		{
			string folder = Path.Combine(Path.GetTempPath(), "AggSharpDemoStateTest-" + Guid.NewGuid().ToString("N"));
			try
			{
				var store = new FileDemoStateStore(Path.Combine(folder, "nested", "state.json"));
				await Assert.That(store.Load()).IsNull();

				store.Save("{\"Theme\":\"Dark\"}");
				await Assert.That(store.Load()).IsEqualTo("{\"Theme\":\"Dark\"}");

				store.Clear();
				await Assert.That(store.Load()).IsNull();
			}
			finally
			{
				if (Directory.Exists(folder))
				{
					Directory.Delete(folder, recursive: true);
				}
			}
		}

		private class MemoryStore : IDemoStateStore
		{
			public MemoryStore(string json = null)
			{
				this.Json = json;
			}

			public string Json { get; private set; }

			public int SaveCount { get; private set; }

			public string Load() => this.Json;

			public void Save(string json)
			{
				this.Json = json;
				this.SaveCount++;
			}

			public void Clear() => this.Json = null;
		}
	}
}
