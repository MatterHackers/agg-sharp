/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Csg.Transform;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Csg.Solids
{
	using Aabb = AxisAlignedBoundingBox;

	public class Round : CsgObjectWrapper
	{
		public int Sides { get; set; } = 30;

		private const double defaultExtraDimension = .02;
		private const double defaultExtraRadialDimension = .2;

		internal Vector3 size;

		private Dictionary<Edge, double> roundedEdges = new Dictionary<Edge, double>();
		private Dictionary<Face, double> roundedPoints = new Dictionary<Face, double>();

		public Round(Vector3 size, string name = "")
			: base(name)
		{
			this.size = size;
			root = new Box(size, name);
		}

		public Round(double xSize, double ySize, double zSize, string name = "")
			: this(new Vector3(xSize, ySize, zSize), name)
		{
		}

		public override Aabb GetAxisAlignedBoundingBox()
		{
			return new Aabb(-size / 2, size / 2);
		}

		public void RoundFace(Face faceToRound, double radius, double extraDimension = defaultExtraDimension)
		{
			switch (faceToRound)
			{
				case Face.Left:
					RoundEdge(Edge.LeftFront, radius, extraDimension);
					RoundEdge(Edge.LeftBack, radius, extraDimension);
					RoundEdge(Edge.LeftTop, radius, extraDimension);
					RoundEdge(Edge.LeftBottom, radius, extraDimension);
					break;

				case Face.Right:
					RoundEdge(Edge.RightFront, radius, extraDimension);
					RoundEdge(Edge.RightBack, radius, extraDimension);
					RoundEdge(Edge.RightTop, radius, extraDimension);
					RoundEdge(Edge.RightBottom, radius, extraDimension);
					break;

				case Face.Front:
					RoundEdge(Edge.LeftFront, radius, extraDimension);
					RoundEdge(Edge.RightFront, radius, extraDimension);
					RoundEdge(Edge.FrontBottom, radius, extraDimension);
					RoundEdge(Edge.FrontTop, radius, extraDimension);
					break;

				case Face.Back:
					RoundEdge(Edge.LeftBack, radius, extraDimension);
					RoundEdge(Edge.RightBack, radius, extraDimension);
					RoundEdge(Edge.BackBottom, radius, extraDimension);
					RoundEdge(Edge.BackTop, radius, extraDimension);
					break;

				case Face.Bottom:
					RoundEdge(Edge.LeftBottom, radius, extraDimension);
					RoundEdge(Edge.RightBottom, radius, extraDimension);
					RoundEdge(Edge.FrontBottom, radius, extraDimension);
					RoundEdge(Edge.BackBottom, radius, extraDimension);
					break;

				case Face.Top:
					RoundEdge(Edge.LeftTop, radius, extraDimension);
					RoundEdge(Edge.RightTop, radius, extraDimension);
					RoundEdge(Edge.FrontTop, radius, extraDimension);
					RoundEdge(Edge.BackTop, radius, extraDimension);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		private Vector3 GetEdgeOffset(Face faceFlags)
		{
			if ((faceFlags & Face.Left) != 0 && (faceFlags & Face.Right) != 0)
			{
				throw new Exception("Cant have both left and right face at the same time.");
			}
			if ((faceFlags & Face.Front) != 0 && (faceFlags & Face.Back) != 0)
			{
				throw new Exception("Cant have both front and back face at the same time.");
			}
			if ((faceFlags & Face.Bottom) != 0 && (faceFlags & Face.Top) != 0)
			{
				throw new Exception("Cant have both bottom and top face at the same time.");
			}

			Vector3 offset = Vector3.Zero;
			if ((faceFlags & Face.Left) != 0)
			{
				offset.X = -size.X / 2;
			}
			else if ((faceFlags & Face.Right) != 0)
			{
				offset.X = size.X / 2;
			}
			if ((faceFlags & Face.Front) != 0)
			{
				offset.Y = -size.Y / 2;
			}
			else if ((faceFlags & Face.Back) != 0)
			{
				offset.Y = size.Y / 2;
			}
			if ((faceFlags & Face.Bottom) != 0)
			{
				offset.Z = -size.Z / 2;
			}
			else if ((faceFlags & Face.Top) != 0)
			{
				offset.Z = size.Z / 2;
			}

			return offset;
		}

		public void RoundEdge(Edge edgeToRound, double radius, double extraDimension = defaultExtraDimension)
		{
			if (roundedEdges.ContainsKey(edgeToRound))
			{
				return;
			}
			CsgObject newRound = null;
			double radiusBoxSize = radius + extraDimension;
			switch (edgeToRound)
			{
				case Edge.LeftFront:
					{
						double zSize = size.Z + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, radiusBoxSize, zSize);
						CsgObject frontTopCut = new Cylinder(radius, zSize + extraDimension * 2, Sides, Alignment.z);
						frontTopCut = new Align(frontTopCut, Face.Left | Face.Front, newRound, Face.Left | Face.Front, extraDimension, extraDimension, 0);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Left | Face.Front, GetEdgeOffset(Face.Left | Face.Front), -extraDimension, -extraDimension, 0);
					}
					break;

				case Edge.LeftBack:
					{
						double zSize = size.Z + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, radiusBoxSize, zSize);
						CsgObject BackTopCut = new Cylinder(radius, zSize + extraDimension * 2, Sides, Alignment.z);
						BackTopCut = new Align(BackTopCut, Face.Left | Face.Back, newRound, Face.Left | Face.Back, extraDimension, -extraDimension, 0);
						newRound -= BackTopCut;
						newRound = new Align(newRound, Face.Left | Face.Back, GetEdgeOffset(Face.Left | Face.Back), -extraDimension, extraDimension, 0);
					}
					break;

				case Edge.LeftTop:
					{
						double ySize = size.Y + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, ySize, radiusBoxSize);
						CsgObject frontTopCut = new Cylinder(radius, ySize + extraDimension * 2, Sides, Alignment.y);
						frontTopCut = new Align(frontTopCut, Face.Left | Face.Top, newRound, Face.Left | Face.Top, extraDimension, 0, -extraDimension);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Left | Face.Top, GetEdgeOffset(Face.Left | Face.Top), -extraDimension, 0, extraDimension);
					}
					break;

				case Edge.LeftBottom:
					{
						double ySize = size.Y + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, ySize, radiusBoxSize);
						CsgObject frontTopCut = new Cylinder(radius, ySize + extraDimension * 2, Sides, Alignment.y);
						frontTopCut = new Align(frontTopCut, Face.Left | Face.Bottom, newRound, Face.Left | Face.Bottom, extraDimension, 0, extraDimension);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Left | Face.Bottom, GetEdgeOffset(Face.Left | Face.Bottom), -extraDimension, 0, -extraDimension);
					}
					break;

				case Edge.RightFront:
					{
						double zSize = size.Z + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, radiusBoxSize, zSize);
						CsgObject frontTopCut = new Cylinder(radius, zSize + extraDimension * 2, Sides, Alignment.z);
						frontTopCut = new Align(frontTopCut, Face.Right | Face.Front, newRound, Face.Right | Face.Front, -extraDimension, extraDimension, 0);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Right | Face.Front, GetEdgeOffset(Face.Right | Face.Front), extraDimension, -extraDimension, 0);
					}
					break;

				case Edge.RightTop:
					{
						double ySize = size.Y + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, ySize, radiusBoxSize);
						CsgObject frontTopCut = new Cylinder(radius, ySize + extraDimension * 2, Sides, Alignment.y);
						frontTopCut = new Align(frontTopCut, Face.Right | Face.Top, newRound, Face.Right | Face.Top, -extraDimension, 0, -extraDimension);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Right | Face.Top, GetEdgeOffset(Face.Right | Face.Top), extraDimension, 0, extraDimension);
					}
					break;

				case Edge.RightBottom:
					{
						double ySize = size.Y + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, ySize, radiusBoxSize);
						CsgObject frontBottomCut = new Cylinder(radius, ySize + extraDimension * 2, Sides, Alignment.y);
						frontBottomCut = new Align(frontBottomCut, Face.Right | Face.Bottom, newRound, Face.Right | Face.Bottom, -extraDimension, 0, extraDimension);
						newRound -= frontBottomCut;
						newRound = new Align(newRound, Face.Right | Face.Bottom, GetEdgeOffset(Face.Right | Face.Bottom), extraDimension, 0, -extraDimension);
					}
					break;

				case Edge.RightBack:
					{
						double zSize = size.Z + 2 * extraDimension;
						newRound = new Box(radiusBoxSize, radiusBoxSize, zSize);
						CsgObject BackTopCut = new Cylinder(radius, zSize + extraDimension * 2, Sides, Alignment.z);
						BackTopCut = new Align(BackTopCut, Face.Right | Face.Back, newRound, Face.Right | Face.Back, -extraDimension, -extraDimension, 0);
						newRound -= BackTopCut;
						newRound = new Align(newRound, Face.Right | Face.Back, GetEdgeOffset(Face.Right | Face.Back), extraDimension, extraDimension, 0);
					}
					break;

				case Edge.FrontTop:
					{
						double xSize = size.X + 2 * extraDimension;
						newRound = new Box(xSize, radiusBoxSize, radiusBoxSize);
						CsgObject frontTopCut = new Cylinder(radius, xSize + extraDimension * 2, Sides, Alignment.x);
						frontTopCut = new Align(frontTopCut, Face.Front | Face.Top, newRound, Face.Front | Face.Top, 0, extraDimension, -extraDimension);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Front | Face.Top, GetEdgeOffset(Face.Front | Face.Top), 0, -extraDimension, extraDimension);
					}
					break;

				case Edge.FrontBottom:
					{
						double xSize = size.X + 2 * extraDimension;
						newRound = new Box(xSize, radiusBoxSize, radiusBoxSize);
						CsgObject frontTopCut = new Cylinder(radius, xSize + extraDimension * 2, Sides, Alignment.x);
						frontTopCut = new Align(frontTopCut, Face.Front | Face.Bottom, newRound, Face.Front | Face.Bottom, 0, extraDimension, extraDimension);
						newRound -= frontTopCut;
						newRound = new Align(newRound, Face.Front | Face.Bottom, GetEdgeOffset(Face.Front | Face.Bottom), 0, -extraDimension, -extraDimension);
					}
					break;

				case Edge.BackBottom:
					{
						double xSize = size.X + 2 * extraDimension;
						newRound = new Box(xSize, radiusBoxSize, radiusBoxSize);
						CsgObject backBottomCut = new Cylinder(radius, xSize + extraDimension * 2, Sides, Alignment.x);
						backBottomCut = new Align(backBottomCut, Face.Back | Face.Bottom, newRound, Face.Back | Face.Bottom, 0, -extraDimension, extraDimension);
						newRound -= backBottomCut;
						newRound = new Align(newRound, Face.Back | Face.Bottom, GetEdgeOffset(Face.Back | Face.Bottom), 0, extraDimension, -extraDimension);
					}
					break;

				case Edge.BackTop:
					{
						double xSize = size.X + 2 * extraDimension;
						newRound = new Box(xSize, radiusBoxSize, radiusBoxSize);
						CsgObject backTopCut = new Cylinder(radius, xSize + extraDimension * 2, Sides, Alignment.x);
						backTopCut = new Align(backTopCut, Face.Back | Face.Top, newRound, Face.Back | Face.Top, 0, -extraDimension, -extraDimension);
						newRound -= backTopCut;
						newRound = new Align(newRound, Face.Back | Face.Top, GetEdgeOffset(Face.Back | Face.Top), 0, extraDimension, extraDimension);
					}
					break;

				default:
					throw new NotImplementedException("Don't know how to round " + edgeToRound.ToString());
			}

			if (root is Box)
			{
				root = newRound;
			}
			else
			{
				root += newRound;
			}

			roundedEdges.Add(edgeToRound, radius);

			CheckCornersAndRoundIfNeeded(extraDimension);
		}

		private void CheckCornerAndRoundIfNeeded(Edge edge1, Edge edge2, Edge edge3, double extraDimension)
		{
			if (roundedEdges.ContainsKey(edge1) && roundedEdges.ContainsKey(edge2) && roundedEdges.ContainsKey(edge3))
			{
				// check if they are all the same radius
				if (roundedEdges[edge1] == roundedEdges[edge2] && roundedEdges[edge2] == roundedEdges[edge3])
				{
					RoundPoint((Face)(edge1 | edge2 | edge3), roundedEdges[edge1], extraDimension);
				}
			}
		}

		private void CheckCornersAndRoundIfNeeded(double extraDimension)
		{
			CheckCornerAndRoundIfNeeded(Edge.LeftFront, Edge.LeftTop, Edge.FrontTop, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.LeftFront, Edge.LeftBottom, Edge.FrontBottom, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.RightFront, Edge.RightTop, Edge.FrontTop, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.RightFront, Edge.RightBottom, Edge.FrontBottom, extraDimension);

			CheckCornerAndRoundIfNeeded(Edge.LeftBack, Edge.LeftTop, Edge.BackTop, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.LeftBack, Edge.LeftBottom, Edge.BackBottom, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.RightBack, Edge.RightTop, Edge.BackTop, extraDimension);
			CheckCornerAndRoundIfNeeded(Edge.RightBack, Edge.RightBottom, Edge.BackBottom, extraDimension);
		}

		public void RoundPoint(Face threeFacesThatSharePoint, double radius, double extraDimension = defaultExtraDimension)
		{
			if (roundedPoints.ContainsKey(threeFacesThatSharePoint))
			{
				return;
			}

			double radiusBoxSize = radius + extraDimension;

			switch (threeFacesThatSharePoint)
			{
				case Face.Left | Face.Front | Face.Bottom:
					{
						RoundEdge(Edge.LeftFront, radius, extraDimension);
						RoundEdge(Edge.LeftBottom, radius, extraDimension);
						RoundEdge(Edge.FrontBottom, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, extraDimension, extraDimension, extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), -extraDimension, -extraDimension, -extraDimension);
						root += pointRound;
					}
					break;

				case Face.Left | Face.Front | Face.Top:
					{
						RoundEdge(Edge.LeftFront, radius, extraDimension);
						RoundEdge(Edge.LeftTop, radius, extraDimension);
						RoundEdge(Edge.FrontTop, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, extraDimension, extraDimension, -extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), -extraDimension, -extraDimension, extraDimension);
						root += pointRound;
					}
					break;

				case Face.Left | Face.Back | Face.Bottom:
					{
						RoundEdge(Edge.LeftBack, radius, extraDimension);
						RoundEdge(Edge.LeftBottom, radius, extraDimension);
						RoundEdge(Edge.BackBottom, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, extraDimension, -extraDimension, extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), -extraDimension, extraDimension, -extraDimension);
						root += pointRound;
					}
					break;

				case Face.Left | Face.Back | Face.Top:
					{
						RoundEdge(Edge.LeftBack, radius, extraDimension);
						RoundEdge(Edge.LeftTop, radius, extraDimension);
						RoundEdge(Edge.BackTop, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, extraDimension, -extraDimension, -extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), -extraDimension, extraDimension, extraDimension);
						root += pointRound;
					}
					break;

				case Face.Right | Face.Front | Face.Bottom:
					{
						RoundEdge(Edge.RightFront, radius, extraDimension);
						RoundEdge(Edge.RightBottom, radius, extraDimension);
						RoundEdge(Edge.FrontBottom, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, -extraDimension, extraDimension, extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), extraDimension, -extraDimension, -extraDimension);
						root += pointRound;
					}
					break;

				case Face.Right | Face.Front | Face.Top:
					{
						RoundEdge(Edge.RightFront, radius, extraDimension);
						RoundEdge(Edge.RightTop, radius, extraDimension);
						RoundEdge(Edge.FrontTop, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, -extraDimension, extraDimension, -extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), extraDimension, -extraDimension, extraDimension);
						root += pointRound;
					}
					break;

				case Face.Right | Face.Back | Face.Bottom:
					{
						RoundEdge(Edge.RightBack, radius, extraDimension);
						RoundEdge(Edge.RightBottom, radius, extraDimension);
						RoundEdge(Edge.BackBottom, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, -extraDimension, -extraDimension, extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), extraDimension, extraDimension, -extraDimension);
						root += pointRound;
					}
					break;

				case Face.Right | Face.Back | Face.Top:
					{
						RoundEdge(Edge.RightBack, radius, extraDimension);
						RoundEdge(Edge.RightTop, radius, extraDimension);
						RoundEdge(Edge.BackTop, radius, extraDimension);

						CsgObject pointRound = new Box(radiusBoxSize, radiusBoxSize, radiusBoxSize, "box");
						CsgObject pointCut = new Sphere(radius, "sphere");
						pointCut = new Align(pointCut, threeFacesThatSharePoint, pointRound, threeFacesThatSharePoint, -extraDimension, -extraDimension, -extraDimension);
						pointRound -= pointCut;
						pointRound = new Align(pointRound, threeFacesThatSharePoint, GetEdgeOffset(threeFacesThatSharePoint), extraDimension, extraDimension, extraDimension);
						root += pointRound;
					}
					break;

				default:
					throw new NotImplementedException("Don't know how to round " + threeFacesThatSharePoint.ToString());
			}

			if (!roundedPoints.ContainsKey(threeFacesThatSharePoint))
			{
				roundedPoints.Add(threeFacesThatSharePoint, radius);
			}
		}

		public void RoundAll(double radius, double extraDimension = defaultExtraDimension)
		{
			RoundPoint(Face.Left | Face.Front | Face.Bottom, radius, extraDimension);
			RoundPoint(Face.Left | Face.Front | Face.Top, radius, extraDimension);
			RoundPoint(Face.Left | Face.Back | Face.Bottom, radius, extraDimension);
			RoundPoint(Face.Left | Face.Back | Face.Top, radius, extraDimension);
			RoundPoint(Face.Right | Face.Front | Face.Bottom, radius, extraDimension);
			RoundPoint(Face.Right | Face.Front | Face.Top, radius, extraDimension);
			RoundPoint(Face.Right | Face.Back | Face.Bottom, radius, extraDimension);
			RoundPoint(Face.Right | Face.Back | Face.Top, radius, extraDimension);
		}

		public static CsgObject CreateFillet(CsgObject objectA, Face faceA, CsgObject objectB, Face faceB, double radius, double extraDimension = defaultExtraDimension)
		{
			int centralAxis = 0; // we start defaulted to x
			switch ((Edge)(faceA | faceB))
			{
				case Edge.LeftTop:
				case Edge.LeftBottom:
				case Edge.RightTop:
				case Edge.RightBottom:
					centralAxis = 1; // y axis
					break;

				case Edge.LeftFront:
				case Edge.LeftBack:
				case Edge.RightFront:
				case Edge.RightBack:
					centralAxis = 2; // z axis
					break;
			}

			AxisAlignedBoundingBox boundsA = objectA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox boundsB = objectB.GetAxisAlignedBoundingBox();

			double maxMin = Math.Max(boundsA.MinXYZ[centralAxis], boundsB.MinXYZ[centralAxis]);
			double minMax = Math.Min(boundsA.MaxXYZ[centralAxis], boundsB.MaxXYZ[centralAxis]);
			if (maxMin >= minMax)
			{
				throw new ArgumentException("Your two objects must overlap to have a fillet be created.");
			}

			Vector3 size = new Vector3(radius, radius, radius);
			size[centralAxis] = minMax - maxMin;
			Round newFilletRound = new Round(size);

			Face faceToGetBevelFor = CsgObject.GetOposite(faceA) | CsgObject.GetOposite(faceB);
			newFilletRound.RoundEdge((Edge)faceToGetBevelFor, radius, extraDimension);

			CsgObject newFillet = new SetCenter(newFilletRound, objectA.GetCenter());
			newFillet = new Align(newFillet, CsgObject.GetOposite(faceA), objectA, faceA);
			newFillet = new Align(newFillet, CsgObject.GetOposite(faceB), objectB, faceB);

			return newFillet;
		}

		public static CsgObject CreateBevel(double innerRadius, double outerRadius, double height, Alignment alignment = Alignment.z, double extraDimension = defaultExtraRadialDimension, string name = "")
		{
			double width = outerRadius - innerRadius;
			List<Vector2> points = new List<Vector2>();

			int numCurvePoints = 6;
			for (int curvePoint = 0; curvePoint <= numCurvePoints; curvePoint++)
			{
				double x = width - Math.Cos((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * width;
				double y = height - Math.Sin((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * height;
				points.Add(new Vector2(x, y));
			}
			points.Add(new Vector2(width + extraDimension, 0));
			points.Add(new Vector2(width + extraDimension, height));

			return new RotateExtrude(points.ToArray(), innerRadius, alignment, name);
		}

		public static CsgObject CreateNegativeBevel(double innerRadius, double outerRadius, double height, Alignment alignment = Alignment.z, double extraDimension = defaultExtraRadialDimension, string name = "")
		{
			double width = outerRadius - innerRadius;
			List<Vector2> points = new List<Vector2>();

			int numCurvePoints = 6;
			for (int curvePoint = 0; curvePoint <= numCurvePoints; curvePoint++)
			{
				double x = width - Math.Cos((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * width;
				double y = height - Math.Sin((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * height;
				points.Add(new Vector2(x, y));
			}
			points.Add(new Vector2(width, 0));
			points.Add(new Vector2(width, height));

			CsgObject bevel = new RotateExtrude(points.ToArray(), innerRadius, alignment, name);

			points.Clear();
			points.Add(new Vector2(0, -extraDimension));
			points.Add(new Vector2(width + extraDimension, -extraDimension));
			points.Add(new Vector2(width + extraDimension, height + extraDimension));
			points.Add(new Vector2(0, height + extraDimension));

			CsgObject cut = new RotateExtrude(points.ToArray(), innerRadius, alignment, name);
			cut = new Align(cut, Face.Bottom, bevel, Face.Bottom, 0, 0, .1);
			//return cut;
			bevel = cut - bevel;

			return bevel;
		}

		public static CsgObject CreateFillet(double innerRadius, double outerRadius, double height, Alignment alignment = Alignment.z, double extraDimension = defaultExtraRadialDimension, string name = "")
		{
			double width = outerRadius - innerRadius;
			List<Vector2> points = new List<Vector2>();

			int numCurvePoints = 8;
			for (int curvePoint = numCurvePoints; curvePoint >= 0; curvePoint--)
			{
				double x = width - Math.Cos((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * width;
				double y = height - Math.Sin((MathHelper.Tau / 4 * curvePoint / numCurvePoints)) * height;
				points.Add(new Vector2(x, y));
			}
			points.Add(new Vector2(-extraDimension, height));
			points.Add(new Vector2(-extraDimension, 0));

			return new RotateExtrude(points.ToArray(), innerRadius, alignment, name);
		}

		public static CsgObject CreateBevel(CsgObject objectToApplyBevelTo, Edge edgeToRound, int radius)
		{
			Round bevel = new Round(objectToApplyBevelTo.Size);
			bevel.RoundEdge(edgeToRound, radius);
			return bevel;
		}
	}
}