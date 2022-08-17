/*
Copyright (c) 2022, Lars Brubaker
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

using MatterHackers.VectorMath;
using System;
using static System.Math;

// A collection of signed distance field functions
namespace DualContouring
{
    public interface ISdf
	{
        double Sdf(Vector3 p);

        AxisAlignedBoundingBox Bounds { get; }
    }

    public class Sphere : ISdf
    {
        public double Radius;

        public double Sdf(Vector3 p)
        {
            return p.Length - Radius;
        }

        public AxisAlignedBoundingBox Bounds => new AxisAlignedBoundingBox(-Radius, -Radius, -Radius, Radius, Radius, Radius);
    }

    public class Transform : ISdf
    {
        public ISdf Input { get; }
        public Matrix4X4 Matrix;
        public Matrix4X4 Inverse;

        public Transform(ISdf input, Matrix4X4 matrix)
        {
            this.Input = input;
            Matrix = matrix;
            this.Inverse = matrix.Inverted;
        }

        public double Sdf(Vector3 p)
        {
            return Input.Sdf(p.Transform(Inverse));
        }

        public AxisAlignedBoundingBox Bounds => Input.Bounds.NewTransformed(Matrix);
    }


    public class Cylinder : ISdf
    {
        public double Height;
        public double Radius;

        public double Sdf(Vector3 p)
        {
            if (p.Z > Height)
            {
                return p.Z - Height;
            }
            else if (p.Z < 0)
            {
                return -p.Z;
            }
            else
            {
                return new Vector2(p.X, p.Y).Length - Radius;
            }
        }

        public AxisAlignedBoundingBox Bounds => new AxisAlignedBoundingBox(-Radius, -Radius, 0, Radius, Radius, Height);
    }

    public class Box : ISdf
    {
        private Vector3 halfSize;

        public Vector3 Size 
        { 
            get => halfSize * 2;
            set => halfSize = value / 2;
        }

        public double Sdf(Vector3 p)
        {
            Vector3 q = p.Abs() - halfSize;
            return Vector3.ComponentMax(q, 0.0).Length + Min(Max(q.X, Max(q.Y, q.Z)), 0.0);
        }

        public AxisAlignedBoundingBox Bounds => new AxisAlignedBoundingBox(-halfSize, halfSize);
    }

    public class Union : ISdf
	{
        public ISdf[] Items;

        public double Sdf(Vector3 p)
        {
            var d = Items[0].Sdf(p);
            for (int i = 1; i < Items.Length; i++)
            {
                d = Min(d, Items[i].Sdf(p));
            }

            return d;
        }

        public AxisAlignedBoundingBox Bounds
        {
            get
            {
                var b = Items[0].Bounds;
                for (int i = 1; i < Items.Length; i++)
                {
                    b = AxisAlignedBoundingBox.Union(b, Items[i].Bounds);
                }

                return b;
            }
        }
    }

    public class Intersection : ISdf
    {
        public ISdf[] Items;

        public double Sdf(Vector3 p)
        {
            var d = Items[0].Sdf(p);
            for (int i = 1; i < Items.Length; i++)
            {
                d = Max(d, Items[i].Sdf(p));
            }

            return d;
        }

        public AxisAlignedBoundingBox Bounds
        {
            get
            {
                var b = Items[0].Bounds;
                for (int i = 1; i < Items.Length; i++)
                {
                    b = AxisAlignedBoundingBox.Intersection(b, Items[i].Bounds);
                }

                return b;
            }
        }
    }

    public class Subtraction : ISdf
    {
        public ISdf[] Items;

        public double Sdf(Vector3 p)
        {
            var d = -Items[0].Sdf(p);
            for (int i = 1; i < Items.Length; i++)
            {
                d = Min(d, Items[i].Sdf(p));
            }

            return d;
        }

        public AxisAlignedBoundingBox Bounds
        {
            get
            {
                return Items[0].Bounds;
            }
        }
    }
}