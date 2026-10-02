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
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// agg-sharp's own widgets name their icons as PNGs, but an app that ships an .svg of the same base name
	/// gets the SVG - which draws with LCD subpixel edges - while an app that only ships the PNG keeps working.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: <see cref="StaticData.RootPath"/> is process wide. The icon cache is keyed
	/// by name and ignores the root, so a loaded SVG is recognised by its <see cref="ImageBuffer.LcdCoverage"/>,
	/// which no PNG ever carries.
	/// </remarks>
	[NotInParallel]
	public class SvgFirstIconTests
	{
		private const string BlackSquare = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><rect x=\"3\" y=\"3\" width=\"10\" height=\"10\" fill=\"#000\"/></svg>";

		[Test]
		public async Task PreferSvgIconPicksTheSvgOnlyWhenTheAppShipsOne()
		{
			await WithIconRoot(async root =>
			{
				WriteBlankPng(root, "both.png", 16, 16);
				File.WriteAllText(Path.Combine(root, "Icons", "both.svg"), BlackSquare);
				WriteBlankPng(root, "png_only.png", 16, 16);

				await Assert.That(StaticData.Instance.PreferSvgIcon("both.png")).IsEqualTo("both.svg");
				await Assert.That(StaticData.Instance.PreferSvgIcon("png_only.png")).IsEqualTo("png_only.png");
				await Assert.That(StaticData.Instance.PreferSvgIcon("both.svg")).IsEqualTo("both.svg");
			});
		}

		[Test]
		public async Task TheMenuCheckMarkUsesTheSvgWhenShipped()
		{
			await WithIconRoot(async root =>
			{
				File.WriteAllText(Path.Combine(root, "Icons", "fa-check_16.svg"), BlackSquare);

				await Assert.That(MenuCheckMark.Create(Color.Black).LcdCoverage).IsNotNull()
					.Because("with only fa-check_16.svg shipped the check must come from it, not the drawn stroke");
			});
		}

		[Test]
		public async Task TheMenuCheckMarkStillUsesAPngOnlyApp()
		{
			await WithIconRoot(async root =>
			{
				WriteBlankPng(root, "fa-check_16.png", 16, 16);

				ImageBuffer check = MenuCheckMark.Create(Color.Black);
				await Assert.That(check.LcdCoverage).IsNull();
				await Assert.That(check.Width).IsEqualTo((int)(16 * GuiWidget.DeviceScale));
			});
		}

		[Test]
		public async Task TreeArrowsUseTheSvgsWhenShipped()
		{
			await WithIconRoot(async root =>
			{
				File.WriteAllText(Path.Combine(root, "Icons", "fa-angle-right_12.svg"), BlackSquare);
				File.WriteAllText(Path.Combine(root, "Icons", "fa-angle-down_12.svg"), BlackSquare);

				GuiWidget expandWidget = new TreeNode(new ThemeConfig()).Descendants<FlowLayoutWidget>()
					.First(w => w.Name == "Expand Widget");
				expandWidget.OnLoad(null);

				await Assert.That(((ImageBuffer)PrivateField(expandWidget, "arrowRight")).LcdCoverage).IsNotNull();
				await Assert.That(((ImageBuffer)PrivateField(expandWidget, "arrowDown")).LcdCoverage).IsNotNull();
			});
		}

		private static object PrivateField(object target, string name)
			=> target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

		private static async Task WithIconRoot(System.Func<string, Task> test)
		{
			string savedRootPath = StaticData.RootPath;
			string root = Path.Combine(Path.GetTempPath(), "AggSvgFirstIcon_" + Path.GetRandomFileName());
			Directory.CreateDirectory(Path.Combine(root, "Icons"));
			try
			{
				StaticData.RootPath = root;
				await test(root);
			}
			finally
			{
				StaticData.RootPath = savedRootPath;
				Directory.Delete(root, true);
			}
		}

		private static void WriteBlankPng(string root, string iconName, int width, int height)
		{
			var image = new ImageBuffer(width, height);
			image.NewGraphics2D().Clear(Color.White);
			ImageIO.SaveImageData(Path.Combine(root, "Icons", iconName), image);
		}
	}
}
