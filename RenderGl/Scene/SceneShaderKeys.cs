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

using System.Collections.Generic;

namespace MatterHackers.RenderGl.Scene
{
	/// <summary>
	/// The scene renderer's half of the shader-name contract: module source keys and entry point names,
	/// authored as data because WGSL cannot be reflected at runtime.
	/// <para>
	/// The same strings appear as literals in the backend's shader-source provider, which cannot
	/// reference this project; a test compares the two lists, which is where that duplication is kept
	/// honest. This mirrors <c>GlShaderKeys</c>, the compat layer's equivalent.
	/// </para>
	/// </summary>
	public static class SceneShaderKeys
	{
		/// <summary>The mesh pipeline module: scene vertex stage, shading, depth prepass, selection mask.</summary>
		public const string SceneModule = "NodeDesignerScene";

		/// <summary>The full-screen compositor module: copy, transparency resolve, outline composite.</summary>
		public const string PostProcessModule = "NodeDesignerPostProcess";

		/// <summary>Vertex stage for scene meshes.</summary>
		public const string SceneVertexEntryPoint = "sceneVertexMain";

		/// <summary>Lit (or unlit) shading from the command's colour.</summary>
		public const string SceneColorEntryPoint = "sceneColorMain";

		/// <summary>Lit (or unlit) shading from the submesh's texture.</summary>
		public const string SceneTextureEntryPoint = "sceneTextureMain";

		/// <summary>
		/// The sorted alpha-blend transparency mode's textured shading: <see cref="SceneTextureEntryPoint"/>
		/// plus the analytic bed grid, because in that mode the bed is drawn by the ordinary transparent
		/// pass rather than by the peel (the classic <c>SceneTextureAlphaBlendPS</c>).
		/// </summary>
		public const string SceneBedTextureEntryPoint = "sceneBedTextureMain";

		/// <summary>Depth prepass: writes depth, discards what the colour pass would have discarded.</summary>
		public const string SceneDepthOnlyEntryPoint = "sceneDepthOnlyMain";

		/// <summary>
		/// Seeds the first peeled depth range: depth only, keeping every transparent fragment in front of
		/// the opaque scene (the classic <c>DualDepthInitPS</c>).
		/// </summary>
		public const string PeelInitEntryPoint = "peelInitMain";

		/// <summary>Depth-only peel iteration for an untextured submesh: keeps the fragments strictly
		/// inside the remaining range, so the pass's depth test narrows it.</summary>
		public const string PeelDepthColorEntryPoint = "peelDepthColorMain";

		/// <summary>Depth-only peel iteration for a textured submesh.</summary>
		public const string PeelDepthTextureEntryPoint = "peelDepthTextureMain";

		/// <summary>Front/back colour accumulation for an untextured submesh (<c>SceneColorDualPeelPS</c>).</summary>
		public const string PeelColorEntryPoint = "peelColorMain";

		/// <summary>Front/back colour accumulation for a textured submesh (<c>SceneTextureDualPeelPS</c>).</summary>
		public const string PeelTextureEntryPoint = "peelTextureMain";

		/// <summary>Position-only vertex stage for the selection mask.</summary>
		public const string SelectionVertexEntryPoint = "selectionVertexMain";

		/// <summary>Flat fill for the selection mask.</summary>
		public const string SelectionMaskEntryPoint = "selectionMaskMain";

		/// <summary>The three-vertex full-screen triangle.</summary>
		public const string FullscreenVertexEntryPoint = "fullscreenVertexMain";

		/// <summary>Straight texture copy.</summary>
		public const string CopyTextureEntryPoint = "copyTextureMain";

		/// <summary>The transparency resolve (an identity while the accumulation targets are cleared).</summary>
		public const string ResolveDualPeelEntryPoint = "resolveDualPeelMain";

		/// <summary>The selection outline composite.</summary>
		public const string OutlineCompositeEntryPoint = "outlineCompositeMain";

		/// <summary>One axis of the separable blur over the bed shadow mask.</summary>
		public const string BedShadowBlurEntryPoint = "bedShadowBlurMain";

		/// <summary>Tints the bed's own texture by the blurred shadow.</summary>
		public const string BedShadowCompositeEntryPoint = "bedShadowCompositeMain";

		/// <summary>The 9-tap box filter that resolves the 3x supersample capture.</summary>
		public const string Downsample3x3EntryPoint = "downsample3x3Main";

		private static readonly string[] Modules = { SceneModule, PostProcessModule };

		/// <summary>Every module key the scene renderer asks the backend for.</summary>
		public static IReadOnlyList<string> AllModuleKeys => Modules;
	}
}
