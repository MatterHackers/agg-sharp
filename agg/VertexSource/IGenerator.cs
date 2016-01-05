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

		LineCap line_cap();

		LineJoin line_join();

		InnerJoin inner_join();

		void line_cap(LineCap lc);

		void line_join(LineJoin lj);

		void inner_join(InnerJoin ij);

		void width(double w);

		void miter_limit(double ml);

		void miter_limit_theta(double t);

		void inner_miter_limit(double ml);

		void approximation_scale(double approxScale);

		double width();

		double miter_limit();

		double inner_miter_limit();

		double approximation_scale();

		void auto_detect_orientation(bool v);

		bool auto_detect_orientation();

		void shorten(double s);

		double shorten();
	};
}