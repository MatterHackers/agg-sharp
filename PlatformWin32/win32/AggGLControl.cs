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
using OpenTK.WinForms;
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


		public AggGLControl(GLControlSettings graphicsMode)
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

			// OpenTK4 will throw a OpenTK.Windowing.GraphicsLibraryFramework.GLFWException.
		}

		public override string ToString()
		{
			return Id.ToString();
		}
	}
}
