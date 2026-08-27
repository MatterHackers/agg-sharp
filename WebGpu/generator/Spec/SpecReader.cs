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
using System.Globalization;
using MatterHackers.WebGpu.Generator.Model;
using MatterHackers.WebGpu.Generator.Yaml;

namespace MatterHackers.WebGpu.Generator.Spec
{
	/// <summary>
	/// Turns webgpu.yml - the machine readable specification webgpu.h is itself generated from - into
	/// the binding model. Working from the yml rather than parsing the header means enum values, member
	/// types, array member pairs and function signatures all arrive structured instead of guessed at.
	/// </summary>
	public sealed class SpecReader
	{
		private readonly ApiModel model;

		public SpecReader(ApiModel model)
		{
			this.model = model;
		}

		public void Read(YamlNode spec)
		{
			this.AddCoreTypes();
			this.ReadConstants(spec);
			this.ReadEnums(spec);
			this.ReadBitflags(spec);
			this.ReadStructs(spec);
			this.ReadObjects(spec);
			this.ReadCallbacks(spec);
			this.ReadFunctions(spec);
			this.AddSpecGaps();
		}

		/// <summary>
		/// The one declaration webgpu.h carries that webgpu.yml does not describe: wgpuGetProcAddress.
		/// Its C return type is WGPUProc (a bare function pointer), which is returned as an nint because
		/// a C# delegate type could not be cast to the real signature by the caller anyway.
		/// </summary>
		private void AddSpecGaps()
		{
			var getProcAddress = new FunctionDef
			{
				Name = "wgpuGetProcAddress",
				Group = ApiGroup.Portable,
				Returns = TypeRef.PointerWidth("nint"),
			};
			getProcAddress.Parameters.Add(new ParamDef { Name = "procName", Type = TypeRef.ByValueStruct("WGPUStringView") });
			this.model.Functions.Add(getProcAddress);
		}

		/// <summary>
		/// The two structs webgpu.h declares by hand rather than from the yml. They are emitted by hand
		/// in CoreTypes.cs, but the model needs them so member layouts that use them can be sized.
		/// </summary>
		private void AddCoreTypes()
		{
			var stringView = new StructDef { Name = "WGPUStringView", Group = ApiGroup.Portable, Emit = false };
			stringView.Fields.Add(new FieldDef { Name = "data", Type = TypeRef.Pointer("byte") });
			stringView.Fields.Add(new FieldDef { Name = "length", Type = TypeRef.PointerWidth("nuint") });
			this.model.Structs.Add(stringView);

			var chained = new StructDef { Name = "WGPUChainedStruct", Group = ApiGroup.Portable, Emit = false };
			chained.Fields.Add(new FieldDef { Name = "next", Type = TypeRef.Pointer("WGPUChainedStruct") });
			chained.Fields.Add(new FieldDef { Name = "sType", Type = TypeRef.Primitive("WGPUSType", 4) });
			this.model.Structs.Add(chained);
		}

		private void ReadConstants(YamlNode spec)
		{
			foreach (var constant in spec.List("constants"))
			{
				string name = constant.Text("name");
				string value = constant.Text("value");
				var (csType, csValue) = value switch
				{
					"uint32_max" => ("uint", "uint.MaxValue"),
					"uint64_max" => ("ulong", "ulong.MaxValue"),
					"usize_max" => ("nuint", "unchecked((nuint)ulong.MaxValue)"),
					"nan" => ("float", "float.NaN"),
					_ => ("ulong", value),
				};

				this.model.Constants.Add((ConstantName(name), csType, csValue));
			}
		}

		/// <summary>webgpu.h spells constants as WGPU_SCREAMING_SNAKE; keep that so greps carry across.</summary>
		private static string ConstantName(string snake) => "WGPU_" + snake.ToUpperInvariant().Replace("__", "_");

