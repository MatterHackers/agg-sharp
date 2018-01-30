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

namespace MatterHackers.Agg.VertexSource
{
	public struct VertexData
	{
		public VertexData(ShapePath.FlagsAndCommand command, Vector2 position)
		{
			this.command = command;
			this.position = position;
		}

		public ShapePath.FlagsAndCommand command;
		public Vector2 position;

		[JsonIgnore]
		public bool IsMoveTo => ShapePath.is_move_to(command);

		[JsonIgnore]
		public bool IsLineTo => ShapePath.is_line_to(command);

		[JsonIgnore]
		public bool IsClose => ShapePath.is_close(command);

		[JsonIgnore]
		public bool IsStop => ShapePath.is_stop(command);

		public override string ToString()
		{
			return $"{command}:{position}";
		}
	}

	abstract public class VertexSourceLegacySupport : IVertexSource
	{
		private IEnumerator<VertexData> currentEnumerator;

		abstract public IEnumerable<VertexData> Vertices();

		public void rewind(int layerIndex)
		{
			currentEnumerator = Vertices().GetEnumerator();
			currentEnumerator.MoveNext();
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			if (currentEnumerator == null)
			{
				rewind(0);
			}
			x = currentEnumerator.Current.position.X;
			y = currentEnumerator.Current.position.Y;
			ShapePath.FlagsAndCommand command = currentEnumerator.Current.command;

			currentEnumerator.MoveNext();

			return command;
		}

	}

	public interface IVertexSource
	{
		IEnumerable<VertexData> Vertices();

		void rewind(int pathId = 0); // for a PathStorage this is the vertex index.

		ShapePath.FlagsAndCommand vertex(out double x, out double y);
	}

	public interface IVertexSourceProxy : IVertexSource
	{
		IVertexSource VertexSource { get; set; }
	}

	public static class IVertexSourceExtensions
	{
		public static RectangleDouble GetBounds(this IVertexSource source)
		{
			return source.Bounds();
		}

		public static RectangleDouble Bounds(this IVertexSource source)
		{
			RectangleDouble bounds = RectangleDouble.ZeroIntersection;
			foreach (var vertex in source.Vertices())
			{
				if (!vertex.IsClose && !vertex.IsStop)
				{
					bounds.ExpandToInclude(vertex.position);
				}
			}

			return bounds;
		}
	}
}