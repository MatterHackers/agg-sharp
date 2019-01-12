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

namespace MatterHackers.Agg.VertexSource
{
	public interface IGenerator
	{
		double ApproximationScale { get; set; }

		bool AutoDetectOrientation { get; set; }

		InnerJoin InnerJoin { get; set; }

		double InnerMiterLimit { get; set; }

		LineCap LineCap { get; set; }

		LineJoin LineJoin { get; set; }

		double MiterLimit { get; set; }

		void MiterLimitTheta(double t);

		double Shorten { get; set; }

		double Width { get; set; }

		void RemoveAll();
		void Rewind(int path_id);
		void AddVertex(double x, double y, ShapePath.FlagsAndCommand unknown);

		ShapePath.FlagsAndCommand Vertex(ref double x, ref double y);
	};
}