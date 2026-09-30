/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The light rig a 3D scene frame is shaded with. The defaults are the classic look (a flat 0.2
	/// ambient and two Lambert lights); <see cref="LightingPresets"/> holds the alternatives. Directions
	/// are in eye space.
	/// </summary>
	public class LightingData
	{
		/// <summary>A flat ambient added to both hemisphere halves (and the GL-compat light 0).</summary>
		public float[] AmbientLight { get; set; } = { 0, 0, 0, 0 };

		/// <summary>Ambient a face pointing up the screen receives; faces between blend toward <see cref="GroundAmbient"/>.</summary>
		public float[] SkyAmbient { get; set; } = { 0.2f, 0.2f, 0.2f, 1.0f };

		/// <summary>Ambient a face pointing down the screen receives.</summary>
		public float[] GroundAmbient { get; set; } = { 0.2f, 0.2f, 0.2f, 1.0f };

		public float[] DiffuseLight0 { get; set; } = { 0.7f, 0.7f, 0.7f, 1.0f };
		public float[] SpecularLight0 { get; set; } = { 0.5f, 0.5f, 0.5f, 1.0f };
		public float[] LightDirection0 { get; set; } = { -1, -1, 1, 0.0f };

		public float[] DiffuseLight1 { get; set; } = { 0.5f, 0.5f, 0.5f, 1.0f };
		public float[] SpecularLight1 { get; set; } = { 0.3f, 0.3f, 0.3f, 1.0f };
		public float[] LightDirection1 { get; set; } = { 1, 1, 1, 0.0f };

		/// <summary>Brightness of the untinted highlight off light 0; 0 turns it off.</summary>
		public float SpecularStrength { get; set; }

		/// <summary>Blinn-Phong exponent: higher is a smaller, tighter highlight.</summary>
		public float SpecularPower { get; set; } = 32;

		/// <summary>Brightness of the untinted rim on faces turned edge-on to the viewer; 0 turns it off.</summary>
		public float RimStrength { get; set; }

		/// <summary>
		/// Copies every value from <paramref name="source"/> into this instance, so a holder of a
		/// LightingData (a view's own rig) can switch presets without swapping the reference.
		/// </summary>
		public void CopyFrom(LightingData source)
		{
			this.AmbientLight = (float[])source.AmbientLight.Clone();
			this.SkyAmbient = (float[])source.SkyAmbient.Clone();
			this.GroundAmbient = (float[])source.GroundAmbient.Clone();
			this.DiffuseLight0 = (float[])source.DiffuseLight0.Clone();
			this.SpecularLight0 = (float[])source.SpecularLight0.Clone();
			this.LightDirection0 = (float[])source.LightDirection0.Clone();
			this.DiffuseLight1 = (float[])source.DiffuseLight1.Clone();
			this.SpecularLight1 = (float[])source.SpecularLight1.Clone();
			this.LightDirection1 = (float[])source.LightDirection1.Clone();
			this.SpecularStrength = source.SpecularStrength;
			this.SpecularPower = source.SpecularPower;
			this.RimStrength = source.RimStrength;
		}
	}
}