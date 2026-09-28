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
using System;
using System.Collections.Generic;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// conv_marker: draws a marker shape at every marker location. The locator (e.g.
	/// <see cref="TerminalMarkers"/>) is read path by path from 0 until a path yields no marker; each of its
	/// point pairs places shape path i (e.g. <see cref="Arrowhead"/>: 0 = tail, 1 = head) at the first
	/// point, rotated so +x points at the second, after <see cref="Transform"/>.
	/// </summary>
	public class MarkerPlacer : IVertexSource
	{
		private readonly IVertexSource markerLocator;
		private readonly IVertexSource markerShapes;
		private Affine markerTransform;
		private Status status = Status.Initial;
		private int marker;
		private int numMarkers = 1;

		private enum Status
		{
			Initial,
			Markers,
			Polygon,
			Stop
		}

		public MarkerPlacer(IVertexSource markerLocator, IVertexSource markerShapes)
		{
			this.markerLocator = markerLocator;
			this.markerShapes = markerShapes;
		}

		/// <summary>
		/// Applied to each shape before it is rotated and moved onto its marker (e.g. a scale). Affine is a
		/// struct, so assign a new value; changing the one this returns changes only a copy.
		/// </summary>
		public Affine Transform { get; set; } = Affine.NewIdentity();

		public void Rewind(int pathId = 0)
		{
			status = Status.Initial;
			marker = 0;
			numMarkers = 1;
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			FlagsAndCommand cmd = FlagsAndCommand.MoveTo;
			while (!ShapePath.IsStop(cmd))
			{
				switch (status)
				{
					case Status.Initial:
						if (numMarkers == 0)
						{
							cmd = FlagsAndCommand.Stop;
							break;
						}

						markerLocator.Rewind(marker);
						++marker;
						numMarkers = 0;
						status = Status.Markers;
						goto case Status.Markers;

					case Status.Markers:
						{
							if (ShapePath.IsStop(markerLocator.Vertex(out double x1, out double y1)))
							{
								status = Status.Initial;
								break;
							}

							if (ShapePath.IsStop(markerLocator.Vertex(out double x2, out double y2)))
							{
								status = Status.Initial;
								break;
							}

							++numMarkers;
							markerTransform = Transform;
							markerTransform *= Affine.NewRotation(Math.Atan2(y2 - y1, x2 - x1));
							markerTransform *= Affine.NewTranslation(x1, y1);
							markerShapes.Rewind(marker - 1);
							status = Status.Polygon;
							goto case Status.Polygon;
						}

					case Status.Polygon:
						cmd = markerShapes.Vertex(out x, out y);
						if (ShapePath.IsStop(cmd))
						{
							cmd = FlagsAndCommand.MoveTo;
							status = Status.Markers;
							break;
						}

						markerTransform.Transform(ref x, ref y);
						return cmd;

					case Status.Stop:
						cmd = FlagsAndCommand.Stop;
						break;
				}
			}

			return cmd;
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
