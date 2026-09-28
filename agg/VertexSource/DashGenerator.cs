//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.VertexSource
{
	/// <summary>
	/// vcgen_dash: cuts one accumulated polyline (or closed polygon) into dashes. Each dash is a MoveTo
	/// followed by LineTos; gaps are skipped. Driven by <see cref="Dash"/> through
	/// <see cref="VertexSourceAdapter"/>.
	/// </summary>
	internal class DashGenerator : IGenerator
	{
		private const int MaxDashes = 32;

		private readonly double[] dashes = new double[MaxDashes];
		private double totalDashLength;
		private int numDashes;
		private double dashStart;
		private double shorten;
		private double currentDashStart;
		private int currentDash;
		private double currentRest;
		private VertexDistance v1;
		private VertexDistance v2;

		private readonly VertexSequence sourceVertices = new VertexSequence();
		private int closed;
		private Status status = Status.Initial;
		private int sourceVertex;

		private enum Status
		{
			Initial,
			Ready,
			Polyline,
			Stop
		}

		public double Shorten
		{
			get => shorten;
			set => shorten = value;
		}

		public void RemoveAllDashes()
		{
			totalDashLength = 0.0;
			numDashes = 0;
			currentDashStart = 0.0;
			currentDash = 0;
		}

		public void AddDash(double dashLength, double gapLength)
		{
			if (numDashes < MaxDashes)
			{
				totalDashLength += dashLength + gapLength;
				dashes[numDashes++] = dashLength;
				dashes[numDashes++] = gapLength;
			}
		}

		public void DashStart(double ds)
		{
			dashStart = ds;
			CalcDashStart(Math.Abs(ds));
		}

		private void CalcDashStart(double ds)
		{
			currentDash = 0;
			currentDashStart = 0.0;

			// C++ loops forever here (or reads uninitialized dashes) when the pattern has no positive
			// length to walk through; a start offset means nothing then, so stay at the pattern's start.
			// Vertex() likewise yields nothing for such a pattern, where C++ emits zero-length dashes forever.
			if (numDashes == 0 || totalDashLength <= 0)
			{
				return;
			}

			while (ds > 0.0)
			{
				if (ds > dashes[currentDash])
				{
					ds -= dashes[currentDash];
					++currentDash;
					currentDashStart = 0.0;
					if (currentDash >= numDashes)
					{
						currentDash = 0;
					}
				}
				else
				{
					currentDashStart = ds;
					ds = 0.0;
				}
			}
		}

		public void RemoveAll()
		{
			status = Status.Initial;
			sourceVertices.Clear();
			closed = 0;
		}

		public void AddVertex(double x, double y, FlagsAndCommand cmd)
		{
			status = Status.Initial;
			if (ShapePath.IsMoveTo(cmd))
			{
				sourceVertices.modify_last(new VertexDistance(x, y));
			}
			else if (ShapePath.IsVertex(cmd))
			{
				sourceVertices.Add(new VertexDistance(x, y));
			}
			else
			{
				closed = (int)ShapePath.get_close_flag(cmd);
			}
		}

		public void Rewind(int pathId)
		{
			if (status == Status.Initial)
			{
				sourceVertices.close(closed != 0);
				ShapePath.shorten_path(sourceVertices, shorten, closed);
			}

			status = Status.Ready;
			sourceVertex = 0;
		}

		public FlagsAndCommand Vertex(ref double x, ref double y)
		{
			FlagsAndCommand cmd = FlagsAndCommand.MoveTo;
			while (!ShapePath.IsStop(cmd))
			{
				switch (status)
				{
					case Status.Initial:
						Rewind(0);
						goto case Status.Ready;

					case Status.Ready:
						if (numDashes < 2 || totalDashLength <= 0 || sourceVertices.Count < 2)
						{
							cmd = FlagsAndCommand.Stop;
							break;
						}

						status = Status.Polyline;
						sourceVertex = 1;
						v1 = sourceVertices[0];
						v2 = sourceVertices[1];
						currentRest = v1.dist;
						x = v1.x;
						y = v1.y;
						if (dashStart >= 0.0)
						{
							CalcDashStart(dashStart);
						}

						return FlagsAndCommand.MoveTo;

					case Status.Polyline:
						{
							double dashRest = dashes[currentDash] - currentDashStart;

							// Odd entries are gaps: the point that ends a gap starts the next dash.
							FlagsAndCommand dashCommand = (currentDash & 1) != 0 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo;

							if (currentRest > dashRest)
							{
								currentRest -= dashRest;
								++currentDash;
								if (currentDash >= numDashes)
								{
									currentDash = 0;
								}

								currentDashStart = 0.0;
								x = v2.x - (v2.x - v1.x) * currentRest / v1.dist;
								y = v2.y - (v2.y - v1.y) * currentRest / v1.dist;
							}
							else
							{
								currentDashStart += currentRest;
								x = v2.x;
								y = v2.y;
								++sourceVertex;
								v1 = v2;
								currentRest = v1.dist;
								if (closed != 0)
								{
									if (sourceVertex > sourceVertices.Count)
									{
										status = Status.Stop;
									}
									else
									{
										v2 = sourceVertices[sourceVertex >= sourceVertices.Count ? 0 : sourceVertex];
									}
								}
								else
								{
									if (sourceVertex >= sourceVertices.Count)
									{
										status = Status.Stop;
									}
									else
									{
										v2 = sourceVertices[sourceVertex];
									}
								}
							}

							return dashCommand;
						}

					case Status.Stop:
						cmd = FlagsAndCommand.Stop;
						break;
				}
			}

			return FlagsAndCommand.Stop;
		}

		// IGenerator is shaped for the stroke and contour generators; vcgen_dash has no width, joins or
		// caps, so these are inert.
		double IGenerator.ApproximationScale { get; set; } = 1;

		bool IGenerator.AutoDetectOrientation { get; set; }

		InnerJoin IGenerator.InnerJoin { get; set; }

		double IGenerator.InnerMiterLimit { get; set; }

		LineCap IGenerator.LineCap { get; set; }

		LineJoin IGenerator.LineJoin { get; set; }

		double IGenerator.MiterLimit { get; set; }

		double IGenerator.Width { get; set; }

		void IGenerator.MiterLimitTheta(double t)
		{
		}
	}
}
