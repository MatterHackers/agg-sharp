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

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// vcgen_vertex_sequence: hands a sub-path back as it came in, with coinciding points merged and its end
	/// cut back by <see cref="Shorten"/>, followed by an end-poly carrying the sub-path's close flags. The
	/// generator behind <see cref="MarkerAdaptor"/>.
	/// </summary>
	/// <remarks>
	/// C++ stores each point's command and returns it. <see cref="VertexSequence"/> holds no command, so the
	/// first point comes back as a MoveTo and every other as a LineTo - what C++ returns for the flattened
	/// paths this is fed (a curve command would come back as a LineTo, which a rasterizer draws the same way).
	/// </remarks>
	public class VertexSequenceGenerator : IGenerator
	{
		private readonly VertexSequence sourceVertices = new VertexSequence();
		private FlagsAndCommand flags;
		private int currentVertex;
		private bool ready;

		/// <summary>Length cut off the end of each sub-path (vcgen_vertex_sequence::shorten).</summary>
		public double Shorten { get; set; }

		double IGenerator.ApproximationScale { get; set; }

		bool IGenerator.AutoDetectOrientation { get; set; }

		InnerJoin IGenerator.InnerJoin { get; set; }

		double IGenerator.InnerMiterLimit { get; set; }

		LineCap IGenerator.LineCap { get; set; }

		LineJoin IGenerator.LineJoin { get; set; }

		double IGenerator.MiterLimit { get; set; }

		double IGenerator.Width { get; set; }

		public void RemoveAll()
		{
			ready = false;
			sourceVertices.Clear();
			currentVertex = 0;
			flags = 0;
		}

		public void AddVertex(double x, double y, FlagsAndCommand cmd)
		{
			ready = false;
			if (ShapePath.IsMoveTo(cmd))
			{
				sourceVertices.modify_last(new VertexDistance(x, y));
			}
			else if (ShapePath.IsVertex(cmd))
			{
				sourceVertices.Add(new VertexDistance(x, y));
			}
			else
			{
				flags = cmd & FlagsAndCommand.FlagsMask;
			}
		}

		public void Rewind(int pathId)
		{
			if (!ready)
			{
				sourceVertices.close(ShapePath.is_closed(flags));
				ShapePath.shorten_path(sourceVertices, Shorten, (int)(flags & FlagsAndCommand.FlagClose));
			}

			ready = true;
			currentVertex = 0;
		}

		public FlagsAndCommand Vertex(ref double x, ref double y)
		{
			if (!ready)
			{
				Rewind(0);
			}

			if (currentVertex == sourceVertices.Count)
			{
				++currentVertex;
				return FlagsAndCommand.EndPoly | flags;
			}

			if (currentVertex > sourceVertices.Count)
			{
				return FlagsAndCommand.Stop;
			}

			VertexDistance vertex = sourceVertices[currentVertex];
			x = vertex.x;
			y = vertex.y;
			return currentVertex++ == 0 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo;
		}

		void IGenerator.MiterLimitTheta(double t)
		{
		}
	}
}
