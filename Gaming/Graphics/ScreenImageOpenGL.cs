using System;
using System.Collections.Generic;
using System.Text;
using Tao.OpenGl;
using Gaming.Graphics;
using Gaming.Core;

namespace Gaming.Graphics
{
    class ScreenGL
    {
        private static ScreenGL screenImage = new ScreenGL();

        protected class Renderer2DOpenGL : Renderer2D
        {
            BufferedImage m_LineImageCache;
            // 
            public Renderer2DOpenGL()
            {

            }

            private void PushOrthoProjection()
            {
                Gl.glPushAttrib(Gl.GL_TRANSFORM_BIT);
                int[] viewport = new int[4];
                Gl.glGetIntegerv(Gl.GL_VIEWPORT, viewport);
                Gl.glMatrixMode(Gl.GL_PROJECTION);
                Gl.glPushMatrix();
                Gl.glLoadIdentity();
                Gl.glOrtho(viewport[0], viewport[2], viewport[1], viewport[3], 0, 1);
                Gl.glPopAttrib();
                viewport = null;
            }

            private void PopOrthoProjection()
            {
                Gl.glPushAttrib(Gl.GL_TRANSFORM_BIT);
                Gl.glMatrixMode(Gl.GL_PROJECTION);
                Gl.glPopMatrix();
                Gl.glPopAttrib();
            }

            public override void DrawLine(Vector2Df Start, Vector2Df End)
            {
                if(m_LineImageCache == null)
                {
                	//m_LineImageCache = new BufferedImage(4, 1, BufferedImage.PixelType.INT_ARGB);
                	//renderer.FillRect(1, 0, 1, 1);
                	m_LineImageCache = new BufferedImage(4, 4, BufferedImage.PixelType.INT_ARGB);
                	Renderer2D renderer = m_LineImageCache.GetRenderer2D;
                    renderer.Color.AlphaI = 0;
                    renderer.FillRect(0, 0, 4, 4);
                    renderer.Color.AlphaI = 255;
                    renderer.FillRect(1, 2, 1, 2);
#if false // Fill in the rest of the pixels for testing
                	renderer.Color.RedI = 0;
                	renderer.FillRect(0, 0, 4, 1);
                	renderer.FillRect(3, 0, 1, 4);
                	renderer.Color.BlueI = 0;
                	renderer.FillRect(0, 1, 1, 3);
                	renderer.FillRect(0, 1, 3, 1);
                	renderer.FillRect(2, 1, 1, 3);
#endif
                }
	            
                ImageGLDisplayListPlugin GLBuffer = ImageGLDisplayListPlugin.GetImageGLDisplayListPlugin(m_LineImageCache);
                Gl.glEnable(Gl.GL_TEXTURE_2D);
                Gl.glEnable(Gl.GL_BLEND);
                Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
                Gl.glColor4f(m_Color.RedF, m_Color.GreenF, m_Color.BlueF, m_Color.AlphaF);

                Vector2Df Normal = (End - Start);
                Vector2Df PerpendicularNormal = Normal.GetPerpendicularNormal();
                float Width = 1;
                Vector2Df OffsetPos = PerpendicularNormal * Width;
                Vector2Df OffsetNeg = PerpendicularNormal * -(Width + 1);

                PushOrthoProjection();

                Gl.glBindTexture(Gl.GL_TEXTURE_2D, GLBuffer.GLTextureHandle);
                
                Gl.glBegin(Gl.GL_QUADS);
#if true
                Gl.glTexCoord2d(0, .75); Gl.glVertex2f(Start.x + OffsetPos.x, Start.y + OffsetPos.y);
                Gl.glTexCoord2d(0, 1); Gl.glVertex2f(End.x + OffsetPos.x, End.y + OffsetPos.y);
                Gl.glTexCoord2d(.75, 1); Gl.glVertex2f(End.x + OffsetNeg.x, End.y + OffsetNeg.y);
                Gl.glTexCoord2d(.75, .5); Gl.glVertex2f(Start.x + OffsetNeg.x, Start.y + OffsetNeg.y);
#else
                // draw the main line (without the tips)
                Gl.glTexCoord2d(0, .75); Gl.glVertex2f(Start.x + OffsetPos.x + Normal.x, Start.y + OffsetPos.y + Normal.y);
                Gl.glTexCoord2d(0, 1); Gl.glVertex2f(End.x + OffsetPos.x - Normal.x, End.y + OffsetPos.y - Normal.y);
                Gl.glTexCoord2d(.75, 1); Gl.glVertex2f(End.x + OffsetNeg.x - Normal.x, End.y + OffsetNeg.y - Normal.y);
                Gl.glTexCoord2d(.75, .5); Gl.glVertex2f(Start.x + OffsetNeg.x + Normal.x, Start.y + OffsetNeg.y + Normal.y);

                // draw the ending tip anti-aliased
                Gl.glTexCoord2d(0, 1); Gl.glVertex2f(End.x + OffsetPos.x - Normal.x, End.y + OffsetPos.y - Normal.y);
                Gl.glTexCoord2d(0, .25); Gl.glVertex2f(End.x + OffsetPos.x, End.y + OffsetPos.y);
                Gl.glTexCoord2d(.75, .25); Gl.glVertex2f(End.x + OffsetNeg.x, End.y + OffsetNeg.y);
                Gl.glTexCoord2d(.75, 1); Gl.glVertex2f(End.x + OffsetNeg.x - Normal.x, End.y + OffsetNeg.y - Normal.y);

                // draw the starting tip anti-aliased
                Gl.glTexCoord2d(0, .25); Gl.glVertex2f(Start.x + OffsetPos.x, Start.y + OffsetPos.y);
                Gl.glTexCoord2d(0, 1); Gl.glVertex2f(Start.x + OffsetPos.x + Normal.x, Start.y + OffsetPos.y + Normal.y);
                Gl.glTexCoord2d(.75, 1); Gl.glVertex2f(Start.x + OffsetNeg.x + Normal.x, Start.y + OffsetNeg.y + Normal.y);
                Gl.glTexCoord2d(.75, .25); Gl.glVertex2f(Start.x + OffsetNeg.x, Start.y + OffsetNeg.y);
#endif
                Gl.glColor4f(1, 1, 1, 1);
                Gl.glEnd();

                PopOrthoProjection();
            }

