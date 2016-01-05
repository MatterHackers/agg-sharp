//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
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
//
// Arc vertex generator
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

using FlagsAndCommand = MatterHackers.Agg.ShapePath.FlagsAndCommand;

namespace MatterHackers.Agg.VertexSource
{
	//=====================================================================arc
	//
	// See Implementation agg_arc.cpp
	//
	public class Arc : IVertexSource
	{
		private double originX;
		private double originY;

		private double radiusX;
		private double radiusY;

		private double startAngle;
		private double endAngle;
		private double m_Scale = 1.0;
		private bool moveToStart = true;
		private EDirection m_Direction;

		private double m_CurrentFlatenAngle;
		private double flatenDeltaAngle;

		private bool m_IsInitialized = false;
		private ShapePath.FlagsAndCommand m_NextPathCommand;

		public enum EDirection
		{
			ClockWise,
			CounterClockWise,
		}

		public Arc()
		{
		}

		public Arc(double OriginX, double OriginY,
			 double RadiusX, double RadiusY,
			 double Angle1, double Angle2,
			 EDirection Direction = EDirection.CounterClockWise,
			 double Scale = 1.0,
			 bool moveToStart = true)
		{
			init(OriginX, OriginY, RadiusX, RadiusY, Angle1, Angle2, Direction: Direction, Scale: Scale, moveToStart: moveToStart);
		}

		public void init(double OriginX, double OriginY,
				   double RadiusX, double RadiusY,
				   double Angle1, double Angle2,
				   EDirection Direction = EDirection.CounterClockWise,
				   double Scale = 1.0,
				   bool moveToStart = true)
		{
			originX = OriginX;
			originY = OriginY;
			radiusX = RadiusX;
			radiusY = RadiusY;
			m_Scale = Scale;
			this.moveToStart = moveToStart;
			normalize(Angle1, Angle2, Direction);
		}

		public void approximation_scale(double s)
		{
			m_Scale = s;
			m_IsInitialized = false; // force recalc
		}

		public double approximation_scale()
		{
			return m_Scale;
		}

		public IEnumerable<VertexData> Vertices()
		{
			if (!m_IsInitialized)
			{
				normalize(startAngle, endAngle, m_Direction);
			}

			// go to the start
			VertexData vertexData = new VertexData();
			vertexData.command = moveToStart ? FlagsAndCommand.CommandMoveTo : FlagsAndCommand.CommandLineTo;
			vertexData.position.x = originX + Math.Cos(startAngle) * radiusX;
			vertexData.position.y = originY + Math.Sin(startAngle) * radiusY;
			yield return vertexData;

			double angle = startAngle;
			vertexData.command = FlagsAndCommand.CommandLineTo;
			while ((angle < endAngle - flatenDeltaAngle / 4) == (((int)EDirection.CounterClockWise) == 1))
			{
				angle += flatenDeltaAngle;

				vertexData.position.x = originX + Math.Cos(angle) * radiusX;
				vertexData.position.y = originY + Math.Sin(angle) * radiusY;
				yield return vertexData;
			}

			vertexData.position.x = originX + Math.Cos(endAngle) * radiusX;
			vertexData.position.y = originY + Math.Sin(endAngle) * radiusY;
			yield return vertexData;

			vertexData.command = FlagsAndCommand.CommandStop;
			yield return vertexData;
		}

		public void rewind(int unused)
		{
			m_NextPathCommand = ShapePath.FlagsAndCommand.CommandMoveTo;
			m_CurrentFlatenAngle = startAngle;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;

			if (ShapePath.is_stop(m_NextPathCommand))
			{
				return ShapePath.FlagsAndCommand.CommandStop;
			}

			if ((m_CurrentFlatenAngle < endAngle - flatenDeltaAngle / 4) != ((int)EDirection.CounterClockWise == 1))
			{
				x = originX + Math.Cos(endAngle) * radiusX;
				y = originY + Math.Sin(endAngle) * radiusY;
				m_NextPathCommand = ShapePath.FlagsAndCommand.CommandStop;

				return ShapePath.FlagsAndCommand.CommandLineTo;
			}

			x = originX + Math.Cos(m_CurrentFlatenAngle) * radiusX;
			y = originY + Math.Sin(m_CurrentFlatenAngle) * radiusY;

			m_CurrentFlatenAngle += flatenDeltaAngle;

			ShapePath.FlagsAndCommand CurrentPathCommand = m_NextPathCommand;
			m_NextPathCommand = ShapePath.FlagsAndCommand.CommandLineTo;
			return CurrentPathCommand;
		}

		private void normalize(double Angle1, double Angle2, EDirection Direction)
		{
			double ra = (Math.Abs(radiusX) + Math.Abs(radiusY)) / 2;
			flatenDeltaAngle = Math.Acos(ra / (ra + 0.125 / m_Scale)) * 2;
			if (Direction == EDirection.CounterClockWise)
			{
				while (Angle2 < Angle1)
				{
					Angle2 += Math.PI * 2.0;
				}
			}
			else
			{
				while (Angle1 < Angle2)
				{
					Angle1 += Math.PI * 2.0;
				}
				flatenDeltaAngle = -flatenDeltaAngle;
			}

			m_Direction = Direction;
			startAngle = Angle1;
			endAngle = Angle2;
			m_IsInitialized = true;
		}
	}
}