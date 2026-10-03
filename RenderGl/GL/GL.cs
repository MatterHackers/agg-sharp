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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl.OpenGl
{
    /// <summary>
    /// GPU context facade wrapping an IGpuContext with state tracking.
    /// Each instance has its own tracking state (enable flags, matrix/attrib push counts),
    /// enabling isolated off-screen rendering without corrupting the main viewport state.
    /// </summary>
    public class GL
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


        private readonly Dictionary<int, bool> isEnabled = new Dictionary<int, bool>();
        private bool inBegin;
        private readonly GlStackBalance stacks = new GlStackBalance();

        /// <summary>
        /// The underlying GPU context implementation (e.g. VorticeD3DGl).
        /// </summary>
        public IGpuContext GpuContext { get; set; }

        public GL(IGpuContext gpuContext = null)
        {
            GpuContext = gpuContext;
        }

        /// <summary>
        /// Starts a frame: forgets every push, attribute and immediate-mode state the previous frame left
        /// behind, here and in the context. The host calls this once per frame, before the first draw.
        /// A frame that finished cleanly has nothing to forget; one a throw abandoned mid-draw has its
        /// unmatched pushes still counted, and left alone they accumulate frame over frame until the
        /// guard in <see cref="GlStackBalance"/> fails every frame at its first push (see its remarks).
        /// </summary>
        public void BeginFrame()
        {
            inBegin = false;
            stacks.ResetForFrame();
            GpuContext?.BeginFrame();
        }

        public void Begin(BeginMode mode)
        {
            inBegin = true;
            GpuContext?.Begin(mode);
            CheckForError();
        }

        public void BindVertexArray(int array)
        {
            GpuContext?.BindVertexArray(array);
        }

        public void BindBuffer(BufferTarget target, int buffer)
        {
            BindBuffer((int)target, buffer);
        }

        public void BindBuffer(int target, int buffer)
        {
            GpuContext?.BindBuffer(target, buffer);
            CheckForError();
        }

        public void BindFramebuffer(int target, int buffer)
        {
            GpuContext?.BindFramebuffer(target, buffer);
            CheckForError();
        }

        public void BindTexture(int target, int texture)
        {
            GpuContext?.BindTexture(target, texture);
            CheckForError();
        }

        public void BindTexture(TextureTarget target, int texture)
        {
            BindTexture((int)target, texture);
        }

        public void BlendFunc(BlendingFactorSrc sfactor, BlendingFactorDest dfactor)
        {
            BlendFunc((int)sfactor, (int)dfactor);
        }

        public void BlendFunc(int sfactor, int dfactor)
        {
            GpuContext?.BlendFunc(sfactor, dfactor);
            CheckForError();
        }

        public void BufferData(int target, int size, IntPtr data, int usage)
        {
            GpuContext?.BufferData(target, size, data, usage);
            CheckForError();
        }

        public void BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)
        {
            BufferData((int)target, size, data, (int)usage);
        }

        public void CheckForError()
        {
#if DEBUG
            if (GpuContext == null)
            {
                return;
            }

            if (!inBegin)
            {
                var code = GpuContext.GetError();
                if (code != ErrorCode.NoError)
                {
                    throw new Exception($"GL Error: {code}");
                }
            }
#endif
        }

        public void Clear(ClearBufferMask mask)
        {
            Clear((int)mask);
        }

        public void Clear(int mask)
        {
            GpuContext?.Clear(mask);
            CheckForError();
        }

        public void ClearDepth(double depth)
        {
            GpuContext?.ClearDepth(depth);
            CheckForError();
        }

        public void Color4(Color color)
        {
            Color4(color.red, color.green, color.blue, color.alpha);
        }

        public void Color4(int red, int green, int blue, int alpha)
        {
            Color4((byte)red, (byte)green, (byte)blue, (byte)alpha);
        }

        public void Color4(byte red, byte green, byte blue, byte alpha)
        {
            GpuContext?.Color4(red, green, blue, alpha);
            CheckForError();
        }

        public void ColorMask(bool red, bool green, bool blue, bool alpha)
        {
            GpuContext?.ColorMask(red, green, blue, alpha);
            CheckForError();
        }

        public void ColorMaterial(MaterialFace face, ColorMaterialParameter mode)
        {
            GpuContext?.ColorMaterial(face, mode);
            CheckForError();
        }

        public void ColorPointer(int size, ColorPointerType type, int stride, byte[] pointer)
        {
            unsafe
            {
                fixed (byte* intPointer = pointer)
                {
                    ColorPointer(size, type, stride, (IntPtr)intPointer);
                }
            }
        }

        public void ColorPointer(int size, ColorPointerType type, int stride, IntPtr pointer)
        {
            GpuContext?.ColorPointer(size, type, stride, pointer);
            CheckForError();
        }

        public void CullFace(CullFaceMode mode)
        {
            GpuContext?.CullFace(mode);
            CheckForError();
        }

        public void DeleteBuffer(int buffer)
        {
            GpuContext?.DeleteBuffer(buffer);
            CheckForError();
        }

        public void DeleteTexture(int textures)
        {
            GpuContext?.DeleteTexture(textures);
            CheckForError();
        }

        public void DepthFunc(DepthFunction func)
        {
            DepthFunc((int)func);
            CheckForError();
        }

        public void DepthFunc(int func)
        {
            GpuContext?.DepthFunc(func);
            CheckForError();
        }

        public void DepthMask(bool flag)
        {
            GpuContext?.DepthMask(flag);
            CheckForError();
        }

        public void Disable(int cap)
        {
            isEnabled[cap] = false;

            GpuContext?.Disable(cap);
            CheckForError();
        }

        public void Disable(EnableCap cap)
        {
            Disable((int)cap);
        }

        public void DisableClientState(ArrayCap array)
        {
            GpuContext?.DisableClientState(array);
            CheckForError();
        }

        public void DrawArrays(BeginMode mode, int first, int count)
        {
            GpuContext?.DrawArrays(mode, first, count);
            CheckForError();
        }

        public void DrawRangeElements(BeginMode mode, int start, int end, int count, DrawElementsType type, IntPtr indices)
        {
            GpuContext?.DrawRangeElements(mode, start, end, count, type, indices);
            CheckForError();
        }

        public void Enable(int cap)
        {
            isEnabled[cap] = true;
            GpuContext?.Enable(cap);
            CheckForError();
        }

        public void Enable(EnableCap cap)
        {
            Enable((int)cap);
        }

        public void EnableClientState(ArrayCap arrayCap)
        {
            GpuContext?.EnableClientState(arrayCap);
            CheckForError();
        }

        public bool EnableState(int cap)
        {
            if (isEnabled.ContainsKey(cap))
            {
                return isEnabled[cap];
            }

            return false;
        }

        public bool EnableState(EnableCap cap)
        {
            return EnableState((int)cap);
        }

        public void End()
        {
            GpuContext?.End();
            inBegin = false;

            CheckForError();
        }

        public void GenTextures(int v, out int tex)
        {
            tex = 0;
            GpuContext?.GenTextures(v, out tex);
        }

        public void TexParameteri(int target, int pname, int param)
        {
            GpuContext?.TexParameteri(target, pname, param);
        }

        public void Finish()
        {
            GpuContext?.Finish();
            CheckForError();
        }

        public void FrontFace(FrontFaceDirection mode)
        {
            GpuContext?.FrontFace(mode);
            CheckForError();
        }

        public int GenBuffer()
        {
            if (GpuContext != null)
            {
                var buffer = GpuContext.GenBuffer();
                CheckForError();
                return buffer;
            }

            return 0;
        }

        public int GenTexture()
        {
            var texture = GpuContext?.GenTexture();
            CheckForError();
            return texture.Value;
        }

        public ErrorCode GetError()
        {
            if (GpuContext != null)
            {
                return GpuContext.GetError();
            }

            return ErrorCode.NoError;
        }

        public void GenFramebuffers(int n, out int framebuffers)
        {
            framebuffers = 0;
            GpuContext?.GenFramebuffers(n, out framebuffers);
            CheckForError();
        }

        public void FramebufferTexture2D(int target, int attachment, int textarget, int texture, int level)
        {
            GpuContext?.FramebufferTexture2D(target, attachment, textarget, texture, level);
            CheckForError();
        }

        public string GetString(StringName name)
        {
            if (GpuContext != null)
            {
                CheckForError();
                return GpuContext.GetString(name);
            }

            return "";
        }

        public void IndexPointer(IndexPointerType type, int stride, IntPtr pointer)
        {
            GpuContext?.IndexPointer(type, stride, pointer);
            CheckForError();
        }

        public void BufferData(int target, float[] v, int usage)
        {
            unsafe
            {
                fixed (float* data = v)
                {
                    BufferData(target, sizeof(float) * v.Length, (IntPtr)data, usage);
                }
            }
        }

        public void BufferData(int target, PositionNormal[] v, int usage)
        {
            unsafe
            {
                fixed (PositionNormal* data = v)
                {
                    BufferData(target, sizeof(PositionNormal) * v.Length, (IntPtr)data, usage);
                }
            }
        }

        public void UniformMatrix4fv(int location, int count, int transpose, float[] value)
        {
            GpuContext?.UniformMatrix4fv(location, count, transpose, value);
            CheckForError();
        }

        public void BufferData(int target, int[] faceIndex, int usage)
        {
            unsafe
            {
                fixed (int* data = faceIndex)
                {
                    BufferData(target, sizeof(int) * faceIndex.Length, (IntPtr)data, usage);
                }
            }
        }

        public void GenVertexArrays(int v, out int vAO)
        {
            vAO = 0;
            GpuContext?.GenVertexArrays(v, out vAO);
            CheckForError();
        }

        public void VertexAttribPointer(int index, int size, int type, int normalized, int stride, IntPtr pointer)
        {
            GpuContext?.VertexAttribPointer(index, size, type, normalized, stride, pointer);
            CheckForError();
        }

        public void EnableVertexAttribArray(int index)
        {
            GpuContext?.EnableVertexAttribArray(index);
            CheckForError();
        }

        public void Light(LightName light, LightParameter pname, float[] param)
        {
            GpuContext?.Light(light, pname, param);
            CheckForError();
        }

        public void GenBuffers(int n, out int buffer)
        {
            buffer = 0;
            GpuContext?.GenBuffers(n, out buffer);
            CheckForError();
        }

        public void print_shader_info_log(int shader)
        {
            var shaderInfo = GpuContext?.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(shaderInfo))
            {
                Debug.WriteLine(shaderInfo);
            }
        }

        public int load_shader(string src, int shaderType)
        {
            if (string.IsNullOrEmpty(src))
            {
                return 0;
            }

            int s = CreateShader(shaderType);
            if (s == 0)
            {
                Debug.WriteLine("Error: load_shader() failed to create shader.\n");
                return 0;
            }
            // Pass shader source string
            ShaderSource(s, 1, src, null);
            CompileShader(s);
            // Print info log (if any)
            print_shader_info_log(s);
            return s;
        }

        private void CompileShader(int id)
        {
            GpuContext?.CompileShader(id);
            CheckForError();
        }

        private void ShaderSource(int id, int count, string src, object p)
        {
            GpuContext?.ShaderSource(id, count, src, p);
            CheckForError();
        }

        public int CreateShader(int shaderType)
        {
            var id = GpuContext?.CreateShader(shaderType);
            CheckForError();
            return id == null ? 0 : id.Value;
        }

        public bool create_shader_program(string geom_source,
            string vert_source,
            string frag_source,
            out int id)
        {
            id = CreateProgram();
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
                AttachShader(id, g);
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

                AttachShader(id, v);
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
                AttachShader(id, f);
            }

            // Link program
            LinkProgram(id);

            void detach(int idIn, int shader)
            {
                if (shader != 0)
                {
                    DetachShader(idIn, shader);
                    DeleteShader(shader);
                }
            }

            detach(id, g);
            detach(id, f);
            detach(id, v);

            return true;
        }

        private void DeleteShader(int shader)
        {
            GpuContext?.DeleteShader(shader);
        }

        private void DetachShader(int id, int shader)
        {
            GpuContext?.DetachShader(id, shader);
        }

        private void LinkProgram(int id)
        {
            GpuContext?.LinkProgram(id);
        }

        private void AttachShader(int program, int shader)
        {
            GpuContext?.AttachShader(program, shader);
        }

        private int CreateProgram()
        {
            var id = GpuContext?.CreateProgram();
            CheckForError();
            return id == null ? 0 : id.Value;
        }

        public void Uniform1f(int location, float v0)
        {
            GpuContext?.Uniform1f(location, v0);
            CheckForError();
        }

        public void LoadIdentity()
        {
            GpuContext?.LoadIdentity();
            CheckForError();
        }

        public void ClearColor(double r, double g, double b, double a)
        {
            GpuContext?.ClearColor(r, g, b, a);
            CheckForError();
        }

        public int GenFramebuffer()
        {
            var texture = GpuContext?.GenFramebuffer();
            CheckForError();
            return texture.Value;
        }

        public void LoadMatrix(double[] m)
        {
            GpuContext?.LoadMatrix(m);
            CheckForError();
        }

        public void DrawElements(int mode, int count, int elementType, IntPtr indices)
        {
            GpuContext?.DrawElements(mode, count, elementType, indices);
            CheckForError();
        }

        public void MatrixMode(MatrixMode mode)
        {
            stacks.MatrixMode = mode;
            GpuContext?.MatrixMode(mode);
            CheckForError();
        }

        public void MultMatrix(float[] m)
        {
            GpuContext?.MultMatrix(m);
            CheckForError();
        }

        public void ActiveTexture(int texture)
        {
            GpuContext?.ActiveTexture(texture);
            CheckForError();
        }

        public void Normal3(double x, double y, double z)
        {
            GpuContext?.Normal3(x, y, z);
            CheckForError();
        }

        public void NormalPointer(NormalPointerType type, int stride, float[] pointer)
        {
            unsafe
            {
                fixed (float* floatPointer = pointer)
                {
                    NormalPointer(type, stride, (IntPtr)floatPointer);
                }
            }
        }

        public void NormalPointer(NormalPointerType type, int stride, IntPtr pointer)
        {
            GpuContext?.NormalPointer(type, stride, pointer);
            CheckForError();
        }

        public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
        {
            GpuContext?.Ortho(left, right, bottom, top, zNear, zFar);
            CheckForError();
        }

        public void PolygonOffset(float factor, float units)
        {
            GpuContext?.PolygonOffset(factor, units);
            CheckForError();
        }

        public void PopAttrib()
        {
            stacks.PopAttrib();
            GpuContext?.PopAttrib();
            CheckForError();
        }

        public void PopMatrix()
        {
            stacks.PopMatrix();
            GpuContext?.PopMatrix();
            CheckForError();
        }

        public void PushAttrib(AttribMask mask)
        {
            stacks.PushAttrib();
            GpuContext?.PushAttrib(mask);
            CheckForError();
        }

        public void Uniform1i(int location, int v0)
        {
            GpuContext?.Uniform1i(location, v0);
            CheckForError();
        }

        public void PushMatrix()
        {
            stacks.PushMatrix();
            GpuContext?.PushMatrix();
            CheckForError();
        }

        public int GetUniformLocation(int program, string name)
        {
            var value = GpuContext?.GetUniformLocation(program, name);
            CheckForError();
            return value == null ? 0 : value.Value;
        }

        public void UseProgram(int program)
        {
            GpuContext?.UseProgram(program);
            CheckForError();
        }

        public void Rotate(double angle, double x, double y, double z)
        {
            GpuContext?.Rotate(angle, x, y, z);
            CheckForError();
        }

        public void Scale(double x, double y, double z)
        {
            GpuContext?.Scale(x, y, z);
            CheckForError();
        }

        public void Scissor(int x, int y, int width, int height)
        {
            GpuContext?.Scissor(x, y, width, height);
            CheckForError();
        }

        public void ShadeModel(ShadingModel model)
        {
            GpuContext?.ShadeModel(model);
            CheckForError();
        }

        public void TexCoord2(Vector2 uv)
        {
            TexCoord2(uv.X, uv.Y);
        }

        public void TexCoord2(Vector2Float uv)
        {
            TexCoord2(uv.X, uv.Y);
        }

        public void TexCoord2(double x, double y)
        {
            GpuContext?.TexCoord2(x, y);
            CheckForError();
        }

        public void TexCoordPointer(int size, TexCordPointerType type, int stride, IntPtr pointer)
        {
            GpuContext?.TexCoordPointer(size, type, stride, pointer);
            CheckForError();
        }

        public void TexEnv(TextureEnvironmentTarget target, TextureEnvParameter pname, float param)
        {
            GpuContext?.TexEnv(target, pname, param);
            CheckForError();
        }

        public void TexImage2D(int target,
            int level,
            int internalFormat,
            int width,
            int height,
            int border,
            int format,
            int type,
            byte[] pixels)
        {
            GpuContext?.TexImage2D(target,
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

        public void TexImage2D(TextureTarget target,
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

        public void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
            GpuContext?.TexParameter(target, pname, param);
            CheckForError();
        }

        public void Translate(MatterHackers.VectorMath.Vector3 vector)
        {
            Translate(vector.X, vector.Y, vector.Z);
        }

        public void Translate(double x, double y, double z)
        {
            GpuContext?.Translate(x, y, z);
            CheckForError();
        }

        public void Vertex2(Vector2 position)
        {
            Vertex2(position.X, position.Y);
        }

        public void Vertex2(double x, double y)
        {
            GpuContext?.Vertex2(x, y);
            CheckForError();
        }

        public void Vertex3(Vector3 position)
        {
            Vertex3(position.X, position.Y, position.Z);
        }

        public void Vertex3(Vector3Float position)
        {
            Vertex3(position.X, position.Y, position.Z);
        }

        public void Vertex3(double x, double y, double z)
        {
            GpuContext?.Vertex3(x, y, z);
            CheckForError();
        }

        public void VertexPointer(int size, VertexPointerType type, int stride, float[] pointer)
        {
            unsafe
            {
                fixed (float* pArray = pointer)
                {
                    VertexPointer(size, type, stride, new IntPtr(pArray));
                }
            }
        }

        public void VertexPointer(int size, VertexPointerType type, int stride, IntPtr pointer)
        {
            GpuContext?.VertexPointer(size, type, stride, pointer);
            CheckForError();
        }

        public void Viewport(int x, int y, int width, int height)
        {
            GpuContext?.Viewport(x, y, width, height);
            CheckForError();
        }

        public void EnableOrDisable(EnableCap depthTest, bool doDepthTest)
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

        public int GenLists(int v)
        {
            var result = GpuContext?.GenLists(v);
            CheckForError();
            return result ?? 0;
        }

        public void NewList(int displayListId, object compile)
        {
            GpuContext?.NewList(displayListId, compile);
            CheckForError();
        }

        public void EndList()
        {
            GpuContext?.EndList();
            CheckForError();
        }

        public void CallList(int displayListId)
        {
            GpuContext?.CallList(displayListId);
            CheckForError();
        }

        public void DeleteLists(int id, int v)
        {
            GpuContext?.DeleteLists(id, v);
            CheckForError();
        }
    }
}
