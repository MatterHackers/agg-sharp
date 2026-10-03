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

        /// <summary>
        /// Under an <see cref="Affine"/> the output is fixed by the source and the six coefficients, so those
        /// are hashed rather than walking (and allocating) the transformed vertices. Any other transform is
        /// hashed by its output, as before.
        /// </summary>
        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            if (TransformToApply is Affine affine)
            {
                return GetLongHashCode(VertexSource, affine, hash);
            }

            foreach (var vertex in this.Vertices())
            {
                hash = vertex.GetLongHashCode(hash);
            }

            return hash;
        }

        /// <summary>
        /// The hash a <see cref="VertexSourceApplyTransform"/> of <paramref name="vertexSource"/> under
        /// <paramref name="affine"/> has, without making one - so a cache lookup needs neither the wrapper
        /// nor the boxed transform.
        /// </summary>
        public static ulong GetLongHashCode(IVertexSource vertexSource, Affine affine, ulong hash = 14695981039346656037)
        {
            // Tags the parameter hash so it cannot line up with a vertex walk of the same numbers.
            hash = 0x4170706c79417866UL.GetLongHashCode(hash);
            hash = vertexSource.GetLongHashCode(hash);
            hash = affine.sx.GetLongHashCode(hash);
            hash = affine.shy.GetLongHashCode(hash);
            hash = affine.shx.GetLongHashCode(hash);
            hash = affine.sy.GetLongHashCode(hash);
            hash = affine.tx.GetLongHashCode(hash);
            return affine.ty.GetLongHashCode(hash);
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