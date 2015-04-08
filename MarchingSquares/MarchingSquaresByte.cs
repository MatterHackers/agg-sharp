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

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MarchingSquares
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class LineSegment
	{
		public Vector2 start;
		public Vector2 end;
		public RGBA_Bytes color;

		public LineSegment(double x1, double y1, double x2, double y2, RGBA_Bytes color)
			: this(new Vector2(x1, y1), new Vector2(x2, y2), color)
		{
		}

		public LineSegment(Vector2 start, Vector2 end, RGBA_Bytes color)
		{
			this.start = start;
			this.end = end;
			this.color = color;
		}
	}

	public class SimpleRange
	{
		private int starting;
		private int ending;

		public SimpleRange(int starting = 0, int ending = 255)
		{
			this.starting = Math.Min(starting, 254);
			this.ending = Math.Max(ending, starting + 1);
		}

		public double Threshold(RGBA_Bytes color)
		{
			if (color.Red0To255 < starting)
			{
				return 0;
			}
			else if (color.Red0To255 > ending)
			{
				return 1;
			}
			else
			{
				double value = (double)(color.Red0To255 - starting) / (double)(ending - starting);

				return value;
			}
		}
	}

	public class MarchingSquaresByte
	{
		private ImageBuffer imageToMarch;

		public delegate double PositiveArea0to1(RGBA_Bytes color);

		private PositiveArea0to1 thresholdFunction;
		private int debugColor;

		public List<LineSegment> LineSegments = new List<LineSegment>();
		private double[] thersholdPerPixel = null;

		public MarchingSquaresByte(ImageBuffer imageToMarch, PositiveArea0to1 thresholdFunction, int debugColor)
		{
			thersholdPerPixel = new double[imageToMarch.Width * imageToMarch.Height];
			{
				byte[] buffer = imageToMarch.GetBuffer();
				int strideInBytes = imageToMarch.StrideInBytes();
				for (int y = 0; y < imageToMarch.Height; y++)
				{
					int imageBufferOffset = imageToMarch.GetBufferOffsetY(y);
					int thresholdBufferOffset = y * imageToMarch.Width;

					for (int x = 0; x < imageToMarch.Width; x++)
					{
						int imageBufferOffsetWithX = imageBufferOffset + x * 4;
						thersholdPerPixel[thresholdBufferOffset + x] = thresholdFunction(GetRGBA(buffer, imageBufferOffsetWithX));
					}
				}
			}

			this.thresholdFunction = thresholdFunction;
			this.imageToMarch = imageToMarch;
			this.debugColor = debugColor;

			CreateLineSegments();
		}

		public MarchingSquaresByte(ImageBuffer imageToMarch, int thresholdFrom0, int debugColor)
			: this(imageToMarch, new SimpleRange(thresholdFrom0).Threshold, debugColor)
		{
		}

		public Polygons CreateLineLoops(int pixelsToIntPointsScale)
		{
			Polygons LineLoops = new Polygons();

			bool[] hasBeenAddedList = new bool[LineSegments.Count];

			for (int segmentToAddIndex = 0; segmentToAddIndex < LineSegments.Count; segmentToAddIndex++)
			{
				if (!hasBeenAddedList[segmentToAddIndex])
				{
					// now find all the connected segments until we get back to this one
					Polygon loopToAdd = new Polygon();

					// walk the loop
					int currentSegmentIndex = segmentToAddIndex;
					LineSegment currentSegment = LineSegments[currentSegmentIndex];
					Vector2 connectionVertex = currentSegment.end;
					loopToAdd.Add(new IntPoint((long)(connectionVertex.x * pixelsToIntPointsScale), (long)(connectionVertex.y * pixelsToIntPointsScale)));
					hasBeenAddedList[currentSegmentIndex] = true;
					bool addedToLoop = false;
					do
					{
						bool foundNextSegment = false;
						addedToLoop = false;
						for (int segmentToCheckIndex = 0; segmentToCheckIndex < LineSegments.Count; segmentToCheckIndex++)
						{
							LineSegment segmentToCheck = LineSegments[segmentToCheckIndex];
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
									int i = 0;
								}

								if (foundNextSegment)
								{
									hasBeenAddedList[segmentToCheckIndex] = true;
									currentSegmentIndex = segmentToCheckIndex;
									currentSegment = segmentToCheck;
									loopToAdd.Add(new IntPoint((long)(connectionVertex.x * pixelsToIntPointsScale), (long)(connectionVertex.y * pixelsToIntPointsScale)));
									addedToLoop = true;
									break;
								}
							}
						}
					} while (addedToLoop);

					LineLoops.Add(loopToAdd);
				}
			}

			return LineLoops;
		}

		private RGBA_Bytes GetRGBA(byte[] buffer, int offset)
		{
			return new RGBA_Bytes(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
		}

		public void CreateLineSegments()
		{
			LineSegments.Clear();

			for (int y = 0; y < imageToMarch.Height - 1; y++)
			{
				for (int x = 0; x < imageToMarch.Width - 1; x++)
				{
					double point0 = thersholdPerPixel[(x + 0) + (y + 1) * imageToMarch.Width];
					double point1 = thersholdPerPixel[(x + 1) + (y + 1) * imageToMarch.Width];
					double point2 = thersholdPerPixel[(x + 1) + (y + 0) * imageToMarch.Width];
					double point3 = thersholdPerPixel[(x + 0) + (y + 0) * imageToMarch.Width];

					int flags = (point0 > 0 ? 1 : 0);
					flags = (flags << 1) | (point1 > 0 ? 1 : 0);
					flags = (flags << 1) | (point2 > 0 ? 1 : 0);
					flags = (flags << 1) | (point3 > 0 ? 1 : 0);

					bool wasFlipped = false;
					if (flags == 5)
					{
						double average = (point0 + point1 + point2 + point3) / 4.0;
						if (average > .5)
						{
							flags = 10;
							wasFlipped = true;
						}
					}
					else if (flags == 10)
					{
						double average = (point0 + point1 + point2 + point3) / 4.0;
						if (average < .5)
						{
							flags = 5;
							wasFlipped = true;
						}
					}

					AddSegmentForFlags(x, y, flags, wasFlipped);
				}
			}
		}

		private LineSegment GetInterpolatedSegment(LineSegment segmentA, LineSegment segmentB)
		{
			double colorAStartThreshold = thersholdPerPixel[((int)segmentA.start.x) + ((int)segmentA.start.y) * imageToMarch.Width];
			double colorAEndThreshold = thersholdPerPixel[((int)segmentA.end.x) + ((int)segmentA.end.y) * imageToMarch.Width];

			Vector2 directionA = segmentA.end - segmentA.start;
			double offsetA = 1 - (colorAEndThreshold + colorAStartThreshold) / 2.0;
			directionA *= offsetA;

			double colorBStartThreshold = thersholdPerPixel[((int)segmentB.start.x) + ((int)segmentB.start.y) * imageToMarch.Width];
			double colorBEndThreshold = thersholdPerPixel[((int)segmentB.end.x) + ((int)segmentB.end.y) * imageToMarch.Width];

			Vector2 directionB = segmentB.end - segmentB.start;
			double ratioB = 1 - (colorBEndThreshold + colorBStartThreshold) / 2.0;
			directionB *= ratioB;

			double offsetToPixelCenter = .5;
			LineSegment segment = new LineSegment(
				(segmentA.start.x + directionA.x) + offsetToPixelCenter,
				(segmentA.start.y + directionA.y) + offsetToPixelCenter,
				(segmentB.start.x + directionB.x) + offsetToPixelCenter,
				(segmentB.start.y + directionB.y) + offsetToPixelCenter,
				segmentA.color);

			return segment;
		}

		private void AddSegmentForFlags(int x, int y, int flags, bool wasFlipped)
		{
			RGBA_Bytes color = RGBA_Bytes.Green;
			if (flags == debugColor)
			{
				color = RGBA_Bytes.Red;
			}

			switch (flags)
			{
				case 1:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x + 1, y, x, y, color)));
					break;

				case 2:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x, y, x + 1, y, color)));
					break;

				case 3:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x + 1, y + 1, x + 1, y, color)));
					break;

				case 4:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
					break;

				case 5:
					if (wasFlipped)
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x, y, x + 1, y, color)));
					}
					else
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
					}
					break;

				case 6:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x + 1, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
					break;

				case 7:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x, y, color), new LineSegment(x, y + 1, x + 1, y + 1, color)));
					break;

				case 8:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
					break;

				case 9:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
					break;

				case 10:
					if (wasFlipped)
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x, y, color), new LineSegment(x, y + 1, x, y, color)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y + 1, x + 1, y + 1, color), new LineSegment(x + 1, y, x + 1, y + 1, color)));
					}
					else
					{
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x, y, x + 1, y, color)));
						LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
					}
					break;

				case 11:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y + 1, x + 1, y, color), new LineSegment(x + 1, y + 1, x, y + 1, color)));
					break;

				case 12:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x + 1, y, x + 1, y + 1, color)));
					break;

				case 13:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x + 1, y, x + 1, y + 1, color), new LineSegment(x + 1, y, x, y, color)));
					break;

				case 14:
					LineSegments.Add(GetInterpolatedSegment(new LineSegment(x, y, x, y + 1, color), new LineSegment(x, y, x + 1, y, color)));
					break;
			}
		}

		public void DrawSegments(Graphics2D graphics2D)
		{
			foreach (LineSegment lineSegment in LineSegments)
			{
				PathStorage m_LinesToDraw = new PathStorage();
				m_LinesToDraw.remove_all();
				m_LinesToDraw.MoveTo(lineSegment.start.x, lineSegment.start.y);
				m_LinesToDraw.LineTo(lineSegment.end.x, lineSegment.end.y);
				Stroke StrockedLineToDraw = new Stroke(m_LinesToDraw, .25);
				graphics2D.Render(StrockedLineToDraw, lineSegment.color);
			}
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}