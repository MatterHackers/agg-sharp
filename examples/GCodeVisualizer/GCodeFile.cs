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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
    public class GCodeFile
    {
        List<int> indexOfChangeInZ = new List<int>();
        Vector2 center = Vector2.Zero;
        double parsingLastZ;

        public List<PrinterMachineInstruction> GCodeCommandQueue = new List<PrinterMachineInstruction>();

        public GCodeFile()
        {
        }

        public GCodeFile(string pathAndFileName)
        {
            this.Load(pathAndFileName);
        }

        public static GCodeFile ParseGCodeString(string gcodeContents)
        {
            DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(gcodeContents);
            ParseFileContents(null, doWorkEventArgs);
            return (GCodeFile)doWorkEventArgs.Result;
        }

        public static GCodeFile Load(Stream fileStream)
        {
            GCodeFile loadedGCode = null;
            try
            {
                string gCodeString = "";
                using (StreamReader sr = new StreamReader(fileStream))
                {
                    gCodeString = sr.ReadToEnd();
                }

                DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(gCodeString);
                ParseFileContents(null, doWorkEventArgs);
                loadedGCode = (GCodeFile)doWorkEventArgs.Result;
            }
            catch (IOException)
            {
            }

            return loadedGCode;
        }

        static public void LoadInBackground(BackgroundWorker backgroundWorker, string fileName)
        {
            if (Path.GetExtension(fileName).ToUpper() == ".GCODE")
            {
                try
                {
                    if (File.Exists(fileName))
                    {
                        string gCodeString = "";
                        using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader gcodeStream = new StreamReader(fileStream))
                            {
                                gCodeString = gcodeStream.ReadToEnd();
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
            using (FileStream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    GCodeFile loadedFile = GCodeFile.Load(streamReader.BaseStream);
                    
                    this.indexOfChangeInZ = loadedFile.indexOfChangeInZ;
                    this.center = loadedFile.center;
                    this.parsingLastZ = loadedFile.parsingLastZ;

                    this.GCodeCommandQueue = loadedFile.GCodeCommandQueue;
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
            GCodeFile loadedGCodeFile = new GCodeFile();

            int crCount = CountNumLines(gCodeString);
            int lineIndex = 0;
            foreach (string outputString in CustomSplit(gCodeString, '\n'))
            {
                string lineString = outputString.Trim();
                machineInstructionForLine = new PrinterMachineInstruction(lineString, machineInstructionForLine);
                // take off any comments before we check its length
                if (lineString.Contains(";"))
                {
                    int position = lineString.IndexOf(';');
                    if (position > -1)
                    {
                        lineString = lineString.Substring(0, position);
                    }
                }

                if (lineString.Length > 0)
                {
                    switch (lineString[0])
                    {
                        case 'M':
                            loadedGCodeFile.ParseMLine(lineString, machineInstructionForLine);
                            break;

                        case 'G':
                            loadedGCodeFile.ParseGLine(lineString, machineInstructionForLine);
                            break;

                        default:
                            break;
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
            double feedRate = 0;
            Vector3 lastPrinterPosition = new Vector3();
            double lastEPosition = 0;

            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();

            for(int lineIndex = 0; lineIndex < GCodeCommandQueue.Count; lineIndex++)
            {
                PrinterMachineInstruction instruction = GCodeCommandQueue[lineIndex];
                string line = instruction.Line;
                double maxDeltaThisLine = 0;
                PrinterMachineInstruction newLine = GCodeCommandQueue[lineIndex];
                string lineToParse = line.ToUpper().Trim();
                if (lineToParse.StartsWith("G0") || lineToParse.StartsWith("G1"))
                {
                    double newFeedRate = 0;
                    if (GetFirstNumberAfter("F", lineToParse, ref newFeedRate))
                    {
                        feedRate = newFeedRate;
                    }

                    Vector3 attemptedDestination = lastPrinterPosition;
                    GetFirstNumberAfter("X", lineToParse, ref attemptedDestination.x);
                    GetFirstNumberAfter("Y", lineToParse, ref attemptedDestination.y);
                    GetFirstNumberAfter("Z", lineToParse, ref attemptedDestination.z);

                    Vector3 deltaPosition = attemptedDestination - lastPrinterPosition;

                    double ePosition = lastEPosition;
                    GetFirstNumberAfter("E", lineToParse, ref ePosition);

                    //if (newLine.extrusionType == PrinterMachineState.MovementTypes.Absolute)
                    {
                        double deltaEPosition = Math.Abs(ePosition - lastEPosition);
                        maxDeltaThisLine = Math.Max(deltaEPosition, deltaPosition.Length);
                    }
                    //else
                    {
                        //maxDeltaThisLine = Math.Max(ePosition, deltaPosition.Length);
                    }

                    lastEPosition = ePosition;
                    lastPrinterPosition = attemptedDestination;
                }
                else if (lineToParse.StartsWith("G92"))
                {
                    double ePosition = 0;
                    if (GetFirstNumberAfter("E", lineToParse, ref ePosition))
                    {
                        lastEPosition = ePosition;
                    }
                }

                if (feedRate > 0)
                {
                    newLine.secondsThisLine = maxDeltaThisLine / (feedRate / 60);
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
                line.secondsToEndFromHere = accumulatedTime;
            }
        }

        public static int CalculateChecksum(string commandToGetChecksumFor)
        {
            int checksum = 0;
            if (commandToGetChecksumFor.Length > 0)
            {
                checksum = commandToGetChecksumFor[0];
                for (int i = 1; i < commandToGetChecksumFor.Length; i++)
                {
                    checksum ^= commandToGetChecksumFor[i];
                }
            }
            return checksum;
        }

        public static bool GetFirstStringAfter(string stringToCheckAfter, string fullStringToLookIn, string separatorString, ref string nextString, int startIndex = 0)
        {
            int stringPos = fullStringToLookIn.IndexOf(stringToCheckAfter, startIndex);
            if (stringPos != -1)
            {
                int separatorPos = fullStringToLookIn.IndexOf(separatorString, stringPos);
                if (separatorPos != -1)
                {
                    nextString = fullStringToLookIn.Substring(stringPos + stringToCheckAfter.Length, separatorPos - (stringPos + stringToCheckAfter.Length));
                    return true;
                }
            }

            return false;
        }

        const string matchDouble = @"^-*[0-9]*\.?[0-9]*";
        public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, int startIndex = 0)
        {
            int stringPos = stringWithNumber.IndexOf(stringToCheckAfter, startIndex);
            if (stringPos != -1)
            {
                string startingAfterCheckString = stringWithNumber.Substring(stringPos + stringToCheckAfter.Length).Trim();
                string matchString = Regex.Match(startingAfterCheckString, matchDouble).Value;
                return double.TryParse(matchString, out readValue);
            }

            return false;
        }

        public static string ReplaceNumberAfter(char charToReplaceAfter, string stringWithNumber, double numberToPutIn)
        {
            int charPos = stringWithNumber.IndexOf(charToReplaceAfter);
            if (charPos != -1)
            {
                int spacePos = stringWithNumber.IndexOf(" ", charPos);
                if (spacePos == -1)
                {
                    string newString = string.Format("{0}{1:0.#####}", stringWithNumber.Substring(0, charPos + 1), numberToPutIn);
                    return newString;
                }
                else
                {
                    string newString = string.Format("{0}{1:0.#####}{2}", stringWithNumber.Substring(0, charPos + 1), numberToPutIn, stringWithNumber.Substring(spacePos));
                    return newString;
                }
            }

            return stringWithNumber;
        }

        public Vector2 Center
        {
            get { return center; }
        }

        public List<int> IndexOfChangeInZ
        {
            get { return indexOfChangeInZ; }
        }

        public int NumChangesInZ
        {
            get { return indexOfChangeInZ.Count; }
        }

        void ParseMLine(string lineString, PrinterMachineInstruction processingMachineState)
        {
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

                case "82":
                    // set extruder to absolute mode
                    break;

                case "84":
                    // lineString = "M84     ; disable motors\r"
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

                case "117":
                    // in Marlin: Display Message
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

                case "301":
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

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }

        double amountOfAccumulatedE = 0;
        void ParseGLine(string lineString, PrinterMachineInstruction processingMachineState)
        {
            PrinterMachineInstruction machineStateForLine = new PrinterMachineInstruction(lineString, processingMachineState);
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
                    for (int argIndex = 1; argIndex < splitOnSpace.Length; argIndex++)
                    {
                        double value;

                        if (splitOnSpace[argIndex].Length > 0 && double.TryParse(splitOnSpace[argIndex].Substring(1), NumberStyles.Number, null, out value))
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
                                    processingMachineState.EPosition = value + amountOfAccumulatedE;
                                }
                                else
                                {
                                    processingMachineState.EPosition += value;
                                }
                            }
                            double valueF = 0;
                            if (GCodeFile.GetFirstNumberAfter("F", lineString, ref valueF))
                            {
                                processingMachineState.FeedRate = valueF;
                            }
                        }
                    }

                    if (processingMachineState.Z != parsingLastZ)
                    {
                        indexOfChangeInZ.Add(GCodeCommandQueue.Count);
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
                        amountOfAccumulatedE = (processingMachineState.EPosition - ePosition);
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

        public Vector2 GetWeightedCenter()
        {
            Vector2 total = new Vector2();
            int count = 0;

            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                total += new Vector2(state.Position.x, state.Position.y);
                count++;
            }

            return total / count;
        }

        public RectangleDouble GetBounds()
        {
            RectangleDouble bounds = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);

            foreach (PrinterMachineInstruction state in GCodeCommandQueue)
            {
                bounds.Left = Math.Min(state.Position.x, bounds.Left);
                bounds.Right = Math.Max(state.Position.x, bounds.Right);
                bounds.Bottom = Math.Min(state.Position.y, bounds.Bottom);
                bounds.Top = Math.Max(state.Position.y, bounds.Top);
            }

            return bounds;
        }

        public bool IsExtruding(int vertexIndexToCheck)
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

        public double GetFilamentUsedMm(double nozzleDiameter)
        {
            double lastEPosition = 0;
            double filamentMm = 0;
            for(int i=0; i<GCodeCommandQueue.Count; i++)
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

        public double GetFilamentCubicMm(double filamentDiameterMm)
        {
            double filamentUsedMm = GetFilamentUsedMm(filamentDiameterMm);
            double nozzleRadius = filamentDiameterMm / 2;
            double areaSquareMm = (nozzleRadius * nozzleRadius) * Math.PI;

            return areaSquareMm * filamentUsedMm;
        }

        public double GetFilamentWeightGrams(double filamentDiameterMm, double densityGramsPerCubicCm)
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
    }
}
