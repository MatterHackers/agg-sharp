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
//
// vpgen_segmentator
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// C++ AGG's vpgen_segmentator: splits each line segment into pieces about 1 / <see cref="ApproximationScale"/>
	/// long, so a non-linear transform applied afterwards can bend it.
	/// </summary>
	internal class SegmentatorProcessor : IVertexProcessor
	{
		private double x1;
		private double y1;
		private double dx;
		private double dy;
		private double dl;
		private double ddl;
		private FlagsAndCommand command;

		public double ApproximationScale { get; set; } = 1.0;

		public bool AutoClose => false;

		public bool AutoUnclose => false;

		public void Reset()
		{
			command = FlagsAndCommand.Stop;
		}

		public void MoveTo(double x, double y)
		{
			x1 = x;
			y1 = y;
			dx = 0.0;
			dy = 0.0;
			dl = 2.0;
			ddl = 2.0;
			command = FlagsAndCommand.MoveTo;
		}

		public void LineTo(double x, double y)
		{
			x1 += dx;
			y1 += dy;
			dx = x - x1;
			dy = y - y1;
			double len = Math.Sqrt(dx * dx + dy * dy) * ApproximationScale;
			if (len < 1e-30)
			{
				len = 1e-30;
			}

			ddl = 1.0 / len;

			// A pending move-to still has to be emitted, so the segment starts at its first point;
			// otherwise that point was already emitted as the previous segment's end.
			dl = (command == FlagsAndCommand.MoveTo) ? 0.0 : ddl;
			if (command == FlagsAndCommand.Stop)
			{
				command = FlagsAndCommand.LineTo;
			}
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (command == FlagsAndCommand.Stop)
			{
				return FlagsAndCommand.Stop;
			}

			FlagsAndCommand result = command;
			command = FlagsAndCommand.LineTo;
			if (dl >= 1.0 - ddl)
			{
				dl = 1.0;
				command = FlagsAndCommand.Stop;
				x = x1 + dx;
				y = y1 + dy;
				return result;
			}

			x = x1 + dx * dl;
			y = y1 + dy * dl;
			dl += ddl;
			return result;
		}
	}
}
