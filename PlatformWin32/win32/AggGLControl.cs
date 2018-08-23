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

#endif

namespace MatterHackers.Agg.UI
{
	public class AggGLControl : GLControl
	{
		internal static AggGLControl currentControl;

		private static int nextId;
		public int Id;

		internal RemoveGlDataCallBackHolder releaseAllGlData = new RemoveGlDataCallBackHolder();

		// If you have an error here it is likely that you need to build your project with Platform Target x86.
		public AggGLControl()
			: base(new OpenTK.Graphics.GraphicsMode(32, 24, 0, 8))
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

		public override string ToString()
		{
			return Id.ToString();
		}
	}
}
