// This file is part of RoundCornerBox, a single c++ header for the creation of
// round-corner boxes.
//
// Copyright (C) 2016 Raymond Fei <fyun@acm.org>
// Ported to C# 2021 Lars Brubaker
//
// This Source Code Form is subject to the terms of the Mozilla Public License
// v. 2.0. If a copy of the MPL was not distributed with this file, You can
// obtain one at http://mozilla.org/MPL/2.0/.

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Solids
{
	public class RoundedCornerBox
	{
		private List<int> indices = new List<int>();

		private Dictionary<int, int> indexToVerts;

		private int edgeCount;

		private double radius;

		private List<Vector3> normals = new List<Vector3>();

		private List<Vector3> vertices = new List<Vector3>();

		public static Mesh Create(int segments, Vector3 sizeXyz, double radius)
		{
			var roundBox = new RoundedCornerBox(segments, sizeXyz, radius);

			return new Mesh(roundBox.vertices, roundBox.indices);
		}

		public RoundedCornerBox(int segments, Vector3 sizeXyz, double radius)
		{
			edgeCount = 2 * (segments + 1);
			indexToVerts = new Dictionary<int, int>(edgeCount * edgeCount * edgeCount * 3);
			this.radius = radius;
			double dx = 0;
			if (segments > 0)
			{
				dx = radius / segments;
			}

			double[] sign = new double[] { -1.0, 1.0 };
			int[] ks = new int[] { 0, segments * 2 + 1 };

			var minSize = Math.Min(sizeXyz.X, Math.Min(sizeXyz.Y, sizeXyz.Z));
			if (radius > minSize / 2)
			{
				radius = minSize / 2;
			}

			var faceXyz = (sizeXyz / 2) - new Vector3(radius, radius, radius);

			// xy-planes
			for (int kidX = 0; kidX < 2; ++kidX)
			{
				int k = ks[kidX];
				var origin = new Vector3(-faceXyz[0] - radius, -faceXyz[1] - radius, (faceXyz[2] + radius) * sign[kidX]);
				for (int j = 0; j <= segments; ++j)
				{
					for (int i = 0; i <= segments; ++i)
					{
						var pos = origin + new Vector3(dx * i, dx * j, 0.0);
						AddVertex(i, j, k, pos, new Vector3(-faceXyz[0], -faceXyz[1], faceXyz[2] * sign[kidX]));

						pos = origin + new Vector3(dx * i + 2.0 * faceXyz[0] + radius, dx * j, 0.0);
						AddVertex(i + segments + 1, j, k, pos, new Vector3(faceXyz[0], -faceXyz[1], faceXyz[2] * sign[kidX]));

						pos = origin + new Vector3(dx * i + 2.0 * faceXyz[0] + radius, dx * j + 2.0 * faceXyz[1] + radius, 0.0);
						AddVertex(i + segments + 1, j + segments + 1, k, pos, new Vector3(faceXyz[0], faceXyz[1], faceXyz[2] * sign[kidX]));

						pos = origin + new Vector3(dx * i, dx * j + 2.0 * faceXyz[1] + radius, 0.0);
						AddVertex(i, j + segments + 1, k, pos, new Vector3(-faceXyz[0], faceXyz[1], faceXyz[2] * sign[kidX]));
					}
				}

				// corners
				for (int j = 0; j < segments; ++j)
				{
					for (int i = 0; i < segments; ++i)
					{
						AddFace(TranslateIndices(i, j, k),
								TranslateIndices(i + 1, j + 1, k),
								TranslateIndices(i, j + 1, k), kidX == 0);
						AddFace(TranslateIndices(i, j, k),
								TranslateIndices(i + 1, j, k),
								TranslateIndices(i + 1, j + 1, k), kidX == 0);

						AddFace(TranslateIndices(i, j + segments + 1, k),
								TranslateIndices(i + 1, j + segments + 2, k),
								TranslateIndices(i, j + segments + 2, k), kidX == 0);
						AddFace(TranslateIndices(i, j + segments + 1, k),
								TranslateIndices(i + 1, j + segments + 1, k),
								TranslateIndices(i + 1, j + segments + 2, k), kidX == 0);

						AddFace(TranslateIndices(i + segments + 1, j + segments + 1, k),
								TranslateIndices(i + segments + 2, j + segments + 2, k),
								TranslateIndices(i + segments + 1, j + segments + 2, k), kidX == 0);
						AddFace(TranslateIndices(i + segments + 1, j + segments + 1, k),
								TranslateIndices(i + segments + 2, j + segments + 1, k),
								TranslateIndices(i + segments + 2, j + segments + 2, k), kidX == 0);

						AddFace(TranslateIndices(i + segments + 1, j, k),
								TranslateIndices(i + segments + 2, j + 1, k),
								TranslateIndices(i + segments + 1, j + 1, k), kidX == 0);
						AddFace(TranslateIndices(i + segments + 1, j, k),
								TranslateIndices(i + segments + 2, j, k),
								TranslateIndices(i + segments + 2, j + 1, k), kidX == 0);
					}
				}

				// sides
				for (int i = 0; i < segments; ++i)
				{
					AddFace(TranslateIndices(i, segments, k),
							TranslateIndices(i + 1, segments + 1, k),
							TranslateIndices(i, segments + 1, k), kidX == 0);
					AddFace(TranslateIndices(i, segments, k),
							TranslateIndices(i + 1, segments, k),
							TranslateIndices(i + 1, segments + 1, k), kidX == 0);

					AddFace(TranslateIndices(segments, i, k),
							TranslateIndices(segments + 1, i + 1, k),
							TranslateIndices(segments, i + 1, k), kidX == 0);
					AddFace(TranslateIndices(segments, i, k),
							TranslateIndices(segments + 1, i, k),
							TranslateIndices(segments + 1, i + 1, k), kidX == 0);

					AddFace(TranslateIndices(i + segments + 1, segments, k),
							TranslateIndices(i + segments + 2, segments + 1, k),
							TranslateIndices(i + segments + 1, segments + 1, k), kidX == 0);
					AddFace(TranslateIndices(i + segments + 1, segments, k),
							TranslateIndices(i + segments + 2, segments, k),
							TranslateIndices(i + segments + 2, segments + 1, k), kidX == 0);

					AddFace(TranslateIndices(segments, i + segments + 1, k),
							TranslateIndices(segments + 1, i + segments + 2, k),
							TranslateIndices(segments, i + segments + 2, k), kidX == 0);
					AddFace(TranslateIndices(segments, i + segments + 1, k),
							TranslateIndices(segments + 1, i + segments + 1, k),
							TranslateIndices(segments + 1, i + segments + 2, k), kidX == 0);
				}

				// central
				AddFace(TranslateIndices(segments, segments, k),
						TranslateIndices(segments + 1, segments + 1, k),
						TranslateIndices(segments, segments + 1, k), kidX == 0);
				AddFace(TranslateIndices(segments, segments, k),
						TranslateIndices(segments + 1, segments, k),
						TranslateIndices(segments + 1, segments + 1, k), kidX == 0);
			}

			// xz-planes
			for (int kidx = 0; kidx < 2; ++kidx)
			{
				int k = ks[kidx];
				var origin = new Vector3(-faceXyz[0] - radius, (faceXyz[1] + radius) * sign[kidx], -faceXyz[2] - radius);
				for (int j = 0; j <= segments; ++j)
				{
					for (int i = 0; i <= segments; ++i)
					{
						var pos = origin + new Vector3(dx * i, 0.0, dx * j);
						AddVertex(i, k, j, pos, new Vector3(-faceXyz[0], faceXyz[1] * sign[kidx], -faceXyz[2]));

						pos = origin + new Vector3(dx * i + 2.0 * faceXyz[0] + radius, 0.0, dx * j);
						AddVertex(i + segments + 1, k, j, pos, new Vector3(faceXyz[0], faceXyz[1] * sign[kidx], -faceXyz[2]));

						pos = origin + new Vector3(dx * i + 2.0 * faceXyz[0] + radius, 0.0, dx * j + 2.0 * faceXyz[2] + radius);
						AddVertex(i + segments + 1, k, j + segments + 1, pos, new Vector3(faceXyz[0], faceXyz[1] * sign[kidx], faceXyz[2]));

						pos = origin + new Vector3(dx * i, 0.0, dx * j + 2.0 * faceXyz[2] + radius);
						AddVertex(i, k, j + segments + 1, pos, new Vector3(-faceXyz[0], faceXyz[1] * sign[kidx], faceXyz[2]));
					}
				}

				// corners
				for (int j = 0; j < segments; ++j)
				{
					for (int i = 0; i < segments; ++i)
					{
						AddFace(TranslateIndices(i, k, j),
								TranslateIndices(i + 1, k, j + 1),
								TranslateIndices(i, k, j + 1), kidx == 1);
						AddFace(TranslateIndices(i, k, j),
								TranslateIndices(i + 1, k, j),
								TranslateIndices(i + 1, k, j + 1), kidx == 1);

						AddFace(TranslateIndices(i, k, j + segments + 1),
								TranslateIndices(i + 1, k, j + segments + 2),
								TranslateIndices(i, k, j + segments + 2), kidx == 1);
						AddFace(TranslateIndices(i, k, j + segments + 1),
								TranslateIndices(i + 1, k, j + segments + 1),
								TranslateIndices(i + 1, k, j + segments + 2), kidx == 1);

						AddFace(TranslateIndices(i + segments + 1, k, j + segments + 1),
								TranslateIndices(i + segments + 2, k, j + segments + 2),
								TranslateIndices(i + segments + 1, k, j + segments + 2), kidx == 1);
						AddFace(TranslateIndices(i + segments + 1, k, j + segments + 1),
								TranslateIndices(i + segments + 2, k, j + segments + 1),
								TranslateIndices(i + segments + 2, k, j + segments + 2), kidx == 1);

						AddFace(TranslateIndices(i + segments + 1, k, j),
								TranslateIndices(i + segments + 2, k, j + 1),
								TranslateIndices(i + segments + 1, k, j + 1), kidx == 1);
						AddFace(TranslateIndices(i + segments + 1, k, j),
								TranslateIndices(i + segments + 2, k, j),
								TranslateIndices(i + segments + 2, k, j + 1), kidx == 1);
					}
				}

				// sides
				for (int i = 0; i < segments; ++i)
				{
					AddFace(TranslateIndices(i, k, segments),
							TranslateIndices(i + 1, k, segments + 1),
							TranslateIndices(i, k, segments + 1), kidx == 1);
					AddFace(TranslateIndices(i, k, segments),
							TranslateIndices(i + 1, k, segments),
							TranslateIndices(i + 1, k, segments + 1), kidx == 1);

					AddFace(TranslateIndices(segments, k, i),
							TranslateIndices(segments + 1, k, i + 1),
							TranslateIndices(segments, k, i + 1), kidx == 1);
					AddFace(TranslateIndices(segments, k, i),
							TranslateIndices(segments + 1, k, i),
							TranslateIndices(segments + 1, k, i + 1), kidx == 1);

					AddFace(TranslateIndices(i + segments + 1, k, segments),
							TranslateIndices(i + segments + 2, k, segments + 1),
							TranslateIndices(i + segments + 1, k, segments + 1), kidx == 1);
					AddFace(TranslateIndices(i + segments + 1, k, segments),
							TranslateIndices(i + segments + 2, k, segments),
							TranslateIndices(i + segments + 2, k, segments + 1), kidx == 1);

					AddFace(TranslateIndices(segments, k, i + segments + 1),
							TranslateIndices(segments + 1, k, i + segments + 2),
							TranslateIndices(segments, k, i + segments + 2), kidx == 1);
					AddFace(TranslateIndices(segments, k, i + segments + 1),
							TranslateIndices(segments + 1, k, i + segments + 1),
							TranslateIndices(segments + 1, k, i + segments + 2), kidx == 1);
				}
				// central
				AddFace(TranslateIndices(segments, k, segments),
						TranslateIndices(segments + 1, k, segments + 1),
						TranslateIndices(segments, k, segments + 1), kidx == 1);
				AddFace(TranslateIndices(segments, k, segments),
						TranslateIndices(segments + 1, k, segments),
						TranslateIndices(segments + 1, k, segments + 1), kidx == 1);
			}

			// yz-planes
			for (int kidx = 0; kidx < 2; ++kidx)
			{
				int k = ks[kidx];
				var origin = new Vector3((faceXyz[0] + radius) * sign[kidx], -faceXyz[1] - radius, -faceXyz[2] - radius);
				for (int j = 0; j <= segments; ++j)
				{
					for (int i = 0; i <= segments; ++i)
					{
						var pos = origin + new Vector3(0.0, dx * i, dx * j);
						AddVertex(k, i, j, pos, new Vector3(faceXyz[0] * sign[kidx], -faceXyz[1], -faceXyz[2]));

						pos = origin + new Vector3(0.0, dx * i + 2.0 * faceXyz[1] + radius, dx * j);
						AddVertex(k, i + segments + 1, j, pos, new Vector3(faceXyz[0] * sign[kidx], faceXyz[1], -faceXyz[2]));

						pos = origin + new Vector3(0.0, dx * i + 2.0 * faceXyz[1] + radius, dx * j + 2.0 * faceXyz[2] + radius);
						AddVertex(k, i + segments + 1, j + segments + 1, pos, new Vector3(faceXyz[0] * sign[kidx], faceXyz[1], faceXyz[2]));

						pos = origin + new Vector3(0.0, dx * i, dx * j + 2.0 * faceXyz[2] + radius);
						AddVertex(k, i, j + segments + 1, pos, new Vector3(faceXyz[0] * sign[kidx], -faceXyz[1], faceXyz[2]));
					}
				}

				// corners
				for (int j = 0; j < segments; ++j)
				{
					for (int i = 0; i < segments; ++i)
					{
						AddFace(TranslateIndices(k, i, j),
								TranslateIndices(k, i + 1, j + 1),
								TranslateIndices(k, i, j + 1), kidx == 0);
						AddFace(TranslateIndices(k, i, j),
								TranslateIndices(k, i + 1, j),
								TranslateIndices(k, i + 1, j + 1), kidx == 0);

						AddFace(TranslateIndices(k, i, j + segments + 1),
								TranslateIndices(k, i + 1, j + segments + 2),
								TranslateIndices(k, i, j + segments + 2), kidx == 0);
						AddFace(TranslateIndices(k, i, j + segments + 1),
								TranslateIndices(k, i + 1, j + segments + 1),
								TranslateIndices(k, i + 1, j + segments + 2), kidx == 0);

						AddFace(TranslateIndices(k, i + segments + 1, j + segments + 1),
								TranslateIndices(k, i + segments + 2, j + segments + 2),
								TranslateIndices(k, i + segments + 1, j + segments + 2), kidx == 0);
						AddFace(TranslateIndices(k, i + segments + 1, j + segments + 1),
								TranslateIndices(k, i + segments + 2, j + segments + 1),
								TranslateIndices(k, i + segments + 2, j + segments + 2), kidx == 0);

						AddFace(TranslateIndices(k, i + segments + 1, j),
								TranslateIndices(k, i + segments + 2, j + 1),
								TranslateIndices(k, i + segments + 1, j + 1), kidx == 0);
						AddFace(TranslateIndices(k, i + segments + 1, j),
								TranslateIndices(k, i + segments + 2, j),
								TranslateIndices(k, i + segments + 2, j + 1), kidx == 0);
					}
				}

				// sides
				for (int i = 0; i < segments; ++i)
				{
					AddFace(TranslateIndices(k, i, segments),
							TranslateIndices(k, i + 1, segments + 1),
							TranslateIndices(k, i, segments + 1), kidx == 0);
					AddFace(TranslateIndices(k, i, segments),
							TranslateIndices(k, i + 1, segments),
							TranslateIndices(k, i + 1, segments + 1), kidx == 0);

					AddFace(TranslateIndices(k, segments, i),
							TranslateIndices(k, segments + 1, i + 1),
							TranslateIndices(k, segments, i + 1), kidx == 0);
					AddFace(TranslateIndices(k, segments, i),
							TranslateIndices(k, segments + 1, i),
							TranslateIndices(k, segments + 1, i + 1), kidx == 0);

					AddFace(TranslateIndices(k, i + segments + 1, segments),
							TranslateIndices(k, i + segments + 2, segments + 1),
							TranslateIndices(k, i + segments + 1, segments + 1), kidx == 0);
					AddFace(TranslateIndices(k, i + segments + 1, segments),
							TranslateIndices(k, i + segments + 2, segments),
							TranslateIndices(k, i + segments + 2, segments + 1), kidx == 0);

					AddFace(TranslateIndices(k, segments, i + segments + 1),
							TranslateIndices(k, segments + 1, i + segments + 2),
							TranslateIndices(k, segments, i + segments + 2), kidx == 0);
					AddFace(TranslateIndices(k, segments, i + segments + 1),
							TranslateIndices(k, segments + 1, i + segments + 1),
							TranslateIndices(k, segments + 1, i + segments + 2), kidx == 0);
				}

				// central
				AddFace(TranslateIndices(k, segments, segments),
						TranslateIndices(k, segments + 1, segments + 1),
						TranslateIndices(k, segments, segments + 1), kidx == 0);
				AddFace(TranslateIndices(k, segments, segments),
						TranslateIndices(k, segments + 1, segments),
						TranslateIndices(k, segments + 1, segments + 1), kidx == 0);
			}
		}

		private void AddFace(int i, int j, int k, bool inversed)
		{
			indices.Add(i);

			if (inversed)
			{
				indices.Add(k);
				indices.Add(j);
			}
			else
			{
				indices.Add(j);
				indices.Add(k);
			}
		}

		private void AddVertex(int i, int j, int k, Vector3 pos, Vector3 base_pos)
		{
			int pidx = k * edgeCount * edgeCount + j * edgeCount + i;
			if (!indexToVerts.ContainsKey(pidx))
			{
				int next_idx = vertices.Count;
				indexToVerts[pidx] = next_idx;

				var dir = pos - base_pos;
				if (dir.LengthSquared > 0.0)
				{
					dir.Normalize();
					vertices.Add(base_pos + dir * radius);
					normals.Add(dir);
				}
				else
				{
					vertices.Add(pos);
					normals.Add(pos.GetNormal());
				}
			}
		}

		private int TranslateIndices(int i, int j, int k)
		{
			int pidx = k * edgeCount * edgeCount + j * edgeCount + i;
			return indexToVerts[pidx];
		}
	}
}