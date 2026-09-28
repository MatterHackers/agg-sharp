using System;

namespace MatterHackers.Agg
{
	public enum CommandHint
	{
        None,
        C4ControlFromPrev,
		C4ControlToPoint,
		C4Point,
        C3ControlFromPrev,
        C3Point,
    }

    [Flags]
    public enum FlagsAndCommand
    {
        Stop = 0x00,
        MoveTo = 0x01,
        LineTo = 0x02,
        Curve3 = 0x03,
        Curve4 = 0x04,
        EndPoly = 0x0F,
        CommandsMask = 0x0F,

        FlagNone = 0x00,
        FlagCCW = 0x10,
        FlagCW = 0x20,
        FlagClose = 0x40,
        FlagsMask = 0xF0
    };
    
	public static class ShapePath
	{
		public static bool IsVertex(FlagsAndCommand c)
		{
			return c >= FlagsAndCommand.MoveTo
				&& c < FlagsAndCommand.EndPoly;
		}

		public static bool is_drawing(FlagsAndCommand c)
		{
			return c >= FlagsAndCommand.LineTo && c < FlagsAndCommand.EndPoly;
		}

		public static bool IsStop(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.Stop;
		}

		public static bool IsMoveTo(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.MoveTo;
		}

		public static bool IsLineTo(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.LineTo;
		}

		public static bool IsCurve(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.Curve3
				|| c == FlagsAndCommand.Curve4;
		}

		public static bool is_curve3(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.Curve3;
		}

		public static bool is_curve4(FlagsAndCommand c)
		{
			return c == FlagsAndCommand.Curve4;
		}

		public static bool is_end_poly(FlagsAndCommand c)
		{
			return (c & FlagsAndCommand.CommandsMask) == FlagsAndCommand.EndPoly;
		}

		public static bool IsClose(FlagsAndCommand c)
		{
			return (c & ~(FlagsAndCommand.FlagCW | FlagsAndCommand.FlagCCW)) ==
				   (FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose);
		}

		public static bool IsNextPoly(FlagsAndCommand c)
		{
			return IsStop(c) || IsMoveTo(c) || is_end_poly(c);
		}

		public static bool is_cw(FlagsAndCommand c)
		{
			return (c & FlagsAndCommand.FlagCW) != 0;
		}

		public static bool is_ccw(FlagsAndCommand c)
		{
			return (c & FlagsAndCommand.FlagCCW) != 0;
		}

		public static bool is_oriented(FlagsAndCommand c)
		{
			return (c & (FlagsAndCommand.FlagCW | FlagsAndCommand.FlagCCW)) != 0;
		}

		public static bool is_closed(FlagsAndCommand c)
		{
			return (c & FlagsAndCommand.FlagClose) != 0;
		}

		public static FlagsAndCommand get_close_flag(FlagsAndCommand c)
		{
			return (FlagsAndCommand)(c & FlagsAndCommand.FlagClose);
		}

		public static FlagsAndCommand clear_orientation(FlagsAndCommand c)
		{
			return c & ~(FlagsAndCommand.FlagCW | FlagsAndCommand.FlagCCW);
		}

		public static FlagsAndCommand get_orientation(FlagsAndCommand c)
		{
			return c & (FlagsAndCommand.FlagCW | FlagsAndCommand.FlagCCW);
		}

		/*
		//---------------------------------------------------------set_orientation
		public static path_flags_e set_orientation(int c, path_flags_e o)
		{
			return clear_orientation(c) | o;
		}
		 */

		static public void shorten_path(MatterHackers.Agg.VertexSequence vs, double s)
		{
			shorten_path(vs, s, 0);
		}

		static public void shorten_path(VertexSequence vs, double s, int closed)
		{
			if (s > 0.0 && vs.Count > 1)
			{
				double d;
				int n = (int)(vs.Count - 2);
				// C++ loops while (n), so the first segment is never tested: a shorten longer than it moved the
				// end point back past the start and reversed the path. Testing it too drops the last point, and
				// the path empties below (ShortenPathTests).
				while (n >= 0)
				{
					d = vs[n].dist;
					if (d > s) break;
					vs.RemoveLast();
					s -= d;
					--n;
				}
				if (vs.Count < 2)
				{
					vs.Clear();
				}
				else
				{
					n = (int)vs.Count - 1;
					// By ref, as C++'s vertex_type&: the moved end point has to land in the sequence
					// (a copy left Shorten doing nothing).
					ref VertexDistance prev = ref vs.Array[n - 1];
					ref VertexDistance last = ref vs.Array[n];
					d = (prev.dist - s) / prev.dist;
					double x = prev.x + (last.x - prev.x) * d;
					double y = prev.y + (last.y - prev.y) * d;
					last.x = x;
					last.y = y;
					if (!prev.IsEqual(last)) vs.RemoveLast();
					vs.close(closed != 0);
				}
			}
		}
	}
}