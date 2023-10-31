﻿using MatterHackers.VectorMath;
using System;

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
using System.Linq;

namespace MatterHackers.Agg.VertexSource
{

	public class ReversePath : VertexSourceLegacySupport
	{
		private bool convertNextToMove;

		public ReversePath(IVertexSource sourcePath)
		{
			SourcePath = sourcePath;
		}

		public IVertexSource SourcePath { get; }

		public override IEnumerable<VertexData> Vertices()
		{
			IVertexSource sourcePath = SourcePath;
			foreach (VertexData vertexData in sourcePath.Vertices().Reverse())
			{
				// when we hit the initial stop. Skip it
				if (vertexData.IsClose || vertexData.IsStop)
				{
					if (vertexData.IsClose)
					{
						convertNextToMove = true;
					}

					continue;
				}

				if (convertNextToMove)
				{
					convertNextToMove = false;

					yield return new VertexData()
					{
						Position = vertexData.Position,
						Command = FlagsAndCommand.MoveTo
					};

					continue;
				}

				yield return vertexData;
			}

			// and send the actual stop
			yield return new VertexData(FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW, new Vector2());
			yield return new VertexData(FlagsAndCommand.Stop, new Vector2());
		}
	}
}