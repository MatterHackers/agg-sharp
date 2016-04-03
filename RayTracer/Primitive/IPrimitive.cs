using MatterHackers.Agg;
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

namespace MatterHackers.RayTracer
{
	/// <summary>
	/// element in a scene
	/// </summary>
	public interface IPrimitive : ITraceable
	{
		/// <summary>
		/// Get the color for a primitive at the given info.
		/// </summary>
		/// <param name="info"></param>
		/// <returns></returns>
		RGBA_Floats GetColor(IntersectInfo info);

		/// <summary>
		/// Specifies the ambient and diffuse color of the element.
		/// </summary>
		MaterialAbstract Material { get; set; }
	}
}