		private void ReadEnums(YamlNode spec)
		{
			foreach (var node in spec.List("enums"))
			{
				var enumDef = new EnumDef
				{
					Name = "WGPU" + Naming.Pascal(node.Text("name")),
					Underlying = "int",
					Group = ApiGroup.Portable,
				};

				// The value of an entry is simply its index in the yml list; a `- null` placeholder burns
				// an index so that enums which have no meaningful zero start at one.
				var entries = node.List("entries");
				for (int i = 0; i < entries.Count; i++)
				{
					if (entries[i].IsNull)
					{
						continue;
					}

					enumDef.Entries.Add(new EnumEntry { Name = Naming.Pascal(entries[i].Text("name")), Value = (ulong)i });
				}

				enumDef.Entries.Add(new EnumEntry { Name = "Force32", Value = 0x7FFFFFFF });
				this.model.Enums.Add(enumDef);
			}
		}

		private void ReadBitflags(YamlNode spec)
		{
			foreach (var node in spec.List("bitflags"))
			{
				var flags = new EnumDef
				{
					Name = "WGPU" + Naming.Pascal(node.Text("name")),
					IsFlags = true,
					Underlying = "ulong",
					Group = ApiGroup.Portable,
				};

				var byName = new Dictionary<string, ulong>(StringComparer.Ordinal);
				int bit = 0;
				foreach (var entry in node.List("entries"))
				{
					string name = Naming.Pascal(entry.Text("name"));
					ulong value;
					var combination = entry.Child("value_combination");
					if (combination != null && combination.Kind == YamlKind.Sequence)
					{
						value = 0;
						foreach (var part in combination.Items)
						{
							value |= byName[Naming.Pascal(part.Scalar)];
						}
					}
					else if (bit == 0)
					{
						// The first entry is always the empty set ("none"); real bits start at 1 << 0.
						value = 0;
						bit++;
					}
					else
					{
						value = 1UL << (bit - 1);
						bit++;
					}

					byName[name] = value;
					flags.Entries.Add(new EnumEntry { Name = name, Value = value });
				}

				this.model.Enums.Add(flags);
			}
		}

		private void ReadStructs(YamlNode spec)
		{
			foreach (var node in spec.List("structs"))
			{
				string snakeName = node.Text("name");
				var structDef = new StructDef { Name = "WGPU" + Naming.Pascal(snakeName), Group = ApiGroup.Portable };
				string kind = node.Text("type");
				if (kind == "extension")
				{
					structDef.Fields.Add(new FieldDef { Name = "chain", Type = TypeRef.ByValueStruct("WGPUChainedStruct") });
				}
				else if (kind != "standalone")
				{
					structDef.Fields.Add(new FieldDef { Name = "nextInChain", Type = TypeRef.Pointer("WGPUChainedStruct") });
				}

				foreach (var member in node.List("members"))
				{
					this.AddMemberFields(structDef.Fields, member);
				}

				this.model.Structs.Add(structDef);

				if (node.Flag("free_members"))
				{
					// Structs the implementation allocates into carry a matching wgpu<Name>FreeMembers.
					var free = new FunctionDef { Name = "wgpu" + Naming.Pascal(snakeName) + "FreeMembers", Group = ApiGroup.Portable, Returns = null };
					free.Parameters.Add(new ParamDef { Name = Naming.Camel(snakeName), Type = TypeRef.ByValueStruct(structDef.Name) });
					this.model.Functions.Add(free);
				}
			}
		}

		/// <summary>
		/// Appends the C fields a yml member expands to. An `array&lt;T&gt;` member is two C fields: a
		/// size_t count named after the singular of the member, then the element pointer.
		/// </summary>
		private void AddMemberFields(List<FieldDef> fields, YamlNode member)
		{
			string snakeName = member.Text("name");
			string type = member.Text("type");
			string pointer = member.Text("pointer");
			if (type.StartsWith("array<", StringComparison.Ordinal))
			{
				string element = type.Substring("array<".Length, type.Length - "array<".Length - 1);
				fields.Add(new FieldDef { Name = Naming.ArrayCountMember(snakeName), Type = TypeRef.PointerWidth("nuint") });
				fields.Add(new FieldDef { Name = Naming.Camel(snakeName), Type = this.ResolveType(element, "immutable") });
				return;
			}

			fields.Add(new FieldDef { Name = Naming.Camel(snakeName), Type = this.ResolveType(type, pointer) });
		}

