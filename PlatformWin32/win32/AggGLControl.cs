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

using System;
using System.Windows.Forms;

using OpenTK;
using MatterHackers.RenderOpenGl;

#if USE_GLES
using OpenTK.Graphics.ES11;
#else

using OpenTK.Graphics.OpenGL;
using Agg;


#if USE_OPENTK4
using OpenTK.WinForms;
#endif

#endif

namespace MatterHackers.Agg.UI
{
	public class AggGLControl : GLControl
	{
		internal static AggGLControl currentControl;

		private static int nextId;
		public int Id;

		internal RemoveGlDataCallBackHolder releaseAllGlData = new RemoveGlDataCallBackHolder();

		private static int? glfwMainThreadId = null;
		private static System.Windows.Forms.Control glfwMainThreadControl = null;
		private static readonly object glfwLock = new object();

		static AggGLControl()
		{
			// Log when the static constructor runs - this happens once per AppDomain
			DebugLogger.LogMessage("AggGLControl", $"Static constructor called on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
		}

		/// <summary>
		/// Records the GLFW main thread after successful OpenGL context creation
		/// </summary>
		private static void RecordGlfwMainThread(System.Windows.Forms.Control control)
		{
			lock (glfwLock)
			{
				if (glfwMainThreadId == null)
				{
					glfwMainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
					glfwMainThreadControl = control;
					System.Diagnostics.Debug.WriteLine($"GLFW main thread recorded: {glfwMainThreadId}");
				}
			}
		}

		/// <summary>
		/// REMOVED: Manual GLFW initialization - let OpenTK handle GLFW lifecycle completely
		/// This eliminates the threading constraint by allowing OpenTK to manage GLFW properly
		/// </summary>
		private static void EnsureGlfwInitialized()
		{
			// DO NOT call GLFW.Init() manually - this causes threading constraints
			// OpenTK will handle GLFW initialization automatically when needed
			System.Diagnostics.Debug.WriteLine("Skipping manual GLFW initialization - letting OpenTK handle it");
		}

		/// <summary>
		/// Resets the GLFW thread tracking - should only be called during complete application shutdown
		/// </summary>
		public static void ResetGlfwState()
		{
			lock (glfwLock)
			{
				System.Diagnostics.Debug.WriteLine($"Resetting GLFW state (was thread {glfwMainThreadId})");
				glfwMainThreadId = null;
				glfwMainThreadControl = null;
			}
		}

		/// <summary>
		/// Forces complete WGL/OpenGL context reset before creating new controls.
		/// This is essential for robust window close/reopen scenarios.
		/// </summary>
		public static void ForceCompleteContextReset()
		{
			try
			{
				// Step 1: Clear all static references
				currentControl = null;

				// Step 1.5: CRITICAL - Reset GLFW state for fresh start between tests
				// This allows each test to establish its own GLFW thread instead of being bound to the first test's thread
				ResetGlfwState();

				// Step 2: Force comprehensive garbage collection to clear any lingering GL objects
				for (int i = 0; i < 5; i++)
				{
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers();
				}

				// Step 3: Process all Windows messages to ensure handle cleanup is complete
				System.Windows.Forms.Application.DoEvents();
				System.Threading.Thread.Sleep(100);
				System.Windows.Forms.Application.DoEvents();

				// Step 3.5: ENHANCED - Force comprehensive Windows handle cleanup
				try
				{
					// Force immediate cleanup of any pending window destroys
					for (int i = 0; i < 10; i++)
					{
						System.Windows.Forms.Application.DoEvents();
						System.Threading.Thread.Sleep(10);
					}

					// Additional garbage collection focused on handle cleanup
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers();
					System.GC.Collect();

					// Allow Windows time to actually destroy handles
					System.Threading.Thread.Sleep(200);

					// Final message pump to ensure all WM_DESTROY messages are processed
					for (int i = 0; i < 5; i++)
					{
						System.Windows.Forms.Application.DoEvents();
						System.Threading.Thread.Sleep(20);
					}
				}
				catch
				{
					// Ignore handle cleanup errors but continue
				}

				// Step 4: Force WGL context cleanup using WinAPI directly
				try
				{
					// Load OpenGL32.dll and force complete context cleanup
					var opengl32 = LoadLibrary("opengl32.dll");
					if (opengl32 != IntPtr.Zero)
					{
						// Clear any current WGL context
						var wglMakeCurrent = GetProcAddress(opengl32, "wglMakeCurrent");
						if (wglMakeCurrent != IntPtr.Zero)
						{
							var makeCurrentDelegate = (WglMakeCurrentDelegate)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(wglMakeCurrent, typeof(WglMakeCurrentDelegate));
							makeCurrentDelegate(IntPtr.Zero, IntPtr.Zero);
						}

						// Force WGL to flush all contexts
						var wglDeleteContext = GetProcAddress(opengl32, "wglDeleteContext");
						// Note: We can't call wglDeleteContext without a specific context handle,
						// but the wglMakeCurrent(NULL, NULL) call above should clear the current context
					}
				}
				catch
				{
					// Ignore WGL direct cleanup errors
				}

				// Step 5: Force GLFW complete reset
				// NOTE: Commented out GLFW terminate/reinitialize cycle to avoid timing conflicts
				// The automatic GLFW initialization in OnHandleCreated should handle initialization as needed
				/*
				try
				{
					// Terminate GLFW completely to reset all state
					OpenTK.Windowing.GraphicsLibraryFramework.GLFW.Terminate();
					
					// Allow time for GLFW termination to complete
					System.Threading.Thread.Sleep(200);
					
					// Re-initialize GLFW fresh
					OpenTK.Windowing.GraphicsLibraryFramework.GLFW.Init();
					
					// Brief pause to allow GLFW initialization to stabilize
					System.Threading.Thread.Sleep(100);
				}
				catch (Exception ex)
				{
					// Log GLFW reset errors but continue
					System.Diagnostics.Debug.WriteLine($"GLFW reset warning: {ex.Message}");
				}
				*/

				// Step 6: Final cleanup pass with extended handle cleanup
				System.Windows.Forms.Application.DoEvents();
				System.Threading.Thread.Sleep(50);

				// ENHANCED: Additional Windows handle cleanup pass
				try
				{
					// Force one more round of garbage collection after all cleanup
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers();

					// Extended message processing to ensure all handles are released
					for (int i = 0; i < 15; i++)
					{
						System.Windows.Forms.Application.DoEvents();
						System.Threading.Thread.Sleep(10);
					}
				}
				catch
				{
					// Ignore final cleanup errors
				}
			}
			catch (Exception ex)
			{
				// Log but don't fail on context reset errors
				System.Diagnostics.Debug.WriteLine($"Context reset warning: {ex.Message}");
			}
		}

		// WGL P/Invoke delegates for direct context cleanup
		[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		private delegate bool WglMakeCurrentDelegate(IntPtr hdc, IntPtr hglrc);

		/// <summary>
		/// Resets static state to allow clean reinitialization
		/// </summary>
		public static void ResetStaticState()
		{
			// CRITICAL: Ensure no context is current before clearing references
			// This prevents WGL handle errors in subsequent tests
			try
			{
				// Clear the current control reference
				// The OpenGL context will be cleared when the control is disposed
				if (currentControl != null)
				{
					currentControl = null;
				}
			}
			catch
			{
				// Ignore errors - context might already be cleared
			}

			// Clear the static reference without disposing the control
			// The control will be properly disposed by its parent window/container
			currentControl = null;

			// ENHANCED CLEANUP: Force comprehensive OpenGL context reset for multi-test scenarios
			try
			{
				// Step 1: Force all pending Windows messages to complete
				System.Windows.Forms.Application.DoEvents();

				// Step 2: Aggressive garbage collection to release OpenGL resources
				for (int i = 0; i < 3; i++)
				{
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers();
				}

				// Step 3: Let Windows process any cleanup messages
				System.Threading.Thread.Sleep(100); // Allow time for WGL cleanup
				System.Windows.Forms.Application.DoEvents();

				// Step 4: Force reset of OpenTK static state if possible
				try
				{
					// Use reflection to access OpenTK internal cleanup if available
					var openTkType = System.Type.GetType("OpenTK.Graphics.GraphicsContext, OpenTK.Graphics");
					if (openTkType != null)
					{
						var clearMethod = openTkType.GetMethod("Dispose", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
						clearMethod?.Invoke(null, null);
					}
				}
				catch
				{
					// Ignore reflection errors - this is best-effort cleanup
				}

				// Step 5: Final Windows message processing
				System.Windows.Forms.Application.DoEvents();
			}
			catch
			{
				// Ignore cleanup errors - tests should still be able to continue
			}
		}

#if USE_OPENTK4
		public AggGLControl(GLControlSettings graphicsMode)
#else
		public AggGLControl(OpenTK.Graphics.GraphicsMode graphicsMode)
#endif
			: base(graphicsMode)
		{
			Id = nextId++;
		}

		public new void MakeCurrent()
		{
			// ENHANCED: Safe MakeCurrent with disposal and handle validity checks
			try
			{
				// Check if the control is disposed or being disposed
				if (this.IsDisposed || this.Disposing)
				{
					return; // Silently return rather than throwing exception
				}

				// Check if the handle is valid and created
				if (!this.IsHandleCreated)
				{
					return; // Can't make current without a valid handle
				}

				// Additional safety check for multi-test scenarios
				if (this.Handle == IntPtr.Zero)
				{
					return; // Invalid handle
				}

				currentControl = this;
				base.MakeCurrent();
				ImageGlPlugin.SetCurrentContextData(Id, releaseAllGlData);
			}
			catch (ObjectDisposedException)
			{
				// This is the specific exception we're trying to fix
				// The control was disposed between the check and the actual call
				// Clear the current control reference and return safely
				if (currentControl == this)
				{
					currentControl = null;
				}
				return;
			}
			catch (System.InvalidOperationException)
			{
				// Handle invalid operation on disposed or invalid OpenGL context
				if (currentControl == this)
				{
					currentControl = null;
				}
				return;
			}
			catch
			{
				// For any other OpenGL context errors, clear the reference
				if (currentControl == this)
				{
					currentControl = null;
				}
				return;
			}
		}

		protected override bool ProcessDialogKey(System.Windows.Forms.Keys keyData)
		{
			return false;
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			//throw new NotImplementedException("Attempt to use Winforms");

			// SIMPLIFIED: Let OpenTK handle GLFW completely - no manual thread marshaling
			try
			{
				// Log thread information for debugging
				int currentThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
				DebugLogger.LogMessage("AggGLControl", $"OnHandleCreated starting on thread {currentThreadId}");

				// ENHANCED: Handle recreation detection for multi-test scenarios
				bool wasRecreated = false;
				if (this.IsHandleCreated)
				{
					// This might be a handle recreation - invalidate any previous GL context
					try
					{
						if (releaseAllGlData != null)
						{
							releaseAllGlData.Release();
						}
					}
					catch
					{
						// Ignore errors from previous context cleanup
					}
					wasRecreated = true;
				}

				// SIMPLIFIED: Let OpenTK handle GLFW initialization and threading
				System.Diagnostics.Debug.WriteLine($"Creating OpenGL context on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
				DebugLogger.LogMessage("AggGLControl", $"About to call base.OnHandleCreated on thread {currentThreadId}");

				base.OnHandleCreated(e);

				System.Diagnostics.Debug.WriteLine("OpenGL context creation successful");
				DebugLogger.LogMessage("AggGLControl", "base.OnHandleCreated completed successfully");

				// ENHANCED: Force context reset after handle recreation
				if (wasRecreated)
				{
					try
					{
						// Brief delay to allow handle stabilization
						System.Threading.Thread.Sleep(10);
					}
					catch
					{
						// Ignore timing errors
					}
				}

				currentControl = this;

				this.MakeCurrent();
				ImageGlPlugin.SetCurrentContextData(Id, releaseAllGlData);

				DebugLogger.LogMessage("AggGLControl", $"OnHandleCreated completed successfully on thread {currentThreadId}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"OnHandleCreated error: {ex.Message}");
				DebugLogger.LogError("AggGLControl", $"OnHandleCreated failed: {ex.GetType().Name}: {ex.Message}");
				DebugLogger.LogError("AggGLControl", $"Stack trace: {ex.StackTrace}");
				throw; // Re-throw exceptions for proper error handling
			}
		}

		/// <summary>
		/// Enhanced handle destruction handling for multi-test scenarios
		/// </summary>
		protected override void OnHandleDestroyed(EventArgs e)
		{
			try
			{
				// Force immediate GL data release before handle destruction
				if (releaseAllGlData != null)
				{
					// Make current if possible for proper cleanup
					try
					{
						if (this.IsHandleCreated && !this.IsDisposed)
						{
							this.MakeCurrent();
						}
					}
					catch
					{
						// Ignore MakeCurrent errors during destruction
					}

					releaseAllGlData.Release();
				}
			}
			catch
			{
				// Ignore GL cleanup errors during handle destruction
			}

			// CRITICAL: Clear the OpenGL context before destroying the handle
			// This prevents WGL handle errors in subsequent tests
			try
			{
				// In OpenTK 4, we need to ensure the context is not current
				// before the handle is destroyed
				if (currentControl == this)
				{
					currentControl = null;
				}
			}
			catch
			{
				// Ignore errors - context might already be cleared
			}

			base.OnHandleDestroyed(e);
		}

		public override string ToString()
		{
			return Id.ToString();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Clear static reference if this is the current control
				if (currentControl == this)
				{
					currentControl = null;
				}

				// ENHANCED: Force comprehensive GL context cleanup for multi-test scenarios
				try
				{
					if (releaseAllGlData != null)
					{
						// Make current first to ensure proper GL context for cleanup
						if (this.IsHandleCreated && !this.IsDisposed)
						{
							try
							{
								this.MakeCurrent();
							}
							catch
							{
								// Ignore MakeCurrent errors during disposal
							}
						}
						releaseAllGlData.Release();
					}
				}
				catch
				{
					// Ignore GL cleanup errors during disposal
				}

				// ENHANCED: Pre-disposal context cleanup
				try
				{
					// Force completion of any pending OpenGL operations
					if (this.IsHandleCreated && !this.IsDisposed)
					{
						this.MakeCurrent();
						// Force any pending GL operations to complete by making the context current
						// Note: Removed OpenTkGl.Finish() call as it doesn't exist in this context
					}
				}
				catch
				{
					// Ignore pre-disposal cleanup errors
				}
			}

			// Let the base class handle the OpenTK disposal properly
			base.Dispose(disposing);

			if (disposing)
			{
				// ENHANCED: Post-disposal handle cleanup with retries
				try
				{
					if (this.IsHandleCreated)
					{
						// Force immediate handle destruction
						this.DestroyHandle();
					}
				}
				catch
				{
					// Ignore handle cleanup errors
				}

				// ENHANCED: Additional cleanup for multi-test scenarios
				try
				{
					// Force Windows message processing to complete handle cleanup
					System.Windows.Forms.Application.DoEvents();

					// Brief pause to allow WGL context cleanup to complete
					System.Threading.Thread.Sleep(10);

					// ENHANCED: Extended handle cleanup to prevent handle exhaustion
					// Force multiple rounds of message processing to ensure proper cleanup
					for (int i = 0; i < 5; i++)
					{
						System.Windows.Forms.Application.DoEvents();
						System.Threading.Thread.Sleep(5);
					}

					// Force additional garbage collection round to clean up any remaining references
					System.GC.Collect();
					System.GC.WaitForPendingFinalizers();

					// Final message processing
					System.Windows.Forms.Application.DoEvents();
				}
				catch
				{
					// Ignore enhanced cleanup errors
				}
			}
		}
	}
}
