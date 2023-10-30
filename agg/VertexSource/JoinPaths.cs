﻿using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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
	/// This class is used to strip out the close and first move of multiple paths
	/// so they render as a single set of LineTo (s) and internal MoveTo (s)
	/// </summary>
	public class JoinPaths : VertexSourceLegacySupport
	{
		public JoinPaths()
		{
		}

		public JoinPaths(IVertexSource a, IVertexSource b)
			: this(new IVertexSource[] { a, b })
		{
		}

		public JoinPaths(IEnumerable<IVertexSource> paths)
		{
			SourcePaths.AddRange(paths);
		}

		public List<IVertexSource> SourcePaths { get; } = new List<IVertexSource>();

		public override IEnumerable<VertexData> Vertices()
		{
			for (int i = 0; i < SourcePaths.Count; i++)
			{
				IVertexSource sourcePath = SourcePaths[i];
				bool firstMove = true;
				foreach (VertexData vertexData in sourcePath.Vertices())
				{
					// skip the initial command if it is not the first path and is a moveto.
					if (i > 0
						&& firstMove
						&& ShapePath.IsMoveTo(vertexData.command))
					{
						continue;
					}

					// when we hit a stop move on to the next path
					if (ShapePath.IsStop(vertexData.command))
					{
						break;
					}
					yield return vertexData;
				}
			}

			// and send the actual stop
			yield return new VertexData(ShapePath.FlagsAndCommand.EndPoly | ShapePath.FlagsAndCommand.FlagClose | ShapePath.FlagsAndCommand.FlagCCW, new Vector2());
			yield return new VertexData(ShapePath.FlagsAndCommand.Stop, new Vector2());
		}
	}
}