		private void ReadCallbacks(YamlNode spec)
		{
			foreach (var node in spec.List("callbacks"))
			{
				string snakeName = node.Text("name");
				string baseName = "WGPU" + Naming.Pascal(snakeName);
				var callback = new CallbackDef { Name = baseName + "Callback", Group = ApiGroup.Portable };
				foreach (var arg in node.List("args"))
				{
					callback.Parameters.Add(new ParamDef
					{
						Name = Naming.Camel(arg.Text("name")),
						Type = this.ResolveType(arg.Text("type"), arg.Text("pointer")),
					});
				}

				callback.Parameters.Add(new ParamDef { Name = "userdata1", Type = TypeRef.Pointer("void") });
				callback.Parameters.Add(new ParamDef { Name = "userdata2", Type = TypeRef.Pointer("void") });
				this.model.Callbacks.Add(callback);

				// Every callback also implies the CallbackInfo struct that carries it into the API. The
				// callback mode field exists only for the deferred (callback_mode) style; immediate style
				// callbacks such as uncaptured_error fire without one.
				var info = new StructDef { Name = baseName + "CallbackInfo", Group = ApiGroup.Portable };
				info.Fields.Add(new FieldDef { Name = "nextInChain", Type = TypeRef.Pointer("WGPUChainedStruct") });
				if (node.Text("style") == "callback_mode")
				{
					info.Fields.Add(new FieldDef { Name = "mode", Type = TypeRef.Primitive("WGPUCallbackMode", 4) });
				}

				info.Fields.Add(new FieldDef { Name = "callback", Type = TypeRef.PointerWidth(FunctionPointerType(callback)) });
				info.Fields.Add(new FieldDef { Name = "userdata1", Type = TypeRef.Pointer("void") });
				info.Fields.Add(new FieldDef { Name = "userdata2", Type = TypeRef.Pointer("void") });
				this.model.Structs.Add(info);
			}
		}

		/// <summary>The unmanaged function pointer spelling for a callback, used inside blittable structs.</summary>
		public static string FunctionPointerType(CallbackDef callback)
		{
			var parts = new List<string>();
			foreach (var parameter in callback.Parameters)
			{
				parts.Add(parameter.Type.CsType);
			}

			parts.Add("void");
			return "delegate* unmanaged[Cdecl]<" + string.Join(", ", parts) + ">";
		}

		private void ReadObjects(YamlNode spec)
		{
			foreach (var node in spec.List("objects"))
			{
				string snakeName = node.Text("name");
				string handleName = "WGPU" + Naming.Pascal(snakeName);
				this.model.Handles.Add(new HandleDef { Name = handleName, Group = ApiGroup.Portable });

				foreach (var method in node.List("methods"))
				{
					var function = new FunctionDef
					{
						Name = "wgpu" + Naming.Pascal(snakeName) + Naming.Pascal(method.Text("name")),
						Group = ApiGroup.Portable,
						Returns = this.ResolveReturn(method.Child("returns"), method.Text("callback")),
					};

					function.Parameters.Add(new ParamDef { Name = Naming.Camel(snakeName), Type = TypeRef.ByValueStruct(handleName) });
					foreach (var arg in method.List("args"))
					{
						this.AddArgumentParameters(function.Parameters, arg);
					}

					string callback = method.Text("callback");
					if (callback != null)
					{
						string info = "WGPU" + Naming.Pascal(callback.Substring("callback.".Length)) + "CallbackInfo";
						function.Parameters.Add(new ParamDef { Name = "callbackInfo", Type = TypeRef.ByValueStruct(info) });
					}

					this.model.Functions.Add(function);
				}

				// Reference counting is implicit in the yml: every object type has AddRef and Release.
				foreach (string suffix in new[] { "AddRef", "Release" })
				{
					var refCount = new FunctionDef { Name = "wgpu" + Naming.Pascal(snakeName) + suffix, Group = ApiGroup.Portable };
					refCount.Parameters.Add(new ParamDef { Name = Naming.Camel(snakeName), Type = TypeRef.ByValueStruct(handleName) });
					this.model.Functions.Add(refCount);
				}
			}
		}

