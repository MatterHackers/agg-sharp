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
	public class GCodeFileLoaded : GCodeFile
	{
		static readonly Vector4 VelocitySameAsStopMmPerS = new Vector4(8, 8, .4, 5);
		static readonly Vector4 MaxAccelerationMmPerS2 = new Vector4(1000, 1000, 100, 5000);
		static readonly Vector4 MaxVelocityMmPerS = new Vector4(500, 500, 5, 25);

		double amountOfAccumulatedEWhileParsing = 0;
		
		List<int> indexOfChangeInZ = new List<int>();
        Vector2 center = Vector2.Zero;
        double parsingLastZ;
        bool gcodeHasExplicitLayerChangeInfo = false;
        double firstLayerThickness;
        double layerThickness;

        List<PrinterMachineInstruction> GCodeCommandQueue = new List<PrinterMachineInstruction>();

        public GCodeFileLoaded(bool gcodeHasExplicitLayerChangeInfo = false)
        {
            this.gcodeHasExplicitLayerChangeInfo = gcodeHasExplicitLayerChangeInfo;
        }

		public GCodeFileLoaded(string pathAndFileName, bool gcodeHasExplicitLayerChangeInfo = false)
        {
            this.gcodeHasExplicitLayerChangeInfo = gcodeHasExplicitLayerChangeInfo;
            this.Load(pathAndFileName);
        }

        public override PrinterMachineInstruction Instruction(int index)
        {
            return GCodeCommandQueue[index];
        }

		public override int Count
        {
            get { return GCodeCommandQueue.Count; }
        }

		public override void Clear()
        {
            indexOfChangeInZ.Clear();
            GCodeCommandQueue.Clear();
        }

		public override double TotalSecondsInPrint 
		{
			get
			{
				return Instruction(0).secondsToEndFromHere;
			}
		}

        public override void Add(PrinterMachineInstruction printerMachineInstruction)
        {
            Insert(Count, printerMachineInstruction);
        }

		public override void Insert(int insertIndex, PrinterMachineInstruction printerMachineInstruction)
        {
            for (int i = 0; i < indexOfChangeInZ.Count; i++)
            {
                if (insertIndex < indexOfChangeInZ[i])
                {
                    indexOfChangeInZ[i]++;
                }
            }

            GCodeCommandQueue.Insert(insertIndex, printerMachineInstruction);
        }

        public static GCodeFile ParseGCodeString(string gcodeContents)
        {
            DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(gcodeContents);
            ParseFileContents(null, doWorkEventArgs);
            return (GCodeFile)doWorkEventArgs.Result;
        }

		public static GCodeFileLoaded Load(Stream fileStream)
        {
			GCodeFileLoaded loadedGCode = null;
            try
            {
                string gCodeString = "";
                using (StreamReader sr = new StreamReader(fileStream))
                {
                    gCodeString = sr.ReadToEnd();
                }

                DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(gCodeString);
                ParseFileContents(null, doWorkEventArgs);
                loadedGCode = (GCodeFileLoaded)doWorkEventArgs.Result;
            }
            catch (IOException)
            {
            }

            return loadedGCode;
        }

		static public new void LoadInBackground(BackgroundWorker backgroundWorker, string fileName)
        {
            if (Path.GetExtension(fileName).ToUpper() == ".GCODE")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
						if (FileTooBigToLoad(fileName))
						{
							// It is too big do the processing, report back no load.
							backgroundWorker.RunWorkerAsync(null);
							return;
						}

                        string gCodeString = "";
                        using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader gcodeStream = new StreamReader(fileStream))
                            {
                                long bytes = gcodeStream.BaseStream.Length;
                                char[] content = new char[bytes];
                                gcodeStream.Read(content, 0, (int)bytes);
                                gCodeString = new string(content);
                            }
                        }

                        backgroundWorker.DoWork += new DoWorkEventHandler(ParseFileContents);

                        backgroundWorker.RunWorkerAsync(gCodeString);
                    }
                    else
                    {
                        backgroundWorker.RunWorkerAsync(null);
                    }
                }
                catch (IOException)
                {
                }
            }
            else
            {
                backgroundWorker.RunWorkerAsync(null);
            }
        }

        public void Load(string gcodePathAndFileName)
        {
			if (!FileTooBigToLoad(gcodePathAndFileName))
			{
				using (FileStream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (StreamReader streamReader = new StreamReader(fileStream))
					{
						GCodeFileLoaded loadedFile = GCodeFileLoaded.Load(streamReader.BaseStream);

						this.indexOfChangeInZ = loadedFile.indexOfChangeInZ;
						this.center = loadedFile.center;
						this.parsingLastZ = loadedFile.parsingLastZ;

						this.GCodeCommandQueue = loadedFile.GCodeCommandQueue;
					}
				}
			}
        }

        private static IEnumerable<string> CustomSplit(string newtext, char splitChar)
        {
            int endOfLastFind = 0;
            int positionOfSplitChar = newtext.IndexOf(splitChar);
            while (positionOfSplitChar != -1)
            {
                string text = newtext.Substring(endOfLastFind, positionOfSplitChar - endOfLastFind).Trim();
                yield return text;
                endOfLastFind = positionOfSplitChar + 1;
                positionOfSplitChar = newtext.IndexOf(splitChar, endOfLastFind);
            }

            string lastText = newtext.Substring(endOfLastFind);
            yield return lastText;
        }

        private static int CountNumLines(string gCodeString)
        {
            int crCount = 0;
            foreach (char testCharacter in gCodeString)
            {
                if (testCharacter == '\n')
                {
                    crCount++;
                }
            }

            return crCount + 1;
        }

        public static void ParseFileContents(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string gCodeString = (string)doWorkEventArgs.Argument;
            if (gCodeString == null)
            {
                return;
            }

            BackgroundWorker backgroundWorker = sender as BackgroundWorker;

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();
            PrinterMachineInstruction machineInstructionForLine = new PrinterMachineInstruction("None");

            bool gcodeHasExplicitLayerChangeInfo = false;
            if (gCodeString.Contains("; LAYER:"))
            {
                gcodeHasExplicitLayerChangeInfo = true;
            }

			GCodeFileLoaded loadedGCodeFile = new GCodeFileLoaded(gcodeHasExplicitLayerChangeInfo);

            int crCount = CountNumLines(gCodeString);
            int lineIndex = 0;
            foreach (string outputString in CustomSplit(gCodeString, '\n'))
            {
                string lineString = outputString.Trim();
                machineInstructionForLine = new PrinterMachineInstruction(lineString, machineInstructionForLine);

                if (lineString.Length > 0)
                {
                    switch (lineString[0])
                    {
                        case 'G':
                            loadedGCodeFile.ParseGLine(lineString, machineInstructionForLine);
                            break;

                        case 'M':
                            loadedGCodeFile.ParseMLine(lineString, machineInstructionForLine);
                            break;

                        case 'T':
                            double extruderIndex = 0;
                            if (GetFirstNumberAfter("T", lineString, ref extruderIndex))
                            {
                                machineInstructionForLine.ExtruderIndex = (int)extruderIndex;
                            }
                            break;

                        case ';':
                            if (gcodeHasExplicitLayerChangeInfo && lineString.StartsWith("; LAYER:"))
                            {
                                loadedGCodeFile.IndexOfChangeInZ.Add(loadedGCodeFile.GCodeCommandQueue.Count);
                            }
                            if(lineString.StartsWith("; layerThickness"))
                            {
                                loadedGCodeFile.layerThickness = double.Parse(lineString.Split('=')[1]);
                            }
                            else if(lineString.StartsWith("; firstLayerThickness"))
                            {
                                loadedGCodeFile.firstLayerThickness = double.Parse(lineString.Split('=')[1]);
                            }
                            break;

                        case '@':
                            break;

                        default:
#if DEBUG
                            throw new NotImplementedException();
#else
                            break;
#endif
                    }
                }

                loadedGCodeFile.GCodeCommandQueue.Add(machineInstructionForLine);

                if (backgroundWorker != null)
                {
                    if (backgroundWorker.CancellationPending)
                    {
                        return;
                    }

                    if (backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                    {
                        backgroundWorker.ReportProgress(lineIndex * 100 / crCount / 2);
                        maxProgressReport.Restart();
                    }
                }

                lineIndex++;
            }

            loadedGCodeFile.AnalyzeGCodeLines(backgroundWorker);

            doWorkEventArgs.Result = loadedGCodeFile;
        }

        void AnalyzeGCodeLines(BackgroundWorker backgroundWorker = null)
        {
            double feedRateMmPerMin = 0;
            Vector3 lastPrinterPosition = new Vector3();
            double lastEPosition = 0;

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();

            for (int lineIndex = 0; lineIndex < GCodeCommandQueue.Count; lineIndex++)
            {
                PrinterMachineInstruction instruction = GCodeCommandQueue[lineIndex];
                string line = instruction.Line;
                Vector3 deltaPositionThisLine = new Vector3();
                double deltaEPositionThisLine = 0;
                PrinterMachineInstruction newLine = GCodeCommandQueue[lineIndex];
                string lineToParse = line.ToUpper().Trim();
                if (lineToParse.StartsWith("G0") || lineToParse.StartsWith("G1"))
                {
                    double newFeedRateMmPerMin = 0;
                    if (GetFirstNumberAfter("F", lineToParse, ref newFeedRateMmPerMin))
                    {
                        feedRateMmPerMin = newFeedRateMmPerMin;
                    }

                    Vector3 attemptedDestination = lastPrinterPosition;
                    GetFirstNumberAfter("X", lineToParse, ref attemptedDestination.x);
                    GetFirstNumberAfter("Y", lineToParse, ref attemptedDestination.y);
                    GetFirstNumberAfter("Z", lineToParse, ref attemptedDestination.z);

                    double ePosition = lastEPosition;
                    GetFirstNumberAfter("E", lineToParse, ref ePosition);

                    deltaPositionThisLine = attemptedDestination - lastPrinterPosition;
                    deltaEPositionThisLine = Math.Abs(ePosition - lastEPosition);

                    lastPrinterPosition = attemptedDestination;
                    lastEPosition = ePosition;
                }
                else if (lineToParse.StartsWith("G92"))
                {
                    double ePosition = 0;
                    if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
                    {
                        lastEPosition = ePosition;
                    }
                }

                if (feedRateMmPerMin > 0)
                {
                    newLine.secondsThisLine = (float)GetSecondsThisLine(lineIndex, deltaPositionThisLine, deltaEPositionThisLine, feedRateMmPerMin);
                }

                if (backgroundWorker != null)
                {
                    if (backgroundWorker.CancellationPending)
                    {
                        return;
                    }

                    if (backgroundWorker.WorkerReportsProgress && maxProgressReport.ElapsedMilliseconds > 200)
                    {
                        backgroundWorker.ReportProgress(lineIndex * 100 / GCodeCommandQueue.Count / 2 + 50);
                        maxProgressReport.Restart();
                    }
                }
                lineIndex++;
            }

            double accumulatedTime = 0;
            for (int i = GCodeCommandQueue.Count - 1; i >= 0; i--)
            {
                PrinterMachineInstruction line = GCodeCommandQueue[i];
                accumulatedTime += line.secondsThisLine;
                line.secondsToEndFromHere = (float)accumulatedTime;
            }
        }

        private double GetSecondsThisLine(int lineIndex, Vector3 deltaPositionThisLine, double deltaEPositionThisLine, double feedRateMmPerMin)
        {
            double startingVelocityMmPerS = VelocitySameAsStopMmPerS.x;
            double endingVelocityMmPerS = VelocitySameAsStopMmPerS.x;
            double maxVelocityMmPerS = Math.Min(feedRateMmPerMin / 60, MaxVelocityMmPerS.x);
            double acceleration = MaxAccelerationMmPerS2.x;
            double lengthOfThisMoveMm = Math.Max(deltaPositionThisLine.Length, deltaEPositionThisLine);

            double distanceToMaxVelocity = GetDistanceToReachEndingVelocity(startingVelocityMmPerS, maxVelocityMmPerS, acceleration);
            if (distanceToMaxVelocity <= lengthOfThisMoveMm / 2)
            {
                // we will reach max velocity then run at it and then decelerate
                double accelerationTime = GetTimeToAccelerateDistance(startingVelocityMmPerS, distanceToMaxVelocity, acceleration) * 2;
                double runningTime = (lengthOfThisMoveMm - (distanceToMaxVelocity * 2)) / maxVelocityMmPerS;
                return accelerationTime + runningTime;
            }
            else
            {
                // we will accelerate to the center then decelerate
                double accelerationTime = GetTimeToAccelerateDistance(startingVelocityMmPerS, lengthOfThisMoveMm/2, acceleration) * 2;
                return accelerationTime;
            }
        }

        double GetTimeToAccelerateDistance(double startingVelocityMmPerS, double distanceMm, double accelerationMmPerS2)
        {
            // d = vi * t + .5 * a * t^2;
            // t = (√(vi^2+2ad)-vi)/a
            double startingVelocityMmPerS2 = startingVelocityMmPerS * startingVelocityMmPerS;
            double distanceAcceleration2 = 2 * accelerationMmPerS2 * distanceMm;
            return (Math.Sqrt(startingVelocityMmPerS2 + distanceAcceleration2) - startingVelocityMmPerS) / accelerationMmPerS2;
        }

        double GetDistanceToReachEndingVelocity(double startingVelocityMmPerS, double endingVelocityMmPerS, double accelerationMmPerS2)
        {
            double endingVelocityMmPerS2 = endingVelocityMmPerS * endingVelocityMmPerS;
            double startingVelocityMmPerS2 = startingVelocityMmPerS * startingVelocityMmPerS;
            return (endingVelocityMmPerS2 - startingVelocityMmPerS2) / (2.0 * accelerationMmPerS2);
        }

        public Vector2 Center
        {
            get { return center; }
        }

		public override int GetInstructionIndexAtLayer(int layerIndex)
		{
			return IndexOfChangeInZ[layerIndex];
		}

        List<int> IndexOfChangeInZ
        {
            get { return indexOfChangeInZ; }
        }

		public override int NumChangesInZ
        {
            get { return indexOfChangeInZ.Count; }
        }

        void ParseMLine(string lineString, PrinterMachineInstruction processingMachineState)
        {
            // take off any comments before we check its length
            int commentIndex = lineString.IndexOf(';');
            if (commentIndex != -1)
            {
                lineString = lineString.Substring(0, commentIndex);
            }

            string[] splitOnSpace = lineString.Split(' ');
            switch (splitOnSpace[0].Substring(1).Trim())
            {
                case "01":
                    // show a message?
                    break;

                case "6":
                    // wait for tool to heat up (wait for condition?)
                    break;

                case "101":
                    // extrude on, forward
                    break;

                case "18":
                    // turn off stepers
                    break;

                case "42":
                    // Stop on material exhausted / Switch I/O pin
                    break;

                case "73":
                    // makerbot, Manually set build percentage
                    break;

                case "82":
                    // set extruder to absolute mode
                    break;

                case "84":
                    // lineString = "M84     ; disable motors\r"
                    break;

                case "92":
                    // set steps per mm
                    break;

                case "102":
                    // extrude on reverse
                    break;

                case "103":
                    // extrude off
                    break;

                case "104":
                    // set extruder tempreature
                    break;

                case "105":
                    // M105 Custom code for temperature reading. (Not used)
                    break;

                case "106":
                    // turn fan on
                    break;

                case "107":
                    // turn fan off
                    break;

                case "108":
                    // set extruder speed
                    break;

                case "109":
                    // set heated platform temperature
                    break;

                case "114":
                    break;

                case "117":
                    // in Marlin: Display Message
                    break;

                case "126":
                    // enable fan (makerbot)
                    break;

                case "127":
                    // disable fan (makerbot)
                    break;

                case "132":
                    // recall stored home offsets for axis xyzab
                    break;

                case "140":
                    // set bed temperature
                    break;

                case "190":
                    // wait for bed temperature to be reached
                    break;

                case "200":
                    // M200 sets the filament diameter.
                    break;

                case "201":
                    // set axis acceleration
                    break;

                case "204": // - Set default acceleration
                    break;

                case "207": // M207: calibrate z axis by detecting z max length
                    break;

                case "208": // M208: set axis max travel
                    break;

                case "209": // M209: enable automatic retract
                    break;

                case "210": // Set homing rate
                    break;

                case "226": // user request pause
                    break;

                case "227": // Enable Automatic Reverse and Prime
                    break;

                case "301":
                    break;

                case "565": // M565: Set Z probe offset
                    break;

#if DEBUG
                default:
                    throw new NotImplementedException(lineString);
#else
                default:
                    break;
#endif
            }
        }

        void ParseGLine(string lineString, PrinterMachineInstruction processingMachineState)
        {
            // take off any comments before we check its length
            int commentIndex = lineString.IndexOf(';');
            if (commentIndex != -1)
            {
                lineString = lineString.Substring(0, commentIndex);
            }

            string[] splitOnSpace = lineString.Split(' ');
            string onlyNumber = splitOnSpace[0].Substring(1).Trim();
            switch (onlyNumber)
            {
                case "0":
                    goto case "1";

                case "4":
                case "04":
                    // wait a given number of miliseconds
                    break;

                case "1":
                    // get the x y z to move to
                    {
                        double valueX = 0;
                        if (GCodeFile.GetFirstNumberAfter("X", lineString, ref valueX))
                        {
                            processingMachineState.X = valueX;
                        }
                        double valueY = 0;
                        if (GCodeFile.GetFirstNumberAfter("Y", lineString, ref valueY))
                        {
                            processingMachineState.Y = valueY;
                        }
                        double valueZ = 0;
                        if (GCodeFile.GetFirstNumberAfter("Z", lineString, ref valueZ))
                        {
                            processingMachineState.Z = valueZ;
                        }
                        double valueE = 0;
                        if (GCodeFile.GetFirstNumberAfter("E", lineString, ref valueE))
                        {
                            if (processingMachineState.movementType == PrinterMachineInstruction.MovementTypes.Absolute)
                            {
                                processingMachineState.EPosition = valueE + amountOfAccumulatedEWhileParsing;
                            }
                            else
                            {
                                processingMachineState.EPosition += valueE;
                            }
                        }
                        double valueF = 0;
                        if (GCodeFile.GetFirstNumberAfter("F", lineString, ref valueF))
                        {
                            processingMachineState.FeedRate = valueF;
                        }
                    }

                    if (!gcodeHasExplicitLayerChangeInfo)
                    {
                        if (processingMachineState.Z != parsingLastZ || indexOfChangeInZ.Count == 0)
                        {
                            // if we changed z or there is a movement and we have never started a layer index
                            indexOfChangeInZ.Add(GCodeCommandQueue.Count);
                        }
                    }
                    parsingLastZ = processingMachineState.Position.z;
                    break;

                case "10": // firmware retract
                    break;

                case "11": // firmware unretract
                    break;

                case "21":
                    // set to metric
                    break;

                case "28":
                    // G28 	Return to home position (machine zero, aka machine reference point)
                    break;

                case "29":
                    // G29 Probe the z-bed in 3 places
                    break;

                case "30":
                    // G30 Probe z in current position
                    break;

                case "90": // G90 is Absolute Distance Mode
                    processingMachineState.movementType = PrinterMachineInstruction.MovementTypes.Absolute;
                    break;

                case "91": // G91 is Incremental Distance Mode
                    processingMachineState.movementType = PrinterMachineInstruction.MovementTypes.Relative;
                    break;

                case "92":
                    // set current head position values (used to reset origin)
                    double ePosition = 0;
                    if (GetFirstNumberAfter("E", lineString, ref ePosition))
                    {
                        // remember how much e position we just gave up
                        amountOfAccumulatedEWhileParsing = (processingMachineState.EPosition - ePosition);
                    }
                    break;

                case "161":
                    // home x,y axis minimum
                    break;

                case "162":
                    // home z axis maximum
                    break;

#if DEBUG
                default:
                    throw new NotImplementedException();
#else
                default:
                    break;
#endif
            }
        }

		public override Vector2 GetWeightedCenter()
        {
            Vector2 total = new Vector2();
#if !MULTI_THREAD
            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                total += new Vector2(state.Position.x, state.Position.y);
            }
#else
            Parallel.For<Vector2>(
                0,
                GCodeCommandQueue.Count,
                () => new Vector2(),
                (int index, ParallelLoopState loop, Vector2 subtotal) =>
                {
                    PrinterMachineInstruction state = GCodeCommandQueue[index];
                    subtotal += new Vector2(state.Position.x, state.Position.y);
                    return subtotal;
                },
                    (x) =>
                    {
                        total += new Vector2(x.x, x.y);
                    }
            );
#endif      

            return total / GCodeCommandQueue.Count;
        }

        public override RectangleDouble GetBounds()
        {
            RectangleDouble bounds = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
#if !MULTI_THREAD
            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                bounds.Left = Math.Min(state.Position.x, bounds.Left);
                bounds.Right = Math.Max(state.Position.x, bounds.Right);
                bounds.Bottom = Math.Min(state.Position.y, bounds.Bottom);
                bounds.Top = Math.Max(state.Position.y, bounds.Top);
            }
