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
	[TestFixture, Category("Agg.VectorMath")]
	public class BvhTreeTests
	{
		[Test]
		public void TestBvhSimpleTreeSearches()
		{
			var data = new List<BvhTreeItemData<int>>();
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(-10, -20, -30, 10, 20, 30)));
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(-9, -18, -29, 9, 19, 29)));
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(-9, -17, -29, 9, 19, 28)));
			data.Add(new BvhTreeItemData<int>(4, new AxisAlignedBoundingBox(-9, -16, -29, 9, 19, 27)));

			var bvhBuilder = new TradeOffBvhConstructor<int>();
			var tree = bvhBuilder.CreateNewHierachy(data);

			Assert.IsTrue(tree.CountBranches() == 1, "all the bounds overlap, there is no value in splitting them up");

			var results = new List<int>();
			tree.SearchBounds(new AxisAlignedBoundingBox(-9, -19, -29, 9, 19, 29), results);
			Assert.AreEqual(results.Count(), 4, "Found all items.");

			results.Clear();
			tree.SearchPoint(0, 0, 0, results);
			Assert.AreEqual(4, results.Count(), "All or around this point.");
		}

		[Test]
		public void TestBvhTreeSearches()
		{
			// for this shape imagine the 8 corners of a Rubik's cube and the center
			var data = new List<BvhTreeItemData<int>>();
			// left, front, bottom
			data.Add(new BvhTreeItemData<int>(0, new AxisAlignedBoundingBox(0, 0, 0, 1, 1, 1)));
			// right, front, bottom
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(2, 0, 0, 3, 1, 1)));
			// left, back, bottom
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(0, 2, 0, 1, 3, 1)));
			// right, back, bottom
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(2, 2, 0, 3, 3, 1)));
			// center, center, center
			data.Add(new BvhTreeItemData<int>(4, new AxisAlignedBoundingBox(1, 1, 1, 2, 2, 2)));
			// left, front, top
			data.Add(new BvhTreeItemData<int>(5, new AxisAlignedBoundingBox(0, 0, 2, 1, 1, 3)));
			// right, front, top
			data.Add(new BvhTreeItemData<int>(6, new AxisAlignedBoundingBox(2, 0, 2, 3, 1, 3)));
			// left, back, top
			data.Add(new BvhTreeItemData<int>(7, new AxisAlignedBoundingBox(0, 2, 2, 1, 3, 3)));
			// right, back, top
			data.Add(new BvhTreeItemData<int>(8, new AxisAlignedBoundingBox(2, 2, 2, 3, 3, 3)));
			var bvhBuilder = new TradeOffBvhConstructor<int>();
			var tree = bvhBuilder.CreateNewHierachy(data);

			Assert.AreEqual(7, tree.CountBranches(), "these must be split up into multiple bounds");

			var results = new List<int>();
			tree.SearchBounds(new AxisAlignedBoundingBox(0, 0, 0, 3, 3, 3), results);
			Assert.AreEqual(results.Count(), 9, "Found all items.");

			results.Clear();
			tree.SearchPoint(1.5, 1.5, 1.5, results);
			Assert.AreEqual(1, results.Count(), "All or around this point.");
			Assert.AreEqual(4, results[0], "found the center point.");

			results.Clear();
			tree.SearchBounds(new AxisAlignedBoundingBox(1.5, 1.5, 0, 3, 3, 3), results);
			Assert.AreEqual(3, results.Count(), "All or around this point.");
			Assert.IsTrue(results.Contains(4), "found the center point.");
			Assert.IsTrue(results.Contains(8), "found the center point.");
			Assert.IsTrue(results.Contains(3), "found the center point.");
		}

		[Test]
		public void TestBvhAlongRay()
		{
			// for this shape imagine the 8 corners of a Rubik's cube and the center
			var data = new List<BvhTreeItemData<int>>();
			// left, front, bottom
			data.Add(new BvhTreeItemData<int>(0, new AxisAlignedBoundingBox(0, 0, 0, 1, 1, 1)));
			// right, front, bottom
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(2, 0, 0, 3, 1, 1)));
			// left, back, bottom
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(0, 2, 0, 1, 3, 1)));
			// right, back, bottom
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(2, 2, 0, 3, 3, 1)));
			// center, center, center
			data.Add(new BvhTreeItemData<int>(4, new AxisAlignedBoundingBox(1, 1, 1, 2, 2, 2)));
			// left, front, top
			data.Add(new BvhTreeItemData<int>(5, new AxisAlignedBoundingBox(0, 0, 2, 1, 1, 3)));
			// right, front, top
			data.Add(new BvhTreeItemData<int>(6, new AxisAlignedBoundingBox(2, 0, 2, 3, 1, 3)));
			// left, back, top
			data.Add(new BvhTreeItemData<int>(7, new AxisAlignedBoundingBox(0, 2, 2, 1, 3, 3)));
			// right, back, top
			data.Add(new BvhTreeItemData<int>(8, new AxisAlignedBoundingBox(2, 2, 2, 3, 3, 3)));
			var bvhBuilder = new TradeOffBvhConstructor<int>();
			var tree = bvhBuilder.CreateNewHierachy(data);

			Assert.AreEqual(7, tree.CountBranches(), "these must be split up into multiple bounds");

			var results = new List<int>();
			tree.AlongRay(new Ray(new Vector3(-.5, .5, .5), Vector3.UnitX), results);
			Assert.AreEqual(results.Count(), 2, "Found all items.");
			Assert.IsTrue(results.Contains(0), "found the front left point.");
			Assert.IsTrue(results.Contains(1), "found the front right point.");

			results.Clear();
			tree.AlongRay(new Ray(new Vector3(-.5, 1.5, 1.5), -Vector3.UnitX), results);
			Assert.AreEqual(results.Count(), 0, "Found no items.");

			results.Clear();
			tree.AlongRay(new Ray(new Vector3(-.5, 1.5, 1.5), Vector3.UnitX), results);
			Assert.AreEqual(results.Count(), 1, "Found only center.");
			Assert.IsTrue(results.Contains(4), "found the center point.");

			results.Clear();
			tree.AlongRay(new Ray(new Vector3(-.5, -.5, -.5), new Vector3(1, 1, 1).GetNormal()), results);
			Assert.AreEqual(results.Count(), 3, "Found all items.");
			Assert.IsTrue(results.Contains(0), "found the left front bottom.");
			Assert.IsTrue(results.Contains(4), "found the center.");
			Assert.IsTrue(results.Contains(8), "found the right back top.");

			results.Clear();
			var shortRay = new Ray(new Vector3(-.5, -.5, -.5), new Vector3(1, 1, 1).GetNormal())
			{
				maxDistanceToConsider = 1
			};
			tree.AlongRay(shortRay, results);
			Assert.AreEqual(results.Count(), 1, "Found all items.");
			Assert.IsTrue(results.Contains(0), "found the left front bottom.");
		}

		[Test]
		public void TestBvhClosestIntersection()
		{
			// for this shape imagine the 8 corners of a Rubik's cube and the center
			var data = new List<BvhTreeItemData<int>>();
			// left, front, bottom
			data.Add(new BvhTreeItemData<int>(0, new AxisAlignedBoundingBox(0, 0, 0, 1, 1, 1)));
			// right, front, bottom
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(2, 0, 0, 3, 1, 1)));
			// left, back, bottom
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(0, 2, 0, 1, 3, 1)));
			// right, back, bottom
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(2, 2, 0, 3, 3, 1)));
			// center, center, center
			data.Add(new BvhTreeItemData<int>(4, new AxisAlignedBoundingBox(1, 1, 1, 2, 2, 2)));
			// left, front, top
			data.Add(new BvhTreeItemData<int>(5, new AxisAlignedBoundingBox(0, 0, 2, 1, 1, 3)));
			// right, front, top
			data.Add(new BvhTreeItemData<int>(6, new AxisAlignedBoundingBox(2, 0, 2, 3, 1, 3)));
			// left, back, top
			data.Add(new BvhTreeItemData<int>(7, new AxisAlignedBoundingBox(0, 2, 2, 1, 3, 3)));
			// right, back, top
			data.Add(new BvhTreeItemData<int>(8, new AxisAlignedBoundingBox(2, 2, 2, 3, 3, 3)));
			var bvhBuilder = new TradeOffBvhConstructor<int>();
			var tree = bvhBuilder.CreateNewHierachy(data);

			Assert.AreEqual(7, tree.CountBranches(), "these must be split up into multiple bounds");

			var hitInfo = tree.GetClosestIntersection(new Ray(new Vector3(-.5, .5, .5), Vector3.UnitX));
			Assert.IsNotNull(hitInfo, "found a hit");
			Assert.AreEqual(0, hitInfo.ClosestHitObject, "found the left front bottom");
			Assert.AreEqual(.5, hitInfo.DistanceToHit);
			Assert.AreEqual(new Vector3(0, .5, .5), hitInfo.HitPosition);
			Assert.AreEqual(IntersectionType.FrontFace, hitInfo.HitType);
			Assert.AreEqual(new Vector3(-1, 0, 0), hitInfo.NormalAtHit);

			hitInfo = tree.GetClosestIntersection(new Ray(new Vector3(3.5, .5, .5), -Vector3.UnitX));
			Assert.IsNotNull(hitInfo, "found a hit");
			Assert.AreEqual(1, hitInfo.ClosestHitObject, "found the right front bottom");
			Assert.AreEqual(.5, hitInfo.DistanceToHit);
			Assert.AreEqual(new Vector3(3, .5, .5), hitInfo.HitPosition);
			Assert.AreEqual(IntersectionType.FrontFace, hitInfo.HitType);
			Assert.AreEqual(new Vector3(1, 0, 0), hitInfo.NormalAtHit);

			hitInfo = tree.GetClosestIntersection(new Ray(new Vector3(-.5, 1.5, 1.5), -Vector3.UnitX));
			Assert.IsNull(hitInfo, "Found no items.");

			hitInfo = tree.GetClosestIntersection(new Ray(new Vector3(-.5, 1.5, 1.5), Vector3.UnitX));
			Assert.AreEqual(4, hitInfo.ClosestHitObject, "Found only center.");
			Assert.AreEqual(new Vector3(1, 1.5, 1.5), hitInfo.HitPosition);
			Assert.AreEqual(IntersectionType.FrontFace, hitInfo.HitType);
			Assert.AreEqual(new Vector3(-1, 0, 0), hitInfo.NormalAtHit);
		}

		[Test]
		public void TestBvhTreePointSearches()
		{
			// for this shape imagine the 8 corners of a Rubik's cube and the center
			var data = new List<BvhTreeItemData<int>>();
			// left, front, bottom
			data.Add(new BvhTreeItemData<int>(0, new AxisAlignedBoundingBox(0, 0, 0, 0, 0, 0)));
			// right, front, bottom
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(2, 0, 0, 2, 0, 0)));
			// left, back, bottom
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(0, 2, 0, 0, 2, 0)));
			// right, back, bottom
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(2, 2, 0, 2, 2, 0)));
			// center, center, center
			data.Add(new BvhTreeItemData<int>(4, new AxisAlignedBoundingBox(1, 1, 1, 1, 1, 1)));
			// left, front, top
			data.Add(new BvhTreeItemData<int>(0, new AxisAlignedBoundingBox(0, 0, 2, 0, 0, 2)));
			// right, front, top
			data.Add(new BvhTreeItemData<int>(1, new AxisAlignedBoundingBox(2, 0, 2, 2, 0, 2)));
			// left, back, top
			data.Add(new BvhTreeItemData<int>(2, new AxisAlignedBoundingBox(0, 2, 2, 0, 2, 2)));
			// right, back, top
			data.Add(new BvhTreeItemData<int>(3, new AxisAlignedBoundingBox(2, 2, 2, 2, 2, 2)));
			var bvhBuilder = new TradeOffBvhConstructor<int>();
			var tree = bvhBuilder.CreateNewHierachy(data);

			var results = new List<int>();
			tree.SearchBounds(new AxisAlignedBoundingBox(0, 0, 0, 3, 3, 3), results);
			Assert.AreEqual(results.Count(), 9, "Found all items.");

			results.Clear();
			tree.SearchPoint(1, 1, 1, results);
			Assert.AreEqual(1, results.Count(), "All or around this point.");
			Assert.AreEqual(4, results[0], "found the center point.");
		}
	}
}