		private void ReadFunctions(YamlNode spec)
		{
			foreach (var node in spec.List("functions"))
			{
				var function = new FunctionDef
				{
					Name = "wgpu" + Naming.Pascal(node.Text("name")),
					Group = ApiGroup.Portable,
					Returns = this.ResolveReturn(node.Child("returns"), null),
				};

				foreach (var arg in node.List("args"))
				{
					this.AddArgumentParameters(function.Parameters, arg);
				}

				this.model.Functions.Add(function);
			}
		}

		private void AddArgumentParameters(List<ParamDef> parameters, YamlNode arg)
		{
			string snakeName = arg.Text("name");
			string type = arg.Text("type");
			if (type.StartsWith("array<", StringComparison.Ordinal))
			{
				string element = type.Substring("array<".Length, type.Length - "array<".Length - 1);
				parameters.Add(new ParamDef { Name = Naming.ArrayCountMember(snakeName), Type = TypeRef.PointerWidth("nuint") });
				parameters.Add(new ParamDef { Name = Naming.Camel(snakeName), Type = this.ResolveType(element, "immutable") });
				return;
			}

			parameters.Add(new ParamDef { Name = Naming.Camel(snakeName), Type = this.ResolveType(type, arg.Text("pointer")) });
		}

		private TypeRef ResolveReturn(YamlNode returns, string callback)
		{
			if (callback != null)
			{
				// Every deferred-callback entry point hands back the future that tracks it.
				return TypeRef.ByValueStruct("WGPUFuture");
			}

			return returns == null ? null : this.ResolveType(returns.Text("type"), returns.Text("pointer"));
		}

		/// <summary>Maps a yml type reference (plus its optional pointer qualifier) onto a C# type.</summary>
		private TypeRef ResolveType(string type, string pointer)
		{
			bool isPointer = pointer == "immutable" || pointer == "mutable";
			if (type == "c_void")
			{
				return TypeRef.Pointer("void");
			}

			string csType = type switch
			{
				"bool" => "WGPUBool",
				"uint16" => "ushort",
				"uint32" => "uint",
				"int32" => "int",
				"uint64" => "ulong",
				"usize" => "nuint",
				"float32" => "float",
				"nullable_float32" => "float",
				"float64_supertype" => "double",
				"out_string" => "WGPUStringView",
				"nullable_string" => "WGPUStringView",
				"string_with_default_empty" => "WGPUStringView",
				_ => null,
			};

			int size = type switch
			{
				"uint16" => 2,
				"uint32" or "int32" or "float32" or "nullable_float32" or "bool" => 4,
				"uint64" or "usize" or "float64_supertype" => 8,
				_ => 0,
			};

			if (csType != null)
			{
				if (isPointer)
				{
					return TypeRef.Pointer(csType);
				}

				// usize is size_t: as wide as a pointer, not fixed at 8. Everything else in the table above
				// is a fixed-width integer or float and is the same on every target.
				if (type == "usize")
				{
					return TypeRef.PointerWidth(csType);
				}

				return size > 0 ? TypeRef.Primitive(csType, size) : TypeRef.ByValueStruct(csType);
			}

			int separator = type.IndexOf('.');
			if (separator < 0)
			{
				throw new InvalidOperationException($"Unhandled yml type '{type}'");
			}

			string kind = type.Substring(0, separator);
			string name = "WGPU" + Naming.Pascal(type.Substring(separator + 1));
			if (kind == "callback")
			{
				name += "CallbackInfo";
			}

			if (isPointer)
			{
				return TypeRef.Pointer(name);
			}

			return kind switch
			{
				"enum" => TypeRef.Primitive(name, 4),

				// A bitflag typedef is uint64_t, so it is eight bytes on every target - unlike an object,
				// which is an opaque pointer and follows the target's pointer width.
				"bitflag" => TypeRef.Primitive(name, 8),
				"object" => TypeRef.PointerWidth(name),
				_ => TypeRef.ByValueStruct(name),
			};
		}

		/// <summary>Parses a C integer literal, accepting the 0x form the headers use.</summary>
		public static ulong ParseInteger(string text)
		{
			text = text.Trim();
			if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				return ulong.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}

			return ulong.Parse(text, CultureInfo.InvariantCulture);
		}
	}
}
