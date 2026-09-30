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
using System.Linq;

namespace MatterHackers.RenderGl
{
	/// <summary>A named light rig, for choosing a scene look by eye.</summary>
	public sealed class LightingPreset
	{
		private readonly Func<LightingData> create;

		public LightingPreset(string name, string description, Func<LightingData> create)
		{
			this.Name = name;
			this.Description = description;
			this.create = create;
		}

		public string Name { get; }

		public string Description { get; }

		/// <summary>A fresh rig each call: RenderHelper.SetGlContext normalises LightDirection0 in place.</summary>
		public LightingData Create() => this.create();
	}

	/// <summary>
	/// The scene looks. MatterCAD's 3D view applies Onshape-like; "Current" is the classic look, which
	/// <c>new LightingData()</c> still gives, and MatterCAD's Debug Lighting menu offers it to compare.
	/// All values are plain data for NodeDesignerScene.wgsl's applyLighting, in eye space.
	/// </summary>
	public static class LightingPresets
	{
		public static LightingPreset Current { get; } = new LightingPreset(
			"Current",
			"Today's look: flat ambient and two plain lights.",
			() => new LightingData());

		public static IReadOnlyList<LightingPreset> All { get; } = new[]
		{
			Current,

			// Onshape: a cool-sky / warm-ground hemisphere plus a key from the upper left, low enough to
			// light side faces, puts a white box's faces at about 0.88 top, 0.76 keyed side, 0.60 far side.
			new LightingPreset(
				"Onshape-like",
				"Bright sky-and-ground fill, a key light from the upper left and a faint tight shine.",
				() => new LightingData
				{
					SkyAmbient = new float[] { 0.44f, 0.46f, 0.50f, 1 },
					GroundAmbient = new float[] { 0.33f, 0.31f, 0.29f, 1 },
					DiffuseLight0 = Gray(0.58f),
					LightDirection0 = new float[] { -0.6f, 0.65f, 1, 0 },
					DiffuseLight1 = Gray(0.2f),
					LightDirection1 = new float[] { 1, -0.3f, 0.6f, 0 },
					SpecularStrength = 0.1f,
					SpecularPower = 60,
				}),
		};

		/// <summary>The preset with this name, or <see cref="Current"/> when none matches (a stale saved choice).</summary>
		public static LightingPreset Find(string name)
			=> All.FirstOrDefault(preset => preset.Name == name) ?? Current;

		private static float[] Gray(float value) => new float[] { value, value, value, 1 };
	}
}
