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
using System.Text.RegularExpressions;
using MatterHackers.WebGpu.Generator.Model;

namespace MatterHackers.WebGpu.Generator.Spec
{
	/// <summary>
	/// Reads wgpu-native's own extensions from wgpu.h. There is no yml for these - the release only
	/// ships the machine readable spec for the portable webgpu.h surface - so this is a deliberately
	/// narrow C reader that understands exactly the declaration shapes wgpu.h uses.
	/// </summary>
	public sealed class WgpuHeaderReader
	{
		private readonly ApiModel model;
		private readonly Dictionary<string, TypeRef> aliases = new Dictionary<string, TypeRef>(StringComparer.Ordinal);

		public WgpuHeaderReader(ApiModel model)
		{
			this.model = model;

			// The two typedefs neither header spells out as a struct, enum or object.
			this.aliases["WGPUSubmissionIndex"] = TypeRef.Primitive("ulong", 8);

			// webgpu.h's bare function pointer, only ever returned by wgpuGetProcAddress. The binding hands
			// it back as an nint because a C# delegate type could not be cast to the real signature anyway.
			this.aliases["WGPUProc"] = TypeRef.PointerWidth("nint");
		}

		public void Read(string headerPath)
		{
			string text = Clean(File.ReadAllText(headerPath));
			this.ReadEnums(text);
			this.ReadFlags(text);
			this.ReadStructs(text);
			this.ReadCallbacks(text);
			this.ReadFunctions(text);
		}

		/// <summary>
		/// Strips comments and preprocessor lines (including their line continuations). Public because the
		/// cross check reads the same headers and has to see the same text.
		/// </summary>
		public static string Clean(string text)
		{
			text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
			text = Regex.Replace(text, @"//[^\n]*", string.Empty);
			text = Regex.Replace(text, @"^[ \t]*#(?:.*\\\r?\n)*.*$", string.Empty, RegexOptions.Multiline);
			text = text.Replace("WGPU_NULLABLE", " ").Replace("WGPU_STRUCTURE_ATTRIBUTE", " ")
				.Replace("WGPU_ENUM_ATTRIBUTE", " ").Replace("WGPU_FUNCTION_ATTRIBUTE", " ").Replace("WGPU_EXPORT", " ");
			return text;
		}

		private void ReadEnums(string text)
		{
			foreach (Match match in Regex.Matches(text, @"typedef enum (\w+)\s*\{(.*?)\}\s*\1\s*;", RegexOptions.Singleline))
			{
				var enumDef = new EnumDef { Name = match.Groups[1].Value, Underlying = "int", Group = ApiGroup.Native };
				var known = new Dictionary<string, ulong>(StringComparer.Ordinal);
				ulong previous = 0;
				foreach (Match entry in Regex.Matches(match.Groups[2].Value, @"(\w+)\s*(?:=\s*([^,\n]+))?\s*(?:,|$)", RegexOptions.Multiline))
				{
					string cName = entry.Groups[1].Value;
					if (cName.Length == 0)
					{
						continue;
					}

					ulong value = entry.Groups[2].Success ? Evaluate(entry.Groups[2].Value, known) : previous + 1;
					previous = value;
					known[cName] = value;
					enumDef.Entries.Add(new EnumEntry { Name = ShortEntryName(cName), Value = value });
				}

				this.model.Enums.Add(enumDef);
			}
		}

		private void ReadFlags(string text)
		{
			foreach (Match match in Regex.Matches(text, @"typedef WGPUFlags (\w+)\s*;"))
			{
				string name = match.Groups[1].Value;
				var flags = new EnumDef { Name = name, IsFlags = true, Underlying = "ulong", Group = ApiGroup.Native };
				var known = new Dictionary<string, ulong>(StringComparer.Ordinal);
				foreach (Match entry in Regex.Matches(text, @"static const " + name + @" (\w+)\s*=\s*([^;]+);"))
				{
					string cName = entry.Groups[1].Value;
					ulong value = Evaluate(entry.Groups[2].Value, known);
					known[cName] = value;
					flags.Entries.Add(new EnumEntry { Name = ShortEntryName(cName), Value = value });
				}

				this.model.Enums.Add(flags);
			}
		}

