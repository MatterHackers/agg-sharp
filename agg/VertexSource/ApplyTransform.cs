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

namespace MatterHackers.Agg.VertexSource
{
	public enum AngleType { Degrees, Radians }

	// in the original agg this was conv_transform
	public class VertexSourceApplyTransform : IVertexSourceProxy
	{
		private Transform.ITransform transformToApply;

		public ITransform Transform
		{
			get => transformToApply;
			set => transformToApply = value;
		}

		public IVertexSource VertexSource { get; set; }

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

				if (ShapePath.IsVertex(transformedVertex.command))
				{
					var position = transformedVertex.position;
					transformToApply.Transform(ref position.X, ref position.Y);
					transformedVertex.position = position;
				}

				yield return transformedVertex;
			}
		}

		public void Rewind(int path_id)
		{
			VertexSource.Rewind(path_id);
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			ShapePath.FlagsAndCommand cmd = VertexSource.vertex(out x, out y);

			if (ShapePath.IsVertex(cmd))
			{
				transformToApply.Transform(ref x, ref y);
			}

			return cmd;
		}

		public void SetTransformToApply(Transform.ITransform newTransformeToApply)
		{
			transformToApply = newTransformeToApply;
		}
	}
}