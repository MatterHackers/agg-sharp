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
// #define USE_UNSAFE // no real code for this yet

using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using static MatterHackers.Agg.ScanlineRasterizer;
using static System.Net.WebRequestMethods;

namespace MatterHackers.Agg
{
    static public class Util
    {
        //----------------------------------------------------------filling_rule_e
        public enum filling_rule_e
        {
            fill_non_zero,
            fill_even_odd
        }

        public static double GetRatio(double startRatio, double endRatio, int index, int count)
        {
            return startRatio + (endRatio - startRatio) * index / count;
        }

        public static double GetRatio(double startRatio, double endRatio, double partialRatio)
        {
            return startRatio + (endRatio - startRatio) * partialRatio;
        }


        public static void memcpy(byte[] dest, int destIndex, byte[] source, int sourceIndex, int count)
        {
#if USE_UNSAFE
#else
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = source[sourceIndex + i];
            }
#endif
        }

        public static string AddQueryPram(string url, string key, string value)
        {
            if (url.Contains("?"))
            {
                return url + "&" + key + "=" + value;
            }
            else
            {
                return url + "?" + key + "=" + value;
            }
        }

        public static double ParseDouble(string source, bool fastSimpleNumbers)
        {
            int startIndex = 0;
            return ParseDouble(source, ref startIndex, fastSimpleNumbers);
        }

        // private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+");
        private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");

        private static double GetNextNumber(string source, ref int startIndex)
        {
            Match numberMatch = numberRegex.Match(source, startIndex);
            string returnString = numberMatch.Value;
            startIndex = numberMatch.Index + numberMatch.Length;
            double returnVal;
            double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
            return returnVal;
        }

        public static void CopyFilesWithProgress(IEnumerable<string> sourceFiles,
            string destinationPath,
            Action<double, string> reporter,
            CancellationTokenSource cancellationToken,
            string copyProgressMessage = "Copying")
        {
            var count = sourceFiles.Count();
            var i = 0;
            foreach (var file in sourceFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                var destination = Path.Combine(destinationPath, Path.GetFileName(file));
                var completedRatio = (double)i / count;
                CopyFileWithProgress(file, destination, (ratio, message) => reporter?.Invoke(completedRatio + ratio / count, message), copyProgressMessage);
                i++;
            }
            reporter?.Invoke(1, copyProgressMessage);
        }

