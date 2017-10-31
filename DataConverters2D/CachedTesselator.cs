using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;

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

using System.Collections.Generic;

namespace MatterHackers.DataConverters2D
{
	public class CachedTesselator : VertexTesselatorAbstract
	{
		internal bool lastEdgeFlagSet = false;
        public List<AddedVertex> VerticesCache = new List<AddedVertex>();
		public List<RenderIndices> IndicesCache = new List<RenderIndices>();

        public class AddedVertex
		{
			private Vector2 position;

			public Vector2 Position { get { return position; } }

			internal AddedVertex(double x, double y)
			{
				position.X = x;
				position.Y = y;
			}
		}

		public class RenderIndices
		{
			private int index;
			private bool isEdge;

			public int Index
			{
				get { return index; }
			}

			public bool IsEdge
			{
				get { return isEdge; }
			}

			internal RenderIndices(int index, bool isEdge)
			{
				this.index = index;
				this.isEdge = isEdge;
			}
		}

		public CachedTesselator()
		{
			callVertex = VertexCallBack;
			callEdgeFlag = EdgeFlagCallBack;
			callCombine = CombineCallBack;
		}

		public void Clear()
		{
			lastEdgeFlagSet = false;
			VerticesCache.Clear();
			IndicesCache.Clear();
		}

		public override void BeginPolygon()
		{
			VerticesCache.Clear();
			IndicesCache.Clear();

			base.BeginPolygon();
		}

		public void VertexCallBack(int index)
		{
			IndicesCache.Add(new RenderIndices(index, lastEdgeFlagSet));
		}

		public void EdgeFlagCallBack(bool isEdge)
		{
			lastEdgeFlagSet = isEdge;
		}

		public int CombineCallBack(double[] coords3, int[] data4,
			double[] weight4)
		{
			return AddVertex(coords3[0], coords3[1], false);
		}

		public override void AddVertex(double x, double y)
		{
			AddVertex(x, y, true);
		}

		public int AddVertex(double x, double y, bool passOnToTesselator)
		{
			int clientIndex = VerticesCache.Count;
			VerticesCache.Add(new AddedVertex(x, y));
			double[] coords = new double[2];
			coords[0] = x;
			coords[1] = y;
			if (passOnToTesselator)
			{
				AddVertex(coords, clientIndex);
			}
			return clientIndex;
		}
	}
}