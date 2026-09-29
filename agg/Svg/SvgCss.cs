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
using System.Linq;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The document's &lt;style&gt; sheets applied to its elements, as resvg applies them: type, class, id and
	/// universal selectors, compounds of them, descendant and child combinators, and selector lists. The cascade
	/// is presentation attributes, then sheet rules by specificity (the later of a tie wins), then the style
	/// attribute, then !important sheet rules. A selector it does not understand (attribute, pseudo-class) is
	/// dropped with its rule, as CSS drops an invalid selector.
	/// </summary>
	internal static class SvgCss
	{
		private static readonly string[] CssOnlyProperties = { "mix-blend-mode", "isolation" };

		/// <summary>Merges the sheets and every element's style attribute into its <see cref="SvgElement.Attributes"/>.</summary>
		public static void Apply(SvgElement root)
		{
			List<Rule> rules = root.DescendantsAndSelf()
				.Where(e => e.Name == "style" && (string.IsNullOrEmpty(e["type"]) || e["type"] == "text/css"))
				.SelectMany(e => ParseSheet(e.Text))
				.ToList();

			foreach (SvgElement element in root.DescendantsAndSelf())
			{
				// CSS-only properties (SVG 2, no presentation attribute): as attributes they mean nothing.
				foreach (string cssOnly in CssOnlyProperties)
				{
					element.Attributes.Remove(cssOnly);
				}

				// OrderBy is stable, so rules of equal specificity stay in document order: the later one wins.
				List<Rule> matched = rules.Where(r => r.Selector.Matches(element)).OrderBy(r => r.Selector.Specificity).ToList();
				foreach (Rule rule in matched)
				{
					SetAll(element, rule.Declarations.Where(d => !d.Important));
				}

				if (element["style"] is string style)
				{
					SetAll(element, ParseDeclarations(style));
				}

				foreach (Rule rule in matched)
				{
					SetAll(element, rule.Declarations.Where(d => d.Important));
				}
			}
		}

		/// <summary>The declarations of a style attribute or rule body, with "!important" noted and stripped.</summary>
		internal static List<Declaration> ParseDeclarations(string text)
		{
			var declarations = new List<Declaration>();
			foreach (string declaration in text.Split(';'))
			{
				int colon = declaration.IndexOf(':');
				if (colon <= 0)
				{
					continue;
				}

				string value = declaration.Substring(colon + 1).Trim();
				bool important = value.EndsWith("!important");
				if (important)
				{
					value = value.Substring(0, value.Length - "!important".Length).Trim();
				}

				declarations.Add(new Declaration(declaration.Substring(0, colon).Trim(), value, important));
			}

			return declarations;
		}

		private static void SetAll(SvgElement element, IEnumerable<Declaration> declarations)
		{
			foreach (Declaration declaration in declarations)
			{
				// "marker" is a CSS-only shorthand (not a presentation attribute) for all three marker properties.
				if (declaration.Name == "marker")
				{
					element.Attributes["marker-start"] = element.Attributes["marker-mid"] = element.Attributes["marker-end"] = declaration.Value;
				}
				else
				{
					element.Attributes[declaration.Name] = declaration.Value;
				}
			}
		}

		private static IEnumerable<Rule> ParseSheet(string sheet)
		{
			sheet = Regex.Replace(sheet, @"/\*.*?\*/", "", RegexOptions.Singleline);
			int position = 0;
			while (position < sheet.Length)
			{
				int open = sheet.IndexOf('{', position);
				if (open < 0)
				{
					yield break;
				}

				string selectors = sheet.Substring(position, open - position).Trim();
				int close = MatchingBrace(sheet, open);
				if (close < 0)
				{
					yield break;
				}

				string body = sheet.Substring(open + 1, close - open - 1);
				position = close + 1;

				// An at-rule (@media, @font-face...) is skipped whole.
				if (selectors.StartsWith("@"))
				{
					continue;
				}

				List<Declaration> declarations = ParseDeclarations(body);
				foreach (string selector in selectors.Split(','))
				{
					if (Selector.Parse(selector) is Selector parsed)
					{
						yield return new Rule(parsed, declarations);
					}
				}
			}
		}

		/// <summary>The index of the '}' closing the '{' at <paramref name="open"/>, nested blocks skipped; -1 if none.</summary>
		private static int MatchingBrace(string text, int open)
		{
			int depth = 0;
			for (int i = open; i < text.Length; i++)
			{
				if (text[i] == '{')
				{
					depth++;
				}
				else if (text[i] == '}' && --depth == 0)
				{
					return i;
				}
			}

			return -1;
		}

		internal readonly struct Declaration
		{
			public Declaration(string name, string value, bool important)
			{
				this.Name = name;
				this.Value = value;
				this.Important = important;
			}

			public string Name { get; }

			public string Value { get; }

			public bool Important { get; }
		}

		private sealed class Rule
		{
			public Rule(Selector selector, List<Declaration> declarations)
			{
				this.Selector = selector;
				this.Declarations = declarations;
			}

			public Selector Selector { get; }

			public List<Declaration> Declarations { get; }
		}

		/// <summary>
		/// A complex selector: compound selectors joined by descendant or child combinators, matched right to
		/// left. Specificity is CSS's (ids, classes, types) packed into one number that orders the same way.
		/// </summary>
		internal sealed class Selector
		{
			/// <summary>Each compound, and whether it must be the child (not just a descendant) of the one before it.</summary>
			private readonly List<(Compound Compound, bool ChildOf)> parts;

			private Selector(List<(Compound Compound, bool ChildOf)> parts)
			{
				this.parts = parts;
				this.Specificity = parts.Sum(p => p.Compound.Specificity);
			}

			public int Specificity { get; }

			/// <summary>Parses one selector of a list, or returns null when it uses syntax this does not support.</summary>
			public static Selector Parse(string text)
			{
				string[] tokens = Regex.Split(text.Replace(">", " > ").Trim(), @"\s+").Where(t => t.Length > 0).ToArray();
				var parts = new List<(Compound, bool)>();
				bool childOf = false;
				foreach (string token in tokens)
				{
					if (token == ">")
					{
						if (parts.Count == 0 || childOf)
						{
							return null;
						}

						childOf = true;
						continue;
					}

					if (!(Compound.Parse(token) is Compound compound))
					{
						return null;
					}

					parts.Add((compound, childOf));
					childOf = false;
				}

				return parts.Count == 0 || childOf ? null : new Selector(parts);
			}

			public bool Matches(SvgElement element) => this.Matches(element, this.parts.Count - 1);

			private bool Matches(SvgElement element, int index)
			{
				if (!this.parts[index].Compound.Matches(element))
				{
					return false;
				}

				if (index == 0)
				{
					return true;
				}

				if (this.parts[index].ChildOf)
				{
					return element.Parent != null && this.Matches(element.Parent, index - 1);
				}

				for (SvgElement ancestor = element.Parent; ancestor != null; ancestor = ancestor.Parent)
				{
					if (this.Matches(ancestor, index - 1))
					{
						return true;
					}
				}

				return false;
			}
		}

		/// <summary>A compound selector such as <c>rect.a.b#c</c>, <c>.a</c> or <c>*</c>.</summary>
		private sealed class Compound
		{
			private readonly List<string> classes = new List<string>();
			private readonly List<string> ids = new List<string>();
			private string type;

			public int Specificity => this.ids.Count * 10000 + this.classes.Count * 100 + (this.type == null ? 0 : 1);

			public static Compound Parse(string token)
			{
				Match match = Regex.Match(token, @"^(\*|[A-Za-z][\w-]*)?((?:[.#][\w-]+)*)$");
				if (!match.Success)
				{
					return null;
				}

				string type = match.Groups[1].Value;
				var compound = new Compound { type = type.Length > 0 && type != "*" ? type : null };
				foreach (Match part in Regex.Matches(match.Groups[2].Value, @"[.#][\w-]+"))
				{
					(part.Value[0] == '.' ? compound.classes : compound.ids).Add(part.Value.Substring(1));
				}

				return compound;
			}

			public bool Matches(SvgElement element)
			{
				if (this.type != null && this.type != element.Name)
				{
					return false;
				}

				if (this.ids.Any(id => element["id"] != id))
				{
					return false;
				}

				string[] elementClasses = (element["class"] ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
				return this.classes.All(c => elementClasses.Contains(c));
			}
		}
	}
}
