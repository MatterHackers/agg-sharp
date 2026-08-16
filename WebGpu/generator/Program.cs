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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.WebGpu.Generator.Emit;
using MatterHackers.WebGpu.Generator.Model;
using MatterHackers.WebGpu.Generator.Spec;
using MatterHackers.WebGpu.Generator.Yaml;

namespace MatterHackers.WebGpu.Generator
{
	/// <summary>
	/// Regenerates the MatterHackers.WebGpu binding from the pinned wgpu-native headers. Run by hand
	/// when the pinned version changes: <c>dotnet run --project generator</c> from the WebGpu folder.
	/// The generated sources are checked in so that nobody else has to run this.
	/// </summary>
	public static class Program
	{
		public static int Main(string[] args)
		{
			string webGpuFolder = args.Length > 0
				? Path.GetFullPath(args[0])
				: FindWebGpuFolder(AppContext.BaseDirectory);
			string headers = Path.Combine(webGpuFolder, "headers");
			string output = Path.Combine(webGpuFolder, "Generated");
			string version = ReadPinnedVersion(Path.Combine(headers, "README.md"));

			Console.WriteLine($"WebGPU binding generator - wgpu-native {version}");
			Console.WriteLine($"  headers: {headers}");
			Console.WriteLine($"  output:  {output}");

			var model = new ApiModel();
			new SpecReader(model).Read(MiniYaml.ParseFile(Path.Combine(headers, "webgpu.yml")));

			var nativeReader = new WgpuHeaderReader(model);
			nativeReader.Read(Path.Combine(headers, "wgpu.h"));

			// The reader is handed to the cross check as well: it owns the C type spelling rules, and one
			// copy of those is the whole point.
			var issues = new HeaderCrossCheck(model, nativeReader, Path.Combine(headers, "webgpu.h"), Path.Combine(headers, "wgpu.h")).Run();
			issues.AddRange(FindDuplicates(model));

			var emitter = new CSharpEmitter(model, output, version);
			emitter.EmitAll();

			Report(model, emitter);
			if (issues.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine($"{issues.Count} cross check issue(s) against the headers:");
				foreach (string issue in issues)
				{
					Console.WriteLine("  " + issue);
				}

				return 1;
			}

			Console.WriteLine();
			Console.WriteLine("Cross check against webgpu.h and wgpu.h: all struct members (name, order and type), enum values and entry point signatures agree.");
			return 0;
		}

		private static void Report(ApiModel model, CSharpEmitter emitter)
		{
			Console.WriteLine();
			Console.WriteLine($"  enums      {model.Enums.Count,4} ({model.Enums.Count(e => e.IsFlags)} bitflag sets, {model.Enums.Count(e => e.Group == ApiGroup.Native)} wgpu-native only)");
			Console.WriteLine($"  structs    {model.Structs.Count,4} ({model.Structs.Count(s => s.Group == ApiGroup.Native)} wgpu-native only)");
			Console.WriteLine($"  handles    {model.Handles.Count,4}");
			Console.WriteLine($"  callbacks  {model.Callbacks.Count,4}");
			Console.WriteLine($"  functions  {model.Functions.Count,4} ({model.Functions.Count(f => f.Group == ApiGroup.Native)} wgpu-native only)");
			Console.WriteLine($"  constants  {model.Constants.Count,4}");
			Console.WriteLine();
			foreach (string file in emitter.WrittenFiles)
			{
				Console.WriteLine($"  wrote {Path.GetFileName(file)} ({File.ReadAllLines(file).Length} lines)");
			}
		}

		/// <summary>Two declarations with one name would not compile; catch it here with a clear message.</summary>
		private static IEnumerable<string> FindDuplicates(ApiModel model)
		{
			foreach (var group in model.Functions.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
			{
				yield return $"function {group.Key}: declared {group.Count()} times";
			}

			foreach (var group in model.Structs.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
			{
				yield return $"struct {group.Key}: declared {group.Count()} times";
			}

			foreach (var group in model.Enums.GroupBy(e => e.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
			{
				yield return $"enum {group.Key}: declared {group.Count()} times";
			}
		}

		/// <summary>Walks up from the build output to the WebGpu folder so the tool runs with no arguments.</summary>
		private static string FindWebGpuFolder(string start)
		{
			var directory = new DirectoryInfo(start);
			while (directory != null)
			{
				if (Directory.Exists(Path.Combine(directory.FullName, "headers")) && File.Exists(Path.Combine(directory.FullName, "headers", "webgpu.yml")))
				{
					return directory.FullName;
				}

				directory = directory.Parent;
			}

			throw new DirectoryNotFoundException("Could not find the WebGpu folder containing headers/webgpu.yml");
		}

		/// <summary>Reads the pinned wgpu-native version out of the headers README so it lands in every generated file.</summary>
		private static string ReadPinnedVersion(string readmePath)
		{
			foreach (string line in File.ReadAllLines(readmePath))
			{
				var match = System.Text.RegularExpressions.Regex.Match(line, @"\*\*(v[\d.]+)\*\*");
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}

			return "unknown";
		}
	}
}
