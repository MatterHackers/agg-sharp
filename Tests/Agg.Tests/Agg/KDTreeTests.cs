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

using NUnit.Framework;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg
{
	[TestFixture, Category("Agg")]
	public class KDTreeTests
	{
		[Test]
		public void SamePointTest2D()
		{
			Vector2DLeafItem item1 = new Vector2DLeafItem(5, 5);
			Vector2DLeafItem item2 = new Vector2DLeafItem(5, 5);
			Vector2DLeafItem item3 = new Vector2DLeafItem(5, 5);
			IEnumerable<Vector2DLeafItem> enumerable = new Vector2DLeafItem[] { item1, item2, item3 }.AsEnumerable<Vector2DLeafItem>();
			KDTreeNode<Vector2DLeafItem> rootNode = KDTreeNode<Vector2DLeafItem>.CreateTree(enumerable);

			KDTreeNode<Vector2DLeafItem> testNode = rootNode;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 0);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);
			Assert.IsTrue(testNode.LeafItem == item1);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 1);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);
			Assert.IsTrue(testNode.LeafItem == item2);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 0);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit == null);
			Assert.IsTrue(testNode.LeafItem == item3);
		}

		private static void RunTestOnNode3D(Vector3DLeafItem item1, Vector3DLeafItem item2, Vector3DLeafItem item3, KDTreeNode<Vector3DLeafItem> rootNode)
		{
			KDTreeNode<Vector3DLeafItem> testNode = rootNode;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 0);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);
			Assert.IsTrue(testNode.LeafItem == item1);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 1);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);
			Assert.IsTrue(testNode.LeafItem == item2);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 2);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit == null);
			Assert.IsTrue(testNode.LeafItem == item3);
		}

		[Test]
		public void SamePointTest3D()
		{
			Vector3DLeafItem item1 = new Vector3DLeafItem(5, 5, 5);
			Vector3DLeafItem item2 = new Vector3DLeafItem(5, 5, 5);
			Vector3DLeafItem item3 = new Vector3DLeafItem(5, 5, 5);
			IEnumerable<Vector3DLeafItem> enumerable = new Vector3DLeafItem[] { item1, item2, item3 }.AsEnumerable<Vector3DLeafItem>();
			KDTreeNode<Vector3DLeafItem> rootNode = KDTreeNode<Vector3DLeafItem>.CreateTree(enumerable);

			RunTestOnNode3D(item1, item2, item3, rootNode);
		}

		[Test]
		public void CreateFromKDTree()
		{
			Vector3DLeafItem item1 = new Vector3DLeafItem(5, 5, 5);
			Vector3DLeafItem item2 = new Vector3DLeafItem(5, 5, 5);
			Vector3DLeafItem item3 = new Vector3DLeafItem(5, 5, 5);
			IEnumerable<Vector3DLeafItem> enumerable = new Vector3DLeafItem[] { item1, item2, item3 }.AsEnumerable<Vector3DLeafItem>();
			KDTreeNode<Vector3DLeafItem> rootNode = KDTreeNode<Vector3DLeafItem>.CreateTree(enumerable);
			RunTestOnNode3D(item1, item2, item3, rootNode);

			KDTreeNode<Vector3DLeafItem> fromRootNode = KDTreeNode<Vector3DLeafItem>.CreateTree(rootNode.UnorderedEnumerator());

			KDTreeNode<Vector3DLeafItem> testNode = fromRootNode;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 0);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 1);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit != null);

			testNode = testNode.NodeGreaterThanOrEqualToSplit;
			Assert.IsTrue(testNode.DimensionSplitIsOn == 2);
			Assert.IsTrue(testNode.NodeLessThanSplit == null);
			Assert.IsTrue(testNode.NodeGreaterThanOrEqualToSplit == null);
		}

		[Test]
		public void EnumerateFromPoint()
		{
			Vector3DLeafItem item1 = new Vector3DLeafItem(1, 0, 0);
			Vector3DLeafItem item2 = new Vector3DLeafItem(2, 0, 0);
			Vector3DLeafItem item3 = new Vector3DLeafItem(3, 0, 0);
			IEnumerable<Vector3DLeafItem> enumerable = new Vector3DLeafItem[] { item1, item2, item3 }.AsEnumerable<Vector3DLeafItem>();
			KDTreeNode<Vector3DLeafItem> rootNode = KDTreeNode<Vector3DLeafItem>.CreateTree(enumerable);

			int index = 0;
			foreach (Vector3DLeafItem item in rootNode.GetDistanceEnumerator(new double[] { 2.1, 0, 0 }))
			{
				switch (index++)
				{
					case 0:
						Assert.IsTrue(item == item2);
						break;

					case 1:
						Assert.IsTrue(item == item3);
						break;

					case 2:
						Assert.IsTrue(item == item1);
						break;
				}
			}
		}
	}
}