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
using System.Linq;
using Markdig.Agg;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tools
{
	/// <summary>
	/// The "About agg-sharp" window, agg-gui's about (demo-ui/src/windows.rs): a short markdown description of
	/// the library rendered by MarkdigAgg's <see cref="MarkdownWidget"/>, which scrolls its own content the way
	/// agg-gui wraps its MarkdownView in a ScrollView. agg-gui embeds its README; this site has text of its own
	/// (<see cref="AboutMarkdown"/>) because agg-sharp's README describes the repository rather than the demo.
	/// </summary>
	public class AboutWindow : GuiWidget
	{
		public const string RepositoryUrl = "https://github.com/MatterHackers/agg-sharp";

		/// <summary>What the window shows. Kept short and factual; the repository has the rest.</summary>
		public const string AboutMarkdown =
@"# agg-sharp

agg-sharp is a C# graphics and UI library: a port of Maxim Shemanarev's Anti-Grain Geometry (AGG) 2D renderer, grown into a widget toolkit with text, layout, 3D meshes and GPU rendering. It runs on Windows, Mac, Linux and in the browser (WebAssembly), and is the foundation of MatterHackers' MatterCAD.

## This site

- **AGG Demos** - the C++ AGG examples, ported one for one. Tests check that their software-rendered pixels match the C++ output byte for byte.
- **GUI Demo** - a desktop of floating windows showing the widget toolkit, ported from agg-gui's demo. It is drawn on the GPU through WebGPU: D3D12, Metal or Vulkan on the desktop, the browser's WebGPU here.

## Source

agg-sharp is open source under the BSD 2-Clause license: [github.com/MatterHackers/agg-sharp](" + RepositoryUrl + @")
";

		private readonly DemoTheme demoTheme;

		public AboutWindow(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			// agg-gui's MarkdownView padding.
			this.Padding = 12;

			this.Markdown = new MarkdownWidget(demoTheme.Theme)
			{
				Name = "About Markdown",
			};
			this.AddChild(this.Markdown);
			this.ShowMarkdown();

			demoTheme.ThemeChanged += this.OnThemeChanged;
		}

		public MarkdownWidget Markdown { get; }

		public override void OnClosed(EventArgs e)
		{
			// The theme outlives the window.
			this.demoTheme.ThemeChanged -= this.OnThemeChanged;
			base.OnClosed(e);
		}

		private void OnThemeChanged(object sender, EventArgs e)
		{
			// MarkdownWidget copies the theme's colours into the widgets it builds, so recolouring means building
			// them again; the reader keeps their place.
			var scroll = this.Markdown.ScrollPositionFromTop;
			this.Markdown.Markdown = string.Empty;
			this.ShowMarkdown();
			this.Markdown.ScrollPositionFromTop = scroll;
		}

		private void ShowMarkdown()
		{
			this.Markdown.Markdown = AboutMarkdown;

			// Markdown gives its widgets no names; the repository link gets a stable one to be found by.
			TextLinkX link = this.Markdown.Descendants<TextLinkX>().FirstOrDefault(l => l.Url == RepositoryUrl);
			if (link != null)
			{
				link.Name = "About Repository Link";
			}
		}
	}
}
