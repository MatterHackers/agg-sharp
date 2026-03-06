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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public enum SceneRenderPass
	{
		Opaque,
		Transparent,
		Overlay,
	}

	public sealed class SceneRenderContext
	{
		public SceneRenderContext(WorldView worldView, RectangleDouble viewport, LightingData lighting)
		{
			WorldView = worldView;
			Viewport = viewport;
			Lighting = lighting;
		}

		public LightingData Lighting { get; }

		public RectangleDouble Viewport { get; }

		public WorldView WorldView { get; }
	}

	public sealed class MeshRenderCommand
	{
		public bool AllowBspRendering { get; init; } = true;

		public bool BlendTexture { get; init; } = true;

		public Color Color { get; init; }

		public bool ForceCullBackFaces { get; init; } = true;

		public Mesh Mesh { get; init; }

		public Action MeshChanged { get; init; }

		public Matrix4X4? MeshToViewTransform { get; init; }

		public RenderTypes RenderType { get; init; } = RenderTypes.Shaded;

		public Matrix4X4 Transform { get; init; } = Matrix4X4.Identity;

		public Color WireFrameColor { get; init; } = default;
	}

	public interface INativeSceneRenderer
	{
		bool IsSceneRenderingActive { get; }

		void BeginSceneRendering(SceneRenderContext context);

		void EndSceneRendering();

		bool CanRender(MeshRenderCommand command);

		bool TryRender(MeshRenderCommand command);
	}
}
