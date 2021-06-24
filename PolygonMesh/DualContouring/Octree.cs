/*

Implementations of Octree member functions.

Copyright (C) 2011  Tao Ju

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public License
(LGPL) as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DualContouring
{
	public class Octree
	{
		public static readonly int[][] cellProcEdgeMask = new int[6][]
		{
			new int[5]{0,1,2,3,0},
			new int[5]{4,5,6,7,0},
			new int[5]{0,4,1,5,1},
			new int[5]{2,6,3,7,1},
			new int[5]{0,2,4,6,2},
			new int[5]{1,3,5,7,2}
		};

		public static readonly int[][] cellProcFaceMask = new int[12][]
		{
			new int[3]{0,4,0},
			new int[3]{1,5,0},
			new int[3]{2,6,0},
			new int[3]{3,7,0},
			new int[3]{0,2,1},
			new int[3]{4,6,1},
			new int[3]{1,3,1},
			new int[3]{5,7,1},
			new int[3]{0,1,2},
			new int[3]{2,3,2},
			new int[3]{4,5,2},
			new int[3]{6,7,2}
		};

		public static readonly Vector3[] CHILD_MIN_OFFSETS =
		{
	        // needs to match the vertMap from Dual Contouring impl
	        new Vector3( 0, 0, 0 ),
			new Vector3( 0, 0, 1 ),
			new Vector3( 0, 1, 0 ),
			new Vector3( 0, 1, 1 ),
			new Vector3( 1, 0, 0 ),
			new Vector3( 1, 0, 1 ),
			new Vector3( 1, 1, 0 ),
			new Vector3( 1, 1, 1 ),
		};

		public static readonly Color[] DrawColors = new Color[]
		{
			Color.White,
			Color.Yellow,
			Color.Gray,
			Color.Green,
			Color.Blue,
			Color.Black,
			Color.Red,
			Color.Cyan,
		};

		public static readonly int[] edgemask = { 5, 3, 6 };

		public static readonly int[][][] edgeProcEdgeMask = new int[3][][]
		{
			new int[2][]{new int[5]{3,2,1,0,0},new int[5]{7,6,5,4,0}},
			new int[2][]{new int[5]{5,1,4,0,1},new int[5]{7,3,6,2,1}},
			new int[2][]{new int[5]{6,4,2,0,2},new int[5]{7,5,3,1,2}},
		};

		public static readonly int[][] edgevmap = new int[12][]
		{
			new int[2]{2,4},new int[2]{1,5},new int[2]{2,6},new int[2]{3,7},	// x-axis
			new int[2]{0,2},new int[2]{1,3},new int[2]{4,6},new int[2]{5,7},	// y-axis
			new int[2]{0,1},new int[2]{2,3},new int[2]{4,5},new int[2]{6,7}     // z-axis
		};

		public static readonly int[][] faceMap = new int[6][]
		{
			new int[4]{4, 8, 5, 9},
			new int[4]{6, 10, 7, 11},
			new int[4]{0, 8, 1, 10},
			new int[4]{2, 9, 3, 11},
			new int[4]{0, 4, 2, 6},
			new int[4]{1, 5, 3, 7}
		};

		public static readonly int[][][] faceProcEdgeMask = new int[3][][]
		{
			new int[4][]{new int[6]{1,4,0,5,1,1},new int[6]{1,6,2,7,3,1},new int[6]{0,4,6,0,2,2},new int[6]{0,5,7,1,3,2}},
			new int[4][]{new int[6]{0,2,3,0,1,0},new int[6]{0,6,7,4,5,0},new int[6]{1,2,0,6,4,2},new int[6]{1,3,1,7,5,2}},
			new int[4][]{new int[6]{1,1,0,3,2,0},new int[6]{1,5,4,7,6,0},new int[6]{0,1,5,0,4,1},new int[6]{0,3,7,2,6,1}}
		};

		public static readonly int[][][] faceProcFaceMask = new int[3][][]
		{
			new int[4][]{ new int[3]{4,0,0}, new int[3]{5,1,0}, new int[3]{6,2,0}, new int[3]{7,3,0} },
			new int[4][]{ new int[3]{2,0,1}, new int[3]{6,4,1}, new int[3]{3,1,1}, new int[3]{7,5,1} },
			new int[4][]{ new int[3]{1,0,2}, new int[3]{3,2,2}, new int[3]{5,4,2}, new int[3]{7,6,2} }
		};

		public static readonly int[][] processEdgeMask = new int[3][]
		{
			new int[4]{3,2,1,0},new int[4]{7,5,6,4},new int[4]{11,10,9,8}
		};

		// data from the original DC impl, drives the contouring process
		public static readonly int[][] vertMap = new int[8][]
		{
			new int[3]{0,0,0},
			new int[3]{0,0,1},
			new int[3]{0,1,0},
			new int[3]{0,1,1},
			new int[3]{1,0,0},
			new int[3]{1,0,1},
			new int[3]{1,1,0},
			new int[3]{1,1,1}
		};

		public static int MATERIAL_AIR = 0;

		public static int MATERIAL_SOLID = 1;

		public static double QEF_ERROR = 1e-6f;

		public static int QEF_SWEEPS = 4;

		public static Vector3 ApproximateZeroCrossingPosition(Vector3 p0, Vector3 p1)
		{
			// approximate the zero crossing by finding the min value along the edge
			double minValue = 100000f;
			double t = 0f;
			double currentT = 0f;
			const int steps = 8;
			const double increment = 1f / (double)steps;
			while (currentT <= 1.0f)
			{
				Vector3 p = p0 + ((p1 - p0) * currentT);
				double density = Math.Abs(glm.Density_Func(p));
				if (density < minValue)
				{
					minValue = density;
					t = currentT;
				}

				currentT += increment;
			}

			return p0 + ((p1 - p0) * t);
		}

		public static OctreeNode BuildOctree(Vector3 min, int size, double threshold)
		{
			Debug.WriteLine(string.Format("Building Octree at {0}, with size of {1} and threshold of {2}", min, size, threshold));

			var root = new OctreeNode();
			root.min = min;
			root.size = size;
			root.Type = OctreeNodeType.Node_Internal;

			root = ConstructOctreeNodes(root);
			root = SimplifyOctree(root, threshold);

			return root;
		}

		public static Vector3 CalculateSurfaceNormal(Vector3 p)
		{
			double H = 0.001f;
			double dx = glm.Density_Func(p + new Vector3(H, 0.0f, 0.0f)) - glm.Density_Func(p - new Vector3(H, 0.0f, 0.0f));
			double dy = glm.Density_Func(p + new Vector3(0.0f, H, 0.0f)) - glm.Density_Func(p - new Vector3(0.0f, H, 0.0f));
			double dz = glm.Density_Func(p + new Vector3(0.0f, 0.0f, H)) - glm.Density_Func(p - new Vector3(0.0f, 0.0f, H));

			return new Vector3(dx, dy, dz).GetNormal();
		}

		public static OctreeNode ConstructLeaf(OctreeNode leaf)
		{
			if (leaf == null || leaf.size != 1)
			{
				return null;
			}

			int corners = 0;
			for (int i = 0; i < 8; i++)
			{
				Vector3 cornerPos = leaf.min + CHILD_MIN_OFFSETS[i];
				double density = glm.Density_Func(cornerPos);
				int material = density < 0.0f ? MATERIAL_SOLID : MATERIAL_AIR;
				corners |= (material << i);
			}

			if (corners == 0 || corners == 255)
			{
				// voxel is full inside or outside the volume
				//delete leaf
				//setting as null isn't required by the GC in C#... but its in the original, so why not!
				leaf = null;
				return null;
			}

			// otherwise the voxel contains the surface, so find the edge intersections
			const int MAX_CROSSINGS = 6;
			int edgeCount = 0;
			Vector3 averageNormal = Vector3.Zero;
			var qef = new QefSolver();

			for (int i = 0; i < 12 && edgeCount < MAX_CROSSINGS; i++)
			{
				int c1 = edgevmap[i][0];
				int c2 = edgevmap[i][1];

				int m1 = (corners >> c1) & 1;
				int m2 = (corners >> c2) & 1;

				if ((m1 == MATERIAL_AIR && m2 == MATERIAL_AIR) || (m1 == MATERIAL_SOLID && m2 == MATERIAL_SOLID))
				{
					// no zero crossing on this edge
					continue;
				}

				Vector3 p1 = leaf.min + CHILD_MIN_OFFSETS[c1];
				Vector3 p2 = leaf.min + CHILD_MIN_OFFSETS[c2];
				Vector3 p = ApproximateZeroCrossingPosition(p1, p2);
				Vector3 n = CalculateSurfaceNormal(p);
				qef.add(p.X, p.Y, p.Z, n.X, n.Y, n.Z);

				averageNormal += n;

				edgeCount++;
			}

			Vector3 qefPosition = Vector3.Zero;
			qef.solve(qefPosition, QEF_ERROR, QEF_SWEEPS, QEF_ERROR);

			var drawInfo = new OctreeDrawInfo();
			drawInfo.corners = 0;
			drawInfo.index = -1;
			drawInfo.position = new Vector3(qefPosition.X, qefPosition.Y, qefPosition.Z);
			drawInfo.qef = qef.getData();

			Vector3 min = leaf.min;
			var max = new Vector3(leaf.min.X + leaf.size, leaf.min.Y + leaf.size, leaf.min.Z + leaf.size);
			if (drawInfo.position.X < min.X || drawInfo.position.X > max.X ||
				drawInfo.position.Y < min.Y || drawInfo.position.Y > max.Y ||
				drawInfo.position.Z < min.Z || drawInfo.position.Z > max.Z)
			{
				drawInfo.position = qef.GetMassPoint();
			}

			drawInfo.averageNormal = Vector3.Normalize(averageNormal / (double)edgeCount);
			drawInfo.corners = corners;

			leaf.Type = OctreeNodeType.Node_Leaf;
			leaf.drawInfo = drawInfo;

			return leaf;
		}

		public static OctreeNode ConstructOctreeNodes(OctreeNode node)
		{
			if (node == null)
			{
				return null;
			}

			if (node.size == 1)
			{
				return ConstructLeaf(node);
			}

			int childSize = node.size / 2;
			bool hasChildren = false;

			for (int i = 0; i < 8; i++)
			{
				var child = new OctreeNode();
				child.size = childSize;
				child.min = node.min + (CHILD_MIN_OFFSETS[i] * childSize);
				child.Type = OctreeNodeType.Node_Internal;

				node.children[i] = ConstructOctreeNodes(child);
				hasChildren |= (node.children[i] != null);
			}

			if (!hasChildren)
			{
				//delete leaf
				//setting as null isn't required by the GC in C#... but its in the original, so why not!
				return null;
			}

			return node;
		}

		public static void ContourCellProc(OctreeNode node, List<int> indexBuffer)
		{
			if (node == null)
			{
				return;
			}

			if (node.Type == OctreeNodeType.Node_Internal)
			{
				for (int i = 0; i < 8; i++)
				{
					ContourCellProc(node.children[i], indexBuffer);
				}

				for (int i = 0; i < 12; i++)
				{
					var faceNodes = new OctreeNode[2];
					int[] c = { cellProcFaceMask[i][0], cellProcFaceMask[i][1] };

					faceNodes[0] = node.children[c[0]];
					faceNodes[1] = node.children[c[1]];

					ContourFaceProc(faceNodes, cellProcFaceMask[i][2], indexBuffer);
				}

				for (int i = 0; i < 6; i++)
				{
					var edgeNodes = new OctreeNode[4];
					int[] c = new int[4]
					{
						cellProcEdgeMask[i][0],
						cellProcEdgeMask[i][1],
						cellProcEdgeMask[i][2],
						cellProcEdgeMask[i][3],
					};

					for (int j = 0; j < 4; j++)
					{
						edgeNodes[j] = node.children[c[j]];
					}

					ContourEdgeProc(edgeNodes, cellProcEdgeMask[i][4], indexBuffer);
				}
			}
		}

		public static void ContourEdgeProc(OctreeNode[] node, int dir, List<int> indexBuffer)
		{
			if (node[0] == null || node[1] == null || node[2] == null || node[3] == null)
			{
				return;
			}

			if (node[0].Type != OctreeNodeType.Node_Internal &&
				node[1].Type != OctreeNodeType.Node_Internal &&
				node[2].Type != OctreeNodeType.Node_Internal &&
				node[3].Type != OctreeNodeType.Node_Internal)
			{
				ContourProcessEdge(node, dir, indexBuffer);
			}
			else
			{
				for (int i = 0; i < 2; i++)
				{
					var edgeNodes = new OctreeNode[4];
					int[] c = new int[4]
				{
				edgeProcEdgeMask[dir][i][0],
				edgeProcEdgeMask[dir][i][1],
				edgeProcEdgeMask[dir][i][2],
				edgeProcEdgeMask[dir][i][3],
				};

					for (int j = 0; j < 4; j++)
					{
						if (node[j].Type == OctreeNodeType.Node_Leaf || node[j].Type == OctreeNodeType.Node_Psuedo)
						{
							edgeNodes[j] = node[j];
						}
						else
						{
							edgeNodes[j] = node[j].children[c[j]];
						}
					}

					ContourEdgeProc(edgeNodes, edgeProcEdgeMask[dir][i][4], indexBuffer);
				}
			}
		}

		public static void ContourFaceProc(OctreeNode[] node, int dir, List<int> indexBuffer)
		{
			if (node[0] == null || node[1] == null)
			{
				return;
			}

			if (node[0].Type == OctreeNodeType.Node_Internal ||
				node[1].Type == OctreeNodeType.Node_Internal)
			{
				for (int i = 0; i < 4; i++)
				{
					var faceNodes = new OctreeNode[2];
					int[] c = new int[2]
				{
				faceProcFaceMask[dir][i][0],
				faceProcFaceMask[dir][i][1],
				};

					for (int j = 0; j < 2; j++)
					{
						if (node[j].Type != OctreeNodeType.Node_Internal)
						{
							faceNodes[j] = node[j];
						}
						else
						{
							faceNodes[j] = node[j].children[c[j]];
						}
					}

					ContourFaceProc(faceNodes, faceProcFaceMask[dir][i][2], indexBuffer);
				}

				int[][] orders = new int[2][]
			{
			new int[4]{ 0, 0, 1, 1 },
			new int[4]{ 0, 1, 0, 1 },
			};

				for (int i = 0; i < 4; i++)
				{
					var edgeNodes = new OctreeNode[4];
					int[] c = new int[4]
				{
				faceProcEdgeMask[dir][i][1],
				faceProcEdgeMask[dir][i][2],
				faceProcEdgeMask[dir][i][3],
				faceProcEdgeMask[dir][i][4],
				};

					int[] order = orders[faceProcEdgeMask[dir][i][0]];
					for (int j = 0; j < 4; j++)
					{
						if (node[order[j]].Type == OctreeNodeType.Node_Leaf ||
							node[order[j]].Type == OctreeNodeType.Node_Psuedo)
						{
							edgeNodes[j] = node[order[j]];
						}
						else
						{
							edgeNodes[j] = node[order[j]].children[c[j]];
						}
					}

					ContourEdgeProc(edgeNodes, faceProcEdgeMask[dir][i][5], indexBuffer);
				}
			}
		}

		public static void ContourProcessEdge(OctreeNode[] node, int dir, List<int> indexBuffer)
		{
			int minSize = 1000000;      // arbitrary big number
			int minIndex = 0;
			int[] indices = new int[4] { -1, -1, -1, -1 };
			bool flip = false;
			bool[] signChange = new bool[4] { false, false, false, false };

			for (int i = 0; i < 4; i++)
			{
				int edge = processEdgeMask[dir][i];
				int c1 = edgevmap[edge][0];
				int c2 = edgevmap[edge][1];

				int m1 = (node[i].drawInfo.corners >> c1) & 1;
				int m2 = (node[i].drawInfo.corners >> c2) & 1;

				if (node[i].size < minSize)
				{
					minSize = node[i].size;
					minIndex = i;
					flip = m1 != MATERIAL_AIR;
				}

				indices[i] = node[i].drawInfo.index;

				signChange[i] =
					(m1 == MATERIAL_AIR && m2 != MATERIAL_AIR) ||
					(m1 != MATERIAL_AIR && m2 == MATERIAL_AIR);
			}

			if (signChange[minIndex])
			{
				if (!flip)
				{
					indexBuffer.Add(indices[0]);
					indexBuffer.Add(indices[1]);
					indexBuffer.Add(indices[3]);

					indexBuffer.Add(indices[0]);
					indexBuffer.Add(indices[3]);
					indexBuffer.Add(indices[2]);
				}
				else
				{
					indexBuffer.Add(indices[0]);
					indexBuffer.Add(indices[3]);
					indexBuffer.Add(indices[1]);

					indexBuffer.Add(indices[0]);
					indexBuffer.Add(indices[2]);
					indexBuffer.Add(indices[3]);
				}
			}
		}

		public static void DestroyOctree(OctreeNode node)
		{
			if (node == null)
			{
				return;
			}

			for (int i = 0; i < 8; i++)
			{
				DestroyOctree(node.children[i]);
			}
		}

		public static Mesh GenerateMeshFromOctree(OctreeNode node)
		{
			if (node == null)
			{
				return null;
			}

			var vertexBuffer = new List<MeshVertex>();
			var indexBuffer = new List<int>();

			GenerateVertexIndices(node, vertexBuffer);
			ContourCellProc(node, indexBuffer);

			var mesh = new Mesh();

			var vertArray = new Vector3[vertexBuffer.Count];
			//Vector2[] uvs = new Vector2[vertexBuffer.Count];
			for (int i = 0; i < vertexBuffer.Count; i++)
			{
				vertArray[i] = vertexBuffer[i].xyz;
				//uvs[i] = new Vector2(vertexBuffer[i].xyz.X, vertexBuffer[i].xyz.Z);
			}

			//Vector3[] normsArray = new Vector3[vertexBuffer.Count];
			//for (int i = 0; i < vertexBuffer.Count; i++)
			//{
			//    normsArray[i] = vertexBuffer[i].normal;
			//}

			mesh.Vertices = new List<Vector3Float>(vertArray.Length);
			for (int i = 0; i < vertArray.Length; i++)
			{
				mesh.Vertices.Add(new Vector3Float(vertArray[i]));
			}
			// mesh.uv = uvs;
			mesh.Faces = new FaceList();
			for (int i = 0; i < indexBuffer.Count; i += 3)
			{
				mesh.Faces.Add(new Face(indexBuffer[i], indexBuffer[i + 1], indexBuffer[i + 2], mesh.Vertices));
			}
			// mesh.normals = normsArray;

			for (int i = 0; i < 8; i++)
			{
				Debug.WriteLine("vert: " + vertArray[i]);
			}

			for (int i = 0; i < 8; i++)
			{
				Debug.WriteLine("index: " + indexBuffer[i]);
			}

			return mesh;
		}

		public static void GenerateVertexIndices(OctreeNode node, List<MeshVertex> vertexBuffer)
		{
			if (node == null)
			{
				return;
			}

			if (node.Type != OctreeNodeType.Node_Leaf)
			{
				for (int i = 0; i < 8; i++)
				{
					GenerateVertexIndices(node.children[i], vertexBuffer);
				}
			}

			if (node.Type != OctreeNodeType.Node_Internal)
			{
				node.drawInfo.index = vertexBuffer.Count;

				vertexBuffer.Add(new MeshVertex(node.drawInfo.position, node.drawInfo.averageNormal));
			}
		}

		public static OctreeNode SimplifyOctree(OctreeNode node, double threshold)
		{
			if (node == null)
			{
				return null;
			}

			if (node.Type != OctreeNodeType.Node_Internal)
			{
				// can't simplify!
				return node;
			}

			var qef = new QefSolver();
			int[] signs = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
			int midsign = -1;
			int edgeCount = 0;
			bool isCollapsible = true;

			for (int i = 0; i < 8; i++)
			{
				node.children[i] = SimplifyOctree(node.children[i], threshold);

				if (node.children[i] != null)
				{
					OctreeNode child = node.children[i];

					if (child.Type == OctreeNodeType.Node_Internal)
					{
						isCollapsible = false;
					}
					else
					{
						qef.add(child.drawInfo.qef);

						midsign = (child.drawInfo.corners >> (7 - i)) & 1;
						signs[i] = (child.drawInfo.corners >> i) & 1;

						edgeCount++;
					}
				}
			}

			if (!isCollapsible)
			{
				// at least one child is an internal node, can't collapse
				return node;
			}

			Vector3 qefPosition = Vector3.Zero;
			qef.solve(qefPosition, QEF_ERROR, QEF_SWEEPS, QEF_ERROR);
			double error = qef.getError();

			// convert to glm vec3 for ease of use
			var position = new Vector3(qefPosition.X, qefPosition.Y, qefPosition.Z);

			// at this point the masspoint will actually be a sum, so divide to make it the average
			if (error > threshold)
			{
				// this collapse breaches the threshold
				return node;
			}

			if (position.X < node.min.X || position.X > (node.min.X + node.size) ||
				position.Y < node.min.Y || position.Y > (node.min.Y + node.size) ||
				position.Z < node.min.Z || position.Z > (node.min.Z + node.size))
			{
				position = qef.GetMassPoint();
			}

			// change the node from an internal node to a 'psuedo leaf' node
			var drawInfo = new OctreeDrawInfo();
			drawInfo.corners = 0;
			drawInfo.index = -1;

			for (int i = 0; i < 8; i++)
			{
				if (signs[i] == -1)
				{
					// Undetermined, use center sign instead
					drawInfo.corners |= (midsign << i);
				}
				else
				{
					drawInfo.corners |= (signs[i] << i);
				}
			}

			drawInfo.averageNormal = Vector3.Zero;
			for (int i = 0; i < 8; i++)
			{
				if (node.children[i] != null)
				{
					OctreeNode child = node.children[i];
					if (child.Type == OctreeNodeType.Node_Psuedo ||
						child.Type == OctreeNodeType.Node_Leaf)
					{
						drawInfo.averageNormal += child.drawInfo.averageNormal;
					}
				}
			}

			drawInfo.averageNormal = drawInfo.averageNormal.GetNormal();
			drawInfo.position = position;
			drawInfo.qef = qef.getData();

			for (int i = 0; i < 8; i++)
			{
				DestroyOctree(node.children[i]);
				node.children[i] = null;
			}

			node.Type = OctreeNodeType.Node_Psuedo;
			node.drawInfo = drawInfo;

			return node;
		}
	}
}