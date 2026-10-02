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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// The globe a markdown image shows until its picture arrives is black ink in the icon file. It is drawn in the
	/// theme's text colour - the colour of the link text beside it - so it stays visible on a dark theme.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: <see cref="StaticData.RootPath"/> is process wide. ImageLinkSimpleX loads the
	/// globe once per process through a static Lazy, which caches the first load's result - including its exception.
	/// If an earlier test loaded the globe successfully from another root, the colour check still holds; but if it
	/// built an image link under a root with no internet.* icon, a Debug LoadIcon threw "Bad icon load" and this
	/// test fails with that same exception.
	/// </remarks>
	[NotInParallel]
	public class MarkdownLinkIconTests
	{
		private const string BlackGlobe = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"#000\"/></svg>";

		private static readonly Color DarkThemeText = new Color(230, 230, 230);

		[Test]
		public async Task TheImagePlaceholderGlobeIsDrawnInTheThemeTextColor()
		{
			await WithGlobeIcon(async () =>
			{
				var root = new GuiWidget();
				new AggMarkdownDocument { Markdown = "![a picture](pic.png)" }
					.Parse(new ThemeConfig { TextColor = DarkThemeText }, root);

				ImageBuffer globe = root.Descendants<ImageLinkSimpleX>().Single()
					.Descendants<ResponsiveImageSequenceWidget>().Single().ImageSequence.Frames[0];

				await AssertInkIs(globe, DarkThemeText);
			});
		}

		private static async Task AssertInkIs(ImageBuffer image, Color expected)
		{
			var ink = Enumerable.Range(0, image.Width)
				.SelectMany(x => Enumerable.Range(0, image.Height).Select(y => image.GetPixel(x, y)))
				.Where(c => c.alpha == 255)
				.ToList();

			await Assert.That(ink.Count).IsGreaterThan(0);
			await Assert.That(ink.All(c => c.red == expected.red && c.green == expected.green && c.blue == expected.blue)).IsTrue()
				.Because("the globe's opaque ink must be the theme text colour, not the icon file's black");
		}

		private static async Task WithGlobeIcon(System.Func<Task> test)
		{
			string savedRootPath = StaticData.RootPath;
			string root = Path.Combine(Path.GetTempPath(), "AggMarkdownLinkIcon_" + Path.GetRandomFileName());
			Directory.CreateDirectory(Path.Combine(root, "Icons"));
			File.WriteAllText(Path.Combine(root, "Icons", "internet.svg"), BlackGlobe);
			try
			{
				StaticData.RootPath = root;
				await test();
			}
			finally
			{
				StaticData.RootPath = savedRootPath;
				Directory.Delete(root, true);
			}
		}
	}
}
