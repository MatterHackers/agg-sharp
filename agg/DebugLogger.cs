/*
Copyright (c) 2025, Lars Brubaker
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
        [Conditional("DEBUG")]
        public static void Log(string filter, string message, DebugLevel level = DebugLevel.Message)
        {
#if !DEBUG
            // In release builds, only log errors and failures
            if (level < DebugLevel.Error)
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

                // Also write to file with thread synchronization
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

        /// <summary>
        /// Logs a message-level debug entry
        /// This is typically used for tracing execution flow
        /// </summary>
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
        /// <param name="filter">Debug filter category</param>
        /// <param name="message">Debug message</param>
        [Conditional("DEBUG")]
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
        [Conditional("DEBUG")]
        public static void LogFatal(string filter, string message)
        {
            Log(filter, message, DebugLevel.Fatal);
        }
    }
} 