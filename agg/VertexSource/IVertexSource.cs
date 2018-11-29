using MatterHackers.Agg.Image;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;

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

		public VertexData(ShapePath.FlagsAndCommand command, double x, double y)
			: this(command, new Vector2(x, y))
		{
		}

		public ShapePath.FlagsAndCommand command { get; set; }
		public Vector2 position { get; set; }

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
		public static Vector2 GetWeightedCenter(this IVertexSource vertexSource)
		{
			var polygonBounds = vertexSource.GetBounds();

			int width = 128;
			int height = 128;

			// Set the transform to image space
			var polygonsToImageTransform = Affine.NewIdentity();
			// move it to 0, 0
			polygonsToImageTransform *= Affine.NewTranslation(-polygonBounds.Left, -polygonBounds.Bottom);
			// scale to fit cache
			polygonsToImageTransform *= Affine.NewScaling(width / (double)polygonBounds.Width, height / (double)polygonBounds.Height);
			// and move it in 2 pixels
			polygonsToImageTransform *= Affine.NewTranslation(2, 2);

			// and render the polygon to the image
			var imageBuffer = new ImageBuffer(width + 4, height + 4, 8, new blender_gray(1));
			imageBuffer.NewGraphics2D().Render(new VertexSourceApplyTransform(vertexSource, polygonsToImageTransform), Color.White);

			// center for image
			var centerPosition = imageBuffer.GetWeightedCenter(new MapOnMaxIntensity());
			// translate to vertex source coordinates
			polygonsToImageTransform.inverse_transform(ref centerPosition.X, ref centerPosition.Y);

			return centerPosition;
		}

		public static RectangleDouble GetBounds(this IVertexSource source)
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