using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2011
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
//
// Class TypeFace.cs
//
//----------------------------------------------------------------------------
using Typography.OpenFont;

namespace MatterHackers.Agg.Font
{
	public class VertexSourceGlyphTranslator : IGlyphTranslator
	{
		private int polygonStartIndex = 0;
		private VertexStorage vertexStorage;
		Vector2 curve3Control = new Vector2(double.MinValue, double.MinValue);

		public VertexSourceGlyphTranslator(VertexStorage vertexStorage)
		{
			this.vertexStorage = vertexStorage;
		}

		public void BeginRead(int contourCount)
		{
		}

		private void CheckForOpenCurve3()
		{
			if (curve3Control.X != double.MinValue)
			{
				// we started this polygon with a control point so add the required curve3
				var vertex = vertexStorage.vertex(polygonStartIndex, out var x, out var y);
				vertexStorage.curve3(curve3Control.X, curve3Control.Y, x, y);

				// reset the curve3Control point to unitialized
				curve3Control = new Vector2(double.MinValue, double.MinValue);
			}
		}

		public void CloseContour()
		{
			CheckForOpenCurve3();

			//vertexStorage.ClosePolygon();
			if (vertexStorage.Count > polygonStartIndex)
			{
				vertexStorage.invert_polygon(polygonStartIndex);
			}
			polygonStartIndex = vertexStorage.Count;
		}

		public void Curve3(float xControl, float yControl, float x, float y)
		{
			if (polygonStartIndex == vertexStorage.Count)
			{
				// we have not started the polygon so there is no point to curve3 from
				// store this control point and add it at the end of the polygon
				curve3Control = new Vector2(xControl, yControl);
				// then move to the end of this curve
				vertexStorage.MoveTo(x, y);
			}
			else
			{
				vertexStorage.curve3(xControl, yControl, x, y);
			}
		}

		public void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
		{
			vertexStorage.curve4(x1, y1, x2, y2, x3, y3);
		}

		public void EndRead()
		{
		}

		public void LineTo(float x, float y)
		{
			vertexStorage.LineTo(x, y);
		}

		public void MoveTo(float x, float y)
		{
			CheckForOpenCurve3();
			polygonStartIndex = vertexStorage.Count;
			vertexStorage.MoveTo(x, y);
		}
	}
}