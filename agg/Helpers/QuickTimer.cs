﻿/*
Copyright (c) 2025, Lars Brubaker, Kevin Pope
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
using System.Diagnostics;

namespace MatterHackers.Agg
{
    /// <summary>
    /// A tiny class to allow for the quick timing of a block of code in the debugger.
    /// </summary>
    /// /// <example> 
    /// This sample shows how to use QuickTimer.
    /// <code>
    /// class SampleProgram
    /// {
    ///     static int Main() 
    ///     {
    ///			// some code we want to time
    ///			using(new QuickTimer("Time To Get Cookies")
    ///			{
    ///				GetCookies();
    ///			}
    ///			
    ///         return 1;
    ///     }
    /// }
    /// </code>
    /// </example>
    public unsafe class QuickTimer : IDisposable
    {
        private double minTimeToReport;
        private string name;
        private Stopwatch quickTimerTime = Stopwatch.StartNew();
        private double startTime;
        private double* outSeconds; // Pointer to store the address of the out parameter

        public QuickTimer(string name, double minTimeToReport = 0)
        {
            this.minTimeToReport = minTimeToReport;
            this.name = name;
            startTime = quickTimerTime.Elapsed.TotalMilliseconds;
            this.outSeconds = null;
        }

        public QuickTimer(string name, out double seconds, double minTimeToReport = 0)
        {
            this.minTimeToReport = minTimeToReport;
            this.name = name;
            startTime = quickTimerTime.Elapsed.TotalMilliseconds;

            // Store the address of the out parameter
            fixed (double* p = &seconds)
            {
                this.outSeconds = p;
            }
        }

        public void Dispose()
        {
            double totalTime = (quickTimerTime.Elapsed.TotalMilliseconds - startTime) / 1000.0;

            // Update the out parameter if it was provided
            if (outSeconds != null)
            {
                *outSeconds = totalTime;
            }

            if (totalTime > minTimeToReport)
            {
                Debug.WriteLine(name + ": {0:0.0}s".FormatWith(totalTime));
            }
        }
    }
}