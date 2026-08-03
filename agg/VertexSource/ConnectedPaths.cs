using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
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
// Rounded rectangle vertex generator
//
//----------------------------------------------------------------------------
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// This class is used to merge multiple paths into a single IVertexSource path.
	/// This is great to do things like have a path as an outside an a second path that can become an inside hole.
	/// </summary>
	public class CombinePaths : VertexSourceLegacySupport
	{
		public CombinePaths()
		{
		}

		public CombinePaths(IVertexSource a, IVertexSource b)
			: this(new IVertexSource[] { a, b })
		{
		}

		public CombinePaths(IEnumerable<IVertexSource> paths)
		{
			SourcePaths.AddRange(paths);
		}

		public List<IVertexSource> SourcePaths { get; } = new List<IVertexSource>();

		public override IEnumerable<VertexData> Vertices()
		{
			// whether the last thing handed on was a contour closing itself
			var endedClosed = false;

			for (int i = 0; i < SourcePaths.Count; i++)
			{
				IVertexSource sourcePath = SourcePaths[i];
				foreach (VertexData vertexData in sourcePath.Vertices())
				{
					// when we hit a stop move on to the next path
					if (ShapePath.IsStop(vertexData.Command))
					{
						break;
					}

					endedClosed = vertexData.IsClose;

					yield return vertexData;
				}
			}

			// This terminator used to be sent unconditionally, which closed a final contour that had been
			// deliberately left open - a line combined with anything came out the far end as a polygon, and
			// consumers that tell the two apart by the close flag could no longer see the difference. It is
			// only repeated now when the stream is already ending closed, so every path that was closed before
			// produces exactly the vertices it always did.
			if (endedClosed)
			{
				yield return new VertexData(FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW, new Vector2());
			}

			// and send the actual stop
			yield return new VertexData(FlagsAndCommand.Stop, new Vector2());
		}
	}
}