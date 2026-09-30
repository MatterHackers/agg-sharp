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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// Opt-in: renders every case of the full resvg test suite with agg/Svg in software and scores it against its
	/// reference .png two ways, then writes a markdown and CSV report. The pass rule is <see cref="SvgTolerantCompare"/>:
	/// the drawing must be right, but antialiased edges and gradients may differ by a few shades, because agg keeps its
	/// exact-area edge coverage rather than copying tiny-skia's supersampling and gradient rounding. The strict score,
	/// <see cref="SvgCompare"/> (agg-gui's compare.rs rule, the one the SVG Test window uses), is reported beside it. It mirrors agg-gui's AGG_GUI_SVG_REGRESSION runner
	/// (agg-gui/tests/svg_regression.rs).
	///
	/// It is also a ratchet: TestData/Svg/resvg-suite-passing.txt lists every case that passes. The run fails when a
	/// listed case stops passing, and when a case passes that is not listed - the list is updated in the change that
	/// makes it pass (agg-gui's both-ways KNOWN_INCOMPLETE rule, as a pass list). Each run writes the list it
	/// measured beside the report, so updating it is a copy.
	///
	/// The suite is not vendored; point AGG_SVG_SUITE at a checkout (its root or its tests/ folder), e.g.
	///   AGG_SVG_SUITE=~/Development/rust-apps/agg-gui/tests/resvg-test-suite \
	///   dotnet bin/Debug/Agg.Tests.dll --treenode-filter "/*/*/SvgSuiteReport/*"
	/// Optional: AGG_SVG_REPORT_DIR (default: a temp folder), AGG_SVG_FILTER (substring of the case path),
	/// AGG_SVG_TIMEOUT (seconds per case, default 20), AGG_SVG_PARALLEL (default: processor count).
	/// </summary>
	public class SvgSuiteReport
	{
		private record CaseResult(string Name, string Group, string Feature, bool Pass, bool StrictPass, double StrictRatio, double Ratio, double CoverageDelta, int MaxDelta, bool BlankRender, string Error, double Milliseconds);

		[Test]
		public async Task RenderTheResvgSuiteAndWriteAReport()
		{
			string suite = Environment.GetEnvironmentVariable("AGG_SVG_SUITE");
			if (string.IsNullOrEmpty(suite))
			{
				Skip.Test("Set AGG_SVG_SUITE to a resvg-test-suite checkout to run the full SVG suite report.");
			}

			string testsRoot = Directory.Exists(Path.Combine(suite, "tests")) ? Path.Combine(suite, "tests") : suite;
			suiteFonts = SuiteFonts(Path.Combine(testsRoot, "..", "fonts"));
			string reportDir = Environment.GetEnvironmentVariable("AGG_SVG_REPORT_DIR") ?? Path.Combine(Path.GetTempPath(), "agg-svg-suite");
			string filter = Environment.GetEnvironmentVariable("AGG_SVG_FILTER");
			var timeout = TimeSpan.FromSeconds(ReadNumber("AGG_SVG_TIMEOUT", 20));
			int parallel = (int)ReadNumber("AGG_SVG_PARALLEL", Environment.ProcessorCount);

			List<string> cases = Directory.EnumerateFiles(testsRoot, "*.svg", SearchOption.AllDirectories)
				.Where(svg => File.Exists(Path.ChangeExtension(svg, ".png")))
				.Select(svg => Path.GetRelativePath(testsRoot, svg).Replace('\\', '/'))
				.Where(name => filter == null || name.Contains(filter))
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToList();

			var results = new ConcurrentBag<CaseResult>();
			var total = Stopwatch.StartNew();
			using (var gate = new SemaphoreSlim(Math.Max(1, parallel)))
			{
				await Task.WhenAll(cases.Select(async name =>
				{
					await gate.WaitAsync();
					try
					{
						results.Add(await RunCase(testsRoot, name, timeout));
					}
					finally
					{
						gate.Release();
					}
				}));
			}

			total.Stop();
			Directory.CreateDirectory(reportDir);
			var ordered = results.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
			File.WriteAllText(Path.Combine(reportDir, "svg-suite-report.csv"), Csv(ordered));
			File.WriteAllText(Path.Combine(reportDir, "svg-suite-report.md"), Markdown(ordered, testsRoot, total.Elapsed));
			Console.WriteLine($"SVG suite: {ordered.Count(r => r.Pass)} of {ordered.Count} pass ({ordered.Count(r => r.StrictPass)} under agg-gui's strict rule) in {total.Elapsed.TotalSeconds:F1}s. Report: {reportDir}");
			CheckThePassList(ordered, new HashSet<string>(cases, StringComparer.Ordinal), filter == null, reportDir);
		}

		/// <summary>
		/// The ratchet. Only the cases this run rendered are judged, so an AGG_SVG_FILTER run checks its slice; an
		/// unfiltered run also fails on listed cases the suite no longer has.
		/// </summary>
		private static void CheckThePassList(List<CaseResult> results, HashSet<string> cases, bool wholeSuite, string reportDir)
		{
			string listPath = PassListPath();
			var listed = new HashSet<string>(File.ReadAllLines(listPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')), StringComparer.Ordinal);
			var passing = results.Where(r => r.Pass).Select(r => r.Name).ToList();

			// Listed cases this run did not render keep their place, so a filtered run writes a whole list too.
			string measuredPath = Path.Combine(reportDir, "resvg-suite-passing.txt");
			var measured = listed.Where(name => !wholeSuite && !cases.Contains(name)).Concat(passing);
			File.WriteAllLines(measuredPath, measured.Distinct().OrderBy(n => n, StringComparer.Ordinal));

			var regressed = results.Where(r => !r.Pass && listed.Contains(r.Name)).Select(r => r.Name + (r.Error != null ? " (" + r.Error + ")" : "")).ToList();
			var unlisted = passing.Where(name => !listed.Contains(name)).ToList();
			var missing = wholeSuite ? listed.Where(name => !cases.Contains(name)).ToList() : new List<string>();
			if (regressed.Count + unlisted.Count + missing.Count == 0)
			{
				return;
			}

			var message = new StringBuilder($"The SVG suite no longer matches {listPath}.");
			void List(string title, List<string> names)
			{
				if (names.Count > 0)
				{
					message.Append($"\n{title} ({names.Count}):\n  ").Append(string.Join("\n  ", names));
				}
			}

			List("Listed as passing but failed - a regression", regressed);
			List("Passed but not listed - add them to the list in this change", unlisted);
			List("Listed but not in the suite", missing);
			message.Append($"\nThe list this run measured is {measuredPath}.");
			throw new Exception(message.ToString());
		}

		/// <summary>The pass list in the repo tree, found by walking up from the test binary like the rest of TestData.</summary>
		private static string PassListPath()
		{
			string probe = AppContext.BaseDirectory;
			for (int up = 0; up < 8 && probe != null; up++)
			{
				string candidate = Path.Combine(probe, "TestData", "Svg", "resvg-suite-passing.txt");
				if (File.Exists(candidate))
				{
					return candidate;
				}

				probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar));
			}

			throw new FileNotFoundException("Could not find TestData/Svg/resvg-suite-passing.txt above " + AppContext.BaseDirectory);
		}

		/// <summary>The suite's fonts folder, set up as resvg's integration tests set up fontdb, or null when it is missing.</summary>
		private static SvgFontSet suiteFonts;

		/// <summary>
		/// resvg's tests load only the suite's fonts, with these generic families; a family list that matches nothing
		/// falls back to serif, so unstyled text is Noto Serif.
		/// </summary>
		public static SvgFontSet SuiteFonts(string fontsFolder)
		{
			if (!Directory.Exists(fontsFolder))
			{
				return null;
			}

			var fonts = new SvgFontSet { Serif = "Noto Serif", SansSerif = "Noto Sans", Cursive = "Yellowtail", Fantasy = "Sedgwick Ave Display", Monospace = "Noto Mono" };
			fonts.AddFolder(fontsFolder);
			return fonts;
		}

		private static double ReadNumber(string variable, double fallback)
		{
			return double.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
		}

		/// <summary>
		/// One case, on a worker thread so a hang is cut off at <paramref name="timeout"/>. A hung render keeps its
		/// thread until the process exits - there is no safe way to abort it - but the report still completes.
		/// </summary>
		private static async Task<CaseResult> RunCase(string testsRoot, string name, TimeSpan timeout)
		{
			string[] parts = name.Split('/');
			string group = parts[0];
			string feature = parts.Length > 2 ? parts[0] + "/" + parts[1] : parts[0];
			var watch = Stopwatch.StartNew();
			Task<CaseResult> work = Task.Run(() =>
			{
				string svgPath = Path.Combine(testsRoot, name);
				ImageBuffer reference = ImageIO.LoadImage(Path.ChangeExtension(svgPath, ".png"));
				ImageBuffer render = Render(svgPath, reference.Width, reference.Height);
				string dump = Environment.GetEnvironmentVariable("AGG_SVG_DUMP");
				if (!string.IsNullOrEmpty(dump))
				{
					Directory.CreateDirectory(dump);
					string pngPath = Path.Combine(dump, name.Replace('/', '_').Replace(".svg", ".png"));
					ImageIO.SaveImageData(pngPath, render);
				}

				byte[] rendered = SvgCompare.ToRgba(render);
				byte[] expected = SvgCompare.ToRgba(reference);
				SvgCompareResult strict = SvgCompare.Compare(rendered, expected);
				SvgTolerantCompareResult score = SvgTolerantCompare.Compare(rendered, expected, reference.Width);
				bool blank = true;
				for (int i = 3; i < rendered.Length && blank; i += 4)
				{
					blank = rendered[i] == 0;
				}

				return new CaseResult(name, group, feature, score.Pass, strict.Pass, strict.Ratio, score.Ratio, score.CoverageDelta, strict.MaxDelta, blank, null, 0);
			});

			Task finished = await Task.WhenAny(work, Task.Delay(timeout));
			double ms = watch.Elapsed.TotalMilliseconds;
			if (finished != work)
			{
				return new CaseResult(name, group, feature, false, false, 1, 1, 1, 255, false, $"timeout after {timeout.TotalSeconds:F0}s", ms);
			}

			try
			{
				return (await work) with { Milliseconds = ms };
			}
			catch (Exception e)
			{
				return new CaseResult(name, group, feature, false, false, 1, 1, 1, 255, false, $"{e.GetType().Name}: {e.Message}", ms);
			}
		}

		/// <summary>
		/// The SVG Test window's render (<see cref="SvgTestSample.RenderSvg"/>), with hrefs read from disk beside the
		/// case instead of from the demo's embedded resources, and the window's font resolver (Noto Sans for
		/// "Noto Sans"/sans-serif, agg/Svg's Liberation Sans for everything else).
		/// </summary>
		private static ImageBuffer Render(string svgPath, int width, int height)
		{
			string folder = Path.GetDirectoryName(svgPath);
			SvgDocument document = SvgDocument.Parse(File.ReadAllText(svgPath));
			document.ImageDecoder = bytes => ImageIO.LoadImage(new MemoryStream(bytes));
			document.ResourceResolver = href =>
			{
				try
				{
					string path = Path.GetFullPath(Path.Combine(folder, Uri.UnescapeDataString(href)));
					return File.Exists(path) ? File.ReadAllBytes(path) : null;
				}
				catch (Exception)
				{
					return null;
				}
			};
			document.FontResolver = SvgTestSample.ResolveFont;
			document.Fonts = suiteFonts;
			return SvgRenderer.RenderToImage(document, width, height);
		}

		private static string Csv(List<CaseResult> results)
		{
			var csv = new StringBuilder("case,group,feature,pass,strict_pass,strict_mismatch_ratio,mismatch_ratio,coverage_delta,max_delta,blank_render,ms,error\n");
			foreach (CaseResult r in results)
			{
				csv.Append(CultureInfo.InvariantCulture, $"\"{r.Name}\",{r.Group},{r.Feature},{r.Pass},{r.StrictPass},{r.StrictRatio:F6},{r.Ratio:F6},{r.CoverageDelta:F6},{r.MaxDelta},{r.BlankRender},{r.Milliseconds:F0},\"{r.Error?.Replace("\"", "'").Replace('\n', ' ')}\"\n");
			}

			return csv.ToString();
		}

		private static string Markdown(List<CaseResult> results, string testsRoot, TimeSpan elapsed)
		{
			static string Percent(int part, int whole) => whole == 0 ? "-" : (100.0 * part / whole).ToString("F1", CultureInfo.InvariantCulture) + "%";

			var md = new StringBuilder();
			int passed = results.Count(r => r.Pass);
			int strictPassed = results.Count(r => r.StrictPass);
			SvgTolerance t = SvgTolerance.Default;
			md.AppendLine("# agg/Svg vs the resvg test suite");
			md.AppendLine();
			md.AppendLine($"Suite: `{testsRoot}`, {results.Count} cases.");
			md.AppendLine();
			md.AppendLine(CultureInfo.InvariantCulture, $"- Pass rule, tolerant (SvgTolerantCompare): pixels within {t.InteriorTolerance}/255 anywhere and {t.EdgeTolerance}/255 on the reference's edges, at most {t.BadPixelRatio * 100:0.##}% of pixels outside that, coverage within {t.CoverageTolerance * 100:0.##}% of the reference's. Passed {passed} ({Percent(passed, results.Count)}).");
			md.AppendLine($"- Strict (SvgCompare, agg-gui compare.rs; at most 0.1% of pixels miss exactly): passed {strictPassed} ({Percent(strictPassed, results.Count)}).");
			md.AppendLine();
			md.AppendLine($"Crashed or timed out {results.Count(r => r.Error != null)}. Runtime {elapsed.TotalSeconds:F1}s.");
			md.AppendLine();

			// The mismatch buckets hint at the cause: a near miss is usually antialiasing or a one-level colour
			// difference, a blank render or a large miss is usually an unsupported feature.
			var failures = results.Where(r => !r.Pass && r.Error == null).ToList();
			md.AppendLine("## Failures by size of miss");
			md.AppendLine();
			md.AppendLine("| kind | cases |");
			md.AppendLine("|---|---|");
			md.AppendLine($"| near miss (<= 1% of pixels) | {failures.Count(r => r.Ratio <= .01)} |");
			md.AppendLine($"| partial (1% - 10%) | {failures.Count(r => r.Ratio > .01 && r.Ratio <= .1)} |");
			md.AppendLine($"| major (> 10%), drew something | {failures.Count(r => r.Ratio > .1 && !r.BlankRender)} |");
			md.AppendLine($"| blank render (nothing drawn) | {failures.Count(r => r.BlankRender)} |");
			md.AppendLine($"| crash or timeout | {results.Count(r => r.Error != null)} |");
			md.AppendLine();

			var demoNames = new HashSet<string>(SvgTestSample.Names.Select(n => n + ".svg"));
			var demo = results.Where(r => demoNames.Contains(r.Name)).ToList();
			md.AppendLine($"SVG Test window's {demo.Count} samples: {demo.Count(r => r.Pass)} pass, {demo.Count(r => r.StrictPass)} strict.");
			md.AppendLine();

			void Table(string title, Func<CaseResult, string> key)
			{
				md.AppendLine($"## {title}");
				md.AppendLine();
				md.AppendLine("| group | cases | pass | pass % | strict pass | strict % | near miss | crash |");
				md.AppendLine("|---|---|---|---|---|---|---|---|");
				foreach (var g in results.GroupBy(key).OrderBy(g => g.Key, StringComparer.Ordinal))
				{
					int count = g.Count();
					int pass = g.Count(r => r.Pass);
					int strictPass = g.Count(r => r.StrictPass);
					md.AppendLine($"| {g.Key} | {count} | {pass} | {Percent(pass, count)} | {strictPass} | {Percent(strictPass, count)} | {g.Count(r => !r.Pass && r.Error == null && r.Ratio <= .01)} | {g.Count(r => r.Error != null)} |");
				}

				md.AppendLine();
			}

			Table("By group", r => r.Group);
			Table("By feature", r => r.Feature);

			md.AppendLine("## Worst offenders (drew something, largest share of pixels off)");
			md.AppendLine();
			foreach (CaseResult r in failures.Where(r => !r.BlankRender).OrderByDescending(r => r.Ratio).Take(40))
			{
				md.AppendLine(CultureInfo.InvariantCulture, $"- {r.Name}: {r.Ratio:P1} off, coverage {r.CoverageDelta:P1} off, max delta {r.MaxDelta}");
			}

			md.AppendLine();
			md.AppendLine("## Blank renders");
			md.AppendLine();
			foreach (CaseResult r in failures.Where(r => r.BlankRender))
			{
				md.AppendLine($"- {r.Name}");
			}

			md.AppendLine();
			md.AppendLine("## Crashes and timeouts");
			md.AppendLine();
			foreach (CaseResult r in results.Where(r => r.Error != null))
			{
				md.AppendLine($"- {r.Name}: {r.Error}");
			}

			md.AppendLine();
			md.AppendLine("## Slowest cases");
			md.AppendLine();
			foreach (CaseResult r in results.OrderByDescending(r => r.Milliseconds).Take(10))
			{
				md.AppendLine(CultureInfo.InvariantCulture, $"- {r.Name}: {r.Milliseconds:F0} ms");
			}

			return md.ToString();
		}
	}
}
