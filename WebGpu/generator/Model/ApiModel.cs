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

namespace MatterHackers.WebGpu.Generator.Model
{
	/// <summary>
	/// Which header a declaration came from. Everything in <see cref="Portable"/> also exists in
	/// Emscripten's emdawnwebgpu; <see cref="Native"/> declarations are wgpu-native only and using one
	/// is a deliberate step away from browser portability.
	/// </summary>
	public enum ApiGroup
	{
		Portable,
		Native,
	}

	/// <summary>
	/// A resolved C type: how it is spelled in C#, and the size and alignment the C compiler gives it
	/// on our 64 bit targets. <see cref="StructName"/> is set only for by value struct fields, whose
	/// size has to be resolved after every struct is known.
	/// </summary>
	public sealed class TypeRef
	{
		public string CsType { get; init; }

		public string StructName { get; init; }

		public int Size { get; init; }

		public int Align { get; init; }

		public bool IsPointer { get; init; }

		public static TypeRef Primitive(string csType, int size) => new TypeRef { CsType = csType, Size = size, Align = size };

		public static TypeRef Pointer(string csType) => new TypeRef { CsType = csType + "*", Size = 8, Align = 8, IsPointer = true };

		public static TypeRef ByValueStruct(string csType) => new TypeRef { CsType = csType, StructName = csType };
	}

	public sealed class EnumEntry
	{
		public string Name { get; init; }

		public ulong Value { get; init; }
	}

	public sealed class EnumDef
	{
		public string Name { get; init; }

		public bool IsFlags { get; init; }

		public string Underlying { get; init; }

		public ApiGroup Group { get; init; }

		public List<EnumEntry> Entries { get; } = new List<EnumEntry>();
	}

	public sealed class FieldDef
	{
		public string Name { get; init; }

		public TypeRef Type { get; init; }
	}

	public sealed class StructDef
	{
		public string Name { get; init; }

		public ApiGroup Group { get; init; }

		/// <summary>False for the few structs written by hand in CoreTypes.cs; they still need layouts.</summary>
		public bool Emit { get; init; } = true;

		/// <summary>True for a C union: every member sits at offset zero and the size is the widest one.</summary>
		public bool IsUnion { get; init; }

		public List<FieldDef> Fields { get; } = new List<FieldDef>();
	}

	public sealed class HandleDef
	{
		public string Name { get; init; }

		public ApiGroup Group { get; init; }
	}

	public sealed class ParamDef
	{
		public string Name { get; init; }

		public TypeRef Type { get; init; }
	}

	public sealed class CallbackDef
	{
		public string Name { get; init; }

		public ApiGroup Group { get; init; }

		public List<ParamDef> Parameters { get; } = new List<ParamDef>();
	}

	public sealed class FunctionDef
	{
		public string Name { get; init; }

		public ApiGroup Group { get; init; }

		public TypeRef Returns { get; init; }

		public List<ParamDef> Parameters { get; } = new List<ParamDef>();
	}

	/// <summary>
	/// The whole binding surface, gathered from webgpu.yml and wgpu.h before any C# is written.
	/// </summary>
	public sealed class ApiModel
	{
		public List<EnumDef> Enums { get; } = new List<EnumDef>();

		public List<StructDef> Structs { get; } = new List<StructDef>();

		public List<HandleDef> Handles { get; } = new List<HandleDef>();

		public List<CallbackDef> Callbacks { get; } = new List<CallbackDef>();

		public List<FunctionDef> Functions { get; } = new List<FunctionDef>();

		/// <summary>Sentinel values from the spec, kept under their webgpu.h macro names.</summary>
		public List<(string Name, string CsType, string Value)> Constants { get; } = new List<(string, string, string)>();

		private readonly Dictionary<string, (int Size, int Align)> layoutCache = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

		public StructDef FindStruct(string name) => this.Structs.Find(s => string.Equals(s.Name, name, StringComparison.Ordinal));

		/// <summary>
		/// Computes the size and alignment a C compiler gives a struct on our 64 bit targets: every
		/// member sits at the next offset that satisfies its own alignment, and the total is rounded up
		/// to the widest member alignment. This is the expected value the layout tests assert against.
		/// </summary>
		public (int Size, int Align) LayoutOf(StructDef structDef)
		{
			if (this.layoutCache.TryGetValue(structDef.Name, out var cached))
			{
				return cached;
			}

			int offset = 0;
			int maxAlign = 1;
			foreach (var field in structDef.Fields)
			{
				var (size, align) = this.SizeAndAlign(field.Type);
				if (structDef.IsUnion)
				{
					offset = Math.Max(offset, size);
				}
				else
				{
					offset = RoundUp(offset, align);
					offset += size;
				}

				maxAlign = Math.Max(maxAlign, align);
			}

			var layout = (Size: Math.Max(RoundUp(offset, maxAlign), 1), Align: maxAlign);
			this.layoutCache[structDef.Name] = layout;
			return layout;
		}

		/// <summary>Resolves a member's size and alignment, recursing into by value struct members.</summary>
		public (int Size, int Align) SizeAndAlign(TypeRef type)
		{
			if (type.StructName == null)
			{
				return (type.Size, type.Align);
			}

			var nested = this.FindStruct(type.StructName)
				?? throw new InvalidOperationException($"Unknown struct '{type.StructName}' used by value");
			return this.LayoutOf(nested);
		}

		/// <summary>Field offsets of a struct, in declaration order, using the same C layout rules.</summary>
		public List<int> OffsetsOf(StructDef structDef)
		{
			var offsets = new List<int>();
			int offset = 0;
			foreach (var field in structDef.Fields)
			{
				var (size, align) = this.SizeAndAlign(field.Type);
				if (structDef.IsUnion)
				{
					offsets.Add(0);
					continue;
				}

				offset = RoundUp(offset, align);
				offsets.Add(offset);
				offset += size;
			}

			return offsets;
		}

		private static int RoundUp(int value, int align) => align <= 1 ? value : ((value + align - 1) / align) * align;
	}
}
