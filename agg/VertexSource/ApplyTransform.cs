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
		public ITransform TransformToApply { get; private set; }

		public ITransform Transform
		{
			get => TransformToApply;
			set => TransformToApply = value;
		}

		public IVertexSource VertexSource { get; set; }

		public VertexSourceApplyTransform()
		{
		}

		public VertexSourceApplyTransform(ITransform newTransformeToApply)
			: this(null, newTransformeToApply)
		{
		}

		public VertexSourceApplyTransform(IVertexSource vertexSource, ITransform newTransformeToApply)
		{
			VertexSource = vertexSource;
			TransformToApply = newTransformeToApply;
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

				if (ShapePath.IsVertex(transformedVertex.Command))
				{
					var position = transformedVertex.Position;
					TransformToApply.Transform(ref position.X, ref position.Y);
					transformedVertex.Position = position;
				}

				yield return transformedVertex;
			}
		}

		public void Rewind(int path_id)
		{
			VertexSource.Rewind(path_id);
		}

		public FlagsAndCommand Vertex(out double x, out double y)
		{
			FlagsAndCommand cmd = VertexSource.Vertex(out x, out y);

			if (ShapePath.IsVertex(cmd))
			{
				TransformToApply.Transform(ref x, ref y);
			}

			return cmd;
		}

		public void SetTransformToApply(ITransform newTransformeToApply)
		{
			TransformToApply = newTransformeToApply;
		}
	}
}