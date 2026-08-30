/*
Copyright (c) 2026, Lars Brubaker
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
using System.Diagnostics;
using System.IO;

namespace Agg
{
    /// <summary>
    /// Debug logging levels from lowest to highest priority
    /// </summary>
    public enum DebugLevel
    {
        Message = 0,
        Warning = 1,
        Error = 2,
        Fatal = 3
    }

    /// <summary>
    /// Static class for handling debug logging with filter and level-based controls
    /// </summary>
    public static class DebugLogger
    {
        private static readonly HashSet<string> debugFilters = new HashSet<string>();
        private static readonly string debugLogPath = Path.Combine("C:", "Development", "MatterCAD", "debug_log.txt");

        /// <summary>
        /// Whether the log file is written at all. The path above is a DEVELOPMENT machine's checkout, which does
        /// not exist on a customer's machine - and since error logging now survives Release (see <see cref="Log"/>),
        /// this code runs there too. Creating the directory would litter an unrelated drive with a folder that
        /// looks like a source checkout, so the file leg is simply skipped when it is not already there; the
        /// Debug.WriteLine leg still runs everywhere, and a host that wants durable logs has its own crash reporting.
        /// Evaluated once: the answer cannot change usefully mid-session and the check is per-call otherwise.
        /// </summary>
        private static readonly bool logToFile = LogDirectoryExists();

        private static readonly object debugLogLock = new object();
        private static DebugLevel minimumLevel = DebugLevel.Error; // Default to Error level and above

        /// <summary>
        /// Gets or sets the minimum debug level that will be logged
        /// </summary>
        public static DebugLevel MinimumLevel
        {
            get => minimumLevel;
            set => minimumLevel = value;
        }

        /// <summary>
        /// Gets or sets whether every logged line is also written to the console.
        /// </summary>
        /// <remarks>
        /// Opt in, and off by default: an interactive application must not spray its trace over stdout. The
        /// automation runner turns it on for the duration of a test run because a test host's stdout is what
        /// ends up in the TRX, and Debug.WriteLine - the only sink otherwise - reaches a debugger that a build
        /// server does not have. Turning this on also lifts the Release level filter in <see cref="Log"/>:
        /// asking for the console echo is asking for the whole timeline, not just the errors.
        /// </remarks>
        public static bool EchoToConsole { get; set; }

        /// <summary>
        /// Enables debug logging for the specified filter category
        /// </summary>
        /// <param name="filter">Debug filter category to enable</param>
        public static void EnableFilter(string filter)
        {
            debugFilters.Add(filter);
        }

        /// <summary>
        /// Disables debug logging for the specified filter category
        /// </summary>
        /// <param name="filter">Debug filter category to disable</param>
        public static void DisableFilter(string filter)
        {
            debugFilters.Remove(filter);
        }

        /// <summary>
        /// Checks if a debug filter is enabled
        /// </summary>
        /// <param name="filter">Debug filter category to check</param>
        /// <returns>True if the filter is enabled</returns>
        public static bool IsFilterEnabled(string filter)
        {
            return debugFilters.Contains(filter);
        }

        /// <summary>
        /// Clears all debug filters
        /// </summary>
        public static void ClearFilters()
        {
            debugFilters.Clear();
        }

        /// <summary>
        /// Gets all currently enabled debug filters
        /// </summary>
        /// <returns>A copy of the enabled filters</returns>
        public static HashSet<string> GetEnabledFilters()
        {
            return new HashSet<string>(debugFilters);
        }

        private static bool LogDirectoryExists()
        {
            try
            {
                var directory = Path.GetDirectoryName(debugLogPath);

                return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
            }
            catch
            {
                // A path this class cannot even inspect is one it must not try to write.
                return false;
            }
        }

        /// <summary>
        /// Clears the debug log file
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                if (File.Exists(debugLogPath))
                {
                    File.Delete(debugLogPath);
                }
            }
            catch
            {
                // Ignore file access errors
            }
        }

        /// <summary>
        /// Gets the current debug log file path
        /// </summary>
        /// <returns>The debug log file path</returns>
        public static string GetLogPath()
        {
            return debugLogPath;
        }

        /// <summary>
        /// Logs debug information if the specified filter is enabled OR if the level meets the minimum threshold
        /// </summary>
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        /// <param name="level">Debug level (defaults to Message)</param>
        /// <remarks>
        /// NOT [Conditional("DEBUG")]. It used to be, which compiled the release filter below out along with every
        /// call site, so an error logged from the field was recorded nowhere - a failure whose cause was written
        /// only into a build nobody ships. Errors and failures now survive Release; the tracing levels do not
        /// (see <see cref="LogMessage"/> and <see cref="LogWarning"/>), and a direct Log call below Error level
        /// returns here at runtime instead.
        /// </remarks>
        public static void Log(string filter, string message, DebugLevel level = DebugLevel.Message)
        {
#if !DEBUG
            // In release builds, only log errors and failures - unless a caller has explicitly asked for the
            // console echo, which is a request for the full trace and is the only way a Release CI run can see
            // one at all.
            if (level < DebugLevel.Error && !EchoToConsole)
            {
                return;
            }
#endif

            // Log if either:
            // 1. The filter is specifically enabled, OR
            // 2. The level meets or exceeds the minimum level threshold
            bool shouldLog = debugFilters.Contains(filter) || level >= minimumLevel;

            if (shouldLog)
            {
                var levelString = level switch
                {
                    DebugLevel.Message => "MSG",
                    DebugLevel.Warning => "WARN",
                    DebugLevel.Error => "ERROR",
                    DebugLevel.Fatal => "FAIL",
                    _ => "UNKNOWN"
                };

                var logMessage = $"[{levelString}] [{filter}] {message}";
                Debug.WriteLine(logMessage);

                if (EchoToConsole)
                {
                    // Timestamped, unlike the Debug.WriteLine above: the reason this sink exists is to show a
                    // log reader how long a run sat between two lines, and a hang is only visible as a gap.
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {logMessage}");
                }

                // Also write to file with thread synchronization. Guarded and swallowed: logging is never the
                // reason a caller fails, and this now runs on machines where the log directory is missing,
                // read only, or held by another process.
                if (logToFile)
                {
                    lock (debugLogLock)
                    {
                        try
                        {
                            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                            File.AppendAllText(debugLogPath, $"{timestamp} {logMessage}\n");
                        }
                        catch
                        {
                            // Ignore file access errors
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Logs a message-level debug entry
        /// This is typically used for tracing execution flow
        /// </summary>
        /// <remarks>
        /// Compiled out of Release along with its arguments: tracing is a development tool, and Log would discard
        /// it at runtime anyway, so the call sites' string building is not worth paying for in a shipped build.
        /// </remarks>
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        [Conditional("DEBUG")]
        public static void LogMessage(string filter, string message)
        {
            Log(filter, message, DebugLevel.Message);
        }

        /// <summary>
        /// Logs a warning-level debug entry
        /// This is typically used for recoverable errors
        /// </summary>
        /// <remarks>
        /// Compiled out of Release, matching the level filter in <see cref="Log"/>: a release build records
        /// Error and above. Anything that must survive a shipped build belongs at <see cref="LogError"/>.
        /// </remarks>
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        [Conditional("DEBUG")]
        public static void LogWarning(string filter, string message)
        {
            Log(filter, message, DebugLevel.Warning);
        }

        /// <summary>
        /// Logs an error-level debug entry
        /// This is typically used for recoverable errors
        /// </summary>
        /// <remarks>
        /// Survives Release on purpose. Errors are the one level worth recording on a user's machine - a field
        /// failure with no logged cause is the reason this stopped being [Conditional("DEBUG")].
        /// </remarks>
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        public static void LogError(string filter, string message)
        {
            Log(filter, message, DebugLevel.Error);
        }

        /// <summary>
        /// Logs a fatal-level debug entry
        /// This level indicates a critical failure that will likely cause the application to terminate.
        /// </summary>
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        /// <remarks>
        /// Survives Release for the same reason as <see cref="LogError"/>, and more so: this level names the
        /// failure the application is about to die of.
        /// </remarks>
        public static void LogFatal(string filter, string message)
        {
            Log(filter, message, DebugLevel.Fatal);
        }
    }
} 