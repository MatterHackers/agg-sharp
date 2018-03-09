/*
 * Smallest enclosing circle - Library (C#)
 *
 * Copyright (c) 2017 Project Nayuki
 * https://www.nayuki.io/page/smallest-enclosing-circle
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program (see COPYING.txt and COPYING.LESSER.txt).
 * If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;

namespace MatterHackers.VectorMath
{
	public struct Circle
	{
		public Vector2 Center { get; set; }

		// Center
		public double Radius { get; set; }

		private const double MULTIPLICATIVE_EPSILON = 1 + 1e-14;

		// Radius

		public Circle(Vector2 c, double r)
		{
			this.Center = c;
			this.Radius = r;
		}

		public bool Contains(Vector2 p)
		{
			return Center.Distance(p) <= Radius * MULTIPLICATIVE_EPSILON;
		}

		public bool Contains(ICollection<Vector2> ps)
		{
			foreach (Vector2 p in ps)
			{
				if (!Contains(p))
					return false;
			}
			return true;
		}
	}

	public sealed class SmallestEnclosingCircle
	{
		/*
		 * Returns the smallest circle that encloses all the given points. Runs in expected O(n) time, randomized.
		 * Note: If 0 points are given, a circle of radius -1 is returned. If 1 point is given, a circle of radius 0 is returned.
		 */

		// Initially: No boundary points known
		public static Circle MakeCircle(IEnumerable<Vector2> points)
		{
			// Clone list to preserve the caller's data, do Durstenfeld shuffle
			List<Vector2> shuffled = new List<Vector2>(points);
			Random rand = new Random();
			for (int i = shuffled.Count - 1; i > 0; i--)
			{
				int j = rand.Next(i + 1);
				Vector2 temp = shuffled[i];
				shuffled[i] = shuffled[j];
				shuffled[j] = temp;
			}

			// Progressively add points to circle or recompute circle
			Circle c = new Circle(new Vector2(0, 0), -1);
			for (int i = 0; i < shuffled.Count; i++)
			{
				Vector2 p = shuffled[i];
				if (c.Radius < 0 || !c.Contains(p))
				{
					c = MakeCircleOnePoint(shuffled.GetRange(0, i + 1), p);
				}
			}

			return c;
		}

		public static Circle MakeCircumcircle(Vector2 a, Vector2 b, Vector2 c)
		{
			// Mathematical algorithm from Wikipedia: Circumscribed circle
			double ox = (Math.Min(Math.Min(a.X, b.X), c.X) + Math.Max(Math.Min(a.X, b.X), c.X)) / 2;
			double oy = (Math.Min(Math.Min(a.Y, b.Y), c.Y) + Math.Max(Math.Min(a.Y, b.Y), c.Y)) / 2;
			double ax = a.X - ox, ay = a.Y - oy;
			double bx = b.X - ox, by = b.Y - oy;
			double cx = c.X - ox, cy = c.Y - oy;
			double d = (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by)) * 2;
			if (d == 0)
			{
				return new Circle(new Vector2(0, 0), -1);
			}
			double x = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
			double y = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
			Vector2 p = new Vector2(ox + x, oy + y);
			double r = Math.Max(Math.Max(p.Distance(a), p.Distance(b)), p.Distance(c));
			return new Circle(p, r);
		}

		public static Circle MakeDiameter(Vector2 a, Vector2 b)
		{
			Vector2 c = new Vector2((a.X + b.X) / 2, (a.Y + b.Y) / 2);
			return new Circle(c, Math.Max(c.Distance(a), c.Distance(b)));
		}

		// One boundary point known
		private static Circle MakeCircleOnePoint(List<Vector2> points, Vector2 p)
		{
			Circle c = new Circle(p, 0);
			for (int i = 0; i < points.Count; i++)
			{
				Vector2 q = points[i];
				if (!c.Contains(q))
				{
					if (c.Radius == 0)
					{
						c = MakeDiameter(p, q);
					}
					else
					{
						c = MakeCircleTwoPoints(points.GetRange(0, i + 1), p, q);
					}
				}
			}
			return c;
		}

		// Two boundary points known
		private static Circle MakeCircleTwoPoints(List<Vector2> points, Vector2 p, Vector2 q)
		{
			Circle circ = MakeDiameter(p, q);
			Circle left = new Circle(new Vector2(0, 0), -1);
			Circle right = new Circle(new Vector2(0, 0), -1);

			// For each point not in the two-point circle
			Vector2 pq = q - p;
			foreach (Vector2 r in points)
			{
				if (circ.Contains(r))
				{
					continue;
				}

				// Form a circumcircle and classify it on left or right side
				double cross = pq.Cross(r - p);
				Circle c = MakeCircumcircle(p, q, r);
				if (c.Radius < 0)
				{
					continue;
				}
				else if (cross > 0 && (left.Radius < 0 || pq.Cross(c.Center - p) > pq.Cross(left.Center - p)))
				{
					left = c;
				}
				else if (cross < 0 && (right.Radius < 0 || pq.Cross(c.Center - p) < pq.Cross(right.Center - p)))
				{
					right = c;
				}
			}

			// Select which circle to return
			if (left.Radius < 0 && right.Radius < 0)
			{
				return circ;
			}
			else if (left.Radius < 0)
			{
				return right;
			}
			else if (right.Radius < 0)
			{
				return left;
			}
			else
			{
				return left.Radius <= right.Radius ? left : right;
			}
		}
	}
}