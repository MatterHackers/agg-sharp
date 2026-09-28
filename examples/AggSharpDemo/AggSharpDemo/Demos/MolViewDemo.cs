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
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's mol_view.cpp: a simple viewer for MDL SDF molecule files. Bonds are drawn as thin quads (two
	/// for a double bond, a solid or dashed wedge for a stereo bond), atoms other than carbon get a white disc
	/// and a gsv_text label in their element's colour. Page Up and Page Down (or the arrow keys) step through the
	/// molecules; drag with the left button to rotate and zoom, with the right to move; space spins it.
	/// </summary>
	public class MolViewDemo : AggDemo
	{
		private const int StartWidth = 400;
		private const int StartHeight = 400;

		// C++ m_atom_colors, srgba8 values converted to the linear rgba8 the pixel format blends.
		private static readonly Color[] AtomColors =
		{
			SrgbLut.FromSrgba8(0, 0, 0), // general
			SrgbLut.FromSrgba8(0, 0, 120), // N
			SrgbLut.FromSrgba8(200, 0, 0), // O
			SrgbLut.FromSrgba8(120, 120, 0), // S
			SrgbLut.FromSrgba8(80, 50, 0), // P
			SrgbLut.FromSrgba8(0, 200, 0), // halogen
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();
		private readonly List<Molecule> molecules;
		private int currentMolecule;
		private double pdx;
		private double pdy;
		private double prevScale = 1.0;
		private double prevAngle;
		private bool mouseMove;

		/// <summary>Shows C++ AGG's own 1.sdf, embedded in this assembly.</summary>
		public MolViewDemo()
			: this(OpenEmbeddedMolecules())
		{
		}

		/// <summary>Shows the molecules of an SDF file, read as C++ does (LF, CRLF or CR line endings alike).</summary>
		public MolViewDemo(TextReader sdf)
		{
			this.ThicknessSlider = new SliderCtrl(5, 5, StartWidth - 5, 12) { Label = "Thickness={0:F2}" };
			this.TextSizeSlider = new SliderCtrl(5, 20, StartWidth - 5, 27) { Label = "Label Size={0:F2}" };
			this.ctrls.Add(this.ThicknessSlider);
			this.ctrls.Add(this.TextSizeSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.molecules = Molecule.ReadAll(sdf, 100);
			if (this.molecules.Count == 0)
			{
				throw new InvalidDataException("The SDF file holds no molecule mol_view can read.");
			}
		}

		/// <summary>The text of the embedded 1.sdf.</summary>
		public static string EmbeddedMolecules()
		{
			const string resourceName = "MatterHackers.AggSharpDemo.Art.1.sdf";
			using var stream = typeof(MolViewDemo).Assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException(
					$"The resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it from Art/1.sdf.");
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		private static TextReader OpenEmbeddedMolecules() => new StringReader(EmbeddedMolecules());

		/// <summary>C++ <c>m_thickness</c>: bond and label stroke thickness, relative to the average bond.</summary>
		public SliderCtrl ThicknessSlider { get; }

		/// <summary>C++ <c>m_text_size</c>: label size, relative to the average bond.</summary>
		public SliderCtrl TextSizeSlider { get; }

		/// <summary>How many molecules the file held (C++ reads at most 100, stopping at the first it cannot read).</summary>
		public int MoleculeCount => this.molecules.Count;

		/// <summary>C++ <c>m_cur_molecule</c>: the molecule shown, clamped to the ones read.</summary>
		public int CurrentMolecule
		{
			get => this.currentMolecule;
			set
			{
				this.currentMolecule = Math.Max(0, Math.Min(value, this.molecules.Count - 1));
				this.Invalidate();
			}
		}

		/// <summary>C++ <c>m_angle</c>, in radians.</summary>
		public double Angle { get; set; }

		/// <summary>C++ <c>m_scale</c>: the zoom on top of fitting the molecule to the window.</summary>
		public double Scale { get; set; } = 1.0;

		/// <summary>C++ <c>m_center_x</c>: where the molecule's centre is drawn.</summary>
		public double CenterX { get; set; } = StartWidth / 2;

		/// <summary>C++ <c>m_center_y</c>.</summary>
		public double CenterY { get; set; } = StartHeight / 2;

		public override string Name => "mol_view";

		public override string Category => "Vector Graphics";

		public override string Description => "A molecule viewer for SDF files. Page Up and Page Down change the molecule; drag to rotate and zoom, right-drag to move, space to spin.";

		public override int Width => StartWidth;

		public override int Height => StartHeight;

		public override void Draw(Graphics2D graphics)
		{
			// C++ ras.clip_box(0, 0, width, height). The clip moves the edges of whatever crosses the frame's
			// border by a level, so the software rasterizer (the GPU path has none) gets it for this frame only.
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			bool hadClipBox = rasterizer?.HasVectorClipBox ?? false;
			RectangleDouble previousClipBox = rasterizer?.GetVectorClipBox() ?? default;
			rasterizer?.SetVectorClipBox(0, 0, this.Width, this.Height);
			try
			{
				this.DrawScene(graphics);
			}
			finally
			{
				if (hadClipBox)
				{
					rasterizer.SetVectorClipBox(previousClipBox);
				}
				else
				{
					rasterizer?.reset_clipping();
				}
			}
		}

		private void DrawScene(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			Molecule mol = this.molecules[this.currentMolecule];
			double minX = 1e100;
			double maxX = -1e100;
			double minY = 1e100;
			double maxY = -1e100;
			foreach (Atom atom in mol.Atoms)
			{
				minX = Math.Min(minX, atom.X);
				minY = Math.Min(minY, atom.Y);
				maxX = Math.Max(maxX, atom.X);
				maxY = Math.Max(maxY, atom.Y);
			}

			var mtx = Affine.NewTranslation(-(maxX + minX) * 0.5, -(maxY + minY) * 0.5);

			double scale = this.Width / (maxX - minX);
			double t = this.Height / (maxY - minY);
			if (scale > t)
			{
				scale = t;
			}

			double textSize = mol.AverageBondLength * this.TextSizeSlider.Value / 4.0;
			double thickness = mol.AverageBondLength / Math.Sqrt(this.Scale < 0.0001 ? 0.0001 : this.Scale) / 8.0;

			mtx *= Affine.NewScaling(scale * 0.80, scale * 0.80);
			mtx *= Affine.NewRotation(this.Angle);
			mtx *= Affine.NewScaling(this.Scale, this.Scale);
			mtx *= Affine.NewTranslation(this.CenterX, this.CenterY);

			double strokeWidth = this.ThicknessSlider.Value * thickness;
			foreach (Bond bond in mol.Bonds)
			{
				graphics.Render(new VertexSourceApplyTransform(BondPath(bond, strokeWidth), mtx), Color.Black);
			}

			foreach (Atom atom in mol.Atoms)
			{
				if (atom.Label != "C")
				{
					var disc = new Ellipse(atom.X, atom.Y, textSize * 2.5, textSize * 2.5, 20);
					graphics.Render(new VertexSourceApplyTransform(disc, mtx), Color.White);
				}
			}

			textSize *= 3.0;

			foreach (Atom atom in mol.Atoms)
			{
				if (atom.Label != "C")
				{
					var label = Text(atom.Label, atom.X - (textSize / 2), atom.Y - (textSize / 2), textSize, strokeWidth, mtx.GetScale());
					graphics.Render(new VertexSourceApplyTransform(label, mtx), AtomColors[atom.ColorIndex]);
				}
			}

			graphics.Render(Text(mol.Name, 10.0, StartHeight - 20.0, 10.0, 1.5, 1.0), Color.Black);

			this.ctrls.Render(graphics);
		}

		/// <summary>C++ <c>on_idle</c>, running once space has turned wait mode off: a tenth of a degree a frame.</summary>
		public override void OnIdle()
		{
			this.Angle += 0.1 * Math.PI / 180.0;
			this.Invalidate();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button))
			{
				return;
			}

			this.mouseMove = true;
			this.pdx = this.CenterX - x;
			this.pdy = this.CenterY - y;
			this.prevScale = this.Scale;
			this.prevAngle = this.Angle + Math.PI;
			this.Invalidate();
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (this.mouseMove && flags.HasFlag(AggInputFlags.MouseLeft))
			{
				double dx = x - this.CenterX;
				double dy = y - this.CenterY;

				// C++ divides by the press's distance from the centre, so a press right on the centre makes the
				// scale infinite (and the molecule vanish); the port leaves the scale alone there and only turns.
				double pressDistance = Math.Sqrt((this.pdx * this.pdx) + (this.pdy * this.pdy));
				if (pressDistance > 0)
				{
					this.Scale = this.prevScale * Math.Sqrt((dx * dx) + (dy * dy)) / pressDistance;
				}

				this.Angle = this.prevAngle + Math.Atan2(dy, dx) - Math.Atan2(this.pdy, this.pdx);
				this.Invalidate();
			}

			if (this.mouseMove && flags.HasFlag(AggInputFlags.MouseRight))
			{
				this.CenterX = x + this.pdx;
				this.CenterY = y + this.pdy;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.mouseMove = false;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			// As in C++ platform_support, the arrow keys go to a slider that was clicked before they reach on_key.
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			switch (key)
			{
				case Keys.Left:
				case Keys.Up:
				case Keys.PageUp:
					this.CurrentMolecule--;
					break;

				case Keys.Right:
				case Keys.Down:
				case Keys.PageDown:
					this.CurrentMolecule++;
					break;

				case Keys.Space:
					this.WaitMode = !this.WaitMode;
					break;
			}
		}

		/// <summary>C++ <c>bond_vertex_generator</c>: the bond's outline in molecule coordinates.</summary>
		private static VertexStorage BondPath(Bond bond, double thickness)
		{
			var path = new VertexStorage();
			if (bond.Order == 1 && bond.Stereo == 1)
			{
				// solid_wedge: a triangle from the first atom widening to the second.
				agg_math.calc_orthogonal(thickness * 2.0, bond.X1, bond.Y1, bond.X2, bond.Y2, out double dx, out double dy);
				path.MoveTo(bond.X1, bond.Y1);
				path.LineTo(bond.X2 - dx, bond.Y2 - dy);
				path.LineTo(bond.X2 + dx, bond.Y2 + dy);
			}
			else if (bond.Order == 1 && bond.Stereo == 6)
			{
				// dashed_wedge: its ends are swapped, so eight dashes widen from the second atom to the first.
				double x1 = bond.X2;
				double y1 = bond.Y2;
				double x2 = bond.X1;
				double y2 = bond.Y1;
				agg_math.calc_orthogonal(thickness * 2.0, x1, y1, x2, y2, out double dx, out double dy);
				double xt2 = x2 - dx;
				double yt2 = y2 - dy;
				double xt3 = x2 + dx;
				double yt3 = y2 + dy;
				const int NumDashes = 8;
				for (int i = 0; i < NumDashes; i++)
				{
					double k1 = (double)i / NumDashes;
					double k2 = k1 + (0.4 / NumDashes);
					path.MoveTo(x1 + ((xt2 - x1) * k1), y1 + ((yt2 - y1) * k1));
					path.LineTo(x1 + ((xt2 - x1) * k2), y1 + ((yt2 - y1) * k2));
					path.LineTo(x1 + ((xt3 - x1) * k2), y1 + ((yt3 - y1) * k2));
					path.LineTo(x1 + ((xt3 - x1) * k1), y1 + ((yt3 - y1) * k1));
				}
			}
			else if (bond.Order == 2)
			{
				// Every double bond is two lines either side of the bond: C++ leaves its left and right
				// offsets commented out, pending ring perception.
				agg_math.calc_orthogonal(thickness, bond.X1, bond.Y1, bond.X2, bond.Y2, out double dx, out double dy);
				AddLine(path, bond.X1 - dx, bond.Y1 - dy, bond.X2 - dx, bond.Y2 - dy, thickness);
				AddLine(path, bond.X1 + dx, bond.Y1 + dy, bond.X2 + dx, bond.Y2 + dy, thickness);
			}
			else
			{
				// Single bonds, and triple bonds, which C++ has yet to draw as anything else.
				AddLine(path, bond.X1, bond.Y1, bond.X2, bond.Y2, thickness);
			}

			return path;
		}

		/// <summary>C++ <c>agg::line</c>: a quad <paramref name="thickness"/> wide along the segment.</summary>
		private static void AddLine(VertexStorage path, double x1, double y1, double x2, double y2, double thickness)
		{
			agg_math.calc_orthogonal(thickness * 0.5, x1, y1, x2, y2, out double dx, out double dy);
			path.MoveTo(x1 - dx, y1 - dy);
			path.LineTo(x2 - dx, y2 - dy);
			path.LineTo(x2 + dx, y2 + dy);
			path.LineTo(x1 + dx, y1 + dy);
		}

		/// <summary>C++'s gsv_text through a round-joined, round-capped conv_stroke.</summary>
		private static Stroke Text(string text, double x, double y, double size, double width, double approximationScale)
		{
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; mol_view.cpp draws its labels with exactly this font.
			var label = new gsv_text();
#pragma warning restore CS0618
			label.text(text);
			label.start_point(x, y);
			label.size(size, 0.0);
			foreach (VertexData vertex in label.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			return new Stroke(outline, width)
			{
				LineJoin = LineJoin.Round,
				LineCap = LineCap.Round,
				ApproximationScale = approximationScale,
			};
		}

		private readonly struct Atom
		{
			public Atom(double x, double y, string label)
			{
				this.X = x;
				this.Y = y;
				this.Label = label;
				this.ColorIndex = label switch
				{
					"N" => 1,
					"O" => 2,
					"S" => 3,
					"P" => 4,
					"F" or "Cl" or "Br" or "I" => 5,
					_ => 0,
				};
			}

			public double X { get; }

			public double Y { get; }

			public string Label { get; }

			/// <summary>Index into <see cref="AtomColors"/>.</summary>
			public int ColorIndex { get; }
		}

		private readonly struct Bond
		{
			public Bond(double x1, double y1, double x2, double y2, int order, int stereo)
			{
				this.X1 = x1;
				this.Y1 = y1;
				this.X2 = x2;
				this.Y2 = y2;
				this.Order = order;
				this.Stereo = stereo;
			}

			public double X1 { get; }

			public double Y1 { get; }

			public double X2 { get; }

			public double Y2 { get; }

			public int Order { get; }

			public int Stereo { get; }
		}

		/// <summary>C++ <c>molecule</c>: one record of an MDL SDF file.</summary>
		private sealed class Molecule
		{
			public string Name { get; private set; }

			public List<Atom> Atoms { get; } = new List<Atom>();

			public List<Bond> Bonds { get; } = new List<Bond>();

			public double AverageBondLength { get; private set; }

			/// <summary>C++'s read loop: up to <paramref name="max"/> molecules, stopping at the first that does not read.</summary>
			public static List<Molecule> ReadAll(TextReader reader, int max)
			{
				var molecules = new List<Molecule>();
				while (molecules.Count < max)
				{
					var molecule = new Molecule();
					if (!molecule.Read(reader))
					{
						break;
					}

					molecules.Add(molecule);
				}

				return molecules;
			}

			// ReadLine takes LF, CRLF or CR endings alike, so the file reads the same whatever git checked out.
			private bool Read(TextReader reader)
			{
				string line = reader.ReadLine();
				if (line == null)
				{
					return false;
				}

				this.Name = line.Length > 128 ? line.Substring(0, 128) : line;
				if (reader.ReadLine() == null || reader.ReadLine() == null || (line = reader.ReadLine()) == null)
				{
					return false;
				}

				int numAtoms = GetInt(line, 1, 3);
				int numBonds = GetInt(line, 4, 3);
				if (numAtoms <= 0 || numBonds <= 0)
				{
					return false;
				}

				for (int i = 0; i < numAtoms; i++)
				{
					if ((line = reader.ReadLine()) == null)
					{
						return false;
					}

					// C++ also reads the charge (column 39), which nothing draws.
					this.Atoms.Add(new Atom(GetDouble(line, 1, 10), GetDouble(line, 11, 10), GetField(line, 32, 3)));
				}

				double totalLength = 0.0;
				for (int i = 0; i < numBonds; i++)
				{
					if ((line = reader.ReadLine()) == null)
					{
						return false;
					}

					int idx1 = GetInt(line, 1, 3) - 1;
					int idx2 = GetInt(line, 4, 3) - 1;
					if (idx1 < 0 || idx1 >= numAtoms || idx2 < 0 || idx2 >= numAtoms)
					{
						return false;
					}

					Atom a1 = this.Atoms[idx1];
					Atom a2 = this.Atoms[idx2];
					this.Bonds.Add(new Bond(a1.X, a1.Y, a2.X, a2.Y, GetInt(line, 7, 3), GetInt(line, 10, 3)));
					totalLength += Math.Sqrt(((a1.X - a2.X) * (a1.X - a2.X)) + ((a1.Y - a2.Y) * (a1.Y - a2.Y)));
				}

				this.AverageBondLength = totalLength / numBonds;

				while ((line = reader.ReadLine()) != null)
				{
					if (line.StartsWith("$", StringComparison.Ordinal))
					{
						return true;
					}
				}

				return false;
			}

			/// <summary>
			/// Columns <paramref name="pos"/> to pos + len - 1 (1-based) of the line, clipped to it, with leading
			/// spaces skipped and cut at the next space. C++'s get_str trims leading spaces only while the field
			/// ends before the line does, and on an all-space field counts its length below zero and returns an
			/// unset buffer; every field 1.sdf holds reads the same both ways.
			/// </summary>
			private static string GetField(string line, int pos, int len)
			{
				int start = pos - 1;
				if (start >= line.Length)
				{
					return string.Empty;
				}

				int end = Math.Min(line.Length, start + len);
				while (start < end && char.IsWhiteSpace(line[start]))
				{
					start++;
				}

				int stop = start;
				while (stop < end && !char.IsWhiteSpace(line[stop]))
				{
					stop++;
				}

				return line.Substring(start, stop - start);
			}

			/// <summary>C's atoi: the leading integer, 0 when there is none.</summary>
			private static int GetInt(string line, int pos, int len)
			{
				string field = GetField(line, pos, len);
				int i = field.Length > 0 && (field[0] == '-' || field[0] == '+') ? 1 : 0;
				int value = 0;
				for (; i < field.Length && field[i] >= '0' && field[i] <= '9'; i++)
				{
					value = (value * 10) + (field[i] - '0');
				}

				return field.StartsWith("-", StringComparison.Ordinal) ? -value : value;
			}

			/// <summary>C's atof on a plain decimal field; 0 when it does not parse.</summary>
			private static double GetDouble(string line, int pos, int len)
			{
				return double.TryParse(GetField(line, pos, len), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : 0.0;
			}
		}
	}
}
