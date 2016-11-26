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

using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh")]
	public class OctreeTests
	{
		[Test]
		public void AllLeavesPutAtRoot()
		{
			Octree<int> tree = new Octree<int>(1, new Bounds(-10, -20, -30, 10, 20, 30));
			tree.Insert(1, new Bounds(-9, -19, -29, 9, 19, 29));
			tree.Insert(2, new Bounds(-9, -19, -29, 9, 19, 29));
			tree.Insert(3, new Bounds(-9, -19, -29, 9, 19, 29));
			tree.Insert(4, new Bounds(-9, -19, -29, 9, 19, 29));

			Assert.IsTrue(tree.CountBranches() == 1);

			Assert.AreEqual(tree.SearchArea(new Bounds(-9, -19, -29, 9, 19, 29)).Count(), 4, "Found all items.");
			Assert.AreEqual(tree.SearchPoint(0, 0, 0).Count(), 4, "All or around this point.");
			Assert.AreEqual(tree.FindCollisions(1).Count(), 3, "Don't find the item we are starting from.");
			Assert.AreEqual(tree.Count, 4, "Have the right count.");

			tree.Remove(3);
			Assert.AreEqual(tree.SearchArea(new Bounds(-9, -19, -29, 9, 19, 29)).Count(), 3, "Found all items.");
			Assert.AreEqual(tree.SearchPoint(0, 0, 0).Count(), 3, "All or around this point.");
			Assert.AreEqual(tree.FindCollisions(1).Count(), 2, "Don't find the item we are starting from.");
			Assert.AreEqual(tree.Count, 3, "Have the right count.");
		}
	}
}