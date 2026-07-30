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
using System.Globalization;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Reader for the LCD byte-exactness fixture: the manifest and raw mask/composite blobs dumped by the
	/// agg-gui Rust reference (<c>agg-gui\examples\lcd_reference_fixture.rs</c>) and checked in under
	/// <c>TestData\LcdFixture</c>. Consumed by <see cref="LcdRustFixtureTests"/>.
	/// </summary>
	/// <remarks>
	/// The manifest is deliberately a flat, keyword-per-line text file so it needs no serializer and stays
	/// readable in a diff. Every number appears as <c>&lt;decimal&gt;#&lt;16 hex digits&gt;</c> and it is the
	/// <b>hex bit pattern</b> that is parsed: the decimal half is for human eyes only. That is what makes a
	/// coordinate or matrix component bit-identical to the one the reference rasterized, without any literal
	/// being re-typed on this side.
	/// </remarks>
	internal sealed class LcdFixtureManifest
	{
		private const string FixtureFolderName = "LcdFixture";

		private static LcdFixtureManifest loaded;

		private readonly Dictionary<string, LcdFixtureCase> cases;

		private LcdFixtureManifest(string directory, double primaryWeight, double gamma, List<LcdFixtureCase> caseList)
		{
			this.Directory = directory;
			this.PrimaryWeight = primaryWeight;
			this.Gamma = gamma;
			this.cases = caseList.ToDictionary(c => c.Name, StringComparer.Ordinal);
		}

		/// <summary>Folder the blobs were read from.</summary>
		public string Directory { get; }

		/// <summary>The filter center-tap weight the reference used; the fixture requires the default.</summary>
		public double PrimaryWeight { get; }

		/// <summary>The post-filter gamma the reference used; the fixture requires the default.</summary>
		public double Gamma { get; }

		public IEnumerable<string> CaseNames => this.cases.Keys;

		/// <summary>
		/// The cases that carry a stage-3 composite section. Exposed so the test class can assert its
		/// composite case list is exactly this set - a composite added to the reference harness but not
		/// wired up here would otherwise be generated, checked in and never run.
		/// </summary>
		public IEnumerable<string> CompositeCaseNames =>
			this.cases.Where(pair => pair.Value.Composite != null).Select(pair => pair.Key);

		/// <summary>
		/// The cases that carry an <see cref="LcdBuffer"/> section. Exposed for the same reason as
		/// <see cref="CompositeCaseNames"/>: a stage the reference dumps but this side never reads is
		/// coverage lost silently.
		/// </summary>
		public IEnumerable<string> BufferCaseNames =>
			this.cases.Where(pair => pair.Value.Buffer != null).Select(pair => pair.Key);

		/// <summary>
		/// Parses the manifest once per test run - every case is a few KB, and re-parsing per test case
		/// would dominate the run time of an otherwise instant test class.
		/// </summary>
		public static LcdFixtureManifest Load()
		{
			return loaded ??= Parse(LocateFixtureDirectory());
		}

		public LcdFixtureCase Case(string name)
		{
			if (!this.cases.TryGetValue(name, out LcdFixtureCase found))
			{
				throw new InvalidDataException(
					$"The LCD fixture manifest in '{this.Directory}' has no case named '{name}'. "
					+ "Regenerate the fixture with agg-gui\\examples\\lcd_reference_fixture.rs.");
			}

			return found;
		}

		/// <summary>
		/// Finds <c>TestData\LcdFixture</c> by walking up from the test binary to the repository root -
		/// the convention the older test data in <c>TestData</c> already uses - and only falling back to
		/// the copy beside the binary if no parent directory has one.
		/// </summary>
		/// <remarks>
		/// The parent search deliberately comes first. The csproj copies the fixture into the output
		/// directory with <c>PreserveNewest</c> so the tests still work from a bare <c>bin</c> (CI running
		/// the exe without the tree), but <c>PreserveNewest</c> only compares timestamps: regenerate the
		/// fixture with an older mtime than the last build, or have MSBuild skip the copy for any other
		/// reason, and <c>bin\</c> would quietly shadow the source of truth - the tests would then keep
		/// passing against a stale reference. Preferring the tree makes the checked-in fixture the thing
		/// under test whenever the tree is there at all.
		/// </remarks>
		private static string LocateFixtureDirectory()
		{
			var candidates = new List<string>();
			string probe = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
			for (int up = 0; up < 6 && probe != null; up++)
			{
				candidates.Add(Path.Combine(probe, "TestData", FixtureFolderName));
				probe = Path.GetDirectoryName(probe);
			}

			candidates.Add(Path.Combine(AppContext.BaseDirectory, "TestData", FixtureFolderName));

			foreach (string candidate in candidates)
			{
				if (File.Exists(Path.Combine(candidate, "manifest.txt")))
				{
					return candidate;
				}
			}

			throw new FileNotFoundException(
				"Could not find the LCD fixture manifest. Looked for TestData\\" + FixtureFolderName
				+ "\\manifest.txt beside " + AppContext.BaseDirectory + " and in each parent directory.");
		}

		private static LcdFixtureManifest Parse(string directory)
		{
			double primaryWeight = double.NaN;
			double gamma = double.NaN;
			var caseList = new List<LcdFixtureCase>();
			LcdFixtureCaseBuilder building = null;

			foreach (string rawLine in File.ReadAllLines(Path.Combine(directory, "manifest.txt")))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
				{
					continue;
				}

				string[] t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				switch (t[0])
				{
					case "version":
						if (t[1] != "1")
						{
							throw new InvalidDataException($"Unsupported LCD fixture manifest version '{t[1]}'.");
						}

						break;

					case "filter":
						// filter primary <num> gamma <num>
						primaryWeight = Number(t[2]);
						gamma = Number(t[4]);
						break;

					case "case":
						building = new LcdFixtureCaseBuilder(directory, t[1]);
						break;

					case "size":
						building.Width = int.Parse(t[1], CultureInfo.InvariantCulture);
						building.Height = int.Parse(t[2], CultureInfo.InvariantCulture);
						break;

					case "fill":
						// Explicit, not defaulted: a typo or a new rule name added on the reference side
						// must fail here rather than silently rasterize the whole fixture as non-zero.
						building.FillRule = t[1] switch
						{
							"evenodd" => filling_rule_e.fill_even_odd,
							"nonzero" => filling_rule_e.fill_non_zero,
							_ => throw new InvalidDataException(
								$"Unrecognized LCD fixture fill rule '{t[1]}'. Expected 'nonzero' or 'evenodd'."),
						};
						break;

					case "clip":
						// clip none | clip <left> <bottom> <right> <top>
						building.Clip = t[1] == "none"
							? (RectangleDouble?)null
							: new RectangleDouble(Number(t[1]), Number(t[2]), Number(t[3]), Number(t[4]));
						break;

					case "xform":
						building.Transform = new Affine(
							Number(t[1]), Number(t[2]), Number(t[3]), Number(t[4]), Number(t[5]), Number(t[6]));
						break;

					case "path":
						building.Paths.Add(ParsePath(t));
						break;

					case "lcd":
						// lcd <file> <byte length>
						building.LcdFile = t[1];
						building.LcdByteLength = int.Parse(t[2], CultureInfo.InvariantCulture);
						break;

					case "gray":
						building.GrayFile = t[1];
						building.GrayByteLength = int.Parse(t[2], CultureInfo.InvariantCulture);
						break;

					case "composite":
						building.Composite = ParseComposite(t);
						break;

					case "buffer":
						building.Buffer = ParseBuffer(t);
						break;

					case "end":
						caseList.Add(building.Build());
						building = null;
						break;

					default:
						throw new InvalidDataException($"Unrecognized LCD fixture manifest line: '{line}'.");
				}
			}

			return new LcdFixtureManifest(directory, primaryWeight, gamma, caseList);
		}

		/// <summary>
		/// <c>path m x y l x y c x1 y1 x2 y2 x3 y3 z</c> - the same command vocabulary the reference harness
		/// writes, in the order it added the vertices.
		/// </summary>
		private static List<LcdFixtureVertex> ParsePath(string[] t)
		{
			var commands = new List<LcdFixtureVertex>();
			int i = 1;
			while (i < t.Length)
			{
				switch (t[i])
				{
					case "m":
						commands.Add(new LcdFixtureVertex('m', new[] { Number(t[i + 1]), Number(t[i + 2]) }));
						i += 3;
						break;

					case "l":
						commands.Add(new LcdFixtureVertex('l', new[] { Number(t[i + 1]), Number(t[i + 2]) }));
						i += 3;
						break;

					case "c":
						commands.Add(new LcdFixtureVertex('c', new[]
						{
							Number(t[i + 1]), Number(t[i + 2]), Number(t[i + 3]),
							Number(t[i + 4]), Number(t[i + 5]), Number(t[i + 6]),
						}));
						i += 7;
						break;

					case "z":
						commands.Add(new LcdFixtureVertex('z', Array.Empty<double>()));
						i += 1;
						break;

					default:
						throw new InvalidDataException($"Unrecognized LCD fixture path command '{t[i]}'.");
				}
			}

			return commands;
		}

		/// <summary>
		/// <c>composite &lt;file&gt; w h originX originY src r g b a dstfill solid r g b a</c> (or
		/// <c>dstfill halves splitX r g b a r g b a</c>).
		/// </summary>
		private static LcdFixtureComposite ParseComposite(string[] t)
		{
			int width = int.Parse(t[2], CultureInfo.InvariantCulture);
			int height = int.Parse(t[3], CultureInfo.InvariantCulture);
			int originX = int.Parse(t[4], CultureInfo.InvariantCulture);
			int originY = int.Parse(t[5], CultureInfo.InvariantCulture);
			Color source = ColorAt(t, 7);

			// t[11] is the "dstfill" keyword, t[12] the variant.
			int split = int.MaxValue;
			Color low;
			Color high;
			if (t[12] == "halves")
			{
				split = int.Parse(t[13], CultureInfo.InvariantCulture);
				low = ColorAt(t, 14);
				high = ColorAt(t, 18);
			}
			else
			{
				low = ColorAt(t, 13);
				high = low;
			}

			return new LcdFixtureComposite(t[1], width, height, originX, originY, source, split, low, high);
		}

		/// <summary>
		/// <c>buffer &lt;colorFile&gt; &lt;alphaFile&gt; w h clear r g b a src r g b a</c> - the two-plane
		/// stage. The geometry, transform, clip and fill rule are the case's own, because the reference
		/// paints the same paths through <c>LcdBuffer::fill_path</c>.
		/// </summary>
		private static LcdFixtureBuffer ParseBuffer(string[] t)
		{
			return new LcdFixtureBuffer(
				t[1],
				t[2],
				int.Parse(t[3], CultureInfo.InvariantCulture),
				int.Parse(t[4], CultureInfo.InvariantCulture),
				ColorAt(t, 6),
				ColorAt(t, 11));
		}

		private static Color ColorAt(string[] t, int index)
		{
			return new Color(
				byte.Parse(t[index], CultureInfo.InvariantCulture),
				byte.Parse(t[index + 1], CultureInfo.InvariantCulture),
				byte.Parse(t[index + 2], CultureInfo.InvariantCulture),
				byte.Parse(t[index + 3], CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Reads the exact double the reference used, from the raw 64 bit pattern after the '#'. Parsing the
		/// decimal half instead would be one correctly-rounded parse away from working and zero guarantees
		/// away from being provably identical.
		/// </summary>
		/// <remarks>
		/// Both halves are parsed and required to agree. The decimal half is documented as being for human
		/// eyes only, which is exactly what makes it dangerous: someone reading a coordinate, deciding it
		/// looks wrong and editing the readable half would produce a manifest that says one thing and
		/// rasterizes another, forever. The reference writes the shortest round-trip decimal and .NET's
		/// parse is correctly rounded, so the two must land on the identical bit pattern.
		/// </remarks>
		private static double Number(string token)
		{
			int hash = token.IndexOf('#');
			if (hash < 0 || hash + 1 >= token.Length)
			{
				throw new InvalidDataException($"LCD fixture number '{token}' is missing its '#<bits>' half.");
			}

			long bits = long.Parse(
				token.Substring(hash + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

			string decimalHalf = token.Substring(0, hash);
			if (!double.TryParse(decimalHalf, NumberStyles.Float, CultureInfo.InvariantCulture, out double readable))
			{
				throw new InvalidDataException(
					$"LCD fixture number '{token}' has an unparseable decimal half '{decimalHalf}'.");
			}

			if (BitConverter.DoubleToInt64Bits(readable) != bits)
			{
				throw new InvalidDataException(
					$"LCD fixture number '{token}' disagrees with itself: the decimal half reads as "
					+ $"{BitConverter.DoubleToInt64Bits(readable):x16} but the bit pattern says {bits:x16}. "
					+ "The manifest has been hand-edited; regenerate it with "
					+ "agg-gui\\examples\\lcd_reference_fixture.rs instead.");
			}

			return BitConverter.Int64BitsToDouble(bits);
		}

		private sealed class LcdFixtureCaseBuilder
		{
			private readonly string directory;

			public LcdFixtureCaseBuilder(string directory, string name)
			{
				this.directory = directory;
				this.Name = name;
				this.Transform = Affine.NewIdentity();
				this.FillRule = filling_rule_e.fill_non_zero;
				this.Paths = new List<List<LcdFixtureVertex>>();
			}

			public string Name { get; }

			public int Width { get; set; }

			public int Height { get; set; }

			public filling_rule_e FillRule { get; set; }

			public RectangleDouble? Clip { get; set; }

			public Affine Transform { get; set; }

			public List<List<LcdFixtureVertex>> Paths { get; }

			public string LcdFile { get; set; }

			public int LcdByteLength { get; set; }

			public string GrayFile { get; set; }

			public int GrayByteLength { get; set; }

			public LcdFixtureComposite Composite { get; set; }

			public LcdFixtureBuffer Buffer { get; set; }

			public LcdFixtureCase Build()
			{
				return new LcdFixtureCase(
					this.directory,
					this.Name,
					this.Width,
					this.Height,
					this.FillRule,
					this.Clip,
					this.Transform,
					this.Paths,
					this.LcdFile,
					this.LcdByteLength,
					this.GrayFile,
					this.GrayByteLength,
					this.Composite,
					this.Buffer);
			}
		}
	}

	/// <summary>One path command from the manifest: 'm', 'l', 'c' (cubic) or 'z' (close).</summary>
	internal sealed class LcdFixtureVertex
	{
		public LcdFixtureVertex(char kind, double[] coordinates)
		{
			this.Kind = kind;
			this.Coordinates = coordinates;
		}

		public char Kind { get; }

		public double[] Coordinates { get; }
	}

	/// <summary>
	/// One fixture case: the mask size, fill rule, clip, transform and paths the reference rasterized, plus
	/// the names of the blobs holding what it produced.
	/// </summary>
	internal sealed class LcdFixtureCase
	{
		private readonly string directory;

		private readonly List<List<LcdFixtureVertex>> paths;

		private readonly int lcdByteLength;

		private readonly int grayByteLength;

		public LcdFixtureCase(
			string directory,
			string name,
			int maskWidth,
			int maskHeight,
			filling_rule_e fillRule,
			RectangleDouble? clip,
			Affine transform,
			List<List<LcdFixtureVertex>> paths,
			string lcdFile,
			int lcdByteLength,
			string grayFile,
			int grayByteLength,
			LcdFixtureComposite composite,
			LcdFixtureBuffer buffer)
		{
			this.Buffer = buffer;
			this.directory = directory;
			this.Name = name;
			this.MaskWidth = maskWidth;
			this.MaskHeight = maskHeight;
			this.FillRule = fillRule;
			this.Clip = clip;
			this.Transform = transform;
			this.paths = paths;
			this.LcdFile = lcdFile;
			this.lcdByteLength = lcdByteLength;
			this.GrayFile = grayFile;
			this.grayByteLength = grayByteLength;
			this.Composite = composite;
		}

		public string Name { get; }

		public int MaskWidth { get; }

		public int MaskHeight { get; }

		public filling_rule_e FillRule { get; }

		public RectangleDouble? Clip { get; }

		public Affine Transform { get; }

		public string LcdFile { get; }

		public string GrayFile { get; }

		/// <summary>Stage 3 parameters, or null for a case that only pins the mask.</summary>
		public LcdFixtureComposite Composite { get; }

		/// <summary>Two-plane buffer stage parameters, or null for a case that does not pin it.</summary>
		public LcdFixtureBuffer Buffer { get; }

		/// <summary>
		/// Rebuilds the case through the production C# pipeline: one <see cref="LcdMaskBuilder"/> for the
		/// whole case, one <see cref="LcdMaskBuilder.AddPath"/> per manifest path (so multi-path cases
		/// accumulate into the shared gray buffer exactly as the reference's repeated <c>add</c> calls do).
		/// </summary>
		/// <param name="gray">True for <see cref="LcdMaskBuilder.FinalizeGray"/>, false for the LCD
		/// <see cref="LcdMaskBuilder.FinalizeMask"/>. The raster stage is identical either way.</param>
		public LcdMask BuildMask(bool gray)
		{
			var builder = new LcdMaskBuilder(this.MaskWidth, this.MaskHeight, this.Clip, this.FillRule);
			foreach (List<LcdFixtureVertex> path in this.paths)
			{
				builder.AddPath(this.Transform, ToVertexStorage(path));
			}

			return gray ? builder.FinalizeGray() : builder.FinalizeMask();
		}

		/// <summary>The reference's LCD mask bytes.</summary>
		public byte[] ReadLcdBlob()
		{
			return this.ReadBlob(this.LcdFile, this.lcdByteLength);
		}

		/// <summary>The reference's gray-collapse mask bytes.</summary>
		public byte[] ReadGrayBlob()
		{
			return this.ReadBlob(this.GrayFile, this.grayByteLength);
		}

		/// <summary>
		/// Repaints the case through <see cref="LcdBuffer.FillPath"/> - one call per manifest path, over the
		/// cleared background the reference used. This is the only fixture stage that exercises the
		/// bbox-sized mask and the integer origin <see cref="BoundedMaskBuilder"/> derives internally, since
		/// <see cref="BuildMask"/> is handed its mask size explicitly.
		/// </summary>
		public LcdBuffer BuildBuffer()
		{
			if (this.Buffer == null)
			{
				throw new InvalidOperationException($"LCD fixture case '{this.Name}' has no buffer stage.");
			}

			var buffer = new LcdBuffer(this.Buffer.Width, this.Buffer.Height);
			buffer.Clear(this.Buffer.Clear);
			foreach (List<LcdFixtureVertex> path in this.paths)
			{
				buffer.FillPath(ToVertexStorage(path), this.Buffer.Source, this.Transform, this.Clip, this.FillRule);
			}

			return buffer;
		}

		/// <summary>The reference's premultiplied color plane. Throws if the case has no buffer stage.</summary>
		public byte[] ReadBufferColorBlob()
		{
			return this.ReadBufferBlob(plane => plane.ColorFile);
		}

		/// <summary>The reference's per-channel alpha plane. Throws if the case has no buffer stage.</summary>
		public byte[] ReadBufferAlphaBlob()
		{
			return this.ReadBufferBlob(plane => plane.AlphaFile);
		}

		/// <summary>The reference's stage-3 composite result. Throws if the case has no composite.</summary>
		public byte[] ReadCompositeBlob()
		{
			if (this.Composite == null)
			{
				throw new InvalidOperationException($"LCD fixture case '{this.Name}' has no composite stage.");
			}

			return this.ReadBlob(
				this.Composite.DestinationFile, this.Composite.Width * this.Composite.Height * 4);
		}

		private byte[] ReadBufferBlob(Func<LcdFixtureBuffer, string> plane)
		{
			if (this.Buffer == null)
			{
				throw new InvalidOperationException($"LCD fixture case '{this.Name}' has no buffer stage.");
			}

			return this.ReadBlob(plane(this.Buffer), this.Buffer.Width * this.Buffer.Height * 3);
		}

		private static VertexStorage ToVertexStorage(List<LcdFixtureVertex> path)
		{
			var storage = new VertexStorage();
			foreach (LcdFixtureVertex command in path)
			{
				double[] c = command.Coordinates;
				switch (command.Kind)
				{
					case 'm':
						storage.MoveTo(c[0], c[1]);
						break;

					case 'l':
						storage.LineTo(c[0], c[1]);
						break;

					case 'c':
						storage.Curve4(c[0], c[1], c[2], c[3], c[4], c[5]);
						break;

					case 'z':
						storage.ClosePolygon();
						break;

					default:
						// Never silently close: a kind the manifest parser lets through but this switch
						// does not know would otherwise turn into a stray ClosePolygon and change the
						// geometry, which reads as a port bug rather than as a fixture-reader bug.
						throw new InvalidDataException(
							$"Unrecognized LCD fixture vertex kind '{command.Kind}'. Expected 'm', 'l', 'c' or 'z'.");
				}
			}

			return storage;
		}

		/// <summary>
		/// Reads a reference blob and checks it is the length the manifest says it is. A short or padded
		/// file is fixture corruption - a lost binary round trip, a truncated checkout - and has to be
		/// reported as such, because as a plain length mismatch downstream it would look exactly like the
		/// port producing a wrongly sized mask.
		/// </summary>
		private byte[] ReadBlob(string fileName, int expectedLength)
		{
			string path = Path.Combine(this.directory, fileName);
			byte[] bytes = File.ReadAllBytes(path);
			if (bytes.Length != expectedLength)
			{
				throw new InvalidDataException(
					$"LCD fixture reference file corrupted/truncated: '{path}' should be {expectedLength} "
					+ $"bytes per the manifest, file has {bytes.Length}. This is not a port mismatch - the "
					+ "checked-in fixture itself is wrong (a newline-translated binary round trip is the "
					+ "usual cause). Restore or regenerate it.");
			}

			return bytes;
		}
	}

	/// <summary>
	/// The two-plane buffer stage of a fixture case: the buffer size, the background it was cleared to, the
	/// fill color, and the names of the blobs holding the reference's two planes.
	/// </summary>
	internal sealed class LcdFixtureBuffer
	{
		public LcdFixtureBuffer(string colorFile, string alphaFile, int width, int height, Color clear, Color source)
		{
			this.ColorFile = colorFile;
			this.AlphaFile = alphaFile;
			this.Width = width;
			this.Height = height;
			this.Clear = clear;
			this.Source = source;
		}

		/// <summary>Blob of the reference's premultiplied per-channel color plane.</summary>
		public string ColorFile { get; }

		/// <summary>Blob of the reference's per-channel alpha plane.</summary>
		public string AlphaFile { get; }

		public int Width { get; }

		public int Height { get; }

		/// <summary>
		/// Background the buffer was cleared to before the fill. Deliberately semi-transparent in the
		/// fixture, so both planes carry non-trivial starting values and the composite's
		/// <c>(1 - effectiveAlpha)</c> terms are pinned rather than multiplied by zero.
		/// </summary>
		public Color Clear { get; }

		/// <summary>Fill color; its alpha scales every channel's coverage.</summary>
		public Color Source { get; }
	}

	/// <summary>
	/// Stage 3 of a fixture case: composite a mask over a known destination in a known source color at a
	/// known integer origin, and hand back the result as straight RGBA so it can be compared with the
	/// reference's dump byte for byte.
	/// </summary>
	internal sealed class LcdFixtureComposite
	{
		private readonly int splitX;

		private readonly Color lowColor;

		private readonly Color highColor;

		public LcdFixtureComposite(
			string destinationFile,
			int width,
			int height,
			int originX,
			int originY,
			Color source,
			int splitX,
			Color lowColor,
			Color highColor)
		{
			this.DestinationFile = destinationFile;
			this.Width = width;
			this.Height = height;
			this.OriginX = originX;
			this.OriginY = originY;
			this.Source = source;
			this.splitX = splitX;
			this.lowColor = lowColor;
			this.highColor = highColor;
		}

		public string DestinationFile { get; }

		public int Width { get; }

		public int Height { get; }

		public int OriginX { get; }

		public int OriginY { get; }

		public Color Source { get; }

		/// <summary>
		/// Pre-fills a destination, composites <paramref name="mask"/> onto it and returns the bytes as
		/// straight RGBA, row 0 = bottom.
		/// </summary>
		/// <remarks>
		/// The reference's destination is RGBA and agg-sharp's is BGRA, so the channels are read back
		/// through <see cref="ImageBuffer.OrderR"/> and friends rather than copied - a byte-order mixup here
		/// would look like a red/blue swap in the failure report, not like a coverage difference.
		/// </remarks>
		public byte[] Run(LcdMask mask)
		{
			var destination = new ImageBuffer(this.Width, this.Height, 32, new BlenderBGRA());
			byte[] buffer = destination.GetBuffer();
			int bytesPerPixel = destination.GetBytesBetweenPixelsInclusive();

			for (int y = 0; y < this.Height; y++)
			{
				int rowOffset = destination.GetBufferOffsetXY(0, y);
				for (int x = 0; x < this.Width; x++)
				{
					Color fill = x < this.splitX ? this.lowColor : this.highColor;
					int offset = rowOffset + (x * bytesPerPixel);
					buffer[offset + ImageBuffer.OrderR] = fill.red;
					buffer[offset + ImageBuffer.OrderG] = fill.green;
					buffer[offset + ImageBuffer.OrderB] = fill.blue;
					buffer[offset + ImageBuffer.OrderA] = fill.alpha;
				}
			}

			LcdComposite.Composite(destination, mask, this.Source, this.OriginX, this.OriginY);

			var rgba = new byte[this.Width * this.Height * 4];
			for (int y = 0; y < this.Height; y++)
			{
				int rowOffset = destination.GetBufferOffsetXY(0, y);
				for (int x = 0; x < this.Width; x++)
				{
					int offset = rowOffset + (x * bytesPerPixel);
					int output = ((y * this.Width) + x) * 4;
					rgba[output] = buffer[offset + ImageBuffer.OrderR];
					rgba[output + 1] = buffer[offset + ImageBuffer.OrderG];
					rgba[output + 2] = buffer[offset + ImageBuffer.OrderB];
					rgba[output + 3] = buffer[offset + ImageBuffer.OrderA];
				}
			}

			return rgba;
		}
	}
}
