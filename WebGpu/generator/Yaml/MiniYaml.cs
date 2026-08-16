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

namespace MatterHackers.WebGpu.Generator.Yaml
{
	/// <summary>
	/// The kind of value a <see cref="YamlNode"/> holds.
	/// </summary>
	public enum YamlKind
	{
		Scalar,
		Sequence,
		Mapping,
	}

	/// <summary>
	/// A parsed YAML value: a scalar string, an ordered sequence, or a string keyed mapping.
	/// </summary>
	public sealed class YamlNode
	{
		public YamlKind Kind { get; init; }

		public string Scalar { get; init; }

		public List<YamlNode> Items { get; } = new List<YamlNode>();

		public Dictionary<string, YamlNode> Map { get; } = new Dictionary<string, YamlNode>(StringComparer.Ordinal);

		/// <summary>True when the scalar was written in quotes, which is what keeps the enum entry
		/// literally named "null" (WGPUBackendType_Null) from being read as a null placeholder.</summary>
		public bool IsQuoted { get; init; }

		public bool IsNull => this.Kind == YamlKind.Scalar && !this.IsQuoted && (this.Scalar == null || this.Scalar == "null" || this.Scalar == "~");

		/// <summary>Returns the mapping value for <paramref name="key"/>, or null when absent.</summary>
		public YamlNode Child(string key) => this.Kind == YamlKind.Mapping && this.Map.TryGetValue(key, out var value) ? value : null;

		/// <summary>Returns the scalar text of the mapping value for <paramref name="key"/>, or <paramref name="fallback"/>.</summary>
		public string Text(string key, string fallback = null)
		{
			var child = this.Child(key);
			return child == null || child.IsNull ? fallback : child.Scalar;
		}

		public bool Flag(string key) => string.Equals(this.Text(key), "true", StringComparison.Ordinal);

		/// <summary>Returns the sequence items of the mapping value for <paramref name="key"/>, empty when absent.</summary>
		public IReadOnlyList<YamlNode> List(string key)
		{
			var child = this.Child(key);
			return child == null || child.Kind != YamlKind.Sequence ? Array.Empty<YamlNode>() : child.Items;
		}

		public override string ToString() => this.Kind == YamlKind.Scalar ? this.Scalar ?? "null" : this.Kind.ToString();
	}

	/// <summary>
	/// A deliberately small YAML reader covering only the subset webgpu.yml uses: block mappings,
	/// block sequences, plain/quoted scalars, literal block scalars (`|`) and empty flow sequences
	/// (`[]`). Writing ~200 lines here keeps the generator dependency free - a full YAML library would
	/// be the only NuGet reference in the whole binding tool chain, and this input never changes shape.
	/// </summary>
	public static class MiniYaml
	{
		private readonly struct Line
		{
			public Line(int indent, string text)
			{
				this.Indent = indent;
				this.Text = text;
			}

			public int Indent { get; }

			public string Text { get; }
		}

		public static YamlNode ParseFile(string path)
		{
			return Parse(File.ReadAllLines(path));
		}

		public static YamlNode Parse(IReadOnlyList<string> rawLines)
		{
			var lines = new List<Line>();
			var rawIndents = new List<int>();
			for (int i = 0; i < rawLines.Count; i++)
			{
				string raw = rawLines[i].TrimEnd();
				int indent = 0;
				while (indent < raw.Length && raw[indent] == ' ')
				{
					indent++;
				}

				lines.Add(new Line(indent, raw.Substring(indent)));
				rawIndents.Add(indent);
			}

			var reader = new Reader(lines);
			return reader.ParseBlock(0);
		}

		private sealed class Reader
		{
			private readonly List<Line> lines;
			private int index;

			public Reader(List<Line> lines)
			{
				this.lines = lines;
			}

			private bool AtEnd => this.index >= this.lines.Count;

			private Line Current => this.lines[this.index];

			private void SkipBlankAndComments()
			{
				while (!this.AtEnd && (this.Current.Text.Length == 0 || this.Current.Text.StartsWith("#", StringComparison.Ordinal)))
				{
					this.index++;
				}
			}

			public YamlNode ParseBlock(int indent)
			{
				this.SkipBlankAndComments();
				if (this.AtEnd)
				{
					return new YamlNode { Kind = YamlKind.Scalar, Scalar = null };
				}

				bool isSequence = this.Current.Text == "-" || this.Current.Text.StartsWith("- ", StringComparison.Ordinal);
				return isSequence ? this.ParseSequence(indent) : this.ParseMapping(indent);
			}

