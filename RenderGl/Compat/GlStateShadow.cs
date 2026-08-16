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

using System.Collections.Generic;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// The shadow copy of GL's fixed function state. GL is a state machine and WebGPU is not: nothing
	/// here reaches the device when it is set, it is read at draw time to pick a pipeline permutation.
	/// That is the whole reason this class exists separately from the draw path.
	/// </summary>
	public class GlStateShadow
	{
		private readonly Dictionary<int, bool> enableCaps = new Dictionary<int, bool>();
		private readonly Dictionary<ArrayCap, bool> arrayCaps = new Dictionary<ArrayCap, bool>();
		private readonly int[] boundTextures = new int[8];
		private readonly Stack<(AttribMask Mask, GlViewportRect Viewport)> attribStack
			= new Stack<(AttribMask, GlViewportRect)>();

		/// <summary>Per light fixed function parameters, in the order the uniform block wants them.</summary>
		public GlLightState[] Lights { get; } = { new GlLightState(), new GlLightState() };

		/// <summary>GL's source blend factor, as the raw GL enum value.</summary>
		public int BlendSourceFactor { get; set; } = 1;

		/// <summary>GL's destination blend factor, as the raw GL enum value.</summary>
		public int BlendDestinationFactor { get; set; }

		/// <summary>Whether passing fragments write depth.</summary>
		public bool DepthMask { get; set; } = true;

		/// <summary>The depth comparison, already mapped out of GL's enum.</summary>
		public CompareFunction DepthCompare { get; set; } = CompareFunction.Less;

		/// <summary>
		/// Which channels are written. In WebGPU this is baked into the pipeline, so changing it picks a
		/// different cached pipeline rather than setting device state - that is what makes the LCD text
		/// three-pass composite work here.
		/// </summary>
		public ColorWriteMask ColorWriteMask { get; set; } = ColorWriteMask.All;

		/// <summary>Which faces <c>glCullFace</c> selected; only applied when culling is enabled.</summary>
		public CullMode CullFaceMode { get; set; } = CullMode.Back;

		/// <summary>Whether counter-clockwise winding is front facing.</summary>
		public bool FrontFaceCcw { get; set; } = true;

		/// <summary>Whether <c>glShadeModel(GL_FLAT)</c> is in effect.</summary>
		public bool FlatShading { get; set; }

		/// <summary>The <c>glPolygonOffset</c> slope factor.</summary>
		public float PolygonOffsetFactor { get; set; }

		/// <summary>The <c>glPolygonOffset</c> constant units.</summary>
		public float PolygonOffsetUnits { get; set; }

		/// <summary>True when the texture environment is GL_REPLACE rather than the default GL_MODULATE.</summary>
		public bool TextureEnvironmentReplace { get; set; }

		/// <summary>Whether <c>GL_TEXTURE_2D</c> is enabled, shadowed separately because it gates texturing.</summary>
		public bool Texture2DEnabled { get; private set; }

		/// <summary>Whether the scissor test is enabled.</summary>
		public bool ScissorEnabled { get; private set; }

		/// <summary>The texture unit <c>glActiveTexture</c> selected.</summary>
		public int ActiveTextureUnit { get; set; }

		/// <summary>The viewport in GL coordinates, y measured from the bottom.</summary>
		public GlViewportRect Viewport { get; set; }

		/// <summary>True once <c>glViewport</c> has been called at least once.</summary>
		public bool ViewportSet { get; private set; }

		/// <summary>The scissor rectangle in GL coordinates, y measured from the bottom.</summary>
		public GlViewportRect Scissor { get; set; }

		/// <summary>
		/// The clear color <c>glClearColor</c> set. Transparent black is GL's own initial value, and the
		/// distinction is not academic: thumbnail and icon captures clear an offscreen target and expect
		/// the untouched pixels to stay transparent, which an opaque black default would silently fill in.
		/// </summary>
		public MatterHackers.RenderCore.ClearColor ClearValue { get; set; } = MatterHackers.RenderCore.ClearColor.Transparent;

		/// <summary>Whether fixed function lighting is on.</summary>
		public bool LightingEnabled => this.IsEnabled(EnableCap.Lighting);

		/// <summary>Whether blending is on.</summary>
		public bool BlendEnabled => this.IsEnabled(EnableCap.Blend);

		/// <summary>Whether depth testing is on.</summary>
		public bool DepthTestEnabled => this.IsEnabled(EnableCap.DepthTest);

		/// <summary>Whether face culling is on.</summary>
		public bool CullingEnabled => this.IsEnabled(EnableCap.CullFace);

		/// <summary>Whether polygon offset fill is on.</summary>
		public bool PolygonOffsetEnabled => this.IsEnabled(EnableCap.PolygonOffsetFill);

		/// <summary>Enables a capability.</summary>
		/// <param name="capability">The raw GL capability value.</param>
		public void Enable(int capability) => this.SetCapability(capability, true);

		/// <summary>Disables a capability.</summary>
		/// <param name="capability">The raw GL capability value.</param>
		public void Disable(int capability) => this.SetCapability(capability, false);

		/// <summary>Reads a capability, defaulting to disabled as GL does.</summary>
		/// <param name="capability">The capability to read.</param>
		public bool IsEnabled(EnableCap capability)
			=> this.enableCaps.TryGetValue((int)capability, out bool enabled) && enabled;

		/// <summary>Sets a client array capability.</summary>
		/// <param name="arrayCap">The array to change.</param>
		/// <param name="enabled">Whether it is enabled.</param>
		public void SetClientState(ArrayCap arrayCap, bool enabled) => this.arrayCaps[arrayCap] = enabled;

		/// <summary>Reads a client array capability.</summary>
		/// <param name="arrayCap">The array to read.</param>
		public bool IsClientStateEnabled(ArrayCap arrayCap)
			=> this.arrayCaps.TryGetValue(arrayCap, out bool enabled) && enabled;

		/// <summary>Binds a texture name to the active texture unit.</summary>
		/// <param name="texture">The texture name, or 0 for none.</param>
		public void BindTexture(int texture)
		{
			if (this.ActiveTextureUnit >= 0 && this.ActiveTextureUnit < this.boundTextures.Length)
			{
				this.boundTextures[this.ActiveTextureUnit] = texture;
			}
		}

		/// <summary>The texture name bound to a unit, or 0.</summary>
		/// <param name="unit">The texture unit.</param>
		public int BoundTexture(int unit)
			=> unit >= 0 && unit < this.boundTextures.Length ? this.boundTextures[unit] : 0;

		/// <summary>Records the viewport, marking it as explicitly set.</summary>
		/// <param name="rect">The new viewport in GL coordinates.</param>
		public void SetViewport(GlViewportRect rect)
		{
			this.Viewport = rect;
			this.ViewportSet = true;
		}

		/// <summary>
		/// Saves state for a later <see cref="PopAttrib"/>. Only the viewport is actually saved, which
		/// is what the classic path saves; every other bit of the mask is accepted and ignored, and
		/// widening that is a change of behavior, not a fix.
		/// </summary>
		/// <param name="mask">The GL attribute mask.</param>
		public void PushAttrib(AttribMask mask) => this.attribStack.Push((mask, this.Viewport));

		/// <summary>
		/// Restores what <see cref="PushAttrib"/> saved and reports whether the viewport changed, so the
		/// caller can push it at the encoder.
		/// </summary>
		/// <param name="restoredViewport">The viewport to restore, when this returns true.</param>
		public bool PopAttrib(out GlViewportRect restoredViewport)
		{
			restoredViewport = this.Viewport;
			if (this.attribStack.Count == 0)
			{
				return false;
			}

			var saved = this.attribStack.Pop();
			if ((saved.Mask & AttribMask.ViewportBit) == 0)
			{
				return false;
			}

			restoredViewport = saved.Viewport;
			this.Viewport = saved.Viewport;
			return true;
		}

		/// <summary>
		/// Maps GL's blend factor enum onto WebGPU's. Unknown values become
		/// <see cref="BlendFactor.One"/>, matching the classic path's fallback.
		/// </summary>
		/// <param name="glFactor">The raw GL blend factor.</param>
		public static BlendFactor MapBlendFactor(int glFactor)
		{
			switch (glFactor)
			{
				case 0: return BlendFactor.Zero;
				case 1: return BlendFactor.One;
				case 0x0300: return BlendFactor.Src;
				case 0x0301: return BlendFactor.OneMinusSrc;
				case 0x0302: return BlendFactor.SrcAlpha;
				case 0x0303: return BlendFactor.OneMinusSrcAlpha;
				case 0x0304: return BlendFactor.DstAlpha;
				case 0x0305: return BlendFactor.OneMinusDstAlpha;
				case 0x0306: return BlendFactor.Dst;
				case 0x0307: return BlendFactor.OneMinusDst;
				default: return BlendFactor.One;
			}
		}

		/// <summary>Maps GL's depth comparison enum onto WebGPU's, defaulting to Less as GL does.</summary>
		/// <param name="glFunction">The raw GL comparison value.</param>
		public static CompareFunction MapCompareFunction(int glFunction)
		{
			switch (glFunction)
			{
				case 0x0200: return CompareFunction.Never;
				case 0x0201: return CompareFunction.Less;
				case 0x0202: return CompareFunction.Equal;
				case 0x0203: return CompareFunction.LessEqual;
				case 0x0204: return CompareFunction.Greater;
				case 0x0205: return CompareFunction.NotEqual;
				case 0x0206: return CompareFunction.GreaterEqual;
				case 0x0207: return CompareFunction.Always;
				default: return CompareFunction.Less;
			}
		}

		/// <summary>
		/// Maps GL's primitive mode onto WebGPU's. A fan arrives here still labelled a fan - its vertices
		/// were rewritten as a list by <see cref="GlImmediateModeBuffer.ConvertTriangleFanToTriangles"/>
		/// without relabelling, exactly as the classic path leaves it - so it falls through to
		/// <see cref="PrimitiveTopology.TriangleList"/> with everything else.
		/// </summary>
		/// <param name="mode">The GL primitive mode.</param>
		public static PrimitiveTopology MapTopology(BeginMode mode)
		{
			switch (mode)
			{
				case BeginMode.Lines: return PrimitiveTopology.LineList;
				case BeginMode.TriangleStrip: return PrimitiveTopology.TriangleStrip;
				default: return PrimitiveTopology.TriangleList;
			}
		}

		private void SetCapability(int capability, bool enabled)
		{
			this.enableCaps[capability] = enabled;
			if (capability == (int)EnableCap.Texture2D)
			{
				this.Texture2DEnabled = enabled;
			}

			if (capability == (int)EnableCap.ScissorTest)
			{
				this.ScissorEnabled = enabled;
			}
		}
	}

	/// <summary>A viewport or scissor rectangle in GL's coordinates, with y measured from the bottom.</summary>
	public readonly struct GlViewportRect
	{
		/// <summary>Creates a rectangle.</summary>
		/// <param name="x">Left edge.</param>
		/// <param name="y">Bottom edge, measured up from the bottom of the target.</param>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		public GlViewportRect(int x, int y, int width, int height)
		{
			this.X = x;
			this.Y = y;
			this.Width = width;
			this.Height = height;
		}

		/// <summary>Left edge.</summary>
		public int X { get; }

		/// <summary>Bottom edge, measured up from the bottom of the target.</summary>
		public int Y { get; }

		/// <summary>Width in pixels.</summary>
		public int Width { get; }

		/// <summary>Height in pixels.</summary>
		public int Height { get; }

		/// <inheritdoc/>
		public override string ToString() => $"{this.X},{this.Y} {this.Width}x{this.Height}";
	}

	/// <summary>One fixed function light's parameters, in the defaults GL starts with.</summary>
	public class GlLightState
	{
		/// <summary>Eye-space position; w = 0 means a directional light.</summary>
		public float[] Position { get; } = { 0, 0, 1, 0 };

		/// <summary>Ambient color.</summary>
		public float[] Ambient { get; } = { 0, 0, 0, 1 };

		/// <summary>Diffuse color.</summary>
		public float[] Diffuse { get; } = { 1, 1, 1, 1 };

		/// <summary>Specular color. Carried for parity; the canned shaders do not read it yet.</summary>
		public float[] Specular { get; } = { 1, 1, 1, 1 };
	}
}
