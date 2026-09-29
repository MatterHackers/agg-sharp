/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Other
{
	/// <summary>
	/// Rendering markdown queues a layout fix-up on <c>UiThread</c>, and the GUI demo's shell, window host and app
	/// all open the About window's MarkdownWidget. A test that builds one beside a test pumping UiThread has its
	/// tree laid out from two threads at once: PopupAnchorTests and MouseInteractionTests failed with "adding a
	/// child that has previously been removed" and DemoStatePersistenceTests with "take this out of the parent".
	/// So a file that builds one must take <see cref="SharedStateKeys.MarkdownWidget"/> (the UiThread key) or run
	/// alone. This checks the file, not each test, as DeviceScaleWriterIsolationTests does.
	/// </summary>
	public class MarkdownBuilderIsolationTests
	{
		[Test]
		public async Task EveryFileThatBuildsMarkdownExcludesTheUiThreadPumps()
		{
			string testsRoot = TestsRoot();
			var unguarded = new List<string>();
			foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
				if (relative.StartsWith("bin/") || relative.StartsWith("obj/"))
				{
					continue;
				}

				string text = File.ReadAllText(file);
				if (Regex.IsMatch(text, @"new\s+(MarkdownWidget|GuiDemoShell|DemoWindowHost|AggSharpDemoApp)\s*\(")
					&& !Regex.IsMatch(text, @"SharedStateKeys\.(MarkdownWidget|UiThreadAndKeyboard)|nameof\(AutomationRunner\.ShowWindowAndExecuteTests\)")
					&& !Regex.IsMatch(text, @"^\s*\[(?:Test,\s*)?NotInParallel\s*\]", RegexOptions.Multiline))
				{
					unguarded.Add(relative);
				}
			}

			await Assert.That(unguarded).IsEmpty()
				.Because("a test building a MarkdownWidget (or a GUI demo shell, host or app) needs [NotInParallel(SharedStateKeys.MarkdownWidget)]: " + string.Join(", ", unguarded));
		}

		private static string TestsRoot([CallerFilePath] string sourceFilePath = null)
		{
			// This file is at Tests/Agg.Tests/Other/MarkdownBuilderIsolationTests.cs
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), ".."));
		}
	}
}
