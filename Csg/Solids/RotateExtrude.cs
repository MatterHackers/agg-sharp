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

using System;
using System.Collections.Generic;
using System.IO;

using MatterHackers.VectorMath;
using MatterHackers.Csg.Transform;

namespace MatterHackers.Csg.Solids
{
    public class RotateExtrude : CsgObjectWrapper
    {
        public class RotateExtrudePrimitive : Solid
        {
            internal Vector2[] points;
            internal double axisOffset;

            public Vector2[] Points { get { return points; } }
            public double AxisOffset { get { return axisOffset; } }

            internal RotateExtrudePrimitive(Vector2[] points, double axisOffset, string name)
                : base(name)
            {
                this.points = points;
                this.axisOffset = axisOffset;
            }

            public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
            {
                double minY = double.MaxValue;
                double maxY = double.MinValue;
                double maxRadius = 0;
                foreach (Vector2 point in points)
                {
                    maxRadius = Math.Max(Math.Abs(point.x + axisOffset), maxRadius);
                    minY = Math.Min(minY, point.y);
                    maxY = Math.Max(maxY, point.y);
                }
                return new AxisAlignedBoundingBox(new Vector3(-maxRadius, -maxRadius, minY), new Vector3(maxRadius, maxRadius, maxY));
            }
        }

        public RotateExtrude(double[] points, double axisOffset = 0, Alignment alignment = Alignment.z, string name = "")
        {
            if ((points.Length % 2) != 0)
            {
                throw new Exception("You must pass in an even number of points so they can be converted to Vector2s.");
            }
            List<Vector2> vectorPoints = new List<Vector2>();
            for (int i = 0; i < points.Length; i += 2)
            {
                vectorPoints.Add(new Vector2(points[i], points[i + 1]));
            }
            root = new RotateExtrudePrimitive(vectorPoints.ToArray(), axisOffset, name);
            switch (alignment)
            {
                case Alignment.x:
                    root = new Rotate(root, y: MathHelper.DegreesToRadians(90));
                    break;

                case Alignment.y:
                    root = new Rotate(root, x: MathHelper.DegreesToRadians(90));
                    break;

                case Alignment.z:
                    // don't need to do anything
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public RotateExtrude(Vector2[] points, double axisOffset, Alignment alignment = Alignment.z, string name = "")
            : base(name)
        {
            root = new RotateExtrudePrimitive(points, axisOffset, name);
            switch (alignment)
            {
                case Alignment.x:
                    root = new Rotate(root, y: MathHelper.DegreesToRadians(90));
                    break;

                case Alignment.negX:
                    root = new Rotate(root, y: MathHelper.DegreesToRadians(-90));
                    break;

                case Alignment.y:
                    root = new Rotate(root, x: MathHelper.DegreesToRadians(90));
                    break;

				case Alignment.z:
					break;

				case Alignment.negZ:
                    root = new Rotate(root, x: MathHelper.DegreesToRadians(180));
					break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