			private YamlNode ParseSequence(int indent)
			{
				var node = new YamlNode { Kind = YamlKind.Sequence };
				while (true)
				{
					this.SkipBlankAndComments();
					if (this.AtEnd || this.Current.Indent != indent)
					{
						break;
					}

					string text = this.Current.Text;
					if (text != "-" && !text.StartsWith("- ", StringComparison.Ordinal))
					{
						break;
					}

					string rest = text == "-" ? string.Empty : text.Substring(2).TrimStart();
					int restIndent = indent + 2;
					if (rest.Length == 0)
					{
						this.index++;
						this.SkipBlankAndComments();
						node.Items.Add(this.AtEnd || this.Current.Indent <= indent
							? new YamlNode { Kind = YamlKind.Scalar, Scalar = null }
							: this.ParseBlock(this.Current.Indent));
						continue;
					}

					// Rewrite "- key: value" as a mapping line at the item's own indent so the mapping
					// parser can pick up the sibling keys that follow on later lines.
					this.lines[this.index] = new Line(restIndent, rest);
					if (SplitKey(rest, out _, out _))
					{
						node.Items.Add(this.ParseMapping(restIndent));
					}
					else
					{
						node.Items.Add(ScalarNode(rest));
						this.index++;
					}
				}

				return node;
			}

			private YamlNode ParseMapping(int indent)
			{
				var node = new YamlNode { Kind = YamlKind.Mapping };
				while (true)
				{
					this.SkipBlankAndComments();
					if (this.AtEnd || this.Current.Indent != indent)
					{
						break;
					}

					if (!SplitKey(this.Current.Text, out string key, out string value))
					{
						break;
					}

					this.index++;
					if (value == "|" || value == "|-" || value == ">" || value == ">-")
					{
						node.Map[key] = new YamlNode { Kind = YamlKind.Scalar, Scalar = this.ReadBlockScalar(indent) };
					}
					else if (value == "[]")
					{
						node.Map[key] = new YamlNode { Kind = YamlKind.Sequence };
					}
					else if (value.Length == 0)
					{
						this.SkipBlankAndComments();
						node.Map[key] = this.AtEnd || this.Current.Indent < indent || (this.Current.Indent == indent && !this.Current.Text.StartsWith("-", StringComparison.Ordinal))
							? new YamlNode { Kind = YamlKind.Scalar, Scalar = null }
							: this.ParseBlock(this.Current.Indent);
					}
					else
					{
						node.Map[key] = ScalarNode(value);
					}
				}

				return node;
			}

			private string ReadBlockScalar(int keyIndent)
			{
				var text = new List<string>();
				while (!this.AtEnd)
				{
					var line = this.Current;
					if (line.Text.Length == 0)
					{
						text.Add(string.Empty);
						this.index++;
						continue;
					}

					if (line.Indent <= keyIndent)
					{
						break;
					}

					text.Add(new string(' ', line.Indent - keyIndent - 2) + line.Text);
					this.index++;
				}

				while (text.Count > 0 && text[text.Count - 1].Length == 0)
				{
					text.RemoveAt(text.Count - 1);
				}

				return string.Join("\n", text);
			}
		}

		private static bool SplitKey(string text, out string key, out string value)
		{
			key = null;
			value = null;
			int start = text.StartsWith("\"", StringComparison.Ordinal) ? text.IndexOf('"', 1) + 1 : 0;
			if (start <= 0)
			{
				start = 0;
			}

			int colon = -1;
			for (int i = start; i < text.Length; i++)
			{
				if (text[i] == ':' && (i + 1 == text.Length || text[i + 1] == ' '))
				{
					colon = i;
					break;
				}
			}

			if (colon < 0)
			{
				return false;
			}

			key = Unquote(text.Substring(0, colon).Trim());
			value = text.Substring(colon + 1).Trim();
			return true;
		}

		private static YamlNode ScalarNode(string raw)
		{
			string text = Unquote(raw);
			return new YamlNode { Kind = YamlKind.Scalar, Scalar = text, IsQuoted = !ReferenceEquals(text, raw) };
		}

		private static string Unquote(string text)
		{
			if (text.Length >= 2
				&& ((text[0] == '"' && text[text.Length - 1] == '"') || (text[0] == '\'' && text[text.Length - 1] == '\'')))
			{
				return text.Substring(1, text.Length - 2);
			}

			return text;
		}
	}
}
