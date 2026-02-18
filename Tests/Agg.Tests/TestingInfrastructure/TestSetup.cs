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
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.Platform;
namespace MatterHackers.Agg.Tests.TestingInfrastructure
{
    public static class TestSetup
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes the automation testing infrastructure.
        /// This must be called before any GUI automation tests run.
        /// </summary>
        public static void InitializeAutomationTesting()
        {
            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    // Initialize the input method for automation testing
                    AutomationRunner.InputMethod = new WindowsInputMethods();
                    
                    // Set up automation runner defaults
                    AutomationRunner.DrawSimulatedMouse = true;
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to initialize automation testing infrastructure", ex);
                }
            }
        }

        /// <summary>
        /// Cleanup automation testing infrastructure.
        /// Called after tests complete.
        /// </summary>
        public static void CleanupAutomationTesting()
        {
            lock (_lock)
            {
                if (!_isInitialized)
                    return;

                try
                {
                    // Reset automation runner state
                    AutomationRunner.InputMethod = null;
                    
                    _isInitialized = false;
                }
                catch (Exception)
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Static constructor ensures automation infrastructure is initialized when the assembly loads.
        /// This is a simple approach that works with TUnit 0.25.21.
        /// </summary>
        static TestSetup()
        {
            InitializeAutomationTesting();
        }
    }
} 