        public static void CopyFileWithProgress(string sourceFile, string destinationFile, Action<double, string> reporter, string copyProgressMessage = "Copying")
        {
            const int bufferSize = 1024 * 1024; // 1MB buffer
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
            {
                long totalBytes = sourceStream.Length;
                using (FileStream destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write))
                {
                    while ((bytesRead = sourceStream.Read(buffer, 0, bufferSize)) > 0)
                    {
                        destinationStream.Write(buffer, 0, bytesRead);
                        reporter?.Invoke((double)destinationStream.Position / totalBytes, copyProgressMessage);
                    }
                }
            }
        }

        public static double ParseDouble(string source, ref int startIndex, bool fastSimpleNumbers)
        {
#if true
            if (fastSimpleNumbers)
            {
                return ParseDoubleFast(source, ref startIndex);
            }

            return GetNextNumber(source, ref startIndex);
#else
			int startIndexNew = startIndex;
			double newNumber = agg_basics.ParseDoubleFast(source, ref startIndexNew);
			int startIndexOld = startIndex;
			double oldNumber = GetNextNumber(source, ref startIndexOld);
			if (Math.Abs(newNumber - oldNumber) > .0001
				|| startIndexNew != startIndexOld)
			{
				int a = 0;
			}

			startIndex = startIndexNew;
			return newNumber;
#endif
        }

        private static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)\\s*$", RegexOptions.Compiled);
        private static Regex fileNameUnderscoreNumberMatch = new Regex("_\\d+\\s*$", RegexOptions.Compiled);

        public static string GetNonCollidingFileName(string pathAndFilename)
        {
            string extension = Path.GetExtension(pathAndFilename);

            // get the file name without the path
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Path.GetFileName(pathAndFilename));

            var directory = Path.GetDirectoryName(pathAndFilename);
            var existingSimilarFilenames = Directory.GetFiles(directory, "*.*");
            // remove all the file extensions
            existingSimilarFilenames = existingSimilarFilenames.Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();

            var validName = Util.GetNonCollidingName(fileNameWithoutExtension, new HashSet<string>(existingSimilarFilenames));

            // return the validName with path and extension
            return Path.Combine(directory, validName) + extension;
        }

        public static string GetNonCollidingName(string desiredName, HashSet<string> listToCheck, bool lookForParens = true)
        {
            return GetNonCollidingName(desiredName, (name) => !listToCheck.Contains(name), lookForParens);
        }

        public static string GetNonCollidingName(string desiredName, Func<string, bool> isUnique, bool lookForParens = true)
        {
            if (desiredName == null)
            {
                desiredName = "No Name";
            }

            if (isUnique(desiredName))
            {
                return desiredName;
            }
            else
            {
                if (lookForParens)
                {
                    // Drop bracketed number sections from our source filename to ensure we don't generate something like "file (1) (1).amf"
                    if (desiredName.Contains("("))
                    {
                        desiredName = fileNameNumberMatch.Replace(desiredName, "").Trim();
                    }

                    int nextNumberToTry = 1;
                    string candidateName;

                    do
                    {
                        candidateName = $"{desiredName} ({nextNumberToTry++})";
                    } while (!isUnique(candidateName));

                    return candidateName;
                }
                else
                {
                    // Drop the number sections from our source filename to ensure we don't generate something like "file_1_1.amf"
                    desiredName = fileNameUnderscoreNumberMatch.Replace(desiredName, "").Trim();

                    int nextNumberToTry = 1;
                    string candidateName;

                    do
                    {
                        candidateName = $"{desiredName}_{nextNumberToTry++}";
                    } while (!isUnique(candidateName));

                    return candidateName;
                }
            }
        }

        private static double ParseDoubleFast(string source, ref int startIndex)
        {
            int length = source.Length;
            bool negative = false;
            long currentIntPart = 0;
            int fractionDigits = 0;
            long currentFractionPart = 0;
            bool foundNumber = false;

            // find the number start
            while (startIndex < length)
            {
                char next = source[startIndex];
                if (next == '.' || next == '-' || next == '+' || (next >= '0' && next <= '9'))
                {
                    if (next == '.')
                    {
                        break;
                    }

                    if (next == '-')
                    {
                        negative = true;
                    }
                    else if (next == '+')
                    {
                        // this does nothing but lets us get to the else for numbers
                    }
                    else
                    {
                        currentIntPart = next - '0';
                        foundNumber = true;
                    }

                    startIndex++;
                    break;
                }

                startIndex++;
            }

            // accumulate the int part
            while (startIndex < length)
            {
                char next = source[startIndex];
                if (next >= '0' && next <= '9')
                {
                    currentIntPart = (currentIntPart * 10) + next - '0';
                    foundNumber = true;
                }
                else if (next == '.')
                {
                    foundNumber = true;
                    startIndex++;
                    // parse out the fractional part
                    while (startIndex < length)
                    {
                        char nextFraction = source[startIndex];
                        if (nextFraction >= '0' && nextFraction <= '9')
                        {
                            fractionDigits++;
                            currentFractionPart = (currentFractionPart * 10) + nextFraction - '0';
                        }
                        else // we are done
                        {
                            break;
                        }

                        startIndex++;
                    }

                    break;
                }
                else if (!foundNumber && next == ' ')
                {
                    // happy to skip spaces
                }
                else // we are done
                {
                    break;
                }

                startIndex++;
            }

            if (fractionDigits > 0)
            {
                double fractionNumber = currentIntPart + (currentFractionPart / Math.Pow(10.0, fractionDigits));
                if (negative)
                {
                    return -fractionNumber;
                }

                return fractionNumber;
            }
            else
            {
                if (negative)
                {
                    return -currentIntPart;
                }

                return currentIntPart;
            }
        }

        public static int Clamp(int value, int min, int max)
        {
            bool changed = false;
            return Clamp(value, min, max, ref changed);
        }

        public static int Clamp(int value, int min, int max, ref bool changed)
        {
            min = Math.Min(min, max);

            if (value < min)
            {
                value = min;
                changed = true;
            }

            if (value > max)
            {
                value = max;
                changed = true;
            }

            return value;
        }

        public static double Clamp(double value, double min, double max)
        {
            bool changed = false;
            return Clamp(value, min, max, ref changed);
        }

        public static double Clamp(double value, double min, double max, ref bool changed)
        {
            min = Math.Min(min, max);

            if (value < min)
            {
                value = min;
                changed = true;
            }

            if (value > max)
            {
                value = max;
                changed = true;
            }

            return value;
        }

        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static ulong GetLongHashCode(this string data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(GetBytes(data), hash);
        }

        public static ulong GetLongHashCode(this int data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(BitConverter.GetBytes(data), hash);
        }

        public static ulong GetLongHashCode(this double data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(BitConverter.GetBytes(data), hash);
        }

        public static ulong GetLongHashCode(this ulong data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(BitConverter.GetBytes(data), hash);
        }

        public static ulong GetLongHashCode(this long data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(BitConverter.GetBytes(data), hash);
        }
        public static ulong GetLongHashCode(this byte[] data, ulong hash = 14695981039346656037)
        {
            return ComputeHash(data, hash);
        }

        // FNV-1a (64-bit) non-cryptographic hash function.
        // Adapted from: http://github.com/jakedouglas/fnv-java
        public static ulong ComputeHash(byte[] bytes, ulong hash = 14695981039346656037)
        {
            const ulong fnv64Prime = 0x100000001b3;

            for (var i = 0; i < bytes.Length; i++)
            {
                hash = hash ^ bytes[i];
                hash *= fnv64Prime;
            }

            return hash;
        }

        public static void memcpy(int[] dest, int destIndex, int[] source, int sourceIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = source[sourceIndex + i];
            }
        }

        public static void memcpy(float[] dest, int destIndex, float[] source, int sourceIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex++] = source[sourceIndex++];
            }
        }

        public static void memmove(byte[] dest, int destIndex, byte[] source, int sourceIndex, int count)
        {
            if (dest == source
                && destIndex == sourceIndex)
            {
                // there is nothing to do it is already the same
                return;
            }

            if (source != dest
                || destIndex < sourceIndex)
            {
                memcpy(dest, destIndex, source, sourceIndex, count);
            }
            else
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    dest[destIndex + i] = source[sourceIndex + i];
                }
            }
        }

        public static void memmove(int[] dest, int destIndex, int[] source, int sourceIndex, int count)
        {
            if (source != dest
                || destIndex < sourceIndex)
            {
                memcpy(dest, destIndex, source, sourceIndex, count);
            }
            else
            {
                throw new Exception("this code needs to be tested");
                /*
				for (int i = Count-1; i > 0; i--)
				{
					dest[destIndex + i] = source[sourceIndex + i];
				}
				 */
            }
        }

        public static void memmove(float[] dest, int destIndex, float[] source, int sourceIndex, int count)
        {
            if (source != dest
                || destIndex < sourceIndex)
            {
                memcpy(dest, destIndex, source, sourceIndex, count);
            }
            else
            {
                throw new Exception("this code needs to be tested");
                /*
				for (int i = Count-1; i > 0; i--)
				{
					dest[destIndex + i] = source[sourceIndex + i];
				}
				 */
            }
        }

        public static void memset(int[] dest, int destIndex, int val, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = val;
            }
        }

        public static void memset(byte[] dest, int destIndex, byte byteVal, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = byteVal;
            }
        }

        public static void MemClear(int[] dest, int destIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = 0;
            }
        }

        public static void MemClear(byte[] dest, int destIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[destIndex + i] = 0;
            }

            /*
			// dword align to dest
			while (((int)pDest & 3) != 0
				&& Count > 0)
			{
				*pDest++ = 0;
				Count--;
			}

			int NumLongs = Count / 4;

			while (NumLongs-- > 0)
			{
				*((int*)pDest) = 0;

				pDest += 4;
			}

			switch (Count & 3)
			{
				case 3:
					pDest[2] = 0;
					goto case 2;
				case 2:
					pDest[1] = 0;
					goto case 1;
				case 1:
					pDest[0] = 0;
					break;
			}
			 */
        }

        public static bool is_equal_eps(double v1, double v2, double epsilon)
        {
            return Math.Abs(v1 - v2) <= (double)epsilon;
        }

        //------------------------------------------------------------------deg2rad
        public static double deg2rad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        //------------------------------------------------------------------rad2deg
        public static double rad2deg(double rad)
        {
            return rad * 180.0 / Math.PI;
        }

        public static int iround(double v)
        {
            unchecked
            {
                return (int)((v < 0.0) ? v - 0.5 : v + 0.5);
            }
        }

        public static int iround(double v, int saturationLimit)
        {
            if (v < (double)-saturationLimit)
            {
                return -saturationLimit;
            }

            if (v > (double)saturationLimit)
            {
                return saturationLimit;
            }

            return iround(v);
        }

        public static int uround(double v)
        {
            return (int)(uint)(v + 0.5);
        }

        public static int ufloor(double v)
        {
            return (int)(uint)v;
        }

        public static int uceil(double v)
        {
            return (int)(uint)Math.Ceiling(v);
        }

        //----------------------------------------------------poly_subpixel_scale_e
        // These constants determine the subpixel accuracy, to be more precise,
        // the number of bits of the fractional part of the coordinates.
        // The possible coordinate capacity in bits can be calculated by formula:
        // sizeof(int) * 8 - poly_subpixel_shift, i.e, for 32-bit integers and
        // 8-bits fractional part the capacity is 24 bits.
        public enum poly_subpixel_scale_e
        {
            poly_subpixel_shift = 8,                      //----poly_subpixel_shift
            poly_subpixel_scale = 1 << poly_subpixel_shift, //----poly_subpixel_scale
            poly_subpixel_mask = poly_subpixel_scale - 1,  //----poly_subpixel_mask
        }

        public static ImageBuffer TrasparentToColorGradientX(int width, int height, Color color, int distance)
        {
            var innerGradient = new gradient_x();
            var outerGradient = new gradient_clamp_adaptor(innerGradient); // gradient_repeat_adaptor/gradient_reflect_adaptor/gradient_clamp_adaptor

            var rect = new RoundedRect(new RectangleDouble(0, 0, width, height), 0);

            var ras = new ScanlineRasterizer();
            ras.add_path(rect);

            var imageBuffer = new ImageBuffer(width, height);
            imageBuffer.SetRecieveBlender(new BlenderPreMultBGRA());

            var scanlineRenderer = new ScanlineRenderer();
            scanlineRenderer.GenerateAndRender(
                ras,
                new scanline_unpacked_8(),
                imageBuffer,
                new span_allocator(),
                new span_gradient(
                    new span_interpolator_linear(Affine.NewIdentity()),
                    outerGradient,
                    new GradientColors(Enumerable.Range(0, 256).Select(i => new Color(color, i))), // steps from full transparency color to solid color
                    0,
                    distance));

            return imageBuffer;
        }

        private class GradientColors : IColorFunction
        {
            private List<Color> colors;

            public GradientColors(IEnumerable<Color> colors)
            {
                this.colors = new List<Color>(colors);
            }

            public Color this[int v] => colors[v];

            public int size() => colors.Count;
        }

        public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref int readValue, int startIndex = 0, string stopCheckingString = ";")
        {
            return GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref readValue, out _, startIndex, stopCheckingString);
        }

        public static string GetLineWithoutChecksum(string inLine)
        {
            if (inLine.StartsWith("N"))
            {
                int lineNumber = 0;
                if (Util.GetFirstNumberAfter("N", inLine, ref lineNumber, out int numberEnd))
                {
                    var outLine = inLine.Substring(numberEnd).Trim();
                    int checksumStart = outLine.IndexOf('*');
                    if (checksumStart != -1)
                    {
                        return outLine.Substring(0, checksumStart);
                    }
                }
            }

            return inLine;
        }

        public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref int readValue, out int numberEnd, int startIndex = 0, string stopCheckingString = ";")
        {
            double doubleValue = readValue;
            if (GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref doubleValue, out numberEnd, startIndex, stopCheckingString))
            {
                readValue = (int)doubleValue;
                return true;
            }

            return false;
        }

        public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, int startIndex = 0, string stopCheckingString = ";")
        {
            return GetFirstNumberAfter(stringToCheckAfter, stringWithNumber, ref readValue, out _, startIndex, stopCheckingString);
        }

        public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, out int numberEnd, int startIndex = 0, string stopCheckingString = ";")
        {
            int stringPos = stringWithNumber.IndexOf(stringToCheckAfter, Math.Min(stringWithNumber.Length, startIndex));
            int stopPos = stringWithNumber.IndexOf(stopCheckingString);
            if (stringPos != -1
                && (stopPos == -1 || stringPos < stopPos || string.IsNullOrEmpty(stopCheckingString)))
            {
                stringPos += stringToCheckAfter.Length;
                readValue = Util.ParseDouble(stringWithNumber, ref stringPos, true);
                numberEnd = stringPos;

                return true;
            }

            numberEnd = -1;
            return false;
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
    }
}