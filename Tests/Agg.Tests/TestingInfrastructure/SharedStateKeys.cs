/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The one <c>[NotInParallel]</c> constraint key for each piece of process-wide state the tests mutate.
	/// TUnit only serializes tests that share a key, so two tests guarding the same static under different
	/// keys (or one keyed, one unguarded) still race - a test that touches a static below must name its key.
	/// State not listed here (<c>GuiWidget.DeviceScale</c>, <c>LcdRenderSettings</c> and
	/// <c>TypeFacePrinter</c> settings, <c>DebugLogger</c> file and filters, <c>StaticData</c>, the font
	/// caches, the <c>TextEditWidget</c> soft keyboard events) is read by so many tests that its writers
	/// take a keyless <c>[NotInParallel]</c> instead, which excludes every other test.
	/// The values equal the <c>nameof</c> forms older attributes use, so both spellings serialize together.
	/// </summary>
	public static class SharedStateKeys
	{
		/// <summary>
		/// <c>UiThread</c>, the automation runner and the global <c>Keyboard</c> key-down state. Equals
		/// <c>nameof(AutomationRunner.ShowWindowAndExecuteTests)</c>.
		/// </summary>
		public const string UiThreadAndKeyboard = "ShowWindowAndExecuteTests";

		/// <summary>
		/// <c>ThemeConfig.Current</c> - written by every <c>new DemoTheme()</c>, so by every test that builds a
		/// GUI demo window, shell or host. Equals <c>nameof(ThemeConfig.Current)</c>.
		/// </summary>
		public const string ThemeConfigCurrent = "Current";

		/// <summary>
		/// <c>MarkdownWidget.Theme</c> (written by every MarkdownWidget constructor, so by anything that builds
		/// the demo's About window) and <c>MarkdownWidget.LaunchBrowser</c>.
		/// </summary>
		public const string MarkdownWidget = "MarkdownWidget";

		/// <summary><c>Clipboard.Instance</c>, swapped by <c>Clipboard.SetSystemClipboard</c>.</summary>
		public const string Clipboard = "Clipboard";

		/// <summary><c>InputProfiles.Current</c>, which decides whether the on-screen keyboard shows.</summary>
		public const string InputProfiles = "InputProfiles";

		/// <summary><c>UrlLauncher.Provider</c> and <c>SystemAppearance.Provider</c>.</summary>
		public const string SystemServices = "SystemServices";
	}
}
