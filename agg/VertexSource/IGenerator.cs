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
		void AddVertex(double x, double y, ShapePath.FlagsAndCommand unknown);

		void ApproximationScale(double approxScale);

		double ApproximationScale();

		void AutoDetectOrientation(bool v);

		bool AutoDetectOrientation();

		InnerJoin InnerJoin();

		void InnerJoin(InnerJoin ij);

		void InnerMiterLimit(double ml);

		double InnerMiterLimit();

		LineCap LineCap();

		void LineCap(LineCap lc);

		LineJoin LineJoin();

		void LineJoin(LineJoin lj);

		void MiterLimit(double ml);

		double MiterLimit();

		void MiterLimitTheta(double t);

		void RemoveAll();
		void Rewind(int path_id);

		void Shorten(double s);

		double Shorten();

		ShapePath.FlagsAndCommand Vertex(ref double x, ref double y);
		double width();

		void Width(double w);
	};
}