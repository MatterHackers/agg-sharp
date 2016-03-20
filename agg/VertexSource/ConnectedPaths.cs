using MatterHackers.VectorMath;

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
	//------------------------------------------------------------rounded_rect
	//
	// See Implementation agg_rounded_rect.cpp
	//
	public class ConnectedPaths : VertexSourceLegacySupport
	{
		public ConnectedPaths()
		{
		}

		public ConnectedPaths(IVertexSource a, IVertexSource b)
			: this(new IVertexSource[] { a, b })
		{
		}

		public ConnectedPaths(IEnumerable<IVertexSource> paths)
		{
			SourcPaths.AddRange(paths);
		}

		private List<IVertexSource> SourcPaths { get; } = new List<IVertexSource>();

		override public IEnumerable<VertexData> Vertices()
		{
			for (int i = 0; i < SourcPaths.Count; i++)
			{
				IVertexSource sourcePath = SourcPaths[i];
				bool firstMove = true;
				foreach (VertexData vertexData in sourcePath.Vertices())
				{
					// skip the initial command if it is not the first path and is a moveto.
					if (i > 0
						&& firstMove
						&& ShapePath.is_move_to(vertexData.command))
					{
						continue;
					}

					// when we hit a stop move on to the next path
					if (ShapePath.is_stop(vertexData.command))
					{
						break;
					}
					yield return vertexData;
				}
			}

			// and send the actual stop
			yield return new VertexData(ShapePath.FlagsAndCommand.CommandStop, new Vector2());
		}
	}
}