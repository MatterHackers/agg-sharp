/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
    using gl = GL;

    public class DepthPeeling
	{
		// shader id, vertex array object
		int scene_p_id = 0, tex_p_id;
		int VAO;
		int QVAO;
		// Number of passes
		const int renderPasses = 4;
		int[] tex_id = new int[renderPasses], dtex_id = new int[renderPasses], fbo_id = new int[renderPasses];
		// full width/height of window, width/height of viewports
		int full_w = 1440;
		int full_h = 480;
		int w => full_w / (renderPasses + 2);
		int h => full_h / 1;
		int faceCount;

		public DepthPeeling(Mesh mesh)
		{
			gl.create_shader_program(null, scene_v_shader, scene_f_shader, out scene_p_id);
			gl.create_shader_program(null, tex_v_shader, tex_f_shader, out tex_p_id);

			var aabb = mesh.GetAxisAlignedBoundingBox();
			mesh.Translate(-aabb.Center);
			mesh.Transform(Matrix4X4.CreateRotation(new Vector3(23, 51, 12)));
			mesh.Transform(Matrix4X4.CreateScale(1/ Math.Max(aabb.XSize, Math.Max(aabb.YSize, aabb.ZSize))));
			aabb = mesh.GetAxisAlignedBoundingBox();
			var mV = mesh.Vertices.ToFloatArray();
			var mF = mesh.Faces.ToIntArray();
			vao(mV, mF, out VAO);
			faceCount = mesh.Faces.Count;
		
			// square
			var QV =  new float[]
			{
				-1, -1, 0, 
				1, -1, 0,
				1, 1, 0,
				-1, 1, 0
			};
			var QF = new int[]
			{
				0, 1, 2,
				0, 2, 3
			};

			vao(QV, QF, out QVAO);
		}

		public void init_render_to_texture(int w, int h, out int tex, out int dtex, out int fbo)
		{
			void gen_tex(out int texIn)
			{
				// http://www.opengl.org/wiki/Framebuffer_Object_Examples#Quick_example.2C_render_to_texture_.282D.29
				gl.GenTextures(1, out texIn);
				gl.BindTexture(GL.TEXTURE_2D, texIn);
				gl.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, GL.NEAREST);
				gl.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, GL.NEAREST);
			}

			// Generate texture for colors and attached to color component of framebuffer
			gen_tex(out tex);
			gl.TexImage2D(GL.TEXTURE_2D, 0, GL.RGBA32F, w, h, 0, GL.BGRA, GL.FLOAT, null);
			gl.BindTexture(GL.TEXTURE_2D, 0);
			gl.GenFramebuffers(1, out fbo);
			gl.BindFramebuffer(GL.FRAMEBUFFER, fbo);
			// Generate texture for depth and attached to depth component of framebuffer
			gl.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, tex, 0);
			gen_tex(out dtex);
			gl.TexImage2D(GL.TEXTURE_2D, 0, GL.DEPTH_COMPONENT32, w, h, 0, GL.DEPTH_COMPONENT, GL.FLOAT, null);
			gl.FramebufferTexture2D(GL.FRAMEBUFFER, GL.DEPTH_ATTACHMENT, GL.TEXTURE_2D, dtex, 0);
			// Clean up
			gl.BindFramebuffer(GL.FRAMEBUFFER, 0);
			gl.BindTexture(GL.TEXTURE_2D, 0);
		}

		// Prepare VAOs
		void vao(float[] verticesPositions, int[] faceVertexIndices, out int VAO)
		{
			// Generate vertex array
			gl.GenVertexArrays(1, out VAO);
			gl.BindVertexArray(VAO);

			// Generate Vertex Buffer
			gl.GenBuffers(1, out int VBO);
			gl.BindBuffer(GL.ARRAY_BUFFER, VBO);
			gl.BufferData(GL.ARRAY_BUFFER, verticesPositions, GL.STATIC_DRAW);

			// Generate Index Buffer
			gl.GenBuffers(1, out int FBO);
			gl.BindBuffer(GL.ELEMENT_ARRAY_BUFFER, FBO);
			gl.BufferData(GL.ELEMENT_ARRAY_BUFFER, faceVertexIndices, GL.STATIC_DRAW);
			gl.VertexAttribPointer(0, 3, GL.FLOAT, GL.FALSE, 0, IntPtr.Zero);

			gl.EnableVertexAttribArray(0);
			gl.BindBuffer(GL.ARRAY_BUFFER, 0);
			gl.BindVertexArray(0);
		}

		int count = 0;

		// Main display routine
		public void glutDisplayFunc(WorldView worldView)
		{
			GL.PushAttrib(AttribMask.EnableBit | AttribMask.ViewportBit | AttribMask.TransformBit);
			// Projection and modelview matrices
			float near = 0.01f;
			float far = 20;
			var top = Math.Tan(35.0 / 360.0 * Math.PI) * near;
			var right = top * w / h;
			var proj = Matrix4X4.Frustum(-right, right, -top, top, near, far);
			// spin around
			var model = Matrix4X4.CreateRotationY(Math.PI / 180.0 * count++) * Matrix4X4.CreateTranslation(0, 0, -6.5);

			//proj = worldView.ProjectionMatrix;
			//model = worldView.ModelviewMatrix;

			GL.Disable(EnableCap.CullFace);

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadMatrix(proj.GetAsDoubleArray());
			GL.LoadIdentity();
			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			gl.Enable(GL.DEPTH_TEST);
			gl.Viewport(0, 0, w, h);
			// select program and attach uniforms
			gl.UseProgram(scene_p_id);
			int proj_loc = gl.GetUniformLocation(scene_p_id, "proj");
			gl.UniformMatrix4fv(proj_loc, 1, GL.FALSE, proj.GetAsFloatArray());
			int model_loc = gl.GetUniformLocation(scene_p_id, "model");
			gl.UniformMatrix4fv(model_loc, 1, GL.FALSE, model.GetAsFloatArray());
			gl.Uniform1f(gl.GetUniformLocation(scene_p_id, "width"), w);
			gl.Uniform1f(gl.GetUniformLocation(scene_p_id, "height"), h);
			gl.BindVertexArray(VAO);
			gl.Disable(GL.BLEND);
			for (int pass = 0; pass < renderPasses; pass++)
			{
				int first_pass = pass == 0 ? 1 : 0;
				gl.Uniform1i(gl.GetUniformLocation(scene_p_id, "first_pass"), first_pass);
				if (first_pass == 0)
				{
					gl.Uniform1i(gl.GetUniformLocation(scene_p_id, "depth_texture"), 0);
					gl.ActiveTexture(GL.TEXTURE0 + 0);
					GL.BindTexture(TextureTarget.Texture2D, dtex_id[pass - 1]);
				}
				gl.BindFramebuffer(GL.FRAMEBUFFER, fbo_id[pass]);
				gl.ClearColor(0.0, 0.4, 0.7, 0.0);
				gl.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);
				gl.DrawElements(GL.TRIANGLES, faceCount * 3, GL.UNSIGNED_INT, IntPtr.Zero);
			}
			// clean up and set to render to screen
			gl.BindVertexArray(0);
			gl.BindFramebuffer(GL.FRAMEBUFFER, 0);
			gl.ActiveTexture(GL.TEXTURE0 + 0);
			GL.BindTexture(TextureTarget.Texture2D, 0);

			// Get read to draw quads
			gl.BindVertexArray(QVAO);
			gl.ClearColor(0.0, 0.4, 0.7, 0.0);
			gl.Clear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);
			gl.UseProgram(tex_p_id);
			// Draw result of each peel
			for (int pass = 0; pass < renderPasses; pass++)
			{
				int color_tex_loc2 = gl.GetUniformLocation(tex_p_id, "color_texture");
				gl.Uniform1i(color_tex_loc2, 0);
				gl.ActiveTexture(GL.TEXTURE0 + 0);
				GL.BindTexture(TextureTarget.Texture2D, tex_id[pass]);
				int depth_tex_loc2 = gl.GetUniformLocation(tex_p_id, "depth_texture");
				gl.Uniform1i(depth_tex_loc2, 1);
				gl.ActiveTexture(GL.TEXTURE0 + 1);
				GL.BindTexture(TextureTarget.Texture2D, dtex_id[pass]);
				gl.Viewport(pass * w, 0 * h, w, h);
				gl.Uniform1i(gl.GetUniformLocation(tex_p_id, "show_depth"), 0);
				gl.DrawElements(GL.TRIANGLES, 6, GL.UNSIGNED_INT, IntPtr.Zero);
			}

			// Render final result as composite of all textures
			gl.BlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
			gl.Enable(GL.BLEND);
			gl.DepthFunc(GL.ALWAYS);
			gl.Viewport(renderPasses * w, 0 * h, w, h);
			gl.Uniform1i(gl.GetUniformLocation(tex_p_id, "show_depth"), 0);
			int color_tex_loc = gl.GetUniformLocation(tex_p_id, "color_texture");
			int depth_tex_loc = gl.GetUniformLocation(tex_p_id, "depth_texture");
			for (int pass = renderPasses - 1; pass >= 0; pass--)
			{
				gl.Uniform1i(color_tex_loc, 0);
				gl.ActiveTexture(GL.TEXTURE0 + 0);
				GL.BindTexture(TextureTarget.Texture2D, tex_id[pass]);
				gl.Uniform1i(depth_tex_loc, 1);
				gl.ActiveTexture(GL.TEXTURE0 + 1);
				GL.BindTexture(TextureTarget.Texture2D, dtex_id[pass]);
				gl.DrawElements(GL.TRIANGLES, 6, GL.UNSIGNED_INT, IntPtr.Zero);
			}
			gl.DepthFunc(GL.LESS);

			GL.PopAttrib();
			gl.BindVertexArray(0);
			GL.BindTexture(TextureTarget.Texture2D, 0);
			gl.UseProgram(0);
		}

		public void ReshapeFunc(int w, int h)
		{
			full_h = h;
			full_w = w;
			w = full_w / (renderPasses + 2);
			h = full_h / (1);
			// (re)-initialize textures and buffers
			for (var i = 0; i < renderPasses; i++)
			{
				init_render_to_texture(w, h, out tex_id[i], out dtex_id[i], out fbo_id[i]);
			}
		}

		// For rendering a full-viewport quad, set tex-coord from position
		string tex_v_shader = @"
		#version 330 core
		in vec3 position;
		out vec2 tex_coord;
		void main()
		{
		  gl_Position = vec4(position,1.);
		  tex_coord = vec2(0.5*(position.x+1), 0.5*(position.y+1));
		}
		";

		// Render directly from color or depth texture
		string tex_f_shader = @"
		#version 330 core
		in vec2 tex_coord;
		out vec4 color;
		uniform sampler2D color_texture;
		uniform sampler2D depth_texture;
		uniform bool show_depth;
		void main()
		{
		  vec4 depth = texture(depth_texture,tex_coord);
		  // Mask out background which is set to 1
		  if(depth.r<1)
		  {
			color = texture(color_texture,tex_coord);
			if(show_depth)
			{
			  // Depth of background seems to be set to exactly 1.
			  color.rgb = vec3(1,1,1)*(1.-depth.r)/0.006125;
			}
		  }else
		  {
			discard;
		  }
		}
		";

		// Pass-through vertex shader with projection and model matrices
		string scene_v_shader = @"
		#version 330 core
		uniform mat4 proj;
		uniform mat4 model;
		in vec3 position;
		void main()
		{
		  gl_Position = proj * model * vec4(position,1.);
		}
		";

		// Render if first pass or farther than closest frag on last pass
		string scene_f_shader = @"
		#version 330 core
		out vec4 color;
		uniform bool first_pass;
		uniform float width;
		uniform float height;
		uniform sampler2D depth_texture;
		void main()
		{
		  color = vec4(0.8,0.4,0.0,0.25);
		  color.rgb *= (1.-gl_FragCoord.z)/0.0006125;
		  if(!first_pass)
		  {
			vec2 tex_coord = vec2(float(gl_FragCoord.x)/width,float(gl_FragCoord.y)/height);
			float max_depth = texture(depth_texture,tex_coord).r;
			if(gl_FragCoord.z <= max_depth)
			{
			  discard;
			}
		  }
		}
		";
	}
}