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
		StreamReader openGcodeStream;

		bool readLastLineOfFile = false;
		int readLineCount = 0;
		const int MaxLinesToBuffer = 128;
		PrinterMachineInstruction[] readLinesRingBuffer = new PrinterMachineInstruction[MaxLinesToBuffer];

		public GCodeFileStreamed(string fileName)
		{
			openGcodeStream = new StreamReader(fileName);
		}

		~GCodeFileStreamed()
		{
			CloseStream();
		}

		void CloseStream()
		{
			if(openGcodeStream != null )
			{
				openGcodeStream.Close();
				openGcodeStream = null;
			}
		}

		public override int Count 
		{
			get
			{
				if (openGcodeStream != null
					&& !readLastLineOfFile)
				{
					return Math.Max(readLineCount + 1, (int)(openGcodeStream.BaseStream.Length / 14));
				}

				return readLineCount;
			}
		}

		public override double TotalSecondsInPrint
		{
			get
			{
				// We don't know, so we always return 0.
				return 0;
			}
		}
		
		public override void Insert(int indexToStartInjection, PrinterMachineInstruction printerMachineInstruction) 
		{
			using (TimedLock.Lock(this, "Adding Instruction"))
			{
				if (indexToStartInjection < readLineCount - MaxLinesToBuffer)
				{
					throw new Exception("You are asking for a line we no longer have bufferd");
				}

				// Make room for the instruction we are inserting, push all the existing instructions up
				for (int movingIndex = indexToStartInjection; movingIndex < readLineCount; movingIndex++)
				{
					readLinesRingBuffer[(movingIndex + 1) % MaxLinesToBuffer] = readLinesRingBuffer[movingIndex % MaxLinesToBuffer];
				}

				readLinesRingBuffer[indexToStartInjection % MaxLinesToBuffer] = printerMachineInstruction;
				readLineCount++;
			}
		}
		
		public override void Add(PrinterMachineInstruction printerMachineInstruction) 
		{
			throw new NotImplementedException(); 
		}
		
		public override void Clear() 
		{
			CloseStream();

			readLastLineOfFile = false;
			readLineCount = 0;
		}
		
		public override Vector2 GetWeightedCenter() 
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}
		
		public override RectangleDouble GetBounds() 
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}
		
		public override double GetFilamentCubicMm(double p) 
		{
			throw new NotImplementedException("A streamed GCode file should not need to do this. Please validate the code that is calling this.");
		}
		
		public override bool IsExtruding(int i) 
		{
			throw new NotImplementedException(); 
		}
		
		public override double GetLayerHeight() 
		{
			throw new NotImplementedException(); 
		}
		
		public override double GetFirstLayerHeight() 
		{
			throw new NotImplementedException(); 
		}
		
		public override double GetFilamentUsedMm(double p) 
		{
			throw new NotImplementedException(); 
		}

		public override double PercentComplete(int instructionIndex)
		{
			using (TimedLock.Lock(this, "Getting Percent Complete"))
			{
				if (openGcodeStream != null
					&& openGcodeStream.BaseStream.Length > 0)
				{
					return (double)openGcodeStream.BaseStream.Position / (double)openGcodeStream.BaseStream.Length * 100.0;
				}
			}

			return 100;
		}

		public override int GetInstructionIndexAtLayer(int layerIndex) 
		{
			return 0;
		}
		
		public override double GetFilamentDiamter() 
		{
			return 0;
		}
		
		public override double GetFilamentWeightGrams(double p, double density) 
		{
			return 0;
		}
		
		public override int GetLayerIndex(int instructionIndex) 
		{
			return 0;
		}
		
		public override int NumChangesInZ 
		{
			get 
			{
				return 0;
			}
		}
		
		public override PrinterMachineInstruction Instruction(int index) 
		{
			using (TimedLock.Lock(this, "Loading Instruction"))
			{
				if (index < readLineCount - MaxLinesToBuffer)
				{
					throw new Exception("You are asking for a line we no longer have bufferd");
				}

				while (index >= readLineCount)
				{
					string line = openGcodeStream.ReadLine();
					if (line == null)
					{
						readLastLineOfFile = true;
						line = "";
					}

					readLinesRingBuffer[readLineCount % MaxLinesToBuffer] = new PrinterMachineInstruction(line);
					readLineCount++;
				}
			}

			return readLinesRingBuffer[index % MaxLinesToBuffer];
		}
		
		public override double Ratio0to1IntoContainedLayer(int instructionIndex) 
		{
			throw new NotImplementedException(); 
		}
	}
}
