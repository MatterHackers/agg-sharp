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
	/// conv_concat: every vertex of the first source, then every vertex of the second, each untouched. The
	/// path id given to <see cref="Rewind"/> goes to the first source; the second is always read from path 0.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="CombinePaths"/> this reads its sources lazily, in order, and adds nothing: a
	/// <see cref="MarkerPlacer"/> second source is only read once the first (the stroke that fills its
	/// markers) has run out, as C++ relies on.
	/// </remarks>
	public class ConcatPaths : IVertexSource
	{
		private readonly IVertexSource source1;
		private readonly IVertexSource source2;
		private int status = 2;

		public ConcatPaths(IVertexSource source1, IVertexSource source2)
		{
			this.source1 = source1;
			this.source2 = source2;
		}

		public void Rewind(int pathId = 0)
		{
			source1.Rewind(pathId);
			source2.Rewind(0);
			status = 0;
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			FlagsAndCommand cmd;
			if (status == 0)
			{
				cmd = source1.Vertex(out x, out y);
				if (!ShapePath.IsStop(cmd))
				{
					return cmd;
				}

				status = 1;
			}

			if (status == 1)
			{
				cmd = source2.Vertex(out x, out y);
				if (!ShapePath.IsStop(cmd))
				{
					return cmd;
				}

				status = 2;
			}

			return FlagsAndCommand.Stop;
		}

		public IEnumerable<VertexData> Vertices()
		{
			Rewind(0);
			FlagsAndCommand command;
			do
			{
				command = Vertex(out double x, out double y);
				yield return new VertexData(command, new Vector2(x, y));
			}
			while (command != FlagsAndCommand.Stop);
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
