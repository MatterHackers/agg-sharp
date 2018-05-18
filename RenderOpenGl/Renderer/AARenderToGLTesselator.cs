﻿/*
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

using MatterHackers.DataConverters2D;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public class AARenderToGLTesselator : CachedTesselator
	{
		private Vector2 fanPStart;
		private Vector2 fanTStart;
		private Vector2 fanPNext;
		private Vector2 fanTNext;

		public AARenderToGLTesselator()
		{
		}

		protected void DrawNonAATriangle(Vector2 p0, Vector2 p1, Vector2 p2)
		{
			// P1
			GL.TexCoord2(.2, .25);
			GL.Vertex2(p0.X, p0.Y);

			// P2
			GL.TexCoord2(.2, .75);
			GL.Vertex2(p1.X, p1.Y);

			// P3
			GL.TexCoord2(.9, .5);
			GL.Vertex2(p2.X, p2.Y);
		}

		/// <summary>
		/// edge p0 -> p1 is Anti aliased the others are not
		/// </summary>
		/// <param name="aaEdgeP0"></param>
		/// <param name="aaEdgeP1"></param>
		/// <param name="nonAaPoint"></param>
		public void Draw1EdgeTriangle(Vector2 aaEdgeP0, Vector2 aaEdgeP1, Vector2 nonAaPoint)
		{
			//return;
			if (aaEdgeP0 == aaEdgeP1 || aaEdgeP1 == nonAaPoint || nonAaPoint == aaEdgeP0)
			{
				return;
			}
			Vector2 edegP0P1Vector = aaEdgeP1 - aaEdgeP0;
			Vector2 edgeP0P1Normal = edegP0P1Vector;
			edgeP0P1Normal.Normalize();

			Vector2 Normal = edgeP0P1Normal.GetPerpendicularRight();
			double edgeDotP3 = Vector2.Dot(Normal, nonAaPoint - aaEdgeP0);
			if (edgeDotP3 < 0)
			{
				edgeDotP3 = -edgeDotP3;
			}
			else
			{
				Normal = -Normal;
			}

			Vector2 edgeP0Offset = aaEdgeP0 + Normal;
			Vector2 edgeP1Offset = aaEdgeP1 + Normal;

			Vector2 texP0 = new Vector2(1 / 1023.0, .25);
			Vector2 texP1 = new Vector2(1 / 1023.0, .75);
			Vector2 texP2 = new Vector2((1 + edgeDotP3) / 1023.0, .25);
			Vector2 texEdgeP0Offset = new Vector2(0, .25);
			Vector2 texEdgeP1Offset = new Vector2(0, .75);

			FanStart(texP0, aaEdgeP0, texEdgeP0Offset, edgeP0Offset);
			FanDo(texEdgeP1Offset, edgeP1Offset);
			FanDo(texP1, aaEdgeP1);
			FanDo(texP2, nonAaPoint);
		}

		private void FanStart(Vector2 fanTStart, Vector2 fanPStart, Vector2 fanTNext, Vector2 fanPNext)
		{
			this.fanTStart = fanTStart;
			this.fanPStart = fanPStart;
			this.fanTNext = fanTNext;
			this.fanPNext = fanPNext;
		}

		private void FanDo(Vector2 fanTEnd, Vector2 fanPEnd)
		{
			GL.TexCoord2(fanTStart.X, fanTStart.Y);
			GL.Vertex2(fanPStart.X, fanPStart.Y);

			GL.TexCoord2(fanTNext.X, fanTNext.Y);
			GL.Vertex2(fanPNext.X, fanPNext.Y);

			GL.TexCoord2(fanTEnd.X, fanTEnd.Y);
			GL.Vertex2(fanPEnd.X, fanPEnd.Y);

			fanTNext = fanTEnd;
			fanPNext = fanPEnd;
		}

		/// <summary>
		/// Edge p0 -> p1 and p1 -> p2 are Anti Aliased. Edge p2 -> p0 is NOT.
		/// </summary>
		/// <param name="p0"></param>
		/// <param name="p1"></param>
		/// <param name="p2"></param>
		public void Draw2EdgeTriangle(Vector2 p0, Vector2 p1, Vector2 p2)
		{
			//Draw3EdgeTriangle(p0, p1, p2);
			Vector2 centerPoint = p0 + p1 + p2;
			centerPoint /= 3;

			Draw1EdgeTriangle(p0, p1, centerPoint);
			Draw1EdgeTriangle(p1, p2, centerPoint);
			DrawNonAATriangle(p2, p0, centerPoint);
		}

		protected void Draw3EdgeTriangle(Vector2 p0, Vector2 p1, Vector2 p2)
		{
			Vector2 centerPoint = p0 + p1 + p2;
			centerPoint /= 3;

			Draw1EdgeTriangle(p0, p1, centerPoint);
			Draw1EdgeTriangle(p1, p2, centerPoint);
			Draw1EdgeTriangle(p2, p0, centerPoint);
		}

		public void RenderLastToGL()
		{
			GL.Begin(BeginMode.Triangles);

			int numIndicies = IndicesCache.Count;
			for (int i = 0; i < numIndicies; i += 3)
			{
				Vector2 v0 = VerticesCache[IndicesCache[i + 0].Index].Position;
				Vector2 v1 = VerticesCache[IndicesCache[i + 1].Index].Position;
				Vector2 v2 = VerticesCache[IndicesCache[i + 2].Index].Position;
				if (v0 == v1 || v1 == v2 || v2 == v0)
				{
					continue;
				}

				int e0 = IndicesCache[i + 0].IsEdge ? 1 : 0;
				int e1 = IndicesCache[i + 1].IsEdge ? 1 : 0;
				int e2 = IndicesCache[i + 2].IsEdge ? 1 : 0;
				switch (e0 + e1 + e2)
				{
					case 0:
						DrawNonAATriangle(v0, v1, v2);
						break;

					case 1:
						if (e0 == 1)
						{
							Draw1EdgeTriangle(v0, v1, v2);
						}
						else if (e1 == 1)
						{
							Draw1EdgeTriangle(v1, v2, v0);
						}
						else
						{
							Draw1EdgeTriangle(v2, v0, v1);
						}
						break;

					case 2:
						if (e0 == 1)
						{
							if (e1 == 1)
							{
								Draw2EdgeTriangle(v0, v1, v2);
							}
							else
							{
								Draw2EdgeTriangle(v2, v0, v1);
							}
						}
						else
						{
							Draw2EdgeTriangle(v1, v2, v0);
						}
						break;

					case 3:
						Draw3EdgeTriangle(v0, v1, v2);
						break;
				}
			}

			GL.End();
		}
	}
}