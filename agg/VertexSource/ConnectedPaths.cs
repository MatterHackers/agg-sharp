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
					yield return vertexData;
				}
			}

			// and send the actual stop
			yield return new VertexData(FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW, new Vector2());
			yield return new VertexData(FlagsAndCommand.Stop, new Vector2());
		}
	}
}