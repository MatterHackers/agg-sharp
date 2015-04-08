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

namespace MatterHackers.Csg.Solids
{
	public class Cylinder : CsgObjectWrapper
	{
		private double height;

		public class CylinderPrimitive : Solid
		{
			internal double radius1, radius2, height;

			public double Radius1 { get { return radius1; } }

			public double Radius2 { get { return radius2; } }

			public double Height { get { return height; } }

			internal CylinderPrimitive(double radius1, double radius2, double height, string name)
				: base(name)
			{
				this.radius1 = radius1;
				this.radius2 = radius2;
				this.height = height;
			}

			internal CylinderPrimitive(CylinderPrimitive objectToCopy)
				: this(objectToCopy.radius1, objectToCopy.radius2, objectToCopy.height, objectToCopy.name)
			{
			}

			public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
			{
				double maxRadius = Math.Max(radius1, radius2);
				return new AxisAlignedBoundingBox(new Vector3(-maxRadius, -maxRadius, -height / 2), new Vector3(maxRadius, maxRadius, height / 2));
			}
		}

		public Cylinder(double radius, double height, Alignment alignment = Alignment.z, string name = "")
			: this(radius, radius, height, alignment, name)
		{
		}

		public Cylinder(double radius1, double radius2, double height, Alignment alignment = Alignment.z, string name = "")
			: base(name)
		{
			this.height = height;
			root = new CylinderPrimitive(radius1, radius2, height, name);
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