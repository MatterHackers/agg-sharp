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

using System;
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
	// Verifies the medium-severity constructor hygiene fixes: widgets must not do disk I/O,
	// icon processing, or rasterization in their constructors - that work is deferred to
	// OnLoad (or first use for OutputScroll's TypeFacePrinter).
	public class ConstructorHygieneTests
	{
		private static object GetPrivateField(object instance, string fieldName)
		{
			var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			return field.GetValue(instance);
		}

		private static string CreateTempStaticDataRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "AggConstructorHygiene_" + Path.GetRandomFileName());
			Directory.CreateDirectory(Path.Combine(root, "Icons"));
			return root;
		}

		private static void WriteBlankIcon(string rootPath, string iconName, int width, int height)
		{
			var image = new ImageBuffer(width, height);
			var graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			ImageIO.SaveImageData(Path.Combine(rootPath, "Icons", iconName), image);
		}

		[Test]
		[NotInParallel]
		public async Task CheckboxMenuItemDoesNotLoadIconInConstructor()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = CreateTempStaticDataRoot();

			try
			{
				// Point StaticData at a directory that has no icons at all. The old code
				// loaded fa-check_16.png in the constructor and would fail here.
				StaticData.RootPath = tempRoot;

				var theme = new ThemeConfig();
				var item = new PopupMenu.CheckboxMenuItem(new GuiWidget(), theme)
				{
					Checked = true
				};

				await Assert.That(item.Image).IsNull();

				// Provide the icon and load - the deferred work happens now.
				WriteBlankIcon(tempRoot, "fa-check_16.png", 16, 16);
				item.OnLoad(null);

				await Assert.That(item.Image).IsNotNull();
			}
			finally
			{
				StaticData.RootPath = savedRootPath;
				Directory.Delete(tempRoot, true);
			}
		}

		[Test]
		[NotInParallel]
		public async Task TreeExpandWidgetDoesNotLoadIconsInConstructor()
		{
			string savedRootPath = StaticData.RootPath;
			string tempRoot = CreateTempStaticDataRoot();

			try
			{
				// The old code loaded both arrow icons in the TreeExpandWidget constructor
				// and would fail against this iconless StaticData root.
				StaticData.RootPath = tempRoot;

				var theme = new ThemeConfig();
				var treeNode = new TreeNode(theme);

				var expandWidget = treeNode.Descendants<FlowLayoutWidget>()
					.First(w => w.Name == "Expand Widget");

				await Assert.That(GetPrivateField(expandWidget, "arrowRight")).IsNull();
				await Assert.That(GetPrivateField(expandWidget, "arrowDown")).IsNull();

				// Provide the icons and load - the deferred work happens now.
				WriteBlankIcon(tempRoot, "fa-angle-right_12.png", 12, 12);
				WriteBlankIcon(tempRoot, "fa-angle-down_12.png", 12, 12);
				expandWidget.OnLoad(null);

				await Assert.That(GetPrivateField(expandWidget, "arrowRight")).IsNotNull();
				await Assert.That(GetPrivateField(expandWidget, "arrowDown")).IsNotNull();
			}
			finally
			{
				StaticData.RootPath = savedRootPath;
				Directory.Delete(tempRoot, true);
			}
		}

		[Test]
		public async Task SvgWidgetDefersRasterizationUntilLoad()
		{
			string svgPath = Path.Combine(Path.GetTempPath(), "AggConstructorHygiene_" + Path.GetRandomFileName() + ".svg");
			File.WriteAllText(
				svgPath,
				"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path d=\"M2,2 L14,2 L14,14 L2,14 Z\" fill=\"#FF0000\"/></svg>");

			try
			{
				var widget = new SvgWidget(svgPath, 1, 20, 20);

				// Sizing is available immediately, but nothing has been rasterized yet.
				// The old code parsed and rasterized in the constructor.
				await Assert.That(widget.MinimumSize.X).IsEqualTo(20.0);
				await Assert.That(widget.MinimumSize.Y).IsEqualTo(20.0);
				await Assert.That(GetPrivateField(widget, "imageBuffer")).IsNull();

				// Drive the lifecycle through a real draw. OnDraw must trigger the deferred
				// load itself (it renders imageBuffer before base.OnDraw would fire OnLoad).
				widget.OnDraw(new ImageBuffer(30, 30).NewGraphics2D());

				var deferredBuffer = (ImageBuffer)GetPrivateField(widget, "imageBuffer");
				await Assert.That(deferredBuffer).IsNotNull();

				// The deferred path must produce the same pixels the eager LoadSvg path does.
				var eagerWidget = new SvgWidget();
				using (var stream = File.OpenRead(svgPath))
				{
					eagerWidget.LoadSvg(stream, 1, 20, 20);
				}

				var eagerBuffer = (ImageBuffer)GetPrivateField(eagerWidget, "imageBuffer");
				await Assert.That(deferredBuffer.Width).IsEqualTo(eagerBuffer.Width);
				await Assert.That(deferredBuffer.Height).IsEqualTo(eagerBuffer.Height);
				await Assert.That(deferredBuffer.GetBuffer().SequenceEqual(eagerBuffer.GetBuffer())).IsTrue();
			}
			finally
			{
				File.Delete(svgPath);
			}
		}

		[Test]
		public async Task OutputScrollCreatesTypeFacePrinterLazily()
		{
			var outputScroll = new OutputScroll();

			// The old code created the TypeFacePrinter in a field initializer, forcing
			// font machinery at construction time.
			await Assert.That(GetPrivateField(outputScroll, "printer")).IsNull();

			outputScroll.Write("hello");

			await Assert.That(GetPrivateField(outputScroll, "printer")).IsNotNull();
		}

		private class RaisableControl : System.Windows.Forms.Control
		{
			public void RaiseKeyDown(System.Windows.Forms.Keys keys)
			{
				this.OnKeyDown(new System.Windows.Forms.KeyEventArgs(keys));
			}
		}

		[Test]
		[NotInParallel]
		public async Task WinformsEventSinkUnhookRemovesControlHandlers()
		{
			var control = new RaisableControl();
			var systemWindow = new SystemWindow(100, 100);

			int keyDownCount = 0;
			systemWindow.KeyDown += (s, e) => keyDownCount++;

			var eventSink = new WinformsEventSink(control, systemWindow);

			control.RaiseKeyDown(System.Windows.Forms.Keys.A);
			await Assert.That(keyDownCount).IsEqualTo(1);

			eventSink.Unhook();

			// After Unhook no handler wired by the constructor may still be attached.
			control.RaiseKeyDown(System.Windows.Forms.Keys.A);
			await Assert.That(keyDownCount).IsEqualTo(1);

			// Safe to call again.
			eventSink.Unhook();

			Keyboard.Clear();
		}
	}
}
