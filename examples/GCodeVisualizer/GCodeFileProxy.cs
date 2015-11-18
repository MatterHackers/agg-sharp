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
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeFileProxy : GCodeFile
	{
		GCodeFile proxiedGCode;
		public GCodeFileProxy(GCodeFile proxiedGCode)
		{
			this.proxiedGCode = proxiedGCode;
		}

		#region Proxied Functions
		// the number of lines in the file
		public override int LineCount { get { return proxiedGCode.LineCount; } }

		public override int NumChangesInZ { get { return proxiedGCode.NumChangesInZ; } }
		public override double TotalSecondsInPrint { get; }
		public override void Add(PrinterMachineInstruction printerMachineInstruction) { proxiedGCode.Add(printerMachineInstruction); }

		public override void Clear() { proxiedGCode.Clear(); }

		public override RectangleDouble GetBounds()
		{
			return proxiedGCode.GetBounds();
		}

		public override double GetFilamentCubicMm(double filamentDiameter)
		{
			return proxiedGCode.GetFilamentCubicMm(filamentDiameter);
		}

		public override double GetFilamentDiameter()
		{
			return proxiedGCode.GetFilamentDiameter();
		}

		public override double GetFilamentUsedMm(double filamentDiameter)
		{
			return proxiedGCode.GetFilamentUsedMm(filamentDiameter);
		}
		
		public override double GetFilamentWeightGrams(double filamentDiameterMm, double density)
		{
			return proxiedGCode.GetFilamentWeightGrams(filamentDiameterMm, density);
		}

		public override double GetFirstLayerHeight()
		{
			return proxiedGCode.GetFirstLayerHeight();
		}

		public override int GetInstructionIndexAtLayer(int layerIndex)
		{
			return proxiedGCode.GetInstructionIndexAtLayer(layerIndex);
		}

		public override double GetLayerHeight()
		{
			return proxiedGCode.GetLayerHeight();
		}

		public override int GetLayerIndex(int instructionIndex)
		{
			return proxiedGCode.GetLayerIndex(instructionIndex);
		}

		public override Vector2 GetWeightedCenter()
		{
			return proxiedGCode.GetWeightedCenter();
		}

		public override void Insert(int indexToStartInjection, PrinterMachineInstruction printerMachineInstruction)
		{
			proxiedGCode.Insert(indexToStartInjection, printerMachineInstruction);
		}
		public override PrinterMachineInstruction Instruction(int i)
		{
			return proxiedGCode.Instruction(i);
		}

		public override bool IsExtruding(int instructionIndexToCheck)
		{
			return proxiedGCode.IsExtruding(instructionIndexToCheck);
		}
		public override double PercentComplete(int instructionIndex)
		{
			return proxiedGCode.PercentComplete(instructionIndex);
		}

		public override double Ratio0to1IntoContainedLayer(int instructionIndex)
		{
			return proxiedGCode.Ratio0to1IntoContainedLayer(instructionIndex);
		}
		#endregion Abstract Functions
	}
}