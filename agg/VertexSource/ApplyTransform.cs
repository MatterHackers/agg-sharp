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
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	public enum AngleType { Degrees, Radians }

	public static class ExtensionMethods
	{
		public static IVertexSource Rotate(this IVertexSource source, double angle, AngleType angleType = AngleType.Radians)
		{
			if (angleType == AngleType.Degrees)
			{
				angle = MathHelper.DegreesToRadians(angle);
			}

			return new VertexSourceApplyTransform(source, Affine.NewRotation(angle));
		}

		public static IVertexSource Translate(this IVertexSource source, Vector2 vector2)
		{
			return source.Translate(vector2.X, vector2.Y);
		}

		public static IVertexSource Translate(this IVertexSource source, double x, double y)
		{
			return new VertexSourceApplyTransform(source, Affine.NewTranslation(x, y));
		}

		public static IVertexSource Scale(this IVertexSource source, double scale)
		{
			return new VertexSourceApplyTransform(source, Affine.NewScaling(scale));
		}
	}

	// in the original agg this was conv_transform
	public class VertexSourceApplyTransform : IVertexSourceProxy
	{
		private Transform.ITransform transformToApply;

		public ITransform Transform
		{
			get { return transformToApply; }
			set { transformToApply = value; }
		}

		public IVertexSource VertexSource
		{
			get;
			set;
		}

		public VertexSourceApplyTransform()
		{
		}

		public VertexSourceApplyTransform(Transform.ITransform newTransformeToApply)
			: this(null, newTransformeToApply)
		{
		}

		public VertexSourceApplyTransform(IVertexSource vertexSource, Transform.ITransform newTransformeToApply)
		{
			VertexSource = vertexSource;
			transformToApply = newTransformeToApply;
		}

		public void attach(IVertexSource vertexSource)
		{
			VertexSource = vertexSource;
		}

		public IEnumerable<VertexData> Vertices()
		{
			foreach (VertexData vertexData in VertexSource.Vertices())
			{
				VertexData transformedVertex = vertexData;
				if (ShapePath.is_vertex(transformedVertex.command))
				{
					var position = transformedVertex.position;
					transformToApply.transform(ref position.X, ref position.Y);
					transformedVertex.position = position;
				}
				yield return transformedVertex;
			}
		}

		public void rewind(int path_id)
		{
			VertexSource.rewind(path_id);
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			ShapePath.FlagsAndCommand cmd = VertexSource.vertex(out x, out y);
			if (ShapePath.is_vertex(cmd))
			{
				transformToApply.transform(ref x, ref y);
			}
			return cmd;
		}

		public void SetTransformToApply(Transform.ITransform newTransformeToApply)
		{
			transformToApply = newTransformeToApply;
		}
	}
}