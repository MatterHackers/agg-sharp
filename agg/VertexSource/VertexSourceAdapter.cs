using MatterHackers.VectorMath;

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

namespace MatterHackers.Agg.VertexSource
{
	//------------------------------------------------------------null_markers
	public struct null_markers : IMarkers
	{
		public void remove_all()
		{
		}

		public void add_vertex(double x, double y, ShapePath.FlagsAndCommand unknown)
		{
		}

		public void prepare_src()
		{
		}

		public void rewind(int unknown)
		{
		}

		public ShapePath.FlagsAndCommand vertex(ref double x, ref double y)
		{
			return ShapePath.FlagsAndCommand.CommandStop;
		}
	};

	//------------------------------------------------------conv_adaptor_vcgen
	public class VertexSourceAdapter : IVertexSourceProxy
	{
		private IGenerator generator;
		private IMarkers markers;
		private status m_status;
		private ShapePath.FlagsAndCommand m_last_cmd;
		private double m_start_x;
		private double m_start_y;

		public IVertexSource VertexSource
		{
			get;
			set;
		}

		private enum status
		{
			initial,
			accumulate,
			generate
		};

		public VertexSourceAdapter(IVertexSource vertexSource, IGenerator generator)
		{
			markers = new null_markers();
			this.VertexSource = vertexSource;
			this.generator = generator;
			m_status = status.initial;
		}

		public VertexSourceAdapter(IVertexSource vertexSource, IGenerator generator, IMarkers markers)
			: this(vertexSource, generator)
		{
			this.markers = markers;
		}

		private void Attach(IVertexSource vertexSource)
		{
			this.VertexSource = vertexSource;
		}

		protected IGenerator GetGenerator()
		{
			return generator;
		}

		private IMarkers GetMarkers()
		{
			return markers;
		}

		public IEnumerable<VertexData> Vertices()
		{
			rewind(0);
			ShapePath.FlagsAndCommand command = ShapePath.FlagsAndCommand.CommandStop;
			do
			{
				double x;
				double y;
				command = vertex(out x, out y);
				yield return new VertexData(command, new Vector2(x, y));
			} while (command != ShapePath.FlagsAndCommand.CommandStop);
		}

		public void rewind(int path_id)
		{
			VertexSource.rewind(path_id);
			m_status = status.initial;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			ShapePath.FlagsAndCommand command = ShapePath.FlagsAndCommand.CommandStop;
			bool done = false;
			while (!done)
			{
				switch (m_status)
				{
					case status.initial:
						markers.remove_all();
						m_last_cmd = VertexSource.vertex(out m_start_x, out m_start_y);
						m_status = status.accumulate;
						goto case status.accumulate;

					case status.accumulate:
						if (ShapePath.is_stop(m_last_cmd))
						{
							return ShapePath.FlagsAndCommand.CommandStop;
						}

						generator.RemoveAll();
						generator.AddVertex(m_start_x, m_start_y, ShapePath.FlagsAndCommand.CommandMoveTo);
						markers.add_vertex(m_start_x, m_start_y, ShapePath.FlagsAndCommand.CommandMoveTo);

						for (; ; )
						{
							command = VertexSource.vertex(out x, out y);
							//DebugFile.Print("x=" + x.ToString() + " y=" + y.ToString() + "\n");
							if (ShapePath.is_vertex(command))
							{
								m_last_cmd = command;
								if (ShapePath.is_move_to(command))
								{
									m_start_x = x;
									m_start_y = y;
									break;
								}
								generator.AddVertex(x, y, command);
								markers.add_vertex(x, y, ShapePath.FlagsAndCommand.CommandLineTo);
							}
							else
							{
								if (ShapePath.is_stop(command))
								{
									m_last_cmd = ShapePath.FlagsAndCommand.CommandStop;
									break;
								}
								if (ShapePath.is_end_poly(command))
								{
									generator.AddVertex(x, y, command);
									break;
								}
							}
						}
						generator.Rewind(0);
						m_status = status.generate;
						goto case status.generate;

					case status.generate:
						command = generator.Vertex(ref x, ref y);
						//DebugFile.Print("x=" + x.ToString() + " y=" + y.ToString() + "\n");
						if (ShapePath.is_stop(command))
						{
							m_status = status.accumulate;
							break;
						}
						done = true;
						break;
				}
			}
			return command;
		}
	}
}