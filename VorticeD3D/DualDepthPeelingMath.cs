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
using System.Numerics;

namespace MatterHackers.RenderGl
{
	public static class DualDepthPeelingMath
	{
		public static int GetIterationCount(int storedLayerBudget)
		{
			if (storedLayerBudget <= 0)
			{
				return 0;
			}

			return (storedLayerBudget + 1) / 2;
		}

		public static Vector4 AccumulateFrontLayer(Vector4 currentAccumulation, Vector4 layerColor)
		{
			float transmittance = currentAccumulation.W;
			float alpha = Clamp01(layerColor.W);
			var color = currentAccumulation;
			color.X += layerColor.X * alpha * transmittance;
			color.Y += layerColor.Y * alpha * transmittance;
			color.Z += layerColor.Z * alpha * transmittance;
			color.W = transmittance * (1.0f - alpha);
			return color;
		}

		public static Vector4 AccumulateBackLayer(Vector4 currentAccumulation, Vector4 layerColor)
		{
			float alpha = Clamp01(layerColor.W);
			float inverseAlpha = 1.0f - alpha;
			return new Vector4(
				layerColor.X * alpha + currentAccumulation.X * inverseAlpha,
				layerColor.Y * alpha + currentAccumulation.Y * inverseAlpha,
				layerColor.Z * alpha + currentAccumulation.Z * inverseAlpha,
				alpha + currentAccumulation.W * inverseAlpha);
		}

		public static Vector4 ResolveOverOpaque(Vector4 sceneColor, Vector4 frontAccumulation, Vector4 backAccumulation)
		{
			return ResolveForComposition(new Vector4(sceneColor.X, sceneColor.Y, sceneColor.Z, 1.0f), frontAccumulation, backAccumulation);
		}

		public static Vector4 ResolveForComposition(Vector4 sceneColor, Vector4 frontAccumulation, Vector4 backAccumulation)
		{
			float sceneAlpha = Clamp01(sceneColor.W);
			float remainingTransmittance = Clamp01(frontAccumulation.W * (1.0f - backAccumulation.W));
			float transparentAlpha = 1.0f - remainingTransmittance;
			float combinedAlpha = sceneAlpha + (1.0f - sceneAlpha) * transparentAlpha;
			float sceneWeight = sceneAlpha * remainingTransmittance;
			var premultipliedColor = new Vector3(
				frontAccumulation.X + frontAccumulation.W * backAccumulation.X + sceneWeight * sceneColor.X,
				frontAccumulation.Y + frontAccumulation.W * backAccumulation.Y + sceneWeight * sceneColor.Y,
				frontAccumulation.Z + frontAccumulation.W * backAccumulation.Z + sceneWeight * sceneColor.Z);

			if (combinedAlpha <= 1e-6f)
			{
				return Vector4.Zero;
			}

			return new Vector4(
				premultipliedColor.X / combinedAlpha,
				premultipliedColor.Y / combinedAlpha,
				premultipliedColor.Z / combinedAlpha,
				combinedAlpha);
		}

		private static float Clamp01(float value) => Math.Clamp(value, 0.0f, 1.0f);
	}
}
