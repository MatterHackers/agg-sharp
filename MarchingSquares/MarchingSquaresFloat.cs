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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MarchingSquares
{
	public class LineSegmentFloat
	{
		public Vector2 start;
		public Vector2 end;

		public LineSegmentFloat(double x1, double y1, double x2, double y2)
			: this(new Vector2(x1, y1), new Vector2(x2, y2))
		{
		}

		public LineSegmentFloat(Vector2 start, Vector2 end)
		{
			this.start = start;
			this.end = end;
		}
	}

	public class MarchingSquaresFloat
	{
		private List<Vector2> contours = new List<Vector2>();
		private ImageBufferFloat imageToMarch;
		private int threshold;
		private int debugColor;

		private List<LineSegmentFloat> LineSegments = new List<LineSegmentFloat>();

		public List<List<Vector2>> LineLoops = new List<List<Vector2>>();

		public MarchingSquaresFloat(ImageBufferFloat imageToMarch, int threshold, int debugColor)
		{
			this.threshold = threshold;
			this.imageToMarch = imageToMarch;
			this.debugColor = debugColor;

			CreateLineSegments();
		}

		public void CreateLineLoops()
		{
			bool[] hasBeenAddedList = new bool[LineSegments.Count];

			for (int segmentToAddIndex = 0; segmentToAddIndex < LineSegments.Count; segmentToAddIndex++)
			{
				if (!hasBeenAddedList[segmentToAddIndex])
				{
					// now find all the connected segments until we get back to this one
					List<Vector2> loopToAdd = new List<Vector2>();

					// walk the loop
					int currentSegmentIndex = segmentToAddIndex;
					LineSegmentFloat currentSegment = LineSegments[currentSegmentIndex];
					Vector2 connectionVertex = currentSegment.end;
					loopToAdd.Add(connectionVertex);
					hasBeenAddedList[currentSegmentIndex] = true;
					bool addedToLoop = false;
					do
					{
						bool foundNextSegment = false;
						addedToLoop = false;
						for (int segmentToCheckIndex = 0; segmentToCheckIndex < LineSegments.Count; segmentToCheckIndex++)
						{
							LineSegmentFloat segmentToCheck = LineSegments[segmentToCheckIndex];
							if (!hasBeenAddedList[segmentToCheckIndex])
							{
								if (connectionVertex == segmentToCheck.start)
								{
									connectionVertex = segmentToCheck.end;
									foundNextSegment = true;
								}
								else if (connectionVertex == segmentToCheck.end)
								{
									connectionVertex = segmentToCheck.start;
									foundNextSegment = true;
								}
								else
								{
									// int i = 0;
								}

								if (foundNextSegment)
								{
									hasBeenAddedList[segmentToCheckIndex] = true;
									currentSegmentIndex = segmentToCheckIndex;
									currentSegment = segmentToCheck;
									loopToAdd.Add(connectionVertex);
									addedToLoop = true;
									break;
								}
							}
						}
					} while (addedToLoop);

					LineLoops.Add(loopToAdd);
				}
			}
		}

		public void CreateLineSegments()
		{
			LineSegments.Clear();
			float[] buffer = imageToMarch.GetBuffer();
			int strideInFloats = imageToMarch.StrideInFloats();
			for (int y = 0; y < imageToMarch.Height - 1; y++)
			{
				int offset = imageToMarch.GetBufferOffsetY(y);
				for (int x = 0; x < imageToMarch.Width - 1; x++)
				{
					int offsetWithX = offset + x;
					float point0 = buffer[offsetWithX + strideInFloats];
					float point1 = buffer[offsetWithX + 1 + strideInFloats];
					float point2 = buffer[offsetWithX + 1];
					float point3 = buffer[offsetWithX];
					int flags = (point0 > threshold) ? 1 : 0;
					flags = (flags << 1) | ((point1 > threshold) ? 1 : 0);
					flags = (flags << 1) | ((point2 > threshold) ? 1 : 0);
					flags = (flags << 1) | ((point3 > threshold) ? 1 : 0);

					bool wasFlipped = false;
					if (flags == 5)
					{
						float average = (point0 + point1 + point2 + point3) / 4;
						if (average < threshold)
						{
							flags = 10;
							wasFlipped = true;
						}
					}
					else if (flags == 10)
					{
						float average = (point0 + point1 + point2 + point3) / 4;
						if (average < threshold)
						{
							flags = 5;
							wasFlipped = true;
						}
					}

					AddSegmentForFlags(x, y, flags, wasFlipped);
				}
			}
		}

		private LineSegmentFloat GetInterpolatedSegment(LineSegmentFloat segmentA, LineSegmentFloat segmentB)
		{
#if false
            float colorAStart = Math.Min(imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentA.start.x, (int)segmentA.start.y)], 1);
            float colorAEnd = Math.Min(imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentA.end.x, (int)segmentA.end.y)], 1);

            Vector2 directionA = segmentA.end - segmentA.start;
            double offsetA = 1 - (colorAEnd + colorAStart) / 2.0;
            directionA *= offsetA;

            float colorBStart = Math.Min(imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentB.start.x, (int)segmentB.start.y)], 1);
            float colorBEnd = Math.Min(imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentB.end.x, (int)segmentB.end.y)], 1);