            public override void FillRect(float x, float y, float width, float height)
            {
                PushOrthoProjection();
                Gl.glDisable(Gl.GL_TEXTURE_2D);
                Gl.glEnable(Gl.GL_BLEND);
                Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
                Gl.glColor4f(m_Color.RedF, m_Color.GreenF, m_Color.BlueF, m_Color.AlphaF);

                Gl.glBegin(Gl.GL_QUADS);
                Gl.glVertex3d(x+width, y+height, 0);
                Gl.glVertex3d(x, y+height, 0);
                Gl.glVertex3d(x, y, 0);
                Gl.glVertex3d(x+width, y, 0);
                Gl.glEnd();

                Gl.glColor4f(1, 1, 1, 1);
                PopOrthoProjection();
            }

            public override void DrawString(String stringToPrint, float x, float y)
            {
                if (stringToPrint == null)
                {
                    return;
                }

                //Prepare openGL for rendering the font characters
                PushOrthoProjection();
                Gl.glPushAttrib(Gl.GL_LIST_BIT | Gl.GL_CURRENT_BIT | Gl.GL_ENABLE_BIT | Gl.GL_TRANSFORM_BIT);
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glDisable(Gl.GL_LIGHTING);
                Gl.glEnable(Gl.GL_TEXTURE_2D);
                Gl.glDisable(Gl.GL_DEPTH_TEST);
                Gl.glEnable(Gl.GL_BLEND);
                Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
                float[] modelview_matrix = new float[16];
                Gl.glGetFloatv(Gl.GL_MODELVIEW_MATRIX, modelview_matrix);
                Gl.glPushMatrix();
                Gl.glLoadIdentity();
                Gl.glTranslatef(x, y, 0);
                Gl.glMultMatrixf(modelview_matrix);

                Gl.glColor4f(m_Color.RedF, m_Color.GreenF, m_Color.BlueF, m_Color.AlphaF);
                for (int i = 0; i < stringToPrint.Length; i++)
                {
                    int unicodeCharacter = stringToPrint[i];
                    Image characterImage = CurrentFont.GetCharacterImage(unicodeCharacter);
                    ImageGLDisplayListPlugin GLDisplayForImage = ImageGLDisplayListPlugin.GetImageGLDisplayListPlugin(characterImage);

                    Gl.glTranslatef(CurrentFont.GetCharacterLeftOffset(unicodeCharacter), 0, 0);
                    Gl.glPushMatrix();
                    Gl.glTranslatef(0, CurrentFont.GetCharacterTopOffset(unicodeCharacter) - characterImage.Height, 0);

                    Gl.glCallList(GLDisplayForImage.GLDisplayList);

                    Gl.glPopMatrix();
                    //Advance for the next character			
                    Gl.glTranslatef(characterImage.Width, 0, 0);
                }
                Gl.glColor4f(1, 1, 1, 1);

                //Restore openGL state
                Gl.glPopMatrix();
                Gl.glPopAttrib();
                PopOrthoProjection();
            }
        };

        private ScreenGL() // you have to call the instance of this
        {

        }

        static public ScreenGL Instance()
        {
            return screenImage;
        }

        internal static int SmallestHardwareCompatibleTextureSize(int a)
        {
            // TODO: check if the hardware supports non-power 2 textures.
            int rval = 1;
            while (rval < a)
            {
                rval <<= 1;
            }

            return rval;
        }

        public override Renderer2D GetRenderer2D
        {
            get
            {
                return new Renderer2DOpenGL();
            }
        }

        public override int Width
        {
            get
            {
                int[] viewport = new int[4];
                Gl.glGetIntegerv(Gl.GL_VIEWPORT, viewport);
                if (viewport[0] != 0 || viewport[1] != 0)
                {
                    throw new Exception("The viewport x and y are not 0.");
                }
                return viewport[2];
            }
        }

        public override int Height
        {
            get
            {
                int[] viewport = new int[4];
                Gl.glGetIntegerv(Gl.GL_VIEWPORT, viewport);
                if(viewport[0] != 0 || viewport[1] != 0)
                {
                    throw new Exception("The viewport x and y are not 0.");
                }
                return viewport[3];
            }
        }
    }
}
