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

	public class ReversePath : VertexSourceLegacySupport
	{
		public ReversePath(IVertexSource sourcePath)
		{
			SourcPath = sourcePath;
		}

		public IVertexSource SourcPath { get; }

		public override IEnumerable<VertexData> Vertices()
		{
				IVertexSource sourcePath = SourcPath;
				foreach (VertexData vertexData in sourcePath.Vertices())
				{
					// when we hit the initial stop. Skip it
					if (ShapePath.is_stop(vertexData.command))
					{
						break;
					}
					yield return vertexData;
				}

			// and send the actual stop
			yield return new VertexData(ShapePath.FlagsAndCommand.EndPoly | ShapePath.FlagsAndCommand.FlagClose | ShapePath.FlagsAndCommand.FlagCCW, new Vector2());
			yield return new VertexData(ShapePath.FlagsAndCommand.Stop, new Vector2());
		}
	}
}