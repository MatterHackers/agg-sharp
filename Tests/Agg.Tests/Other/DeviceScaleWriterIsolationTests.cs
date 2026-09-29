/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Other
{
	/// <summary>
	/// <c>GuiWidget.DeviceScale</c> is process wide and read by nearly every layout, so SharedStateKeys says a
	/// test that writes it takes a keyless <c>[NotInParallel]</c> (exclusive of every other test). A keyed one
	/// only serializes against tests sharing its key and still runs beside everything else:
	/// CheckRadioToggleStyleTests was keyed on "DeviceScale" and "Current" and ran beside tests pumping UiThread,
	/// which ran its check boxes' queued visibility updates mid-draw.
	/// </summary>
	public class DeviceScaleWriterIsolationTests
	{
		[Test]
		public async Task EveryFileThatWritesDeviceScaleRunsItsWritersAlone()
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

				// An assignment, not a comparison or a lambda arrow; and an attribute line, not a comment naming one.
				if (Regex.IsMatch(text, @"GuiWidget\.DeviceScale\s*=[^=>]")
					&& !Regex.IsMatch(text, @"^\s*\[(?:Test,\s*)?NotInParallel\s*\]", RegexOptions.Multiline))
				{
					unguarded.Add(relative);
				}
			}

			await Assert.That(unguarded).IsEmpty()
				.Because("a test writing GuiWidget.DeviceScale needs a keyless [NotInParallel]: " + string.Join(", ", unguarded));
		}

		private static string TestsRoot([CallerFilePath] string sourceFilePath = null)
		{
			// This file is at Tests/Agg.Tests/Other/DeviceScaleWriterIsolationTests.cs
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), ".."));
		}
	}
}
