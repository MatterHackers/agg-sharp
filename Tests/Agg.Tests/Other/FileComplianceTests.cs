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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Tests that all source files in agg-sharp conform to file size limits.
	/// File size is measured as count of non-empty lines (excluding blank lines and whitespace-only lines).
	/// Default limit: 800 non-empty lines. Legacy files may have explicit higher limits.
	/// </summary>
	public class FileComplianceTests
	{
		/// <summary>
		/// Default maximum non-empty lines for any source file.
		/// </summary>
		private const int DefaultLineLimit = 800;

		/// <summary>
		/// Explicit file size limits for specific legacy files (frozen at current size).
		/// Paths are relative to the project root using forward slashes.
		/// NOTE: Remove any entry from this dict if the file is refactored to 800 lines or less.
		/// Limits should only ever decrease, never increase.
		/// </summary>
		private static readonly Dictionary<string, int> ExplicitFileLimits = new()
		{
			// Legacy files frozen at their current non-empty line count.
			// Add entries here only for files that already exceed the default limit.
			// NOTE: Remove any entry when the file is refactored to 800 lines or less.
			// Limits should only ever decrease, never increase.
			["agg/Font/LiberationSansBoldFont.cs"] = 2751,
			["agg/Font/LiberationSansFont.cs"] = 2774,
			["agg/Graphics2D.cs"] = 995,
			["agg/Image/Blenders/rgb.cs"] = 1714,
			["agg/Image/Blenders/rgba.cs"] = 1921,
			["agg/Image/ImageBuffer.cs"] = 1290,
			["agg/Image/ImageBufferFloat.cs"] = 838,
			["agg/Image/ImageTgaIO.cs"] = 834,
			["agg/Image/RecursiveBlur.cs"] = 1100,
			["agg/ImageLineRenderer.cs"] = 955,
			["agg/LcdCoverage/LcdBuffer.cs"] = 803,
			["agg/OutlineRenderer.cs"] = 1870,
			["agg/Primitives/Color.cs"] = 887,
			["agg/Spans/agg_span_image_filter_rgba.cs"] = 1141,
			["agg/VertexSource/agg_curves.cs"] = 1239,
			["agg/VertexSource/agg_gsv_text.cs"] = 846,
			["agg/VertexSource/VertexStorage.cs"] = 1250,
			["examples/PolygonClipping/GreatBritanPathStorage.cs"] = 1886,
			["examples/PolygonPathing/GreatBritanPathStorage.cs"] = 1886,
			["examples/PolygonPathing/PolygonPathing.cs"] = 890,
			["Gui/GUIWidget.cs"] = 3953,
			["Gui/Menu/PopupMenu.cs"] = 1214,
			["Gui/TextWidgets/InternalTextEditWidget.cs"] = 1537,
			["GuiAutomation/AutomationRunner.cs"] = 2023,
			["MarkdigAgg/MarkdownWidget.cs"] = 989,
			["PlatformBrowser/browser/BrowserSystemWindow.cs"] = 1120,
			["PlatformLinux/linux/X11Selection.cs"] = 1180,
			["PlatformLinux/linux/X11SystemWindow.cs"] = 3172,
			["PlatformLinux/linux/Xlib.cs"] = 949,
			["PlatformMac/mac/MacSystemWindow.cs"] = 2795,
			["PlatformWin32/win32/WinformsSystemWindow.cs"] = 1651,
			["PolygonMesh/Csg/ManifoldKernel.cs"] = 1302,
			["PolygonMesh/Mesh.cs"] = 2435,
			["PolygonMesh/RayTracer/Primitive/Shapes/Cylinder.cs"] = 891,
			["RenderGl/GL/GL.cs"] = 847,
			["RenderGl/Renderer/Graphics2DGpu.cs"] = 1159,
			["RenderGl/Scene/WebGpuSceneRenderer.cs"] = 3020,
			["Tesselate/ActiveRegion.cs"] = 1515,
			["Tesselate/mesh.cs"] = 908,
			["Tesselate/Tesselator.cs"] = 862,
			["Tests/Agg.Tests/Agg Automation Tests/FlowLayoutTests.cs"] = 2010,
			["Tests/Agg.Tests/Agg Automation Tests/MouseInteractionTests.cs"] = 1414,
			["Tests/Agg.Tests/Agg Automation Tests/TextEditTests.cs"] = 914,
			["Tests/Agg.Tests/Agg.PolygonMesh/ManifoldRustBackendTests.cs"] = 971,
			["Tests/Agg.Tests/Agg.UI/AnchorTests.cs"] = 880,
			["Tests/Agg.Tests/Agg.UI/LcdBackbufferTests.cs"] = 954,
			["Tests/Agg.Tests/Agg.UI/MenuBarWidgetTests.cs"] = 865,
			["VectorMath/Matrix4x4.cs"] = 1291,
			["VectorMath/Vector2.cs"] = 1051,
			["VectorMath/Vector3.cs"] = 1304,
			["VectorMath/Vector3Float.cs"] = 1157,
			["VectorMath/Vector4.cs"] = 863,
			["WebGpuRender/WebGpuRenderDevice.cs"] = 2258
		};

		/// <summary>
		/// Directory names to exclude from scanning.
		/// </summary>
		private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
		{
			"bin",
			"obj",
			".git",
			".vs",
			".vscode",
			".claude",
			".cursor",
			"node_modules",
			"Submodules",
			"TestResults",
			"packages",
		};

		/// <summary>
		/// Directory paths, relative to the project root and using forward slashes, to exclude from scanning.
		/// </summary>
		private static readonly HashSet<string> ExcludedRelativeDirectories = new(StringComparer.OrdinalIgnoreCase)
		{
			// Vendored third-party code: we track upstream rather than restructure it, so its file sizes are not ours to police.
			"Typography", // Typography (LayoutFarm) OpenType reader
			"geometry3Sharp", // geometry3Sharp (gradientspace) mesh library
			"clipper_library", // Angus Johnson's Clipper polygon library
			"Triangle", // Triangle.NET port of Shewchuk's Triangle
			"Tesselate/C5", // C5 Generic Collection Library
			// Emitted by WebGpu/generator from webgpu.h; regenerated, never edited by hand.
			"WebGpu/Generated",
		};

		/// <summary>
		/// File extensions to include in scanning.
		/// </summary>
		private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".cs",
		};

		[Test]
		public async Task AllFilesShouldComplyWithSizeLimits()
		{
			var projectRoot = ResolveProjectRoot();
			var files = GetAllProjectFiles(projectRoot);
			var violations = new List<string>();

			foreach (var filePath in files)
			{
				var lineCount = CountNonEmptyLines(filePath);
				var relativePath = GetRelativePath(projectRoot, filePath);
				var limit = GetFileLimit(relativePath);

				if (lineCount > limit)
				{
					violations.Add($"  {relativePath}: {lineCount} non-empty lines (limit: {limit}) - this must be refactored into multiple smaller files. Use the file-size-refactoring skill.");
				}
			}

			if (violations.Count > 0)
			{
				var message = new StringBuilder();
				message.AppendLine($"File size violations found ({violations.Count} files exceed their limits):");
				message.AppendLine();
				foreach (var violation in violations.OrderByDescending(v => v))
				{
					message.AppendLine(violation);
				}

				message.AppendLine();
				message.AppendLine("To fix: Refactor oversized files into smaller, cohesive modules.");
				message.AppendLine("See the file-size-refactoring skill (.claude/skills/file-size-refactoring/SKILL.md) for strategies.");

				Assert.Fail(message.ToString());
			}
		}

		[Test]
		public async Task ComplianceSummaryReport()
		{
			var projectRoot = ResolveProjectRoot();
			var files = GetAllProjectFiles(projectRoot);

			// Count files by extension
			var fileCounts = files
				.GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
				.OrderBy(g => g.Key)
				.ToDictionary(g => g.Key, g => g.Count());

			var violations = new List<(string Path, int Lines, int Limit)>();

			foreach (var filePath in files)
			{
				var lineCount = CountNonEmptyLines(filePath);
				var relativePath = GetRelativePath(projectRoot, filePath);
				var limit = GetFileLimit(relativePath);

				if (lineCount > limit)
				{
					violations.Add((relativePath, lineCount, limit));
				}
			}

			// Output summary
			Console.WriteLine();
			Console.WriteLine("=== File Compliance Summary ===");
			Console.WriteLine($"  Total files analyzed: {files.Count}");
			foreach (var (ext, count) in fileCounts)
			{
				Console.WriteLine($"    {ext}: {count} files");
			}

			if (violations.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine($"  VIOLATIONS: {violations.Count}");
				foreach (var (path, lines, limit) in violations.OrderByDescending(v => v.Lines))
				{
					var excess = lines - limit;
					Console.WriteLine($"    {path}: {lines} lines (limit: {limit}, {excess} over)");
				}
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine("  All files comply with size limits!");
			}

			Console.WriteLine("===============================");

			// This test always passes - it's informational
			await Assert.That(files.Count).IsGreaterThan(0);
		}

		[Test]
		public async Task ScanExcludesVendoredAndBuildDirectories()
		{
			var root = CreateScratchRoot();
			try
			{
				WriteSourceFile(Path.Combine(root, "Kept.cs"));
				WriteSourceFile(Path.Combine(root, "Tesselate", "KeptToo.cs"));
				WriteSourceFile(Path.Combine(root, "Tesselate", "C5", "Vendored.cs"));
				WriteSourceFile(Path.Combine(root, "Typography", "Typography.OpenFont", "Vendored.cs"));
				WriteSourceFile(Path.Combine(root, "obj", "Generated.cs"));

				var files = GetAllProjectFiles(root);

				await Assert.That(files.Select(f => Path.GetFileName(f)).ToList()).IsEquivalentTo(new List<string> { "Kept.cs", "KeptToo.cs" }, CollectionOrdering.Any);
			}
			finally
			{
				Directory.Delete(root, true);
			}
		}

		[Test]
		public async Task ScanToleratesDirectoryDeletedMidWalk()
		{
			var root = CreateScratchRoot();
			try
			{
				// Reproduces the state left by a race: a directory showed up in the parent's listing and was
				// deleted (by a build or another test) before the scan descended into it.
				var vanished = Path.Combine(root, "VanishedMidWalk");
				var files = new List<string>();

				ScanDirectory(root, vanished, files);

				await Assert.That(files.Count).IsEqualTo(0);
			}
			finally
			{
				Directory.Delete(root, true);
			}
		}

		private static string CreateScratchRoot()
		{
			var root = Path.Combine(Path.GetTempPath(), "FileComplianceTests", Path.GetRandomFileName());
			Directory.CreateDirectory(root);
			return root;
		}

		private static void WriteSourceFile(string filePath)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(filePath));
			File.WriteAllText(filePath, "// scan fixture" + Environment.NewLine);
		}

		/// <summary>
		/// Count non-empty lines in a file (excluding blank lines and whitespace-only lines).
		/// </summary>
		/// <exception cref="IOException">
		/// The file could not be read. Deliberately NOT swallowed: this used to answer zero for anything it
		/// could not open, which is a silent pass for every oversized file that happened to be locked by an
		/// editor or a build while the scan ran - the one failure mode a size gate must not have. A file that
		/// vanished mid-walk is a different thing and is handled by the caller, which never offers one.
		/// </exception>
		private static int CountNonEmptyLines(string filePath)
		{
			try
			{
				var lines = File.ReadAllLines(filePath);
				return lines.Count(line => !string.IsNullOrWhiteSpace(line));
			}
			catch (Exception exception) when (exception is FileNotFoundException || exception is DirectoryNotFoundException)
			{
				// Gone between the walk that listed it and this read: a build or test's scratch file, and
				// nothing to measure. Every OTHER failure - a lock, a denial - is a file that still exists
				// and still has to be checked, so it falls through to the throw below.
				return 0;
			}
			catch (Exception exception)
			{
				throw new IOException(
					$"{filePath} could not be read, so its size could not be checked. Close whatever holds it open and run again.",
					exception);
			}
		}

		/// <summary>
		/// Get the line limit for a specific file path.
		/// </summary>
		private static int GetFileLimit(string relativePath)
		{
			// Normalize path separators to forward slashes for comparison
			var normalizedPath = relativePath.Replace('\\', '/');

			foreach (var (explicitPath, explicitLimit) in ExplicitFileLimits)
			{
				var normalizedExplicit = explicitPath.Replace('\\', '/');
				if (normalizedPath.Equals(normalizedExplicit, StringComparison.OrdinalIgnoreCase)
					|| normalizedPath.EndsWith("/" + normalizedExplicit, StringComparison.OrdinalIgnoreCase))
				{
					return explicitLimit;
				}
			}

			return DefaultLineLimit;
		}

		/// <summary>
		/// Get all relevant project files for testing.
		/// </summary>
		private static List<string> GetAllProjectFiles(string projectRoot)
		{
			var files = new List<string>();
			ScanDirectory(projectRoot, projectRoot, files);
			return files;
		}

		private static void ScanDirectory(string projectRoot, string directory, List<string> files)
		{
			// Skip excluded directories
			if (IsExcludedDirectory(projectRoot, directory))
			{
				return;
			}

			// Add matching files
			try
			{
				foreach (var file in Directory.GetFiles(directory))
				{
					var extension = Path.GetExtension(file);
					if (IncludedExtensions.Contains(extension))
					{
						files.Add(file);
					}
				}

				// Recurse into subdirectories
				foreach (var subDir in Directory.GetDirectories(directory))
				{
					ScanDirectory(projectRoot, subDir, files);
				}
			}
			catch (UnauthorizedAccessException)
			{
				// Skip directories we can't access
			}
			catch (DirectoryNotFoundException)
			{
				// A concurrent build or test can delete a directory between the parent's listing and this
				// descent. The directory is gone, so there is nothing left in it to measure.
			}
		}

		/// <summary>
		/// True when the directory is excluded either by name anywhere in the tree or by its path
		/// relative to the project root.
		/// </summary>
		private static bool IsExcludedDirectory(string projectRoot, string directory)
		{
			if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
			{
				return true;
			}

			var relativePath = Path.GetRelativePath(projectRoot, directory).Replace('\\', '/');
			return ExcludedRelativeDirectories.Contains(relativePath);
		}

		/// <summary>
		/// Get a relative path from the project root to the given file.
		/// </summary>
		private static string GetRelativePath(string basePath, string fullPath)
		{
			var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
			var fullUri = new Uri(fullPath);
			return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
		}

		/// <summary>
		/// Resolve the project root directory from this source file's location.
		/// </summary>
		private static string ResolveProjectRoot([CallerFilePath] string sourceFilePath = null)
		{
			// This file is at Tests/Agg.Tests/Other/FileComplianceTests.cs
			// Walk up 3 levels to get to the agg-sharp repo root
			return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath), "..", "..", ".."));
		}
	}
}
