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
#define MULTI_THREAD

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeFileStreamed : GCodeFile
	{
		string fileName;

		public GCodeFileStreamed(string fileName)
		{
			this.fileName = fileName;
		}

		public override int Count { get { throw new NotImplementedException(); } }
		public override void Insert(int indexToStartInjection, PrinterMachineInstruction printerMachineInstruction) { throw new NotImplementedException(); }
		public override void Add(PrinterMachineInstruction printerMachineInstruction) { throw new NotImplementedException(); }
		public override void Clear() { throw new NotImplementedException(); }
		public override Vector2 GetWeightedCenter() { throw new NotImplementedException(); }
		public override RectangleDouble GetBounds() { throw new NotImplementedException(); }
		public override double GetFilamentCubicMm(double p) { throw new NotImplementedException(); }
		public override bool IsExtruding(int i) { throw new NotImplementedException(); }
		public override double GetLayerHeight() { throw new NotImplementedException(); }
		public override double GetFirstLayerHeight() { throw new NotImplementedException(); }
		public override double GetFilamentUsedMm(double p) { throw new NotImplementedException(); }
		public override int GetInstructionIndexAtLayer(int layerIndex) { throw new NotImplementedException(); }
		public override double GetFilamentDiamter() { throw new NotImplementedException(); }
		public override double GetFilamentWeightGrams(double p, double density) { throw new NotImplementedException(); }
		public override int GetLayerIndex(int instructionIndex) { throw new NotImplementedException(); }
		public override int NumChangesInZ { get { throw new NotImplementedException(); } }
		public override PrinterMachineInstruction Instruction(int i) { throw new NotImplementedException(); }
		public override double Ratio0to1IntoContainedLayer(int instructionIndex) { throw new NotImplementedException(); }
	}
}
