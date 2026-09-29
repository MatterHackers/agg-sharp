/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins what the themed widgets drew before their constants became <see cref="ThemeConfig"/> tokens, so the
	/// token initializers are proven to equal the old look. MatterCAD deserializes its theme into
	/// <c>new ThemeConfig()</c> and relies on those initializers; a token whose default drifts would restyle
	/// every MatterCAD field and button without anyone asking for it.
	/// </summary>
	/// <remarks>
	/// Each line is a widget's size and a hash of every pixel it drew, so "pixel-identical" is checked
	/// literally rather than by sampling. Both <c>new ThemeConfig()</c> (MatterCAD's starting point) and
	/// <see cref="ThemeConfig.DefaultTheme"/> (every other agg app's) are covered, hovered and not. The "new"
	/// lines were captured from the code before the refactor and must never change; the "default" lines pin
	/// DefaultTheme's own look, which moved to the approved design on purpose (see DefaultThemeLookTests). Keyless <c>[NotInParallel]</c> because it
	/// writes the process-wide <see cref="ThemeConfig.Current"/> and <see cref="GuiWidget.DeviceScale"/>.
	/// </remarks>
	[NotInParallel]
	public class ThemeTokenDefaultsTests
	{
		private const int HostPad = 4;

		private const string ExpectedSnapshot =
			"new button 70.403x32 22B3ED1469A3BD07\n"
			+ "new textedit 86x20.667 97573CF572D6EFAC\n"
			+ "new textedit-empty 86x20.667 2C7B244FF6615DCC\n"
			+ "new numberedit 78.004x20.667 163E4D4CCFE4C9FE\n"
			+ "new dropdown 100x20.667 572ECB0D9ABF25D8\n"
			+ "new segmented 231x24 B585908644CCFF4A\n"
			+ "new button hover 70.403x32 1A5C70829DAFD817\n"
			+ "new textedit hover 86x20.667 97573CF572D6EFAC\n"
			+ "new textedit-empty hover 86x20.667 2C7B244FF6615DCC\n"
			+ "new numberedit hover 78.004x20.667 163E4D4CCFE4C9FE\n"
			+ "new dropdown hover 100x20.667 1660E80326218678\n"
			+ "new segmented hover 231x24 4CD5832C8A47DDEF\n"
			+ "default button 70.403x36 21012BBD794DF517\n"
			+ "default textedit 86x34 301914D2267AD22B\n"
			+ "default textedit-empty 86x34 D14456D5728F8034\n"
			+ "default numberedit 78.004x34 62375CE4CCAC1DC9\n"
			+ "default dropdown 100x34 A2E7BA02F7997F89\n"
			+ "default segmented 240x40 9DB7F9C711D6A879\n"
			+ "default button hover 70.403x36 562388B3748516AE\n"
			+ "default textedit hover 86x34 AC3E91868A2F3F3F\n"
			+ "default textedit-empty hover 86x34 1DC54208371569B4\n"
			+ "default numberedit hover 78.004x34 2C6D88503806649C\n"
			+ "default dropdown hover 100x34 6FD6550B52C408C0\n"
			+ "default segmented hover 240x40 134203BEE48F8ED3";

		[Test]
		public async Task ThemedWidgetsDrawAsTheyDidBeforeTokens()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				var lines = new List<string>();
				foreach (var (themeName, makeTheme) in new (string, Func<ThemeConfig>)[] { ("new", () => new ThemeConfig()), ("default", ThemeConfig.DefaultTheme) })
				{
					foreach (var hover in new[] { false, true })
					{
						foreach (var (name, make) in Widgets())
						{
							var theme = makeTheme();
							ThemeConfig.Current = theme;
							lines.Add($"{themeName} {name}{(hover ? " hover" : "")} {Snapshot(make(theme), theme, hover)}");
						}
					}
				}

				var actual = string.Join("\n", lines);
				Console.WriteLine(actual);
				await Assert.That(actual).IsEqualTo(ExpectedSnapshot);
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		/// <summary>
		/// MatterCAD saves a user's theme and reads it back over a bare ThemeConfig. A token left unset must
		/// still be unset afterwards - an "unset" that came back as a real value (Transparent, say) would strip
		/// the borders and fills of every field and drop down after the first save.
		/// </summary>
		[Test]
		public async Task UnsetTokensSurviveMatterCadsSaveAndReload()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = 1;
				var original = new ThemeConfig();
				var settings = new JsonSerializerSettings { ContractResolver = new MatterCadThemeResolver() };
				var json = JsonConvert.SerializeObject(original, settings);
				var reloaded = JsonConvert.DeserializeObject<ThemeConfig>(json, settings);

				await Assert.That(reloaded.ControlBorderColorIfSet).IsNull();
				await Assert.That(reloaded.ControlFillColorIfSet).IsNull();

				foreach (var (name, make) in Widgets())
				{
					ThemeConfig.Current = original;
					var before = Snapshot(make(original), original, false);
					ThemeConfig.Current = reloaded;
					var after = Snapshot(make(reloaded), reloaded, false);
					await Assert.That(after).IsEqualTo(before).Because($"{name} must draw the same after a save and reload");
				}
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		/// <summary>
		/// The rule of MatterCAD's ThemeContractResolver (MatterCADLib/ApplicationView/Themes): only writable
		/// properties are saved, and a Color property only when it is not Transparent.
		/// </summary>
		private class MatterCadThemeResolver : DefaultContractResolver
		{
			protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
			{
				var props = base.CreateProperties(type, memberSerialization);
				return props.Where(p => p.Writable).ToList();
			}

			protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);
				if (typeof(Color).IsAssignableFrom(property.PropertyType))
				{
					property.ShouldSerialize = instance => property.ValueProvider.GetValue(instance) is Color color && color != Color.Transparent;
				}

				return property;
			}
		}

		/// <summary>
		/// The same widgets at DeviceScale 2, plus stretched cases - a text field and a segmented control
		/// stretched into a parent narrower than the segmented control's natural width, where it clips - all
		/// under <c>new ThemeConfig()</c> and captured from the code before the refactor. (A segmented control
		/// stretched wider than its natural width now shares the width equally, on purpose; ThemeWidgetTests
		/// covers that.)
		/// </summary>
		[Test]
		public async Task ScaledAndStretchedWidgetsDrawAsTheyDidBeforeTokens()
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				var lines = new System.Collections.Generic.List<string>();
				GuiWidget.DeviceScale = 2;
				foreach (var (name, make) in Widgets())
				{
					var theme = new ThemeConfig();
					ThemeConfig.Current = theme;
					lines.Add($"x2 {name} {Snapshot(make(theme), theme, false)}");
				}

				GuiWidget.DeviceScale = 1;
				{
					var theme = new ThemeConfig();
					ThemeConfig.Current = theme;
					var field = new ThemedTextEditWidget("Text", theme, pixelWidth: 80) { HAnchor = HAnchor.Stretch };
					var fieldParent = new GuiWidget(200, 30);
					fieldParent.AddChild(field);
					fieldParent.PerformLayout();
					fieldParent.RemoveChild(field);
					field.ClearRemovedFlag();
					field.HAnchor = HAnchor.Absolute;
					lines.Add($"stretched textedit {Snapshot(field, theme, false)}");

					var segmented = new SegmentedControl(new[] { "Fast", "Medium", "Best" }, theme, 1) { HAnchor = HAnchor.Stretch };
					var segmentedParent = new GuiWidget(150, 30);
					segmentedParent.AddChild(segmented);
					segmentedParent.PerformLayout();
					segmentedParent.RemoveChild(segmented);
					segmented.ClearRemovedFlag();
					segmented.HAnchor = HAnchor.Absolute;
					lines.Add($"stretched-narrow segmented {Snapshot(segmented, theme, false)}");
				}

				var actual = string.Join("\n", lines);
				Console.WriteLine(actual);
				await Assert.That(actual).IsEqualTo(ExpectedScaledSnapshot);
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		private const string ExpectedScaledSnapshot =
			"x2 button 140.806x64 78D9F965A809331B\n"
			+ "x2 textedit 92x41.333 435F6E92BF8E5479\n"
			+ "x2 textedit-empty 92x41.333 7E1368FA1CC08968\n"
			+ "x2 numberedit 96.008x41.333 ADF08A5D644D4BC2\n"
			+ "x2 dropdown 155.023x41.333 5DFCBC809CEA3B67\n"
			+ "x2 segmented 459x48 C2D2C5819D6CAF17\n"
			+ "stretched textedit 198x20.667 DEB504FF7F531F31\n"
			+ "stretched-narrow segmented 150x24 31FFBEDCF7C57CDD";

		private static IEnumerable<(string, Func<ThemeConfig, GuiWidget>)> Widgets()
		{
			yield return ("button", theme => new ThemedTextButton("Button", theme));
			yield return ("textedit", theme => new ThemedTextEditWidget("Text", theme, pixelWidth: 80));
			yield return ("textedit-empty", theme => new ThemedTextEditWidget("", theme, pixelWidth: 80, messageWhenEmptyAndNotSelected: "Hint"));
			yield return ("numberedit", theme => new ThemedNumberEdit(12, theme, 'X', "mm", pixelWidth: 60));
			yield return ("dropdown", theme =>
			{
				var dropDown = new DropDownList("None", theme.TextColor, pointSize: theme.DefaultFontSize) { MinimumSize = new Vector2(100, 0) };
				dropDown.AddItem("Alpha");
				dropDown.AddItem("Beta");
				dropDown.SelectedIndex = 1;
				return dropDown;
			});
			yield return ("segmented", theme => new SegmentedControl(new[] { "Fast", "Medium", "Best" }, theme, 1));
		}

		/// <summary>
		/// Draws <paramref name="widget"/> inside a padded host filled with the theme background and returns its
		/// size and a hash of the pixels. Hover points the mouse at the widget's left quarter, which on the
		/// segmented control is the unselected first segment.
		/// </summary>
		private static string Snapshot(GuiWidget widget, ThemeConfig theme, bool hover)
		{
			var host = new GuiWidget(Math.Ceiling(widget.Width) + HostPad * 2, Math.Ceiling(widget.Height) + HostPad * 2)
			{
				BackgroundColor = theme.BackgroundColor,
			};
			widget.Position = new Vector2(HostPad, HostPad);
			host.AddChild(widget);
			if (hover)
			{
				host.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, HostPad + widget.Width / 4, HostPad + widget.Height / 2, 0));
			}

			var image = new ImageBuffer((int)host.Width, (int)host.Height);
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			host.OnDraw(graphics2D);
			var hash = Convert.ToHexString(SHA256.HashData(image.GetBuffer())).Substring(0, 16);
			return $"{widget.Width:0.###}x{widget.Height:0.###} {hash}";
		}
	}
}
