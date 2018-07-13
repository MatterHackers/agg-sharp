using MatterHackers.Agg.VertexSource;
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

					rewind(0);
					double x;
					double y;
					ShapePath.FlagsAndCommand cmd;
					int numPoint = 0;
					while (!ShapePath.is_stop(cmd = vertex(out x, out y)))
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

		public abstract int num_paths();

		public abstract IEnumerable<VertexData> Vertices();

		public abstract void rewind(int path_id);

		public abstract ShapePath.FlagsAndCommand vertex(out double x, out double y);

		public virtual IColorType color(int i)
		{
			return (IColorType)new ColorF();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			for (int i = 0; i < num_paths(); i++)
			{
				graphics2D.Render(this, i, color(i).ToColor());
			}
			base.OnDraw(graphics2D);
		}
	}
}