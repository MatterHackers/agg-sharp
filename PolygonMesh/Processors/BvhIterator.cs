using MatterHackers.VectorMath;

// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Processors
{
    public class BvhIterator : IEnumerable<BvhIterator>
	{
		public Matrix4X4 TransformToWorld { get; private set; }
		public IBvhItem Bvh { get; private set; }
		public int Depth { get; private set; } = 0;
		Func<BvhIterator, bool> DecentFilter = null;

		public BvhIterator(IBvhItem referenceItem, Matrix4X4 initialTransform = default(Matrix4X4), int initialDepth = 0, Func<BvhIterator, bool> decentFilter = null)
		{
			TransformToWorld = initialTransform;
			if (TransformToWorld == default(Matrix4X4))
			{
				TransformToWorld = Matrix4X4.Identity;
			}
			Depth = initialDepth;

			Bvh = referenceItem;
			this.DecentFilter = decentFilter;
		}

		public IEnumerator<BvhIterator> GetEnumerator()
		{
			if (DecentFilter?.Invoke(this) != false)
			{
				yield return this;

				if (Bvh.Children != null)
				{
					foreach (var child in Bvh.Children)
					{
						foreach (var subIterator in new BvhIterator(child, Bvh.AxisToWorld * TransformToWorld, Depth + 1, DecentFilter))
						{
							yield return subIterator;
						}
					}
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}
}