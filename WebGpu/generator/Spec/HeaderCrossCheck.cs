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
using System.Text.RegularExpressions;
using MatterHackers.WebGpu.Generator.Model;

namespace MatterHackers.WebGpu.Generator.Spec
{
	/// <summary>
	/// Checks the finished model against the C headers it was built from: webgpu.h for the portable
	/// surface (whose model comes from webgpu.yml, the header's own source) and wgpu.h for the
	/// wgpu-native extensions. Struct members are compared by name, order <i>and</i> C type, and entry
	/// points by full signature, because a member of the right name and the wrong width is exactly the
	/// mistake that shows up as silent memory corruption at run time rather than as a compile error.
	/// <para>
	/// The portable half is a genuine second source: the model comes from the yml and the expectations
	/// from the header. The wgpu.h half is weaker - <see cref="WgpuHeaderReader"/> built the model from
	/// that same file - but the parsing here is written separately, so it still catches a declaration or
	/// member the reader quietly dropped.
	/// </para>
	/// </summary>
	public sealed class HeaderCrossCheck
	{
		private readonly ApiModel model;

		/// <summary>Resolves a header's C type spelling to the C# type; shared with the wgpu.h reader.</summary>
		private readonly WgpuHeaderReader types;

		private readonly string portableHeader;
		private readonly string nativeHeader;

		public HeaderCrossCheck(ApiModel model, WgpuHeaderReader types, string webGpuHeaderPath, string wgpuHeaderPath)
		{
			this.model = model;
			this.types = types;
			this.portableHeader = WgpuHeaderReader.Clean(File.ReadAllText(webGpuHeaderPath));
			this.nativeHeader = WgpuHeaderReader.Clean(File.ReadAllText(wgpuHeaderPath));
		}

		public List<string> Run()
		{
			var issues = new List<string>();
			this.CheckStructs(issues, ApiGroup.Portable, "webgpu.h", this.portableHeader);
			this.CheckStructs(issues, ApiGroup.Native, "wgpu.h", this.nativeHeader);
			this.CheckEnums(issues);
			this.CheckFunctions(issues, ApiGroup.Portable, "webgpu.h", this.portableHeader);
			this.CheckFunctions(issues, ApiGroup.Native, "wgpu.h", this.nativeHeader);
			return issues;
		}

		private void CheckStructs(List<string> issues, ApiGroup group, string headerName, string header)
		{
			foreach (var structDef in this.model.Structs.Where(s => s.Group == group))
			{
				if (structDef.IsUnion)
				{
					// Synthesized from an inline anonymous union, so it has no typedef of its own; its
					// members are checked as part of the struct that declares the union.
					continue;
				}

				var match = Regex.Match(
					header,
					@"typedef struct " + structDef.Name + @"\s*\{(.*?)\n\}\s*" + structDef.Name + @"\s*;",
					RegexOptions.Singleline);
				if (!match.Success)
				{
					issues.Add($"struct {structDef.Name}: not declared in {headerName}");
					continue;
				}

				var headerFields = ParseFields(structDef.Name, match.Groups[1].Value);
				var generated = structDef.Fields;
				if (!headerFields.Select(f => f.Name).SequenceEqual(generated.Select(f => f.Name), StringComparer.Ordinal))
				{
					issues.Add($"struct {structDef.Name}: members [{string.Join(", ", generated.Select(f => f.Name))}] but {headerName} has [{string.Join(", ", headerFields.Select(f => f.Name))}]");
					continue;
				}

				for (int i = 0; i < generated.Count; i++)
				{
					string expected = this.Resolve(headerFields[i].CType, out string failure);
					if (expected == null)
					{
						issues.Add($"struct {structDef.Name}.{generated[i].Name}: {failure}");
					}
					else if (!string.Equals(expected, generated[i].Type.CsType, StringComparison.Ordinal))
					{
						issues.Add($"struct {structDef.Name}.{generated[i].Name}: generated '{generated[i].Type.CsType}' but {headerName} declares '{headerFields[i].CType.Trim()}' ({expected})");
					}
				}
			}
		}

		/// <summary>
		/// Splits a struct body into its members, keeping each one's C type spelling. An inline anonymous
		/// union is first rewritten as a member of the lifted struct the generator emits for it, which is
		/// the same rewrite <see cref="WgpuHeaderReader"/> performs.
		/// </summary>
		private static List<(string Name, string CType)> ParseFields(string structName, string body)
		{
			var union = Regex.Match(body, @"union\s*\{(.*?)\}\s*(\w+)\s*;", RegexOptions.Singleline);
			if (union.Success)
			{
				string member = structName + Naming.Pascal(union.Groups[2].Value) + " " + union.Groups[2].Value + ";";
				body = body.Remove(union.Index, union.Length).Insert(union.Index, member);
			}

			var fields = new List<(string Name, string CType)>();
			foreach (string rawLine in body.Split('\n'))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || !line.EndsWith(";", StringComparison.Ordinal))
				{
					continue;
				}

				var declaration = Regex.Match(line.TrimEnd(';').Trim(), @"^(.*?[\s\*])(\w+)$");
				if (declaration.Success)
				{
					fields.Add((declaration.Groups[2].Value, declaration.Groups[1].Value));
				}
			}

