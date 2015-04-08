//----------------------------------------------------------------------------
// AGG-Sharp - Version 1
// Copyright (C) 2007 Lars Brubaker http://agg-sharp.sourceforge.net/
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: larsbrubaker@gmail.com
//          http://agg-sharp.sourceforge.net/
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{
	public interface IColorType
	{
		RGBA_Floats GetAsRGBA_Floats();

		RGBA_Bytes GetAsRGBA_Bytes();

		RGBA_Bytes gradient(RGBA_Bytes c, double k);

		int Red0To255 { get; set; }

		int Green0To255 { get; set; }

		int Blue0To255 { get; set; }

		int Alpha0To255 { get; set; }

		float Red0To1 { get; set; }

		float Green0To1 { get; set; }

		float Blue0To1 { get; set; }

		float Alpha0To1 { get; set; }
	};
}