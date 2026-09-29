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
using System.Globalization;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.SvgTools
{
	/// <summary>
	/// Reads SVG path data (the d attribute) into a <see cref="VertexStorage"/>, following the SVG path grammar as
	/// svgtypes' PathParser and SimplifyingPathParser read it (the parser usvg uses): segments are read one at a time,
	/// and the first invalid one - a bad or missing number, an arc flag that is not the single character 0 or 1, an
	/// unknown command, numbers after a closepath - ends the path, keeping everything before it (SVG 1.1 F.2 error
	/// processing). Data that does not open with a moveto draws nothing.
	/// </summary>
	internal class SvgPathData
	{
		private readonly string data;
		private int position;

		private SvgPathData(string data)
		{
			this.data = data;
		}

		/// <summary>Replaces <paramref name="storage"/> with <paramref name="dString"/>, drawing arcs with <paramref name="addArc"/>.</summary>
		public static void Parse(VertexStorage storage, string dString, SvgParser.ArcToPath addArc)
		{
			storage.Clear();
			if (string.IsNullOrEmpty(dString))
			{
				return;
			}

			new SvgPathData(dString).ReadInto(storage, addArc);
		}

		private void ReadInto(VertexStorage storage, SvgParser.ArcToPath addArc)
		{
			var current = Vector2.Zero;
			var subpathStart = Vector2.Zero;

			// The control point a following S or T reflects; only meaningful when the previous command was a cubic
			// (for S) or a quadratic (for T). Arcs are stored as cubics, so the stored vertices cannot tell.
			var lastControl = Vector2.Zero;
			char previous = '\0';
			var values = new double[7];

			while (true)
			{
				SkipSpaces();
				if (position >= data.Length)
				{
					return;
				}

				char first = data[position];
				char command;
				bool implicitLineAfterMove = false;
				if (IsCommand(first))
				{
					if (previous == '\0' && first != 'M' && first != 'm')
					{
						return;
					}

					command = first;
					position++;
				}
				else if (previous != '\0' && IsNumberStart(first) && previous != 'Z' && previous != 'z')
				{
					// Numbers after a moveto are implicit linetos; after anything else they repeat the command.
					implicitLineAfterMove = previous == 'M' || previous == 'm';
					command = previous == 'M' ? 'L' : previous == 'm' ? 'l' : previous;
				}
				else
				{
					return;
				}

				char lower = char.ToLowerInvariant(command);
				bool relative = command == lower;
				if (!ReadArguments(lower, values))
				{
					return;
				}

				// SVG 1.1 8.3.3: drawing on after a closepath starts a new subpath at the closed one's start.
				if ((previous == 'Z' || previous == 'z') && lower != 'm' && lower != 'z')
				{
					storage.MoveTo(subpathStart.X, subpathStart.Y);
				}

				var offset = relative ? current : Vector2.Zero;
				switch (lower)
				{
					case 'm':
						current = new Vector2(values[0], values[1]) + offset;
						storage.MoveTo(current.X, current.Y);
						subpathStart = current;
						break;

					case 'l':
						current = new Vector2(values[0], values[1]) + offset;
						storage.LineTo(current.X, current.Y);
						break;

					case 'h':
						current.X = values[0] + offset.X;
						storage.LineTo(current.X, current.Y);
						break;

					case 'v':
						current.Y = values[0] + offset.Y;
						storage.LineTo(current.X, current.Y);
						break;

					case 'c':
						{
							var control1 = new Vector2(values[0], values[1]) + offset;
							lastControl = new Vector2(values[2], values[3]) + offset;
							current = new Vector2(values[4], values[5]) + offset;
							storage.Curve4(control1.X, control1.Y, lastControl.X, lastControl.Y, current.X, current.Y);
						}

						break;

					case 's':
						{
							// SVG 1.1 8.3.6: the first control point reflects the previous cubic's second one, or is the
							// current point when the previous command was not a cubic.
							bool afterCubic = "cCsS".IndexOf(previous) >= 0;
							var control1 = afterCubic ? current * 2 - lastControl : current;
							lastControl = new Vector2(values[0], values[1]) + offset;
							current = new Vector2(values[2], values[3]) + offset;
							storage.Curve4(control1.X, control1.Y, lastControl.X, lastControl.Y, current.X, current.Y);
						}

						break;

					case 'q':
						lastControl = new Vector2(values[0], values[1]) + offset;
						current = new Vector2(values[2], values[3]) + offset;
						storage.Curve3(lastControl.X, lastControl.Y, current.X, current.Y);
						break;

					case 't':
						// SVG 1.1 8.3.7: the same rule for quadratics.
						lastControl = "qQtT".IndexOf(previous) >= 0 ? current * 2 - lastControl : current;
						current = new Vector2(values[0], values[1]) + offset;
						storage.Curve3(lastControl.X, lastControl.Y, current.X, current.Y);
						break;

					case 'a':
						{
							var end = new Vector2(values[5], values[6]) + offset;
							addArc(storage, current, new Vector2(values[0], values[1]), values[2], (int)values[3], (int)values[4], end);
							current = end;
						}

						break;

					case 'z':
						storage.ClosePolygon();
						current = subpathStart;
						break;
				}

				// An implicit lineto keeps the moveto as the command numbers repeat, as svgtypes does.
				previous = implicitLineAfterMove ? (relative ? 'm' : 'M') : command;
			}
		}

		/// <summary>Reads the arguments of one segment of the command <paramref name="lower"/>; false when any is invalid or missing.</summary>
		private bool ReadArguments(char lower, double[] values)
		{
			switch (lower)
			{
				case 'm':
				case 'l':
				case 't':
					return ReadNumbers(values, 0, 2);

				case 'h':
				case 'v':
					return ReadNumbers(values, 0, 1);

				case 'c':
					return ReadNumbers(values, 0, 6);

				case 's':
				case 'q':
					return ReadNumbers(values, 0, 4);

				case 'a':
					return ReadNumbers(values, 0, 3) && ReadFlag(out values[3]) && ReadFlag(out values[4]) && ReadNumbers(values, 5, 2);

				default:
					return true;
			}
		}

		private bool ReadNumbers(double[] values, int start, int count)
		{
			for (int i = start; i < start + count; i++)
			{
				if (!ReadListNumber(out values[i]))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// An arc flag is exactly one character, 0 or 1, so it may run straight into the next number ("a1,1 0 01 5,5");
		/// a comma may follow it.
		/// </summary>
		private bool ReadFlag(out double flag)
		{
			flag = 0;
			SkipSpaces();
			if (position >= data.Length || (data[position] != '0' && data[position] != '1'))
			{
				return false;
			}

			flag = data[position] - '0';
			position++;
			if (position < data.Length && data[position] == ',')
			{
				position++;
			}

			SkipSpaces();
			return true;
		}

		/// <summary>A number, then spaces and at most one comma (svgtypes' parse_list_number).</summary>
		private bool ReadListNumber(out double value)
		{
			if (!ReadNumber(out value))
			{
				return false;
			}

			SkipSpaces();
			if (position < data.Length && data[position] == ',')
			{
				position++;
			}

			return true;
		}

		/// <summary>
		/// sign? digits? ('.' digits?)? exponent?, where an 'e' followed by 'm' or 'x' is a unit, not an exponent.
		/// </summary>
		private bool ReadNumber(out double value)
		{
			value = 0;
			SkipSpaces();
			int start = position;
			if (position < data.Length && (data[position] == '+' || data[position] == '-'))
			{
				position++;
			}

			if (position >= data.Length || (!IsDigit(data[position]) && data[position] != '.'))
			{
				return false;
			}

			SkipDigits();
			if (position < data.Length && data[position] == '.')
			{
				position++;
				SkipDigits();
			}

			if (position < data.Length && (data[position] == 'e' || data[position] == 'E')
				&& position + 1 < data.Length && data[position + 1] != 'm' && data[position + 1] != 'x')
			{
				position++;
				if (data[position] == '+' || data[position] == '-')
				{
					position++;
				}
				else if (!IsDigit(data[position]))
				{
					return false;
				}

				SkipDigits();
			}

			return double.TryParse(data.AsSpan(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
				&& double.IsFinite(value);
		}

		private void SkipDigits()
		{
			while (position < data.Length && IsDigit(data[position]))
			{
				position++;
			}
		}

		private void SkipSpaces()
		{
			while (position < data.Length && (data[position] == ' ' || data[position] == '\t' || data[position] == '\n' || data[position] == '\r'))
			{
				position++;
			}
		}

		private static bool IsDigit(char c) => c >= '0' && c <= '9';

		private static bool IsNumberStart(char c) => IsDigit(c) || c == '.' || c == '-' || c == '+';

		private static bool IsCommand(char c) => "MmZzLlHhVvCcSsQqTtAa".IndexOf(c) >= 0;
	}
}
