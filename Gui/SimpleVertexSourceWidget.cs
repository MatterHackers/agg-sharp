using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
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
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	abstract public class SimpleVertexSourceWidget : GuiWidget, IVertexSource
	{
		private bool localBoundsComeFromPoints = true;

		public SimpleVertexSourceWidget(Vector2 originRelativeParent, bool localBoundsComeFromPoints = true)
		{
			this.localBoundsComeFromPoints = localBoundsComeFromPoints;
			OriginRelativeParent = originRelativeParent;
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				if (localBoundsComeFromPoints)
				{
					RectangleDouble localBounds = new RectangleDouble(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

					Rewind(0);
					double x;
					double y;
					FlagsAndCommand cmd;
					int numPoint = 0;
					while (!ShapePath.IsStop(cmd = Vertex(out x, out y)))
					{
						numPoint++;
						localBounds.ExpandToInclude(x, y);
					}

					if (numPoint == 0)
					{
						localBounds = new RectangleDouble();
					}

					return localBounds;
				}
				else
				{
					return base.LocalBounds;
				}
			}

			set
			{
				if (localBoundsComeFromPoints)
				{
					//throw new NotImplementedException();
					base.LocalBounds = value;
				}
				else
				{
					base.LocalBounds = value;
				}
			}
		}

		/// <summary>
		/// Marks the cached screen clipping stale on the way past, because when the bounds come from the
		/// points there is nothing else that can.
		/// </summary>
		/// <remarks>
		/// Screen clipping is invalidated by writes to LocalBounds, and these controls never write theirs -
		/// they compute it from whatever their vertex source currently holds, and their geometry is moved by
		/// dozens of little setters that report nothing. Asking to be repainted is the one thing they all do
		/// when their points move, so that is where the stamp goes. It costs one interlocked increment.
		/// </remarks>
		public override void Invalidate(RectangleDouble rectToInvalidate)
		{
			if (localBoundsComeFromPoints)
			{
				InvalidateScreenClipping();
			}

			base.Invalidate(rectToInvalidate);
		}

		public abstract int num_paths();

		/// <summary>
		/// The path <see cref="Vertices"/> walks. These controls are multi-path vertex sources
		/// (background, border, curve, handles, ...) and every renderer asks for one path at a time,
		/// so <see cref="OnDraw"/> records which one is being drawn before it hands itself over.
		/// </summary>
		protected int CurrentPathIndex { get; private set; }

		/// <summary>
		/// Adapts the legacy Rewind/Vertex pull pair into the enumeration the renderers actually use.
		/// <para>
		/// This is not optional plumbing: the GPU renderer walks <c>Vertices()</c> twice per shape, once
		/// to hash the geometry for its display list cache and once to feed the tesselator, so a control
		/// that leaves this unimplemented cannot draw at all. Subclasses that build their geometry
		/// procedurally through Rewind/Vertex get a working implementation for free; ones that already
		/// hold a real vertex source should override with something cheaper.
		/// </para>
		/// </summary>
		public virtual IEnumerable<VertexData> Vertices()
		{
			Rewind(CurrentPathIndex);

			FlagsAndCommand command;
			do
			{
				command = Vertex(out double x, out double y);
				yield return new VertexData(command, new Vector2(x, y));
			}
			while (!ShapePath.IsStop(command));
		}

		public abstract void Rewind(int path_id);

		public abstract FlagsAndCommand Vertex(out double x, out double y);

		public virtual IColorType color(int i)
		{
			return (IColorType)new ColorF();
		}

        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            foreach (var vertex in this.Vertices())
            {
                hash = vertex.GetLongHashCode(hash);
            }

            return hash;
        }

        /// <summary>
        /// Draws every path this control is made of, one Render per path, in the colors
        /// <see cref="color"/> hands back.
        /// </summary>
        /// <remarks>
        /// This is the only place a path can be selected, so an override must not draw the paths itself:
        /// <see cref="Graphics2D.Render(IVertexSource, IColorType)"/> pulls <see cref="Vertices"/>, which
        /// rewinds to <see cref="CurrentPathIndex"/>, so a <c>Rewind(i)</c> made before the Render call is
        /// thrown away and the pass draws path 0 again. Overrides that need to paint under or over the
        /// paths (a background, a border) should do that and then call base.
        /// </remarks>
        public override void OnDraw(Graphics2D graphics2D)
		{
			for (int i = 0; i < num_paths(); i++)
			{
				// Render pulls Vertices() rather than taking a path id, so the index has to be handed
				// over out of band or every path would come back as path 0.
				CurrentPathIndex = i;
				graphics2D.Render(this, color(i).ToColor());
			}
			base.OnDraw(graphics2D);
		}
	}
}