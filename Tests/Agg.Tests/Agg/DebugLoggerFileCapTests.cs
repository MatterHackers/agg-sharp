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
using System.Threading.Tasks;
using Agg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// Release never clears the log file, so a repeating error would grow it without limit. The logger
	// caps it: past the limit the file becomes the single ".previous" file and a fresh one starts.
	[NotInParallel(nameof(DebugLogger))]
	public class DebugLoggerFileCapTests
	{
		[Test]
		public async Task LogFileRollsToOnePreviousFileAtTheCap()
		{
			var folder = Path.Combine(Path.GetTempPath(), "DebugLoggerFileCapTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			var logPath = Path.Combine(folder, "debug_log.txt");
			var previousPath = Path.Combine(folder, "debug_log.previous.txt");

			var savedPath = DebugLogger.LogFilePath;
			var savedCap = DebugLogger.MaxLogFileBytes;
			try
			{
				DebugLogger.LogFilePath = logPath;
				DebugLogger.MaxLogFileBytes = 500;

				// Well over two caps' worth of lines: the file must have rolled more than once.
				for (int i = 0; i < 60; i++)
				{
					DebugLogger.LogError("DebugLoggerFileCapTests", $"repeating error {i:D3}");
				}

				await Assert.That(File.Exists(logPath)).IsTrue();
				await Assert.That(File.Exists(previousPath)).IsTrue();
				await Assert.That(new FileInfo(logPath).Length).IsLessThanOrEqualTo(500);
				await Assert.That(new FileInfo(previousPath).Length).IsLessThanOrEqualTo(500);
				await Assert.That(Directory.GetFiles(folder).Length).IsEqualTo(2);

				// The newest line is in the live file, and the oldest lines are gone for good.
				await Assert.That(File.ReadAllText(logPath)).Contains("repeating error 059");
				await Assert.That(File.ReadAllText(previousPath)).DoesNotContain("repeating error 000");
			}
			finally
			{
				DebugLogger.LogFilePath = savedPath;
				DebugLogger.MaxLogFileBytes = savedCap;
				try
				{
					Directory.Delete(folder, true);
				}
				catch
				{
					// A leftover temp folder is not a test failure
				}
			}
		}
	}
}
