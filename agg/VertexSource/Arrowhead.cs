//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// agg::arrowhead: the marker shapes for <see cref="MarkerPlacer"/>. Path 0 is the tail (a six-point
	/// swallowtail), path 1 the head (a four-point arrow), each drawn around the origin pointing along +x
	/// and empty until enabled with <see cref="Tail(double, double, double, double)"/> or
	/// <see cref="Head(double, double, double, double)"/>.
	/// </summary>
	public class Arrowhead : IVertexSource
	{
		private double headD1 = 1.0;
		private double headD2 = 1.0;
		private double headD3 = 1.0;
		private double headD4 = 0.0;
		private double tailD1 = 1.0;
		private double tailD2 = 1.0;
		private double tailD3 = 1.0;
		private double tailD4 = 0.0;
		private bool headFlag;
		private bool tailFlag;
		private readonly double[] coord = new double[16];
		private readonly FlagsAndCommand[] cmd = new FlagsAndCommand[8];
		private int currentId;
		private int currentCoord;

		/// <summary>
		/// Enables the head, in marker space (+x toward the direction point, which for
		/// <see cref="TerminalMarkers"/> heads is back along the path): the tip at (-d1, 0), the notch at
		/// (d2, 0), the barbs at (d2 + d4, +-d3).
		/// </summary>
		public void Head(double d1, double d2, double d3, double d4)
		{
			headD1 = d1;
			headD2 = d2;
			headD3 = d3;
			headD4 = d4;
			headFlag = true;
		}

		public void Head()
		{
			headFlag = true;
		}

		public void NoHead()
		{
			headFlag = false;
		}

		/// <summary>
		/// Enables the tail, a swallowtail in marker space: front at (d1, 0), fins from (d1 - d4, +-d3) back
		/// to (-d2 - d4, +-d3), notch at (-d2, 0).
		/// </summary>
		public void Tail(double d1, double d2, double d3, double d4)
		{
			tailD1 = d1;
			tailD2 = d2;
			tailD3 = d3;
			tailD4 = d4;
			tailFlag = true;
		}

		public void Tail()
		{
			tailFlag = true;
		}

		public void NoTail()
		{
			tailFlag = false;
		}

		public void Rewind(int pathId = 0)
		{
			currentId = pathId;
			currentCoord = 0;
			if (pathId == 0)
			{
				if (!tailFlag)
				{
					cmd[0] = FlagsAndCommand.Stop;
					return;
				}

				coord[0] = tailD1; coord[1] = 0.0;
				coord[2] = tailD1 - tailD4; coord[3] = tailD3;
				coord[4] = -tailD2 - tailD4; coord[5] = tailD3;
				coord[6] = -tailD2; coord[7] = 0.0;
				coord[8] = -tailD2 - tailD4; coord[9] = -tailD3;
				coord[10] = tailD1 - tailD4; coord[11] = -tailD3;

				cmd[0] = FlagsAndCommand.MoveTo;
				cmd[1] = FlagsAndCommand.LineTo;
				cmd[2] = FlagsAndCommand.LineTo;
				cmd[3] = FlagsAndCommand.LineTo;
				cmd[4] = FlagsAndCommand.LineTo;
				cmd[5] = FlagsAndCommand.LineTo;
				cmd[7] = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW;
				// C++ writes stop into slot 6, so the tail ends without its close command; the rasterizer
				// closes the polygon anyway. Kept as is for byte-exact output.
				cmd[6] = FlagsAndCommand.Stop;
				return;
			}

			if (pathId == 1)
			{
				if (!headFlag)
				{
					cmd[0] = FlagsAndCommand.Stop;
					return;
				}

				coord[0] = -headD1; coord[1] = 0.0;
				coord[2] = headD2 + headD4; coord[3] = -headD3;
				coord[4] = headD2; coord[5] = 0.0;
				coord[6] = headD2 + headD4; coord[7] = headD3;

				cmd[0] = FlagsAndCommand.MoveTo;
				cmd[1] = FlagsAndCommand.LineTo;
				cmd[2] = FlagsAndCommand.LineTo;
				cmd[3] = FlagsAndCommand.LineTo;
				cmd[4] = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW;
				cmd[5] = FlagsAndCommand.Stop;
			}
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (currentId < 2)
			{
				int currentIndex = currentCoord * 2;
				x = coord[currentIndex];
				y = coord[currentIndex + 1];
				return cmd[currentCoord++];
			}

			return FlagsAndCommand.Stop;
		}

		/// <summary>
		/// The tail then the head, each ending in Stop only once both are read.
		/// </summary>
		public IEnumerable<VertexData> Vertices()
		{
			for (int pathId = 0; pathId < 2; pathId++)
			{
				Rewind(pathId);
				FlagsAndCommand command;
				while (!ShapePath.IsStop(command = Vertex(out double x, out double y)))
				{
					yield return new VertexData(command, new Vector2(x, y));
				}
			}

			yield return new VertexData(FlagsAndCommand.Stop, Vector2.Zero);
		}

		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			foreach (var vertex in this.Vertices())
			{
				hash = vertex.GetLongHashCode(hash);
			}

			return hash;
		}
	}
}
