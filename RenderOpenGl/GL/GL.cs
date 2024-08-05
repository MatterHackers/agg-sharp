/*
Copyright (c) 2018, Lars Brubaker
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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl.OpenGl
{
    public static class GL
    {
        #region constants
        public const int ALWAYS = 0x0207;
        public const int ARRAY_BUFFER = 0x8892;
        public const int BGRA = 0x80E1;
        public const int BLEND = 0x0BE2;
        public const int COLOR_ATTACHMENT0 = 0x8CE0;
        public const int COLOR_BUFFER_BIT = 0x00004000;
        public const int DEPTH_ATTACHMENT = 0x8D00;
        public const int DEPTH_BUFFER_BIT = 0x00000100;
        public const int DEPTH_COMPONENT = 0x1902;
        public const int DEPTH_COMPONENT32 = 0x81A7;
        public const int DEPTH_TEST = 0x0B71;
        public const int ELEMENT_ARRAY_BUFFER = 0x8893;
        public const int FALSE = 0;
        public const int FLOAT = 0x1406;
        public const int FRAGMENT_SHADER = 0x8B30;
        public const int FRAMEBUFFER = 0x8D40;
        public const int GEOMETRY_SHADER = 0x8DD9;
        public const int LESS = 0x0201;
        public const int NEAREST = 0x2600;
        public const int ONE_MINUS_SRC_ALPHA = 0x0303;
        public const int RGBA32F = 0x8814;
        public const int SRC_ALPHA = 0x0302;
        public const int STATIC_DRAW = 0x88E4;
        public const int TEXTURE_2D = 0x0DE1;
        public const int TEXTURE_MAG_FILTER = 0x2800;
        public const int TEXTURE_MIN_FILTER = 0x2801;
        public const int TEXTURE0 = 0x84C0;
        public const int TRIANGLES = 0x0004;
        public const int UNSIGNED_INT = 0x1405;
        public const int VERTEX_SHADER = 0x8B31;
        public const int GL_COMPILE = 0x1300;
        #endregion constants


        private static readonly Dictionary<int, bool> IsEnabled = new Dictionary<int, bool>();
        private static IOpenGL _instance = null;
        private static bool inBegin;
        private static int pushAttribCount = 0;
        private static Dictionary<MatrixMode, int> pushMatrixCount = new Dictionary<MatrixMode, int>()
        {
            [OpenGl.MatrixMode.Modelview] = 0,
            [OpenGl.MatrixMode.Projection] = 0,
        };

        private static int threadId = -1;
        private static MatrixMode matrixMode = OpenGl.MatrixMode.Modelview;

        public static IOpenGL Instance
        {
            get
            {
                if (threadId == -1)
                {
                    threadId = Thread.CurrentThread.ManagedThreadId;
                }

                if (Thread.CurrentThread.ManagedThreadId != threadId)
                {
                    throw new Exception("You mush only cal GL on the main thread.");
                }

                CheckForError();
                return _instance;
            }

            set
            {
                _instance = value;
            }
        }

        public static void Begin(BeginMode mode)
        {
            inBegin = true;
            Instance?.Begin(mode);
            CheckForError();
        }

        public static void BindVertexArray(int array)
        {
            Instance?.BindVertexArray(array);
        }

        public static void BindBuffer(BufferTarget target, int buffer)
        {
            BindBuffer((int)target, buffer);
        }

        public static void BindBuffer(int target, int buffer)
        {
            Instance?.BindBuffer(target, buffer);
            CheckForError();
        }

        public static void BindFramebuffer(int target, int buffer)
        {
            Instance?.BindFramebuffer(target, buffer);
            CheckForError();
        }

        public static void BindTexture(int target, int texture)
        {
            Instance?.BindTexture(target, texture);
            CheckForError();
        }

        public static void BindTexture(TextureTarget target, int texture)
        {
            BindTexture((int)target, texture);
        }

        public static void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
        {
            BlendFunc((int)sfactor, (int)dfactor);
        }

        public static void BlendFunc(int sfactor, int dfactor)
        {
            Instance?.BlendFunc(sfactor, dfactor);
            CheckForError();
        }

        public static void BufferData(int target, int size, IntPtr data, int usage)
        {
            Instance?.BufferData(target, size, data, usage);
            CheckForError();
        }

        public static void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
        {
            BufferData((int)target, size, data, (int)usage);
        }

        public static void CheckForError()
        {
#if DEBUG
            if (!inBegin)
            {
                var code = _instance.GetError();
                if (code != ErrorCode.NoError)
                {
                    throw new Exception($"OpenGL Error: {code}");
                }
            }
#endif
        }

        public static void Clear(ClearBufferMask mask)
        {
            Clear((int)mask);
        }

        public static void Clear(int mask)
        {
            Instance?.Clear(mask);
            CheckForError();
        }

        public static void ClearDepth(double depth)
        {
            Instance?.ClearDepth(depth);
            CheckForError();
        }

        public static void Color4(Color color)
        {
            Color4(color.red, color.green, color.blue, color.alpha);
        }

        public static void Color4(int red, int green, int blue, int alpha)
        {
            Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
        }

        public static void Color4(byte red, byte green, byte blue, byte alpha)
        {
            Instance?.Color4(red, green, blue, alpha);
            CheckForError();
        }

        public static void ColorMask(bool red, bool green, bool blue, bool alpha)
        {
            Instance?.ColorMask(red, green, blue, alpha);
            CheckForError();
        }

        public static void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
        {
            Instance?.ColorMaterial(face, mode);
            CheckForError();
        }

        public static void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
        {
            unsafe
            {
                fixed (byte* intPointer = pointer)
                {
                    ColorPointer(size, type, stride, (IntPtr)intPointer);
                }
            }
        }

        public static void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
        {
            Instance?.ColorPointer(size, type, stride, pointer);
            CheckForError();
        }

        public static void CullFace(CullFaceMode mode)
        {
            Instance?.CullFace(mode);
            CheckForError();
        }

        public static void DeleteBuffer(int buffer)
        {
            Instance?.DeleteBuffer(buffer);
            CheckForError();
        }

        public static void DeleteTexture(int textures)
        {
            Instance?.DeleteTexture(textures);
            CheckForError();
        }

        public static void DepthFunc(DepthFunction func)
        {
            DepthFunc((int)func);
            CheckForError();
        }

        public static void DepthFunc(int func)
        {
            Instance?.DepthFunc(func);
            CheckForError();
        }

        public static void DepthMask(bool flag)
        {
            Instance?.DepthMask(flag);
            CheckForError();
        }

        public static void Disable(int cap)
        {
            IsEnabled[cap] = false;

            Instance?.Disable(cap);
            CheckForError();
        }

        public static void Disable(EnableCap cap)
        {
            Disable((int)cap);
        }

        public static void DisableClientState(ArrayCap array)
        {
            Instance?.DisableClientState(array);
            CheckForError();
        }

        public static void DrawArrays(BeginMode mode, int first, int count)
        {
            Instance?.DrawArrays(mode, first, count);
            CheckForError();
        }

        public static void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
        {
            Instance?.DrawRangeElements(mode, start, end, count, type, indices);
            CheckForError();
        }

        public static void Enable(int cap)
        {
            IsEnabled[cap] = true;
            Instance?.Enable(cap);
            CheckForError();
        }

        public static void Enable(EnableCap cap)
        {
            Enable((int)cap);
        }

        public static void EnableClientState(ArrayCap arrayCap)
        {
            Instance?.EnableClientState(arrayCap);
            CheckForError();
        }

        public static bool EnableState(int cap)
        {
            if (IsEnabled.ContainsKey(cap))
            {
                return IsEnabled[cap];
            }

            return false;
        }

        public static bool EnableState(EnableCap cap)
        {
            return EnableState((int)cap);
        }

        public static void End()
        {
            Instance?.End();
            inBegin = false;

            CheckForError();
        }

        public static void GenTextures(int v, out int tex)
        {
            tex = 0;
            Instance?.GenTextures(v, out tex);
        }

        public static void TexParameteri(int target, int pname, int param)
        {
            Instance?.TexParameteri(target, pname, param);
        }

        public static void Finish()
        {
            Instance?.Finish();
            CheckForError();
        }

        public static void FrontFace(FrontFaceDirection mode)
        {
            Instance?.FrontFace(mode);
            CheckForError();
        }

        public static int GenBuffer()
        {
            if (Instance != null)
            {
                var buffer = Instance.GenBuffer();
                CheckForError();
                return buffer;
            }

            return 0;
        }

        public static int GenTexture()
        {
            var texture = Instance?.GenTexture();
            CheckForError();
            return texture.Value;
        }

        public static ErrorCode GetError()
        {
            if (Instance != null)
            {
                return Instance.GetError();
            }

            return ErrorCode.NoError;
        }

        public static void GenFramebuffers(int n, out int framebuffers)
        {
            framebuffers = 0;
            Instance?.GenFramebuffers(n, out framebuffers);
            CheckForError();
        }

        public static void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level)
        {
            Instance?.FramebufferTexture2D(target, attachment, textarget, texture, level);
            CheckForError();
        }

        public static string GetString(StringName name)
        {
            if (Instance != null)
            {
                CheckForError();
                return Instance.GetString(name);
            }

            return "";
        }

        public static void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
        {
            Instance?.IndexPointer(type, stride, pointer);
            CheckForError();
        }

        public static void BufferData(int target, float[] v, int usage)
        {
            unsafe
            {
                fixed (float* data = v)
                {
                    BufferData(target, sizeof(float) * v.Length, (IntPtr)data, usage);
                }
            }
        }

        public static void BufferData(int target, PositionNormal[] v, int usage)
        {
            unsafe
            {
                fixed (PositionNormal* data = v)
                {
                    BufferData(target, sizeof(PositionNormal) * v.Length, (IntPtr)data, usage);
                }
            }
        }

        public static void UniformMatrix4fv(int location, int count, int transpose, float[] value)
        {
            Instance?.UniformMatrix4fv(location, count, transpose, value);
            CheckForError();
        }

        public static void BufferData(int target, int[] faceIndex, int usage)
        {
            unsafe
            {
                fixed (int* data = faceIndex)
                {
                    BufferData(target, sizeof(int) * faceIndex.Length, (IntPtr)data, usage);
                }
            }
        }

        public static void GenVertexArrays(int v, out int vAO)
        {
            vAO = 0;
            Instance?.GenVertexArrays(v, out vAO);
            CheckForError();
        }

        public static void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer)
        {
            Instance?.VertexAttribPointer(index, size, type, normalized, stride, pointer);
            CheckForError();
        }

        public static void EnableVertexAttribArray(int index)
        {
            Instance?.EnableVertexAttribArray(index);
            CheckForError();
        }

        public static void Light(LightName light, LightParameter pname, float[] param)
        {
            Instance?.Light(light, pname, param);
            CheckForError();
        }

        public static void GenBuffers(int n, out int buffer)
        {
            buffer = 0;
            Instance?.GenBuffers(n, out buffer);
            CheckForError();
        }

        public static void print_shader_info_log(int shader)
        {
            var shaderInfo = Instance?.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(shaderInfo))
            {
                Debug.WriteLine(shaderInfo);
            }
        }

        public static int load_shader(string src, int shaderType)
        {
            if (string.IsNullOrEmpty(src))
            {
                return 0;
            }

            int s = GL.CreateShader(shaderType);
            if (s == 0)
            {
                Debug.WriteLine("Error: load_shader() failed to create shader.\n");
                return 0;
            }
            // Pass shader source string
            GL.ShaderSource(s, 1, src, null);
            GL.CompileShader(s);
            // Print info log (if any)
            print_shader_info_log(s);
            return s;
        }

        private static void CompileShader(int id)
        {
            Instance?.CompileShader(id);
            CheckForError();
        }

        private static void ShaderSource(int id, int count, string src, object p)
        {
            Instance?.ShaderSource(id, count, src, p);
            CheckForError();
        }

        public static int CreateShader(int shaderType)
        {
            var id = Instance?.CreateShader(shaderType);
            CheckForError();
            return id == null ? 0 : id.Value;
        }

        public static bool create_shader_program(string geom_source,
            string vert_source,
            string frag_source,
            out int id)
        {
            id = GL.CreateProgram();
            int g = 0, f = 0, v = 0;
            if (!string.IsNullOrEmpty(geom_source))
            {
                // load vertex shader
                g = load_shader(geom_source, GL.GEOMETRY_SHADER);
                if (g == 0)
                {
                    Debug.WriteLine("geometry shader failed to compile.");
                    return false;
                }
                GL.AttachShader(id, g);
            }

            if (vert_source != "")
            {
                // load vertex shader
                v = load_shader(vert_source, GL.VERTEX_SHADER);
                if (v == 0)
                {
                    Debug.WriteLine("vertex shader failed to compile.");
                    return false;
                }

                GL.AttachShader(id, v);
            }

            if (frag_source != "")
            {
                // load fragment shader
                f = load_shader(frag_source, GL.FRAGMENT_SHADER);
                if (f == 0)
                {
                    Debug.WriteLine("fragment shader failed to compile.");
                    return false;
                }
                GL.AttachShader(id, f);
            }

            //// loop over attributes
            //for (
            //  std::map < std::string, gluint >::const_iterator ait = attrib.begin();
            //  ait != attrib.end();
            //  ait++)
            //{
            //	glbindattriblocation(
            //	  id,
            //	  (*ait).second,
            //	  (*ait).first.c_str());
            //}

            // Link program
            GL.LinkProgram(id);

            void detach(int idIn, int shader)
            {
                if (shader != 0)
                {
                    GL.DetachShader(idIn, shader);
                    GL.DeleteShader(shader);
                }
            }

            detach(id, g);
            detach(id, f);
            detach(id, v);

            // print log if any
            // print_program_info_log(id);

            return true;
        }

        private static void DeleteShader(int shader)
        {
            Instance?.DeleteShader(shader);
        }

        private static void DetachShader(int id, int shader)
        {
            Instance?.DetachShader(id, shader);
        }

        private static void LinkProgram(int id)
        {
            Instance?.LinkProgram(id);
        }

        private static void AttachShader(int program, int shader)
        {
            Instance?.AttachShader(program, shader);
        }

        private static int CreateProgram()
        {
            var id = Instance?.CreateProgram();
            CheckForError();
            return id == null ? 0 : id.Value;
        }

        public static void Uniform1f(int location, float v0)
        {
            Instance?.Uniform1f(location, v0);
            CheckForError();
        }

        public static void LoadIdentity()
        {
            Instance?.LoadIdentity();
            CheckForError();
        }

        public static void ClearColor(double r, double g, double b, double a)
        {
            Instance?.ClearColor(r, g, b, a);
            CheckForError();
        }

        public static int GenFramebuffer()
        {
            var texture = Instance?.GenFramebuffer();
            CheckForError();
            return texture.Value;
        }

        public static void LoadMatrix(double[] m)
        {
            Instance?.LoadMatrix(m);
            CheckForError();
        }

        public static void DrawElements(int mode, int count, int elementType, IntPtr indices)
        {
            Instance?.DrawElements(mode, count, elementType, indices);
            CheckForError();
        }

        public static void MatrixMode(MatrixMode mode)
        {
            matrixMode = mode;
            Instance?.MatrixMode(mode);
            CheckForError();
        }

        public static void MultMatrix(float[] m)
        {
            Instance?.MultMatrix(m);
            CheckForError();
        }

        public static void ActiveTexture(int texture)
        {
            Instance?.ActiveTexture(texture);
            CheckForError();
        }

        public static void Normal3(double x, double y, double z)
        {
            Instance?.Normal3(x, y, z);
            CheckForError();
        }

        public static void NormalPointer(NormalPointerType type, int stride, float[] pointer)
        {
            unsafe
            {
                fixed (float* floatPointer = pointer)
                {
                    NormalPointer(type, stride, (IntPtr)floatPointer);
                }
            }
        }

        public static void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
        {
            Instance?.NormalPointer(type, stride, pointer);
            CheckForError();
        }

        public static void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
        {
            Instance?.Ortho(left, right, bottom, top, zNear, zFar);
            CheckForError();
        }

        public static void PolygonOffset(float factor, float units)
        {
            Instance?.PolygonOffset(factor, units);
            CheckForError();
        }

        public static void PopAttrib()
        {
            pushAttribCount--;
            Instance?.PopAttrib();
            CheckForError();
        }

        public static void PopMatrix()
        {
            pushMatrixCount[matrixMode]--;
            if (pushMatrixCount[matrixMode] < 0)
            {
                throw new Exception("popMatrib called too many times.");
            }

            Instance?.PopMatrix();
            CheckForError();
        }

        public static void PushAttrib(AttribMask mask)
        {
            pushAttribCount++;
            if (pushAttribCount > 100)
            {
                throw new Exception("pushAttrib being called without matching PopAttrib");
            }

            Instance?.PushAttrib(mask);
            CheckForError();
        }

        public static void Uniform1i(int location, int v0)
        {
            Instance?.Uniform1i(location, v0);
            CheckForError();
        }

        public static void PushMatrix()
        {
            pushMatrixCount[matrixMode]++;
            if (pushMatrixCount[matrixMode] > 32)
            {
                throw new Exception("PushMatrix being called without matching PopMatrix");
            }

            Instance?.PushMatrix();
            CheckForError();
        }

        public static int GetUniformLocation(int program, string name)
        {
            var value = Instance?.GetUniformLocation(program, name);
            CheckForError();
            return value == null ? 0 : value.Value;
        }

        public static void UseProgram(int program)
        {
            Instance?.UseProgram(program);
            CheckForError();
        }

        public static void Rotate(double angle, double x, double y, double z)
        {
            Instance?.Rotate(angle, x, y, z);
            CheckForError();
        }

        public static void Scale(double x, double y, double z)
        {
            Instance?.Scale(x, y, z);
            CheckForError();
        }

        public static void Scissor(int x, int y, int width, int height)
        {
            Instance?.Scissor(x, y, width, height);
            CheckForError();
        }

        public static void ShadeModel(ShadingModel model)
        {
            Instance?.ShadeModel(model);
            CheckForError();
        }

        public static void TexCoord2(Vector2 uv)
        {
            TexCoord2(uv.X, uv.Y);
        }

        public static void TexCoord2(Vector2Float uv)
        {
            TexCoord2(uv.X, uv.Y);
        }

        public static void TexCoord2(double x, double y)
        {
            Instance?.TexCoord2(x, y);
            CheckForError();
        }

        public static void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
        {
            Instance?.TexCoordPointer(size, type, stride, pointer);
            CheckForError();
        }

        public static void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
        {
            Instance?.TexEnv(target, pname, param);
            CheckForError();
        }

        public static void TexImage2D(int target,
            int level,
            int internalFormat,
            int width,
            int height,
            int border,
            int format,
            int type,
            byte[] pixels)
        {
            Instance?.TexImage2D(target,
                level,
                internalFormat,
                width,
                height,
                border,
                format,
                type,
                pixels);
            CheckForError();
        }

        public static void TexImage2D(TextureTarget target,
            int level,
            PixelInternalFormat internalFormat,
            int width,
            int height,
            int border,
            PixelFormat format,
            PixelType type,
            byte[] pixels)
        {
            TexImage2D((int)target, level, (int)internalFormat, width, height, border, (int)format, (int)type, pixels);
        }

        public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
            Instance?.TexParameter(target, pname, param);
            CheckForError();
        }

        public static void Translate(MatterHackers.VectorMath.Vector3 vector)
        {
            Translate(vector.X, vector.Y, vector.Z);
        }

        public static void Translate(double x, double y, double z)
        {
            Instance?.Translate(x, y, z);
            CheckForError();
        }

        public static void Vertex2(Vector2 position)
        {
            Vertex2(position.X, position.Y);
        }

        public static void Vertex2(double x, double y)
        {
            Instance?.Vertex2(x, y);
            CheckForError();
        }

        public static void Vertex3(Vector3 position)
        {
            Vertex3(position.X, position.Y, position.Z);
        }

        public static void Vertex3(Vector3Float position)
        {
            Vertex3(position.X, position.Y, position.Z);
        }

        public static void Vertex3(double x, double y, double z)
        {
            Instance?.Vertex3(x, y, z);
            CheckForError();
        }

        public static void VertexPointer(int size, VertexPointerType type, int stride, float[] pointer)
        {
            unsafe
            {
                fixed (float* pArray = pointer)
                {
                    VertexPointer(size, type, stride, new IntPtr(pArray));
                }
            }
        }

        public static void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
        {
            Instance?.VertexPointer(size, type, stride, pointer);
            CheckForError();
        }

        public static void Viewport(int x, int y, int width, int height)
        {
            Instance?.Viewport(x, y, width, height);
            CheckForError();
        }

        public static void EnableOrDisable(EnableCap depthTest, bool doDepthTest)
        {
            if (doDepthTest)
            {
                Enable(depthTest);
            }
            else
            {
                Disable(depthTest);
            }
        }

        public static int GenLists(int v)
        {
            var result = Instance?.GenLists(v);
            CheckForError();
            return result ?? 0;
        }

        public static void NewList(int displayListId, object compile)
        {
            Instance?.NewList(displayListId, compile);
            CheckForError();
        }

        public static void EndList()
        {
            Instance?.EndList();
            CheckForError();
        }

        public static void CallList(int displayListId)
        {
            Instance?.CallList(displayListId);
            CheckForError();
        }

        public static void DeleteLists(int id, int v)
        {
            Instance?.DeleteLists(id, v);
            CheckForError();
        }
    }
}