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
// conv_adaptor_vpgen
//
//----------------------------------------------------------------------------
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// C++ AGG's conv_adaptor_vpgen: runs a source path through an <see cref="IVertexProcessor"/> segment by
	/// segment, passing end-poly commands through (and closing polygons when the processor asks to).
	/// </summary>
	public class VertexProcessorAdapter : IVertexSourceProxy
	{
		private double startX;

		private double startY;

		private FlagsAndCommand polyFlags;

		// Vertices of the current sub-path so far; -1 means a move-to to the stored start is pending
		// and -2 that the source has stopped after a forced close.
		private int vertices;

		public VertexProcessorAdapter(IVertexSource vertexSource, IVertexProcessor processor)
		{
			this.VertexSource = vertexSource;
			this.Processor = processor;
		}

		public IVertexSource VertexSource { get; set; }

		protected IVertexProcessor Processor { get; }

		public ulong GetLongHashCode(ulong hash = 14695981039346656037)
		{
			foreach (var vertex in this.Vertices())
			{
				hash = vertex.GetLongHashCode(hash);
			}

			return hash;
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

		public void Rewind(int pathId)
		{
			VertexSource.Rewind(pathId);
			Processor.Reset();
			startX = 0;
			startY = 0;
			polyFlags = 0;
			vertices = 0;
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			FlagsAndCommand command;
			for (; ; )
			{
				command = Processor.Vertex(out x, out y);
				if (!ShapePath.IsStop(command))
				{
					break;
				}

				if (polyFlags != 0 && !Processor.AutoUnclose)
				{
					x = 0.0;
					y = 0.0;
					command = polyFlags;
					polyFlags = 0;
					break;
				}

				if (vertices < 0)
				{
					if (vertices < -1)
					{
						vertices = 0;
						return FlagsAndCommand.Stop;
					}

					Processor.MoveTo(startX, startY);
					vertices = 1;
					continue;
				}

				command = VertexSource.Vertex(out double tx, out double ty);
				if (ShapePath.IsVertex(command))
				{
					if (ShapePath.IsMoveTo(command))
					{
						if (Processor.AutoClose && vertices > 2)
						{
							Processor.LineTo(startX, startY);
							polyFlags = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose;
							startX = tx;
							startY = ty;
							vertices = -1;
							continue;
						}

						Processor.MoveTo(tx, ty);
						startX = tx;
						startY = ty;
						vertices = 1;
					}
					else
					{
						Processor.LineTo(tx, ty);
						++vertices;
					}
				}
				else if (ShapePath.is_end_poly(command))
				{
					polyFlags = command;
					if (ShapePath.is_closed(command) || Processor.AutoClose)
					{
						if (Processor.AutoClose)
						{
							polyFlags |= FlagsAndCommand.FlagClose;
						}

						if (vertices > 2)
						{
							Processor.LineTo(startX, startY);
						}

						vertices = 0;
					}
				}
				else
				{
					// Stop
					if (Processor.AutoClose && vertices > 2)
					{
						Processor.LineTo(startX, startY);
						polyFlags = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose;
						vertices = -2;
						continue;
					}

					break;
				}
			}

			return command;
		}
	}
}
