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
	// in the original agg this was conv_transform
	public class VertexSourceApplyTransform : IVertexSourceProxy
	{
		private Transform.ITransform transformToApply;

		public IVertexSource VertexSource
		{
			get;
			set;
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
					transformToApply.transform(ref transformedVertex.position.x, ref transformedVertex.position.y);
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