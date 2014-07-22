using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if USE_GLES
using OpenTK.Graphics.ES11;
#elif USE_OPENGL
using OpenTK.Graphics.OpenGL;
#endif

namespace MatterHackers.RenderOpenGl.OpenGl
{
    public enum BlendingFactorSrc
    {
        SrcAlpha = 770,
    }

    public enum BlendingFactorDest
    {
        OneMinusSrcAlpha = 771,
    }

    public enum EnableCap
    {
        ScissorTest = 3089,
        Blend = 3042,
    }

    public enum MatrixMode
    {
        Projection = 3,
    }

    public static class GL
    {
        public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
        { 
            OpenTK.Graphics.OpenGL.GL.BlendFunc((OpenTK.Graphics.OpenGL.BlendingFactorSrc)sfactor, (OpenTK.Graphics.OpenGL.BlendingFactorDest) dfactor);
        }

        public static void Scissor(int x, int y, int width, int height)
        {
            OpenTK.Graphics.OpenGL.GL.Scissor(x, y, width, height);
        }

        public static void Enable(EnableCap cap)
        {
            OpenTK.Graphics.OpenGL.GL.Enable((OpenTK.Graphics.OpenGL.EnableCap)cap);
        }

        public static void MatrixMode(MatrixMode mode)
        {
            OpenTK.Graphics.OpenGL.GL.MatrixMode((OpenTK.Graphics.OpenGL.MatrixMode)mode);
        }
    }
}
