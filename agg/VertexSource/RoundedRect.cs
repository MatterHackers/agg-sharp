using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2025 Lars Brubaker
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
//
// Rounded rectangle vertex generator
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
	//------------------------------------------------------------rounded_rect
	//
	// See Implementation agg_rounded_rect.cpp
	//
	public class RoundedRect : VertexSourceLegacySupport
	{
		private RectangleDouble bounds;
		private Vector2 leftBottomRadius;
		private Vector2 rightBottomRadius;
		private Vector2 rightTopRadius;
		private Vector2 leftTopRadius;

		public double ResolutionScale { get; set; } = 1;

		public RoundedRect(double left, double bottom, double right, double top, double radius = 0)
		{
			bounds = new RectangleDouble(left, bottom, right, top);
			leftBottomRadius.X = radius;
			leftBottomRadius.Y = radius;
			rightBottomRadius.X = radius;
			rightBottomRadius.Y = radius;
			rightTopRadius.X = radius;
			rightTopRadius.Y = radius;
			leftTopRadius.X = radius;
			leftTopRadius.Y = radius;

			if (left > right)
			{
				bounds.Left = right;
				bounds.Right = left;
			}

			if (bottom > top)
			{
				bounds.Bottom = top;
				bounds.Top = bottom;
			}
		}

		public RoundedRect(RectangleDouble bounds, double r)
			: this(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, r)
		{
		}

		public RoundedRect(RectangleInt bounds, double r)
			: this(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, r)
		{
		}

		public void rect(double left, double bottom, double right, double top)
		{
			bounds = new RectangleDouble(left, bottom, right, top);
			if (left > right) { bounds.Left = right; bounds.Right = left; }
			if (bottom > top) { bounds.Bottom = top; bounds.Top = bottom; }
		}

		public void radius(double r)
		{
			leftBottomRadius.X = leftBottomRadius.Y = rightBottomRadius.X = rightBottomRadius.Y = rightTopRadius.X = rightTopRadius.Y = leftTopRadius.X = leftTopRadius.Y = r;
		}

		public void radius(double rx, double ry)
		{
			leftBottomRadius.X = rightBottomRadius.X = rightTopRadius.X = leftTopRadius.X = rx;
			leftBottomRadius.Y = rightBottomRadius.Y = rightTopRadius.Y = leftTopRadius.Y = ry;
		}

		public void radius(double leftBottomRadius, double rightBottomRadius, double rightTopRadius, double leftTopRadius)
		{
			this.leftBottomRadius = new Vector2(leftBottomRadius, leftBottomRadius);
			this.rightBottomRadius = new Vector2(rightBottomRadius, rightBottomRadius);
			this.rightTopRadius = new Vector2(rightTopRadius, rightTopRadius);
			this.leftTopRadius = new Vector2(leftTopRadius, leftTopRadius);
		}

		public void radius(double rx1, double ry1, double rx2, double ry2,
							  double rx3, double ry3, double rx4, double ry4)
		{
			leftBottomRadius.X = rx1; leftBottomRadius.Y = ry1; rightBottomRadius.X = rx2; rightBottomRadius.Y = ry2;
			rightTopRadius.X = rx3; rightTopRadius.Y = ry3; leftTopRadius.X = rx4; leftTopRadius.Y = ry4;
		}

		public void normalize_radius()
		{
			double dx = Math.Abs(bounds.Top - bounds.Bottom);
			double dy = Math.Abs(bounds.Right - bounds.Left);

			double k = 1.0;
			double t;
			t = dx / (leftBottomRadius.X + rightBottomRadius.X); if (t < k) k = t;
			t = dx / (rightTopRadius.X + leftTopRadius.X); if (t < k) k = t;
			t = dy / (leftBottomRadius.Y + rightBottomRadius.Y); if (t < k) k = t;
			t = dy / (rightTopRadius.Y + leftTopRadius.Y); if (t < k) k = t;

			if (k < 1.0)
			{
				leftBottomRadius.X *= k; leftBottomRadius.Y *= k; rightBottomRadius.X *= k; rightBottomRadius.Y *= k;
				rightTopRadius.X *= k; rightTopRadius.Y *= k; leftTopRadius.X *= k; leftTopRadius.Y *= k;
			}
		}

        /// <summary>
		/// This is the number of segments that will be used in each turn. Set to 0 to use the default of an angle approximation.
		/// </summary>
        public int NumSegments { get; set; } = 0;
        
		public override IEnumerable<VertexData> Vertices()
		{
			var allPaths = new JoinPaths();

			// Add each corner - either as a single point (if radius = 0) or as an arc
			if (leftBottomRadius.X == 0 && leftBottomRadius.Y == 0)
			{
				// Sharp corner - add single point
				var singlePoint = new VertexStorage();
				singlePoint.Add(bounds.Left, bounds.Bottom, FlagsAndCommand.MoveTo);
				allPaths.SourcePaths.Add(singlePoint);
			}
			else
			{
				// Rounded corner - add arc
				allPaths.SourcePaths.Add(new Arc(bounds.Left + leftBottomRadius.X, bounds.Bottom + leftBottomRadius.Y, 
					leftBottomRadius.X, leftBottomRadius.Y, Math.PI, Math.PI + Math.PI * 0.5, numSegments: NumSegments)
				{
					ResolutionScale = ResolutionScale
				});
			}

			if (rightBottomRadius.X == 0 && rightBottomRadius.Y == 0)
			{
				var singlePoint = new VertexStorage();
				singlePoint.Add(bounds.Right, bounds.Bottom, FlagsAndCommand.LineTo);
				allPaths.SourcePaths.Add(singlePoint);
			}
			else
			{
				allPaths.SourcePaths.Add(new Arc(bounds.Right - rightBottomRadius.X, bounds.Bottom + rightBottomRadius.Y, 
					rightBottomRadius.X, rightBottomRadius.Y, Math.PI + Math.PI * 0.5, 0.0, numSegments: NumSegments)
				{
					ResolutionScale = ResolutionScale
				});
			}

			if (rightTopRadius.X == 0 && rightTopRadius.Y == 0)
			{
				var singlePoint = new VertexStorage();
				singlePoint.Add(bounds.Right, bounds.Top, FlagsAndCommand.LineTo);
				allPaths.SourcePaths.Add(singlePoint);
			}
			else
			{
				allPaths.SourcePaths.Add(new Arc(bounds.Right - rightTopRadius.X, bounds.Top - rightTopRadius.Y, 
					rightTopRadius.X, rightTopRadius.Y, 0.0, Math.PI * 0.5, numSegments: NumSegments)
				{
					ResolutionScale = ResolutionScale
				});
			}

			if (leftTopRadius.X == 0 && leftTopRadius.Y == 0)
			{
				var singlePoint = new VertexStorage();
				singlePoint.Add(bounds.Left, bounds.Top, FlagsAndCommand.LineTo);
				allPaths.SourcePaths.Add(singlePoint);
			}
			else
			{
				allPaths.SourcePaths.Add(new Arc(bounds.Left + leftTopRadius.X, bounds.Top - leftTopRadius.Y, 
					leftTopRadius.X, leftTopRadius.Y, Math.PI * 0.5, Math.PI, numSegments: NumSegments)
				{
					ResolutionScale = ResolutionScale
				});
			}

			return allPaths.Vertices();
		}
	}
}