		private void ReadStructs(string text)
		{
			foreach (Match match in Regex.Matches(text, @"typedef struct (\w+)\s*\{(.*?)\n\}\s*\1\s*;", RegexOptions.Singleline))
			{
				string name = match.Groups[1].Value;
				string body = match.Groups[2].Value;
				var structDef = new StructDef { Name = name, Group = ApiGroup.Native };

				// wgpu.h has exactly one tagged union (the display handle). Lift it into its own explicit
				// layout struct so the generated C# keeps the same size and alignment as the C original.
				var union = Regex.Match(body, @"union\s*\{(.*?)\}\s*(\w+)\s*;", RegexOptions.Singleline);
				if (union.Success)
				{
					string unionName = name + Naming.Pascal(union.Groups[2].Value);
					var unionDef = new StructDef { Name = unionName, Group = ApiGroup.Native, IsUnion = true };
					this.AddFields(unionDef, union.Groups[1].Value);
					this.model.Structs.Add(unionDef);
					body = body.Remove(union.Index, union.Length).Insert(union.Index, unionName + " " + union.Groups[2].Value + ";");
				}

				this.AddFields(structDef, body);
				this.model.Structs.Add(structDef);
			}
		}

		private void AddFields(StructDef structDef, string body)
		{
			foreach (string rawLine in body.Split('\n'))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || !line.EndsWith(";", StringComparison.Ordinal))
				{
					continue;
				}

				var declaration = Regex.Match(line.TrimEnd(';').Trim(), @"^(.*?[\s\*])(\w+)$");
				if (!declaration.Success)
				{
					continue;
				}

