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
    /// <summary>
    /// Allows for the creation of linear extruded polygon objects.  You can also include a twist (in radians) while extruding.
    /// </summary>
    public class LinearExtrude : CsgObjectWrapper
    {
        /// <summary>
        /// This is the internral class for LinearEtrude.  You should not create this directly.
        /// </summary>
        public class LinearExtrudePrimitive : Solid
        {
            internal Vector2[] points;
            internal double height;
            internal double twistRadians;

            public Vector2[] Points { get { return points; } }
            public double Height { get { return height; } }
            public double TwistRadians { get { return twistRadians; } }

            internal LinearExtrudePrimitive(Vector2[] points, double height, double twistRadians, string name)
                : base(name)
            {
                this.points = points;
                this.height = height;
                this.twistRadians = twistRadians;
            }

            public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
            {
                double minY = double.MaxValue;
                double maxY = double.MinValue;
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                foreach (Vector2 point in points)
                {
                    minY = Math.Min(minY, point.y);
                    maxY = Math.Max(maxY, point.y);
                    minX = Math.Min(minX, point.x);
                    maxX = Math.Max(maxX, point.x);
                }
                return new AxisAlignedBoundingBox(new Vector3(minX, minY, -height/2), new Vector3(maxX, maxY, height/2));
            }
        }

        /// <summary>
        /// The constructor takes an array of doubles as the input to the polygon to extrude.
        /// </summary>
        /// <param name="points">Pairs of double values that will be used as the coordinates of the polygon points.</param>
        /// <param name="height"></param>
        /// <param name="alignment"></param>
        /// <param name="twistRadians"></param>
        /// <param name="name"></param>
        public LinearExtrude(double[] points, double height, Alignment alignment = Alignment.z, double twistRadians = 0, string name = "")
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
            root = new LinearExtrudePrimitive(vectorPoints.ToArray(), height, twistRadians, name);
            switch (alignment)
            {
                case Alignment.x:
                    root = new Rotate(root, y: MathHelper.DegreesToRadians(90));
                    break;

                case Alignment.y:
                    root = new Rotate(root, x: MathHelper.DegreesToRadians(90));
                    break;
            }
        }

        /// <summary>
        /// The constructor takes an array of Vector2s as the input to the polygon to extrude.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="height"></param>
        /// <param name="alignment"></param>
        /// <param name="twistRadians"></param>
        /// <param name="name"></param>
        public LinearExtrude(Vector2[] points, double height, Alignment alignment = Alignment.z, double twistRadians = 0, string name = "")
            : base(name)
        {
            root = new LinearExtrudePrimitive(points, height, twistRadians, name);
            switch (alignment)
            {
                case Alignment.x:
                    root = new Rotate(root, y: MathHelper.DegreesToRadians(90));
                    break;

                case Alignment.y:
                    root = new Rotate(root, x: MathHelper.DegreesToRadians(90));
                    break;
            }
        }
    }
}
