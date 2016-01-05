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
// vertex_sequence container and vertex_dist struct
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{
	//----------------------------------------------------------vertex_sequence
	// Modified agg::pod_vector. The data is interpreted as a sequence
	// of vertices. It means that the type T must expose:
	//
	// bool T::operator() (const T& val)
	//
	// that is called every time a new vertex is being added. The main purpose
	// of this operator is the possibility to calculate some values during
	// adding and to return true if the vertex fits some criteria or false if
	// it doesn't. In the last case the new vertex is not added.
	//
	// The simple example is filtering coinciding vertices with calculation
	// of the distance between the current and previous ones:
	//
	//    struct vertex_dist
	//    {
	//        double   x;
	//        double   y;
	//        double   dist;
	//
	//        vertex_dist() {}
	//        vertex_dist(double x_, double y_) :
	//            x(x_),
	//            y(y_),
	//            dist(0.0)
	//        {
	//        }
	//
	//        bool operator () (const vertex_dist& val)
	//        {
	//            return (dist = calc_distance(x, y, val.x, val.y)) > EPSILON;
	//        }
	//    };
	//
	// Function close() calls this operator and removes the last vertex if
	// necessary.
	//------------------------------------------------------------------------
	public class VertexSequence : VectorPOD<VertexDistance>
	{
		public override void add(VertexDistance val)
		{
			if (base.size() > 1)
			{
				if (!Array[base.size() - 2].IsEqual(Array[base.size() - 1]))
				{
					base.RemoveLast();
				}
			}
			base.add(val);
		}

		public void modify_last(VertexDistance val)
		{
			base.RemoveLast();
			add(val);
		}

		public void close(bool closed)
		{
			while (base.size() > 1)
			{
				if (Array[base.size() - 2].IsEqual(Array[base.size() - 1])) break;
				VertexDistance t = this[base.size() - 1];
				base.RemoveLast();
				modify_last(t);
			}

			if (closed)
			{
				while (base.size() > 1)
				{
					if (Array[base.size() - 1].IsEqual(Array[0])) break;
					base.RemoveLast();
				}
			}
		}

		internal VertexDistance prev(int idx)
		{
			return this[(idx + currentSize - 1) % currentSize];
		}

		internal VertexDistance curr(int idx)
		{
			return this[idx];
		}

		internal VertexDistance next(int idx)
		{
			return this[(idx + 1) % currentSize];
		}
	}

	//-------------------------------------------------------------vertex_dist
	// Vertex (x, y) with the distance to the next one. The last vertex has
	// distance between the last and the first points if the polygon is closed
	// and 0.0 if it's a polyline.
	public struct VertexDistance
	{
		public double x;
		public double y;
		public double dist;

		public VertexDistance(double x_, double y_)
		{
			x = x_;
			y = y_;
			dist = 0.0;
		}

		public bool IsEqual(VertexDistance val)
		{
			bool ret = (dist = agg_math.calc_distance(x, y, val.x, val.y)) > agg_math.vertex_dist_epsilon;
			if (!ret) dist = 1.0 / agg_math.vertex_dist_epsilon;
			return ret;
		}
	}

	/*
	//--------------------------------------------------------vertex_dist_cmd
	// Save as the above but with additional "command" value
	struct vertex_dist_cmd : vertex_dist
	{
		unsigned cmd;

		vertex_dist_cmd() {}
		vertex_dist_cmd(double x_, double y_, unsigned cmd_) :
			base (x_, y_)

		{
			cmd = cmd;
		}
	};
	 */
}

//#endif