using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;


namespace AggVisualizers
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public static class PlatingHelper
	{
		public static VertexStorage PolygonToVertexStorage(Polygon polygon)
		{
			VertexStorage output = new VertexStorage();

			bool first = true;
			foreach (IntPoint point in polygon)
			{
				if (first)
				{
					output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
					first = false;
				}
				else
				{
					output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
				}
			}

			output.ClosePolygon();

			output.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);

			return output;
		}

		public static VertexStorage PolygonToVertexStorage(Polygons polygons)
		{
			VertexStorage output = new VertexStorage();

			foreach (Polygon polygon in polygons)
			{
				bool first = true;
				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
					}
				}

				output.ClosePolygon();
			}
			output.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);

			return output;
		}
	}
}