			return fields;
		}

		/// <summary>
		/// Enum values are only checked against webgpu.h. wgpu.h spells its own values as expressions
		/// (<c>1 &lt;&lt; 0</c> and unions of earlier entries) rather than literals, so restating them
		/// here would just be a second copy of the reader's evaluator checking itself.
		/// </summary>
		private void CheckEnums(List<string> issues)
		{
			foreach (var enumDef in this.model.Enums.Where(e => e.Group == ApiGroup.Portable))
			{
				foreach (var entry in enumDef.Entries)
				{
					var match = Regex.Match(this.portableHeader, enumDef.Name + "_" + Regex.Escape(entry.Name) + @"\s*=\s*(0[xX][0-9A-Fa-f]+)");
					if (!match.Success)
					{
						issues.Add($"enum {enumDef.Name}.{entry.Name}: not declared in webgpu.h");
						continue;
					}

					ulong headerValue = SpecReader.ParseInteger(match.Groups[1].Value);
					if (headerValue != entry.Value)
					{
						issues.Add($"enum {enumDef.Name}.{entry.Name}: generated 0x{entry.Value:X} but webgpu.h has 0x{headerValue:X}");
					}
				}
			}
		}

		private void CheckFunctions(List<string> issues, ApiGroup group, string headerName, string header)
		{
			var declared = ParseFunctions(header);
			var generated = this.model.Functions
				.Where(f => f.Group == group)
				.ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

			foreach (string missing in declared.Keys.Except(generated.Keys).OrderBy(n => n, StringComparer.Ordinal))
			{
				issues.Add($"function {missing}: declared in {headerName} but not generated");
			}

			foreach (string extra in generated.Keys.Except(declared.Keys).OrderBy(n => n, StringComparer.Ordinal))
			{
				issues.Add($"function {extra}: generated but not declared in {headerName}");
			}

			foreach (string name in declared.Keys.Intersect(generated.Keys).OrderBy(n => n, StringComparer.Ordinal))
			{
				var function = generated[name];
				string expected = this.Signature(declared[name], out string failure);
				if (expected == null)
				{
					issues.Add($"function {name}: {failure}");
					continue;
				}

				string actual = Signature(function.Returns?.CsType ?? "void", function.Parameters.Select(p => p.Type.CsType));
				if (!string.Equals(expected, actual, StringComparison.Ordinal))
				{
					issues.Add($"function {name}: generated '{actual}' but {headerName} declares '{expected}'");
				}
			}
		}

		/// <summary>
		/// Every <c>wgpu*</c> entry point in a cleaned header, as its return type and parameter type
		/// spellings. Names are unique across both headers, so a plain dictionary is enough.
		/// </summary>
		private static Dictionary<string, (string Returns, List<string> Parameters)> ParseFunctions(string header)
		{
			var functions = new Dictionary<string, (string, List<string>)>(StringComparer.Ordinal);
			foreach (Match match in Regex.Matches(header, @"([A-Za-z_][\w\s\*]*?)\s*\b(wgpu\w+)\s*\(([^;{}]*)\)\s*;", RegexOptions.Singleline))
			{
				string returns = Regex.Replace(match.Groups[1].Value.Trim(), @"\s+", " ");
				if (returns.Length == 0 || returns.Contains("typedef", StringComparison.Ordinal))
				{
					continue;
				}

				var parameters = new List<string>();
				foreach (string raw in match.Groups[3].Value.Split(','))
				{
					string parameter = Regex.Replace(raw.Trim(), @"\s+", " ");
					if (parameter.Length == 0 || parameter == "void")
					{
						continue;
					}

					// Drop the parameter name; an unnamed parameter is a bare type and is kept whole.
					var declaration = Regex.Match(parameter, @"^(.*?[\s\*])(\w+)$");
					parameters.Add(declaration.Success ? declaration.Groups[1].Value : parameter);
				}

				functions[match.Groups[2].Value] = (returns, parameters);
			}

			return functions;
		}

		private string Signature((string Returns, List<string> Parameters) declaration, out string failure)
		{
			string returns = this.Resolve(declaration.Returns, out failure) ?? (declaration.Returns.Trim() == "void" ? "void" : null);
			if (returns == null)
			{
				return null;
			}

			failure = null;
			var parameters = new List<string>();
			foreach (string parameter in declaration.Parameters)
			{
				string resolved = this.Resolve(parameter, out failure);
				if (resolved == null)
				{
					return null;
				}

				parameters.Add(resolved);
			}

			return Signature(returns, parameters);
		}

		private static string Signature(string returns, IEnumerable<string> parameters)
			=> returns + "(" + string.Join(", ", parameters) + ")";

		/// <summary>
		/// Resolves a header type spelling, reporting an unknown or non value type as a cross check issue
		/// rather than letting it abort the whole run.
		/// </summary>
		private string Resolve(string cType, out string failure)
		{
			failure = null;
			try
			{
				return this.types.ResolveCType(cType)?.CsType;
			}
			catch (InvalidOperationException exception)
			{
				failure = exception.Message;
				return null;
			}
		}
	}
}
