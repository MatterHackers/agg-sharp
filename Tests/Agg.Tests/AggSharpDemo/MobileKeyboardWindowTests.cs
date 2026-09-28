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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Mobile Keyboard window (agg-gui's demo-ui/src/windows/mobile_keyboard.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current), nameof(InputProfiles) })]
	public class MobileKeyboardWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Mobile Keyboard");

		[Test]
		public async Task PicksTheProfileAndEachFieldsKeyboardLayer()
		{
			try
			{
				var demoTheme = new DemoTheme(ThemePreference.Light);
				var window = (MobileKeyboardWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
				var host = new GuiWidget(Spec.DefaultWidth * GuiWidget.DeviceScale, Spec.DefaultHeight * GuiWidget.DeviceScale);
				host.AddChild(window);
				host.PerformLayout();

				await Assert.That(window.Name).IsEqualTo("Mobile Keyboard Content");
				await Assert.That(window.ProfileRadios[0].Checked).IsTrue();

				window.ProfileRadios[1].Checked = true;
				await Assert.That(InputProfiles.Current).IsEqualTo(InputProfile.MobileIOS);
				window.ProfileRadios[2].Checked = true;
				await Assert.That(InputProfiles.Current).IsEqualTo(InputProfile.MobileAndroid);

				// Set from elsewhere, the profile shows in the radios.
				InputProfiles.Current = InputProfile.Desktop;
				await Assert.That(window.ProfileRadios[0].Checked).IsTrue();

				await Assert.That(window.PrimaryField.ActualTextEditWidget.KeyboardInputMode).IsEqualTo(KeyboardInputMode.Text);
				window.NumericModeRadio.Checked = true;
				await Assert.That(window.PrimaryField.ActualTextEditWidget.KeyboardInputMode).IsEqualTo(KeyboardInputMode.Numeric);
				window.TextModeRadio.Checked = true;
				await Assert.That(window.PrimaryField.ActualTextEditWidget.KeyboardInputMode).IsEqualTo(KeyboardInputMode.Text);
				await Assert.That(window.NumericField.ActualTextEditWidget.KeyboardInputMode).IsEqualTo(KeyboardInputMode.Numeric);

				var image = new ImageBuffer((int)window.Width, (int)window.Height);
				window.OnDraw(image.NewGraphics2D());
				demoTheme.SetPreference(ThemePreference.Dark);
				await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Dark.PanelFill);
				window.OnDraw(image.NewGraphics2D());
			}
			finally
			{
				InputProfiles.Current = InputProfile.Desktop;
			}
		}

		[Test]
		public async Task TappingKeysTypesIntoTheFocusedField()
		{
			var window = new SystemWindow(Spec.DefaultWidth, Spec.DefaultHeight + 220) { Name = "Mobile Keyboard Test Window" };
			var content = (MobileKeyboardWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			window.AddChild(content);
			var controller = new SoftwareKeyboardController();
			try
			{
				await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
				{
					// On the Desktop default, focusing a field brings nothing up.
					testRunner.ClickByName("Mobile Keyboard Primary Field");
					testRunner.WaitFor(() => content.PrimaryField.ContainsFocus);
					await Assert.That(controller.IsShown).IsFalse();

					testRunner.ClickByName("Mobile Keyboard Profile iPhone");
					testRunner.WaitFor(() => InputProfiles.Current == InputProfile.MobileIOS);
					testRunner.ClickByName("Mobile Keyboard Primary Field");
					testRunner.WaitFor(() => controller.IsShown);

					SoftwareKeyboard keyboard = controller.Keyboard;
					await Assert.That(keyboard.State.Shifted).IsTrue();
					void Tap(SoftwareKeyKind kind, char character = '\0')
					{
						SoftwareKey key = keyboard.State.Rows.SelectMany(r => r).First(k => k.Kind == kind && (kind != SoftwareKeyKind.Character || k.Character == character));
						RectangleDouble bounds = keyboard.BoundsOf(key);
						testRunner.ClickByName("Software Keyboard", offset: new Point2D((int)bounds.Center.X, (int)bounds.Center.Y), origin: AutomationRunner.ClickOrigin.LowerLeft);
					}

					Tap(SoftwareKeyKind.Character, 'o');
					Tap(SoftwareKeyKind.Character, 'k');
					testRunner.WaitFor(() => content.PrimaryField.Text == "Ok");

					// The taps left the focus on the field, and the layer switch stays switched.
					await Assert.That(content.PrimaryField.ContainsFocus).IsTrue();
					Tap(SoftwareKeyKind.LayerSwitch);
					testRunner.WaitFor(() => keyboard.State.Layer == SoftwareKeyboardLayer.Numbers);
					Tap(SoftwareKeyKind.Character, '1');
					testRunner.WaitFor(() => content.PrimaryField.Text == "Ok1");
					await Assert.That(keyboard.State.Layer).IsEqualTo(SoftwareKeyboardLayer.Numbers);

					// The numeric field opens on the numbers; hide takes the keyboard down and keeps the focus.
					testRunner.ClickByName("Mobile Keyboard Numeric Field");
					testRunner.WaitFor(() => controller.Target == content.NumericField.ActualTextEditWidget);
					await Assert.That(keyboard.State.Layer).IsEqualTo(SoftwareKeyboardLayer.Numbers);
					Tap(SoftwareKeyKind.Dismiss);
					testRunner.WaitFor(() => !controller.IsShown);
					await Assert.That(content.NumericField.ContainsFocus).IsTrue();

					testRunner.MarkTestComplete();
				});
			}
			finally
			{
				controller.Dispose();
				InputProfiles.Current = InputProfile.Desktop;
			}
		}
	}
}
