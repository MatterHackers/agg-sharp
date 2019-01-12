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
		void RemoveAll();

		void AddVertex(double x, double y, ShapePath.FlagsAndCommand unknown);

		void Rewind(int path_id);

		ShapePath.FlagsAndCommand Vertex(ref double x, ref double y);

		LineCap LineCap();

		LineJoin LineJoin();

		InnerJoin InnerJoin();

		void LineCap(LineCap lc);

		void LineJoin(LineJoin lj);

		void InnerJoin(InnerJoin ij);

		void Width(double w);

		void MiterLimit(double ml);

		void MiterLimitTheta(double t);

		void InnerMiterLimit(double ml);

		void ApproximationScale(double approxScale);

		double width();

		double MiterLimit();

		double InnerMiterLimit();

		double ApproximationScale();

		void AutoDetectOrientation(bool v);

		bool AutoDetectOrientation();

		void Shorten(double s);

		double Shorten();
	};
}