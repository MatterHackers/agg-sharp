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

		// TODO: Set VSync off for OpenTK 4. Will need access to the NativeWindow.

			/// <summary>
	/// Resets static state to allow clean reinitialization
	/// </summary>
	public static void ResetStaticState()
	{
		// Clear the static reference without disposing the control
		// The control will be properly disposed by its parent window/container
		currentControl = null;
		
		// Force cleanup of any remaining OpenTK contexts and Windows messages
		try
		{
			// Process any pending Windows messages to ensure cleanup is complete
			System.Windows.Forms.Application.DoEvents();
			
			// Force garbage collection to clean up any lingering OpenGL resources
			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();
			System.GC.Collect();
		}
		catch
		{
			// Ignore cleanup errors
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
			currentControl = this;
			base.MakeCurrent();
			ImageGlPlugin.SetCurrentContextData(Id, releaseAllGlData);
		}

		protected override bool ProcessDialogKey(System.Windows.Forms.Keys keyData)
		{
			return false;
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			//throw new NotImplementedException("Attempt to use Winforms");

			base.OnHandleCreated(e);

#if !USE_OPENTK4
			// OpenTK3 GLControl will swallow the GraphicsModeException and create a dummy context instead.
			if (!HasValidContext)
			{
				//System.Windows.Forms.MessageBox.Show(null, "Failed to create GL context.".Localize(), "MatterControl",
				//	MessageBoxButtons.OK, MessageBoxIcon.Error);
				throw new OpenTK.PlatformException("Failed to create GL context.");
			}
#else
			// OpenTK4 will throw a OpenTK.Windowing.GraphicsLibraryFramework.GLFWException.
#endif
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
			
			// Force release of GL data before disposing the base control
			try
			{
				if (releaseAllGlData != null)
				{
					// Make current first to ensure proper GL context for cleanup
					if (this.IsHandleCreated && !this.IsDisposed)
					{
						this.MakeCurrent();
					}
					releaseAllGlData.Release();
				}
			}
			catch
			{
				// Ignore GL cleanup errors during disposal
			}
		}
		
		// Let the base class handle the OpenTK disposal properly
		base.Dispose(disposing);
		
		if (disposing)
		{
			// Ensure handle is destroyed after base disposal
			try
			{
				if (this.IsHandleCreated)
				{
					this.DestroyHandle();
				}
			}
			catch
			{
				// Ignore handle cleanup errors
			}
		}
	}
	}
}