#else
            Parallel.For<RectangleDouble>(
                0, 
                GCodeCommandQueue.Count, 
                () => new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue), 
                (int index, ParallelLoopState loop, RectangleDouble subtotal) =>
                    {
                        PrinterMachineInstruction state = GCodeCommandQueue[index];
                        subtotal.Left = Math.Min(state.Position.x, subtotal.Left);
                        subtotal.Right = Math.Max(state.Position.x, subtotal.Right);
                        subtotal.Bottom = Math.Min(state.Position.y, subtotal.Bottom);
                        subtotal.Top = Math.Max(state.Position.y, subtotal.Top);

                        return subtotal;
                    },
                    (x) => 
                        {
                            bounds.Left = Math.Min(x.Left, bounds.Left);
                            bounds.Right = Math.Max(x.Right, bounds.Right);
                            bounds.Bottom = Math.Min(x.Bottom, bounds.Bottom);
                            bounds.Top = Math.Max(x.Top, bounds.Top);
                        }
            );
#endif      
            return bounds;
        }

		public override bool IsExtruding(int vertexIndexToCheck)
        {
            if (vertexIndexToCheck > 1 && vertexIndexToCheck < GCodeCommandQueue.Count)
            {
                double extrusionLengeth = GCodeCommandQueue[vertexIndexToCheck].EPosition - GCodeCommandQueue[vertexIndexToCheck - 1].EPosition;
                if (extrusionLengeth > 0)
                {
                    return true;
                }
            }

            return false;
        }

		public override double GetFilamentUsedMm(double nozzleDiameter)
        {
            double lastEPosition = 0;
            double filamentMm = 0;
            for (int i = 0; i < GCodeCommandQueue.Count; i++)
            {
                PrinterMachineInstruction instruction = GCodeCommandQueue[i];
                //filamentMm += instruction.EPosition;

                string lineToParse = instruction.Line;
                if (lineToParse.StartsWith("G0") || lineToParse.StartsWith("G1"))
                {
                    double ePosition = lastEPosition;
                    GetFirstNumberAfter("E", lineToParse, ref ePosition);

                    if (instruction.movementType == PrinterMachineInstruction.MovementTypes.Absolute)
                    {
                        double deltaEPosition = ePosition - lastEPosition;
                        filamentMm += deltaEPosition;
                    }
                    else
                    {
                        filamentMm += ePosition;
                    }

                    lastEPosition = ePosition;
                }
                else if (lineToParse.StartsWith("G92"))
                {
                    double ePosition = 0;
                    if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
                    {
                        lastEPosition = ePosition;
                    }
                }
            }

            return filamentMm;
        }

		public override double GetFilamentCubicMm(double filamentDiameterMm)
        {
            double filamentUsedMm = GetFilamentUsedMm(filamentDiameterMm);
            double fillamentRadius = filamentDiameterMm / 2;
            double areaSquareMm = (fillamentRadius * fillamentRadius) * Math.PI;

            return areaSquareMm * filamentUsedMm;
        }

		public override double GetFilamentWeightGrams(double filamentDiameterMm, double densityGramsPerCubicCm)
        {
            double cubicMmPerCubicCm = 1000;
            double gramsPerCubicMm = densityGramsPerCubicCm / cubicMmPerCubicCm;
            double cubicMms = GetFilamentCubicMm(filamentDiameterMm);
            return cubicMms * gramsPerCubicMm;
        }

        public void Save(string dest)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(dest))
            {
                foreach (PrinterMachineInstruction instruction in GCodeCommandQueue)
                {
                    file.WriteLine(instruction.Line);
                }
            }
        }

		public override double GetFilamentDiamter()
        {
            return 3;
        }

		public override double GetLayerHeight()
        {
            if (layerThickness > 0)
            {
                return layerThickness;
            }

            if (indexOfChangeInZ.Count > 2)
            {
                return GCodeCommandQueue[IndexOfChangeInZ[2]].Z - GCodeCommandQueue[IndexOfChangeInZ[1]].Z;
            }

            return .5;
        }

		public override double GetFirstLayerHeight()
        {
            if (firstLayerThickness > 0)
            {
                return firstLayerThickness;
            }

            if (indexOfChangeInZ.Count > 1)
            {
                return GCodeCommandQueue[IndexOfChangeInZ[1]].Z - GCodeCommandQueue[IndexOfChangeInZ[0]].Z;
            }

            return .5;
        }

		public override int GetLayerIndex(int instructionIndex)
		{
			if (instructionIndex >= 0
				&& instructionIndex < Count)
			{
				for (int zIndex = 0; zIndex < NumChangesInZ; zIndex++)
				{
					if (instructionIndex < IndexOfChangeInZ[zIndex])
					{
						return zIndex;
					}
				}

				return NumChangesInZ - 1;
			}

			return -1;
		}

		public override double Ratio0to1IntoContainedLayer(int instructionIndex)
		{
			int currentLayer = GetLayerIndex(instructionIndex);

			if (currentLayer > -1)
			{
				int startIndex = 0;
				if (currentLayer > 0)
				{
					startIndex = IndexOfChangeInZ[currentLayer - 1];
				}
				int endIndex = Count - 1;
				if (currentLayer < NumChangesInZ - 1)
				{
					endIndex = IndexOfChangeInZ[currentLayer];
				}

				int deltaFromStart = Math.Max(0, instructionIndex - startIndex);
				return deltaFromStart / (double)(endIndex - startIndex);
			}

			return 0;
		}
	}
}