				structDef.Fields.Add(new FieldDef
				{
					Name = declaration.Groups[2].Value,
					Type = this.ResolveCType(declaration.Groups[1].Value),
				});
			}
		}

		private void ReadCallbacks(string text)
		{
			foreach (Match match in Regex.Matches(text, @"typedef void \(\*(\w+)\)\(([^;]*?)\)\s*;", RegexOptions.Singleline))
			{
				var callback = new CallbackDef { Name = match.Groups[1].Value, Group = ApiGroup.Native };
				foreach (var parameter in this.ParseParameters(match.Groups[2].Value))
				{
					callback.Parameters.Add(parameter);
				}

				this.model.Callbacks.Add(callback);
			}
		}

		private void ReadFunctions(string text)
		{
			foreach (Match match in Regex.Matches(text, @"([A-Za-z_][\w\s\*]*?)\s*\b(wgpu\w+)\s*\(([^;{}]*)\)\s*;", RegexOptions.Singleline))
			{
				string returnType = match.Groups[1].Value.Trim();
				if (returnType.Contains("typedef", StringComparison.Ordinal) || returnType.Length == 0)
				{
					continue;
				}

				var function = new FunctionDef
				{
					Name = match.Groups[2].Value,
					Group = ApiGroup.Native,
					Returns = returnType == "void" ? null : this.ResolveCType(returnType),
				};

				foreach (var parameter in this.ParseParameters(match.Groups[3].Value))
				{
					function.Parameters.Add(parameter);
				}

				this.model.Functions.Add(function);
			}
		}

		private List<ParamDef> ParseParameters(string text)
		{
			var parameters = new List<ParamDef>();
			foreach (string raw in text.Split(','))
			{
				string parameter = Regex.Replace(raw.Trim(), @"\s+", " ");
				if (parameter.Length == 0 || parameter == "void")
				{
					continue;
				}

				var declaration = Regex.Match(parameter, @"^(.*?[\s\*])(\w+)$");
				if (!declaration.Success)
				{
					// An unnamed parameter, spelled as a bare type.
					parameters.Add(new ParamDef { Name = "arg" + parameters.Count, Type = this.ResolveCType(parameter) });
					continue;
				}

				parameters.Add(new ParamDef
				{
					Name = Naming.Escape(declaration.Groups[2].Value),
					Type = this.ResolveCType(declaration.Groups[1].Value),
				});
			}

			return parameters;
		}

		/// <summary>
		/// Maps a C type spelling onto the C# type the binding uses. Public so the cross check can hold a
		/// header declaration's type against the generated one without a second, differently wrong, copy
		/// of these rules. Only valid once the model is populated - it resolves names against it.
		/// </summary>
		public TypeRef ResolveCType(string text)
		{
			string cleaned = Regex.Replace(text.Replace("const", " ").Replace("struct", " "), @"\s+", " ").Trim();
			int pointerDepth = 0;
			while (cleaned.EndsWith("*", StringComparison.Ordinal))
			{
				pointerDepth++;
				cleaned = cleaned.Substring(0, cleaned.Length - 1).Trim();
			}

			string baseName = cleaned.Trim();
			TypeRef resolved = baseName switch
			{
				"void" => null,
				"uint8_t" => TypeRef.Primitive("byte", 1),
				"uint16_t" => TypeRef.Primitive("ushort", 2),
				"uint32_t" => TypeRef.Primitive("uint", 4),
				"uint64_t" => TypeRef.Primitive("ulong", 8),
				"int8_t" => TypeRef.Primitive("sbyte", 1),
				"int16_t" => TypeRef.Primitive("short", 2),
				"int32_t" or "int" => TypeRef.Primitive("int", 4),
				"int64_t" => TypeRef.Primitive("long", 8),
				"size_t" => TypeRef.PointerWidth("nuint"),
				"float" => TypeRef.Primitive("float", 4),
				"double" => TypeRef.Primitive("double", 8),
				"char" => TypeRef.Primitive("byte", 1),
				"WGPUBool" => TypeRef.Primitive("WGPUBool", 4),
				"WGPUFlags" => TypeRef.Primitive("ulong", 8),
				_ => this.ResolveNamedType(baseName),
			};

			if (pointerDepth == 0)
			{
				return resolved ?? throw new InvalidOperationException($"'{text}' is not a value type");
			}

			string spelling = (resolved?.CsType ?? "void") + new string('*', pointerDepth - 1);
			return TypeRef.Pointer(spelling);
		}

		private TypeRef ResolveNamedType(string name)
		{
			if (this.aliases.TryGetValue(name, out var alias))
			{
				return alias;
			}

			var enumDef = this.model.Enums.Find(e => string.Equals(e.Name, name, StringComparison.Ordinal));
			if (enumDef != null)
			{
				return TypeRef.Primitive(name, enumDef.IsFlags ? 8 : 4);
			}

			if (this.model.Handles.Exists(h => string.Equals(h.Name, name, StringComparison.Ordinal)))
			{
				return TypeRef.PointerWidth(name);
			}

			if (this.model.Structs.Exists(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
			{
				return TypeRef.ByValueStruct(name);
			}

			var callback = this.model.Callbacks.Find(c => string.Equals(c.Name, name, StringComparison.Ordinal));
			if (callback != null)
			{
				// A callback passed straight to an entry point (rather than inside a CallbackInfo struct)
				// gets the same unmanaged function pointer spelling the struct member uses, so there is
				// exactly one way to hand wgpu a callback and no managed delegate to keep alive.
				return TypeRef.PointerWidth(SpecReader.FunctionPointerType(callback));
			}

			throw new InvalidOperationException($"Unknown wgpu.h type '{name}'");
		}

		/// <summary>
		/// wgpu.h entry names are prefixed with a type name that is not always the enum's own (the
		/// WGPUNativeSType entries are spelled WGPUSType_*), so the C# member name is whatever follows
		/// the first underscore.
		/// </summary>
		private static string ShortEntryName(string cName)
		{
			int underscore = cName.IndexOf('_');
			return underscore < 0 ? cName : cName.Substring(underscore + 1);
		}

		/// <summary>Evaluates the small constant expressions wgpu.h uses: literals, shifts and ors.</summary>
		private static ulong Evaluate(string expression, Dictionary<string, ulong> known)
		{
			ulong total = 0;
			foreach (string term in expression.Split('|'))
			{
				string cleaned = term.Replace("(", " ").Replace(")", " ").Trim();
				int shift = cleaned.IndexOf("<<", StringComparison.Ordinal);
				if (shift >= 0)
				{
					total |= Term(cleaned.Substring(0, shift), known) << (int)Term(cleaned.Substring(shift + 2), known);
				}
				else
				{
					total |= Term(cleaned, known);
				}
			}

			return total;
		}

		private static ulong Term(string text, Dictionary<string, ulong> known)
		{
			text = text.Trim();
			if (known.TryGetValue(text, out ulong value))
			{
				return value;
			}

			return SpecReader.ParseInteger(text);
		}
	}
}
