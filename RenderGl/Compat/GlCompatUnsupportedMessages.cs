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
*/

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// The exception messages <see cref="GlCompatContext"/> throws from the GL entry points it does not
	/// emulate (user shaders) or has not implemented yet, each saying what to do instead.
	/// </summary>
	internal static class GlCompatUnsupportedMessages
	{
		internal const string ShaderMessage =
			"GlCompatContext does not emulate user shaders. The renderer draws through the canned "
			+ "pipelines named in GlShaderKeys; add a canned combo there instead of compiling GLSL.";

		internal const string MeshFallbackMessage =
			"TODO (port plan, Phase 3): the client-array draw path is the legacy GL mesh fallback that "
			+ "runs when INativeSceneRenderer.CanRender returns false. The plan closes those gaps in the "
			+ "native renderer rather than teaching the compat layer lit and textured mesh drawing.";

		internal const string FramebufferMessage =
			"TODO (port plan, Phase 2/3): render-to-texture goes through GlCompatContext.SetRenderTarget "
			+ "rather than GL framebuffer objects. Wire the remaining callers to that.";

		internal const string BufferObjectMessage =
			"TODO (port plan, Phase 3): GL buffer objects have no consumer in the renderer today; "
			+ "retained vertex data is owned by the scene renderer, not by GL names.";
	}
}
