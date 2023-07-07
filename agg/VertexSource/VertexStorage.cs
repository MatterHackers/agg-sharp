using MatterHackers.Agg.SvgTools;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MatterHackers.Agg.VertexSource
{
	//---------------------------------------------------------------path_base
	// A container to store vertices with their flags.
	// A path consists of a number of contours separated with "move_to"
	// commands. The path storage can keep and maintain more than one
	// path.
	// To navigate to the beginning of a particular path, use rewind(path_id);
	// Where path_id is what start_new_path() returns. So, when you call
	// start_new_path() you need to store its return value somewhere else
	// to navigate to the path afterwards.
	//
	// See also: vertex_source concept
	//------------------------------------------------------------------------
	public class VertexStorage : IVertexSource, IVertexDest
	{
		#region InternalVertexStorage

		private class VertexDataManager
		{
			private int allocatedVertices;
			private VertexData[] vertexData;
			private int numVertices;

			public VertexDataManager()
			{
			}

			public VertexData this[int index]
			{
				get => vertexData[index];
				set => vertexData[index] = value;
			}


			public void AddVertex(double x, double y, ShapePath.FlagsAndCommand CommandAndFlags)
			{
				int index = numVertices;
				allocate_if_required(numVertices);
				vertexData[index] = new VertexData(CommandAndFlags, x, y);

				numVertices++;
			}

			public ShapePath.FlagsAndCommand command(int index)
			{
				return vertexData[index].command;
			}

			public void free_all()
			{
				vertexData = null;
				numVertices = 0;
			}

			public ShapePath.FlagsAndCommand last_command()
			{
				if (numVertices != 0)
				{
					return command(numVertices - 1);
				}

				return ShapePath.FlagsAndCommand.Stop;
			}

			public ShapePath.FlagsAndCommand last_vertex(out double x, out double y)
			{
				if (numVertices != 0)
				{
					return vertex(numVertices - 1, out x, out y);
				}

				x = 0;
				y = 0;

				return ShapePath.FlagsAndCommand.Stop;
			}

			public double last_x()
			{
				if (numVertices > 0)
				{
					int index = numVertices - 1;
					return vertexData[index].position.X;
				}

				return 0;
			}

			public double last_y()
			{
				if (numVertices > 0)
				{
					int index = numVertices - 1;
					return vertexData[index].position.Y;
				}

				return 0;
			}

			public void modify_command(int index, ShapePath.FlagsAndCommand CommandAndFlags)
			{
				this.vertexData[index].command = CommandAndFlags;
			}

			public void modify_vertex(int index, double x, double y)
			{
				vertexData[index].position = new Vector2(x, y);
			}

			public void modify_vertex(int index, double x, double y, ShapePath.FlagsAndCommand CommandAndFlags)
			{
				vertexData[index].position = new Vector2(x, y);
				vertexData[index].command = CommandAndFlags;
			}

			public ShapePath.FlagsAndCommand prev_vertex(out double x, out double y)
			{
				if (numVertices > 1)
				{
					return vertex(numVertices - 2, out x, out y);
				}

				x = 0;
				y = 0;

				return ShapePath.FlagsAndCommand.Stop;
			}

			public void Clear()
			{
				numVertices = 0;
			}

			public void swap_vertices(int v1, int v2)
			{
				var hold = vertexData[v2];
				vertexData[v2] = vertexData[v1];
				vertexData[v1] = hold;
			}

			public int total_vertices()
			{
				return numVertices;
			}

			public ShapePath.FlagsAndCommand vertex(int index, out double x, out double y)
			{
				x = vertexData[index].position.X;
				y = vertexData[index].position.Y;
				return vertexData[index].command;
			}

			private void allocate_if_required(int indexToAdd)
			{
				if (indexToAdd < numVertices)
				{
					return;
				}

				while (indexToAdd >= allocatedVertices)
				{
					int newSize = allocatedVertices + 256;
					VertexData[] newVertexData = new VertexData[newSize];

					if (vertexData != null)
					{
						for (int i = 0; i < numVertices; i++)
						{
							newVertexData[i] = vertexData[i];
						}
					}

					vertexData = newVertexData;

					allocatedVertices = newSize;
				}
			}

            /// <summary>
            /// Determines if the given point lies within, on, or outside the polygon defined by vertex data.
            /// Based on "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos.
            /// </summary>
            /// <param name="pointToCheck">The point to check.</param>
            /// <returns>
            /// 0 if point is outside the polygon, 
            /// +1 if point is inside the polygon, 
            /// -1 if point is on the polygon boundary.
            /// </returns>
            public int CheckPointInPolygon(Vector2 pointToCheck)
            {
                int pointPosition = 0;
                var vertexCount = vertexData.Length;

                // If the polygon has less than 3 vertices, it's not valid, so the point is outside.
                if (vertexCount < 3)
                {
                    return 0;
                }

                var currentVertex = vertexData[0];
                for (int i = 1; i <= vertexCount; ++i)
                {
                    var nextVertex = (i == vertexCount ? vertexData[0] : vertexData[i]);

                    if (nextVertex.Y == pointToCheck.Y)
                    {
                        if ((nextVertex.X == pointToCheck.X) || (currentVertex.Y == pointToCheck.Y && ((nextVertex.X > pointToCheck.X) == (currentVertex.X < pointToCheck.X))))
                        {
                            return -1;
                        }
                    }

                    if ((currentVertex.Y < pointToCheck.Y) != (nextVertex.Y < pointToCheck.Y))
                    {
                        if (currentVertex.X >= pointToCheck.X)
                        {
                            if (nextVertex.X > pointToCheck.X)
                            {
                                pointPosition = 1 - pointPosition;
                            }
                            else
                            {
                                double d = (double)(currentVertex.X - pointToCheck.X) * (nextVertex.Y - pointToCheck.Y) - (double)(nextVertex.X - pointToCheck.X) * (currentVertex.Y - pointToCheck.Y);

                                if (d == 0)
                                {
                                    return -1;
                                }
                                else if ((d > 0) == (nextVertex.Y > currentVertex.Y))
                                {
                                    pointPosition = 1 - pointPosition;
                                }
                            }
                        }
                        else
                        {
                            if (nextVertex.X > pointToCheck.X)
                            {
                                double d = (double)(currentVertex.X - pointToCheck.X) * (nextVertex.Y - pointToCheck.Y) - (double)(nextVertex.X - pointToCheck.X) * (currentVertex.Y - pointToCheck.Y);

                                if (d == 0)
                                {
                                    return -1;
                                }
                                else if ((d > 0) == (nextVertex.Y > currentVertex.Y))
                                {
                                    pointPosition = 1 - pointPosition;
                                }
                            }
                        }
                    }

                    currentVertex = nextVertex;
                }

                return pointPosition;
            }
        }

        #endregion InternalVertexStorage

        private int iteratorIndex;
		private VertexDataManager vertexDataManager;

		public VertexStorage()
		{
			vertexDataManager = new VertexDataManager();
		}

		public VertexStorage(string svgDString)
			: this()
		{
			SvgDString = svgDString;
		}

		public VertexStorage(IVertexSource copyFrom)
			: this()
		{
			foreach (var vertex in copyFrom.Vertices())
			{
				this.Add(vertex.X, vertex.Y, vertex.command);
			}
		}


		[JsonIgnore]
		public int Count
		{
			get
			{
				return vertexDataManager.total_vertices();
			}
		}

        /// <summary>
        /// Determines if the given point lies within, on, or outside the polygon defined by vertex data.
        /// Based on "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos.
        /// </summary>
        /// <param name="pointToCheck">The point to check.</param>
        /// <returns>
        /// 0 if point is outside the polygon, 
        /// +1 if point is inside the polygon, 
        /// -1 if point is on the polygon boundary.
        /// </returns>
        public int CheckPointInPolygon(Vector2 pointToCheck)
        {
            return vertexDataManager.CheckPointInPolygon(pointToCheck);
        }

		public static bool OldEqualsNewStyle(IVertexSource control, IVertexSource test, double maxError = .0001)
		{
			control.rewind(0);
			double controlX;
			double controlY;
			ShapePath.FlagsAndCommand controlFlagsAndCommand = control.vertex(out controlX, out controlY);

			int index = 0;
			foreach (VertexData vertexData in test.Vertices())
			{
				if (controlFlagsAndCommand != vertexData.command
					|| controlX < vertexData.position.X - maxError || controlX > vertexData.position.X + maxError
					|| controlY < vertexData.position.Y - maxError || controlY > vertexData.position.Y + maxError)
				{
					return false;
				}
				controlFlagsAndCommand = control.vertex(out controlX, out controlY);
				index++;
			}

			if (controlFlagsAndCommand == ShapePath.FlagsAndCommand.Stop)
			{
				return true;
			}

			return false;
		}

		public static bool OldEqualsOldStyle(IVertexSource control, IVertexSource test, double maxError = .0001)
		{
			control.rewind(0);
			double controlX;
			double controlY;
			ShapePath.FlagsAndCommand controlFlagsAndCommand = control.vertex(out controlX, out controlY);

			test.rewind(0);
			double testX;
			double testY;
			ShapePath.FlagsAndCommand otherFlagsAndCommand = test.vertex(out testX, out testY);

			int index = 1;
			if (controlFlagsAndCommand == otherFlagsAndCommand && controlX == testX && Util.is_equal_eps(controlY, testY, .000000001))
			{
				while (controlFlagsAndCommand != ShapePath.FlagsAndCommand.Stop)
				{
					controlFlagsAndCommand = control.vertex(out controlX, out controlY);
					otherFlagsAndCommand = test.vertex(out testX, out testY);
					if (controlFlagsAndCommand != otherFlagsAndCommand
						|| controlX < testX - maxError || controlX > testX + maxError
						|| controlY < testY - maxError || controlY > testY + maxError)
					{
						return false;
					}

					index++;
				}

				return true;
			}

			return false;
		}

		public void add(Vector2 vertex)
		{
			throw new System.NotImplementedException();
		}

		public void Add(double x, double y, ShapePath.FlagsAndCommand flagsAndCommand)
		{
			vertexDataManager.AddVertex(x, y, flagsAndCommand);
		}

		public int arrange_orientations(int start, ShapePath.FlagsAndCommand orientation)
		{
			if (orientation != ShapePath.FlagsAndCommand.FlagNone)
			{
				while (start < vertexDataManager.total_vertices())
				{
					start = arrange_polygon_orientation(start, orientation);
					if (ShapePath.is_stop(vertexDataManager.command(start)))
					{
						++start;
						break;
					}
				}
			}

			return start;
		}

		public void arrange_orientations_all_paths(ShapePath.FlagsAndCommand orientation)
		{
			if (orientation != ShapePath.FlagsAndCommand.FlagNone)
			{
				int start = 0;
				while (start < vertexDataManager.total_vertices())
				{
					start = arrange_orientations(start, orientation);
				}
			}
		}

		// Arrange the orientation of a polygon, all polygons in a path,
		// or in all paths. After calling arrange_orientations() or
		// arrange_orientations_all_paths(), all the polygons will have
		// the same orientation, i.e. path_flags_cw or path_flags_ccw
		//--------------------------------------------------------------------
		public int arrange_polygon_orientation(int start, ShapePath.FlagsAndCommand orientation)
		{
			if (orientation == ShapePath.FlagsAndCommand.FlagNone) return start;

			// Skip all non-vertices at the beginning
			while (start < vertexDataManager.total_vertices() &&
				  !ShapePath.is_vertex(vertexDataManager.command(start))) ++start;

			// Skip all insignificant move_to
			while (start + 1 < vertexDataManager.total_vertices() &&
				  ShapePath.is_move_to(vertexDataManager.command(start)) &&
				  ShapePath.is_move_to(vertexDataManager.command(start + 1))) ++start;

			// Find the last vertex
			int end = start + 1;
			while (end < vertexDataManager.total_vertices() &&
				  !ShapePath.is_next_poly(vertexDataManager.command(end))) ++end;

			if (end - start > 2)
			{
				if (perceive_polygon_orientation(start, end) != orientation)
				{
					// Invert polygon, set orientation flag, and skip all end_poly
					invert_polygon(start, end);
					ShapePath.FlagsAndCommand PathAndFlags;
					while (end < vertexDataManager.total_vertices() &&
						  ShapePath.is_end_poly(PathAndFlags = vertexDataManager.command(end)))
					{
						vertexDataManager.modify_command(end++, PathAndFlags | orientation);// Path.set_orientation(cmd, orientation));
					}
				}
			}

			return end;
		}

		public void close_polygon(ShapePath.FlagsAndCommand flags)
		{
			end_poly(ShapePath.FlagsAndCommand.FlagClose | flags);
		}

		public void ClosePolygon()
		{
			close_polygon(ShapePath.FlagsAndCommand.FlagNone);
		}

		public ShapePath.FlagsAndCommand command(int index)
		{
			return vertexDataManager.command(index);
		}

		// Concatenate path. The path is added as is.
		public void concat_path(IVertexSource vs)
		{
			concat_path(vs, 0);
		}

		public void concat_path(IVertexSource vs, int path_id)
		{
			double x, y;
			ShapePath.FlagsAndCommand PathAndFlags;
			vs.rewind(path_id);
			while (!ShapePath.is_stop(PathAndFlags = vs.vertex(out x, out y)))
			{
				vertexDataManager.AddVertex(x, y, PathAndFlags);
			}
		}

		/// <summary>
		/// Draws a quadratic B�zier curve from the current point to the target point using the supplied control point.
		/// </summary>
		/// <param name="controlPoint">The control point</param>
		/// <param name="point">The new target point</param>
		public void curve3(Vector2 controlPoint, Vector2 point)
		{
			curve3(controlPoint.X, controlPoint.Y, point.X, point.Y);
		}

		/// <summary>
		/// Draws a quadratic B�zier curve from the current point to (x,y) using (xControl,yControl) as the control point.
		/// </summary>
		/// <param name="xControl"></param>
		/// <param name="yControl"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void curve3(double xControl, double yControl, double x, double y)
		{
			vertexDataManager.AddVertex(xControl, yControl, ShapePath.FlagsAndCommand.Curve3);
			vertexDataManager.AddVertex(x, y, ShapePath.FlagsAndCommand.Curve3);
		}

		/// <summary>
		/// <para>Draws a quadratic B�zier curve from the current point to (x,y).</para>
		/// <para>The control point is assumed to be the reflection of the control point on the previous command relative to the current point.</para>
		/// <para>(If there is no previous command or if the previous command was not a curve, assume the control point is coincident with the current point.)</para>
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void curve3(double x, double y)
		{
			double x0;
			double y0;
			if (ShapePath.is_vertex(vertexDataManager.last_vertex(out x0, out y0)))
			{
				double x_ctrl;
				double y_ctrl;
				ShapePath.FlagsAndCommand cmd = vertexDataManager.prev_vertex(out x_ctrl, out y_ctrl);
				if (ShapePath.is_curve(cmd))
				{
					x_ctrl = x0 + x0 - x_ctrl;
					y_ctrl = y0 + y0 - y_ctrl;
				}
				else
				{
					x_ctrl = x0;
					y_ctrl = y0;
				}
				curve3(x_ctrl, y_ctrl, x, y);
			}
		}

		/// <summary>
		/// Draws a quadratic B�zier curve from the current point to (x,y) using (xControl,yControl) as the control point.
		/// </summary>
		/// <param name="xControl"></param>
		/// <param name="yControl"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void curve3_rel(double dx_ctrl, double dy_ctrl, double dx_to, double dy_to)
		{
			rel_to_abs(ref dx_ctrl, ref dy_ctrl);
			rel_to_abs(ref dx_to, ref dy_to);
			vertexDataManager.AddVertex(dx_ctrl, dy_ctrl, ShapePath.FlagsAndCommand.Curve3);
			vertexDataManager.AddVertex(dx_to, dy_to, ShapePath.FlagsAndCommand.Curve3);
		}

		/// <summary>
		/// <para>Draws a quadratic B�zier curve from the current point to (x,y).</para>
		/// <para>The control point is assumed to be the reflection of the control point on the previous command relative to the current point.</para>
		/// <para>(If there is no previous command or if the previous command was not a curve, assume the control point is coincident with the current point.)</para>
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void curve3_rel(double dx_to, double dy_to)
		{
			rel_to_abs(ref dx_to, ref dy_to);
			curve3(dx_to, dy_to);
		}

		public void curve4(double x_ctrl1, double y_ctrl1,
								   double x_ctrl2, double y_ctrl2,
								   double x_to, double y_to)
		{
			vertexDataManager.AddVertex(x_ctrl1, y_ctrl1, ShapePath.FlagsAndCommand.Curve4);
			vertexDataManager.AddVertex(x_ctrl2, y_ctrl2, ShapePath.FlagsAndCommand.Curve4);
			vertexDataManager.AddVertex(x_to, y_to, ShapePath.FlagsAndCommand.Curve4);
		}

		public void curve4(double x_ctrl2, double y_ctrl2,
								   double x_to, double y_to)
		{
			double x0;
			double y0;
			if (ShapePath.is_vertex(last_vertex(out x0, out y0)))
			{
				double x_ctrl1;
				double y_ctrl1;
				ShapePath.FlagsAndCommand cmd = prev_vertex(out x_ctrl1, out y_ctrl1);
				if (ShapePath.is_curve(cmd))
				{
					x_ctrl1 = x0 + x0 - x_ctrl1;
					y_ctrl1 = y0 + y0 - y_ctrl1;
				}
				else
				{
					x_ctrl1 = x0;
					y_ctrl1 = y0;
				}
				curve4(x_ctrl1, y_ctrl1, x_ctrl2, y_ctrl2, x_to, y_to);
			}
		}

		public void curve4_rel(double dx_ctrl1, double dy_ctrl1,
									   double dx_ctrl2, double dy_ctrl2,
									   double dx_to, double dy_to)
		{
			rel_to_abs(ref dx_ctrl1, ref dy_ctrl1);
			rel_to_abs(ref dx_ctrl2, ref dy_ctrl2);
			rel_to_abs(ref dx_to, ref dy_to);
			vertexDataManager.AddVertex(dx_ctrl1, dy_ctrl1, ShapePath.FlagsAndCommand.Curve4);
			vertexDataManager.AddVertex(dx_ctrl2, dy_ctrl2, ShapePath.FlagsAndCommand.Curve4);
			vertexDataManager.AddVertex(dx_to, dy_to, ShapePath.FlagsAndCommand.Curve4);
		}

		public void curve4_rel(double dx_ctrl2, double dy_ctrl2,
									   double dx_to, double dy_to)
		{
			rel_to_abs(ref dx_ctrl2, ref dy_ctrl2);
			rel_to_abs(ref dx_to, ref dy_to);
			curve4(dx_ctrl2, dy_ctrl2, dx_to, dy_to);
		}

		public void end_poly()
		{
			close_polygon(ShapePath.FlagsAndCommand.FlagClose);
		}

		public void end_poly(ShapePath.FlagsAndCommand flags)
		{
			if (ShapePath.is_vertex(vertexDataManager.last_command()))
			{
				vertexDataManager.AddVertex(0.0, 0.0, ShapePath.FlagsAndCommand.EndPoly | flags);
			}
		}

		public bool Equals(IVertexSource other, double maxError = .0001, bool oldStyle = true)
		{
			if (oldStyle)
			{
				return OldEqualsOldStyle(this, other, maxError);
			}
			else
			{
				return OldEqualsNewStyle(this, other, maxError);
			}
		}

		// Flip all vertices horizontally or vertically,
		// between x1 and x2, or between y1 and y2 respectively
		//--------------------------------------------------------------------
		public void flip_x(double x1, double x2)
		{
			for (int i = 0; i < vertexDataManager.total_vertices(); i++)
			{
				ShapePath.FlagsAndCommand PathAndFlags = vertexDataManager.vertex(i, out double x, out double y);
				if (ShapePath.is_vertex(PathAndFlags))
				{
					vertexDataManager.modify_vertex(i, x2 - x + x1, y);
				}
			}
		}

		public void flip_y(double y1, double y2)
		{
			for (int i = 0; i < vertexDataManager.total_vertices(); i++)
			{
				ShapePath.FlagsAndCommand PathAndFlags = vertexDataManager.vertex(i, out double x, out double y);
				if (ShapePath.is_vertex(PathAndFlags))
				{
					vertexDataManager.modify_vertex(i, x, y2 - y + y1);
				}
			}
		}

		public void free_all()
		{
			vertexDataManager.free_all(); iteratorIndex = 0;
		}

		public double GetLastX()
		{
			return vertexDataManager.last_x();
		}

		public double GetLastY()
		{
			return vertexDataManager.last_y();
		}

		public void HorizontalLineTo(double x)
		{
			vertexDataManager.AddVertex(x, GetLastY(), ShapePath.FlagsAndCommand.LineTo);
		}

		public void invert_polygon(int start)
		{
			// Skip all non-vertices at the beginning
			while (start < vertexDataManager.total_vertices() &&
				  !ShapePath.is_vertex(vertexDataManager.command(start))) ++start;

			// Skip all insignificant move_to
			while (start + 1 < vertexDataManager.total_vertices() &&
				  ShapePath.is_move_to(vertexDataManager.command(start)) &&
				  ShapePath.is_move_to(vertexDataManager.command(start + 1))) ++start;

			// Find the last vertex
			int end = start + 1;
			while (end < vertexDataManager.total_vertices() &&
				  !ShapePath.is_next_poly(vertexDataManager.command(end))) ++end;

			invert_polygon(start, end);
		}

		//--------------------------------------------------------------------
		// Join path. The path is joined with the existing one, that is,
		// it behaves as if the pen of a plotter was always down (drawing)
		//template<class VertexSource>
		public void join_path(VertexStorage vs)
		{
			join_path(vs, 0);
		}

		public void join_path(VertexStorage vs, int path_id)
		{
			double x, y;
			vs.rewind(path_id);
			ShapePath.FlagsAndCommand PathAndFlags = vs.vertex(out x, out y);
			if (!ShapePath.is_stop(PathAndFlags))
			{
				if (ShapePath.is_vertex(PathAndFlags))
				{
					double x0, y0;
					ShapePath.FlagsAndCommand PathAndFlags0 = last_vertex(out x0, out y0);
					if (ShapePath.is_vertex(PathAndFlags0))
					{
						if (agg_math.calc_distance(x, y, x0, y0) > agg_math.vertex_dist_epsilon)
						{
							if (ShapePath.is_move_to(PathAndFlags)) PathAndFlags = ShapePath.FlagsAndCommand.LineTo;
							vertexDataManager.AddVertex(x, y, PathAndFlags);
						}
					}
					else
					{
						if (ShapePath.is_stop(PathAndFlags0))
						{
							PathAndFlags = ShapePath.FlagsAndCommand.MoveTo;
						}
						else
						{
							if (ShapePath.is_move_to(PathAndFlags)) PathAndFlags = ShapePath.FlagsAndCommand.LineTo;
						}
						vertexDataManager.AddVertex(x, y, PathAndFlags);
					}
				}
				while (!ShapePath.is_stop(PathAndFlags = vs.vertex(out x, out y)))
				{
					vertexDataManager.AddVertex(x, y, ShapePath.is_move_to(PathAndFlags) ?
													ShapePath.FlagsAndCommand.LineTo :
													PathAndFlags);
				}
			}
		}

		public ShapePath.FlagsAndCommand last_vertex(out double x, out double y)
		{
			return vertexDataManager.last_vertex(out x, out y);
		}

		public void LineTo(Point2D position)
		{
			LineTo(position.x, position.y);
		}

		public void LineTo(Vector2 position)
		{
			LineTo(position.X, position.Y);
		}

		public void LineTo(double x, double y)
		{
			vertexDataManager.AddVertex(x, y, ShapePath.FlagsAndCommand.LineTo);
		}

		public void modify_command(int index, ShapePath.FlagsAndCommand PathAndFlags)
		{
			vertexDataManager.modify_command(index, PathAndFlags);
		}

		public void modify_vertex(int index, double x, double y)
		{
			vertexDataManager.modify_vertex(index, x, y);
		}

		public void modify_vertex(int index, double x, double y, ShapePath.FlagsAndCommand PathAndFlags)
		{
			vertexDataManager.modify_vertex(index, x, y, PathAndFlags);
		}

		public void MoveTo(Vector2 position)
		{
			MoveTo(position.X, position.Y);
		}

		public void MoveTo(double x, double y)
		{
			vertexDataManager.AddVertex(x, y, ShapePath.FlagsAndCommand.MoveTo);
		}

		public string SvgDString
		{
			get
			{
				return this.GetSvgDString();
			}

			set
			{
                this.ParseSvgDString(value); 
			}
		}

		public ShapePath.FlagsAndCommand prev_vertex(out double x, out double y)
		{
			return vertexDataManager.prev_vertex(out x, out y);
		}

		public void rel_to_abs(ref double x, ref double y)
		{
			if (vertexDataManager.total_vertices() != 0)
			{
				double x2;
				double y2;
				if (ShapePath.is_vertex(vertexDataManager.last_vertex(out x2, out y2)))
				{
					x += x2;
					y += y2;
				}
			}
		}

		public void Clear()
		{
			vertexDataManager.Clear();
			iteratorIndex = 0;
		}

		public virtual void rewind(int pathId)
		{
			iteratorIndex = pathId;
		}

		public void ShareVertexData(VertexStorage pathStorageToShareFrom)
		{
			vertexDataManager = pathStorageToShareFrom.vertexDataManager;
		}

		// Make path functions
		//--------------------------------------------------------------------
		public int start_new_path()
		{
			if (!ShapePath.is_stop(vertexDataManager.last_command()))
			{
				vertexDataManager.AddVertex(0.0, 0.0, ShapePath.FlagsAndCommand.Stop);
			}
			return vertexDataManager.total_vertices();
		}
		public int total_vertices()
		{
			return vertexDataManager.total_vertices();
		}

		//--------------------------------------------------------------------
		public void transform(Transform.Affine trans)
		{
			transform(trans, 0);
		}

		public void transform(Transform.Affine trans, int path_id)
		{
			int num_ver = vertexDataManager.total_vertices();
			for (; path_id < num_ver; path_id++)
			{
				double x, y;
				ShapePath.FlagsAndCommand PathAndFlags = vertexDataManager.vertex(path_id, out x, out y);
				if (ShapePath.is_stop(PathAndFlags)) break;
				if (ShapePath.is_vertex(PathAndFlags))
				{
					trans.transform(ref x, ref y);
					vertexDataManager.modify_vertex(path_id, x, y);
				}
			}
		}

		//--------------------------------------------------------------------
		public void transform_all_paths(Transform.Affine trans)
		{
			int index;
			int num_ver = vertexDataManager.total_vertices();
			for (index = 0; index < num_ver; index++)
			{
				double x, y;
				if (ShapePath.is_vertex(vertexDataManager.vertex(index, out x, out y)))
				{
					trans.transform(ref x, ref y);
					vertexDataManager.modify_vertex(index, x, y);
				}
			}
		}

		//--------------------------------------------------------------------
		public void translate(double dx, double dy)
		{
			translate(dx, dy, 0);
		}

		public void translate(double dx, double dy, int path_id)
		{
			int num_ver = vertexDataManager.total_vertices();
			for (; path_id < num_ver; path_id++)
			{
				double x, y;
				ShapePath.FlagsAndCommand PathAndFlags = vertexDataManager.vertex(path_id, out x, out y);
				if (ShapePath.is_stop(PathAndFlags)) break;
				if (ShapePath.is_vertex(PathAndFlags))
				{
					x += dx;
					y += dy;
					vertexDataManager.modify_vertex(path_id, x, y);
				}
			}
		}

		public void translate_all_paths(double dx, double dy)
		{
			int index;
			int num_ver = vertexDataManager.total_vertices();
			for (index = 0; index < num_ver; index++)
			{
				double x, y;
				if (ShapePath.is_vertex(vertexDataManager.vertex(index, out x, out y)))
				{
					x += dx;
					y += dy;
					vertexDataManager.modify_vertex(index, x, y);
				}
			}
		}

		public VertexData this[int index]
		{
			get => vertexDataManager[index];
			set => vertexDataManager[index] = value;
		}

		public ShapePath.FlagsAndCommand vertex(int index, out double x, out double y)
		{
			return vertexDataManager.vertex(index, out x, out y);
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			if (iteratorIndex >= vertexDataManager.total_vertices())
			{
				x = 0;
				y = 0;
				return ShapePath.FlagsAndCommand.Stop;
			}

			return vertexDataManager.vertex(iteratorIndex++, out x, out y);
		}

		public void VerticalLineTo(double y)
		{
			vertexDataManager.AddVertex(GetLastX(), y, ShapePath.FlagsAndCommand.LineTo);
		}

		public IEnumerable<VertexData> Vertices()
		{
			int count = vertexDataManager.total_vertices();
			for (int i = 0; i < count; i++)
			{
				double x = 0;
				double y = 0;
				ShapePath.FlagsAndCommand command = vertexDataManager.vertex(i, out x, out y);
				yield return new VertexData(command, new Vector2(x, y));
			}

			yield return new VertexData(ShapePath.FlagsAndCommand.Stop, new Vector2(0, 0));
		}

		private void invert_polygon(int start, int end)
		{
			int i;
			ShapePath.FlagsAndCommand tmp_PathAndFlags = vertexDataManager.command(start);

			--end; // Make "end" inclusive

			// Shift all commands to one position
			for (i = start; i < end; i++)
			{
				vertexDataManager.modify_command(i, vertexDataManager.command(i + 1));
			}

			// Assign starting command to the ending command
			vertexDataManager.modify_command(end, tmp_PathAndFlags);

			// Reverse the polygon
			while (end > start)
			{
				vertexDataManager.swap_vertices(start++, end--);
			}
		}

		private ShapePath.FlagsAndCommand perceive_polygon_orientation(int start, int end)
		{
			// Calculate signed area (double area to be exact)
			//---------------------
			int np = end - start;
			double area = 0.0;
			int i;
			for (i = 0; i < np; i++)
			{
				double x1, y1, x2, y2;
				vertexDataManager.vertex(start + i, out x1, out y1);
				vertexDataManager.vertex(start + (i + 1) % np, out x2, out y2);
				area += x1 * y2 - y1 * x2;
			}
			return (area < 0.0) ? ShapePath.FlagsAndCommand.FlagCW : ShapePath.FlagsAndCommand.FlagCCW;
		}
	}
}