#else
			float colorAStart = imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentA.start.X, (int)segmentA.start.Y)];
			float colorAEnd = imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentA.end.X, (int)segmentA.end.Y)];

			Vector2 directionA = segmentA.end - segmentA.start;
			double offsetA = 1 - (colorAEnd + colorAStart) / 2.0;
			directionA *= offsetA;

			float colorBStart = imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentB.start.X, (int)segmentB.start.Y)];
			float colorBEnd = imageToMarch.GetBuffer()[imageToMarch.GetBufferOffsetXY((int)segmentB.end.X, (int)segmentB.end.Y)];
#endif

			Vector2 directionB = segmentB.end - segmentB.start;
			double ratioB = 1 - (colorBEnd + colorBStart) / 2.0;
			directionB *= ratioB;

			double offsetToPixelCenter = .5;
			LineSegmentFloat segment = new LineSegmentFloat(
				(segmentA.start.X + directionA.X) + offsetToPixelCenter,
				(segmentA.start.Y + directionA.Y) + offsetToPixelCenter,
				(segmentB.start.X + directionB.X) + offsetToPixelCenter,
				(segmentB.start.Y + directionB.Y) + offsetToPixelCenter);

			return segment;
		}

		private void AddSegmentForFlags(int x, int y, int flags, bool wasFlipped)
		{
			switch (flags)
			{
				case 1:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y + 1, x, y), new LineSegmentFloat(x + 1, y, x, y)));
					break;

				case 2:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y + 1, x + 1, y), new LineSegmentFloat(x, y, x + 1, y)));
					break;

				case 3:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y + 1, x, y), new LineSegmentFloat(x + 1, y + 1, x + 1, y)));
					break;

				case 4:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y, x + 1, y + 1), new LineSegmentFloat(x, y + 1, x + 1, y + 1)));
					break;

				case 5:
					if (wasFlipped)
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x, y + 1), new LineSegmentFloat(x + 1, y + 1, x, y + 1)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y + 1, x + 1, y), new LineSegmentFloat(x, y, x + 1, y)));
					}
					else
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y + 1, x, y), new LineSegmentFloat(x, y + 1, x + 1, y + 1)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y, x + 1, y + 1), new LineSegmentFloat(x + 1, y, x, y)));
					}
					break;

				case 6:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x + 1, y), new LineSegmentFloat(x, y + 1, x + 1, y + 1)));
					break;

				case 7:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y + 1, x, y), new LineSegmentFloat(x, y + 1, x + 1, y + 1)));
					break;

				case 8:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x, y + 1), new LineSegmentFloat(x + 1, y + 1, x, y + 1)));
					break;

				case 9:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y + 1, x, y + 1), new LineSegmentFloat(x + 1, y, x, y)));
					break;

				case 10:
					if (wasFlipped)
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y, x, y), new LineSegmentFloat(x, y + 1, x, y)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y + 1, x + 1, y + 1), new LineSegmentFloat(x + 1, y, x + 1, y + 1)));
					}
					else
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x, y + 1), new LineSegmentFloat(x, y, x + 1, y)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y + 1, x + 1, y), new LineSegmentFloat(x + 1, y + 1, x, y + 1)));
					}
					break;

				case 11:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y + 1, x + 1, y), new LineSegmentFloat(x + 1, y + 1, x, y + 1)));
					break;

				case 12:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x, y + 1), new LineSegmentFloat(x + 1, y, x + 1, y + 1)));
					break;

				case 13:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x + 1, y, x + 1, y + 1), new LineSegmentFloat(x + 1, y, x, y)));
					break;

				case 14:
					LineSegments.Add(GetInterpolatedSegment(new LineSegmentFloat(x, y, x, y + 1), new LineSegmentFloat(x, y, x + 1, y)));
					break;
			}
		}

		public void DrawSegments(Graphics2D graphics2D)
		{
			foreach (LineSegmentFloat lineSegment in LineSegments)
			{
				VertexStorage m_LinesToDraw = new VertexStorage();
				m_LinesToDraw.remove_all();
				m_LinesToDraw.MoveTo(lineSegment.start.X, lineSegment.start.Y);
				m_LinesToDraw.LineTo(lineSegment.end.X, lineSegment.end.Y);
				Stroke StrockedLineToDraw = new Stroke(m_LinesToDraw, .25);
				graphics2D.Render(StrockedLineToDraw, Color.Black);
			}
		}
	}
}