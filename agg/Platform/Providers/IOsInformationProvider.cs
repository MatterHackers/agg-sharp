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
	}
}
