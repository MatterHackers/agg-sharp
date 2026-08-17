using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.Platform
{
	public interface IOsInformationProvider
	{
		OSType OperatingSystem { get; }
		Point2D DesktopSize { get; }
		long PhysicalMemory { get; }

		/// <summary>
		/// How many device pixels the display uses per logical point: 1 on an ordinary monitor, 2 on a
		/// Retina Mac, 1.25/1.5/1.75/2 for Windows' 125%/150%/175%/200% scaling.
		/// </summary>
		/// <remarks>
		/// Purely informational - agg renders in device pixels either way, and nothing in the toolkit reads
		/// this. It exists so an application can size its <em>widgets</em> for the display it is on
		/// (<c>GuiWidget.DeviceScale</c>) instead of drawing a physically half-size UI on a 2x screen.
		/// <para>
		/// Defaulted rather than abstract so an out-of-tree provider keeps compiling; a provider that does
		/// not know reports 1, which is what every provider did before this existed.
		/// </para>
		/// </remarks>
		double DisplayScale => 1;
	}
}
