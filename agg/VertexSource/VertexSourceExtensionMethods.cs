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
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.VertexSource
{
	public static class VertexSourceExtensionMethods
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
}