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

using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;

namespace MatterHackers.Csg
{
	[TestFixture, Category("Agg.Csg")]
	public class CSGTests
	{
		[Test]
		public void MirrorTests()
		{
			{
				Box leftBox = new Box(10, 20, 30, "leftBox", createCentered: false);
				CsgObject rightBox = new Box(11, 21, 31, "rightBox", createCentered: false);
				rightBox = new Align(rightBox, Face.Left, leftBox, Face.Right);
				CsgObject union = new Union(leftBox, rightBox);
				Assert.IsTrue(union.XSize == 21, "Correct XSize");
				AxisAlignedBoundingBox unionBounds = union.GetAxisAlignedBoundingBox();
				Assert.IsTrue(unionBounds.minXYZ == new Vector3(), "MinXYZ at 0");
				Assert.IsTrue(union.GetAxisAlignedBoundingBox().maxXYZ == new Vector3(21, 21, 31), "MaxXYZ correct");
			}

			{
				Box leftBox = new Box(10, 20, 30, "leftBox", createCentered: false);
				CsgObject rightBox = leftBox.NewMirrorAccrossX(name: "rightBox");
				rightBox = new Align(rightBox, Face.Left, leftBox, Face.Right);
				CsgObject union = new Union(leftBox, rightBox);
				Assert.IsTrue(union.XSize == 20, "Correct XSize");
				AxisAlignedBoundingBox unionBounds = union.GetAxisAlignedBoundingBox();
				Assert.IsTrue(unionBounds.minXYZ == new Vector3(), "MinXYZ at 0");
				Assert.IsTrue(union.GetAxisAlignedBoundingBox().maxXYZ == new Vector3(20, 20, 30), "MaxXYZ correct");
			}

			{
				Box frontBox = new Box(10, 20, 30, createCentered: false);
				CsgObject backBox = frontBox.NewMirrorAccrossY();
				backBox = new Align(backBox, Face.Front, frontBox, Face.Back);
				CsgObject union = new Union(frontBox, backBox);
				Assert.IsTrue(union.YSize == 40, "Correct YSize");
				AxisAlignedBoundingBox unionBounds = union.GetAxisAlignedBoundingBox();
				Assert.IsTrue(unionBounds.minXYZ == new Vector3(), "MinXYZ at 0");
				Assert.IsTrue(union.GetAxisAlignedBoundingBox().maxXYZ == new Vector3(10, 40, 30), "MaxXYZ correct");
			}
		}
	}
}