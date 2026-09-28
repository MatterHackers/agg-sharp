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

using System;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The pan/zoom of a <see cref="ScenePanZoom"/> (agg-gui's scene::SceneTransform): a uniform scale followed by a
	/// translation, so <c>screen = Zoom * scene + Offset</c>. Screen is the ScenePanZoom's own local space; scene is
	/// the hosted content's space. Pure maths so it is tested without a widget.
	/// </summary>
	public readonly struct SceneTransform
	{
		public SceneTransform(double zoom, Vector2 offset)
		{
			this.Zoom = zoom;
			this.Offset = offset;
		}

		public static SceneTransform Identity => new SceneTransform(1, Vector2.Zero);

		public double Zoom { get; }

		public Vector2 Offset { get; }

		public Vector2 SceneToScreen(Vector2 scene) => new Vector2(this.Zoom * scene.X + this.Offset.X, this.Zoom * scene.Y + this.Offset.Y);

		public Vector2 ScreenToScene(Vector2 screen)
		{
			// A zero zoom cannot come out of ZoomAt or Fit (both clamp), but a caller-built one must not divide by zero.
			double zoom = Math.Max(this.Zoom, 1e-12);
			return new Vector2((screen.X - this.Offset.X) / zoom, (screen.Y - this.Offset.Y) / zoom);
		}

		/// <summary>The child-to-parent transform the content gets as its ParentToChildTransform.</summary>
		public Affine ToAffine() => Affine.NewScaling(this.Zoom) * Affine.NewTranslation(this.Offset);

		/// <summary>The part of scene space a <paramref name="width"/> x <paramref name="height"/> view shows.</summary>
		public RectangleDouble VisibleSceneRect(double width, double height)
		{
			Vector2 bottomLeft = this.ScreenToScene(Vector2.Zero);
			double zoom = Math.Max(this.Zoom, 1e-12);
			return new RectangleDouble(bottomLeft.X, bottomLeft.Y, bottomLeft.X + width / zoom, bottomLeft.Y + height / zoom);
		}

		/// <summary>Moves the scene by <paramref name="screenDelta"/> screen pixels.</summary>
		public SceneTransform Pan(Vector2 screenDelta) => new SceneTransform(this.Zoom, this.Offset + screenDelta);

		/// <summary>
		/// Zooms to <paramref name="newZoom"/> (clamped to <paramref name="minZoom"/>..<paramref name="maxZoom"/>) keeping
		/// the scene point under <paramref name="screenAnchor"/> where it is on screen - zoom about the cursor.
		/// </summary>
		public SceneTransform ZoomAt(Vector2 screenAnchor, double newZoom, double minZoom, double maxZoom)
		{
			double zoom = ClampZoom(newZoom, minZoom, maxZoom);
			Vector2 scene = this.ScreenToScene(screenAnchor);
			return new SceneTransform(zoom, new Vector2(screenAnchor.X - zoom * scene.X, screenAnchor.Y - zoom * scene.Y));
		}

		/// <summary>
		/// Centres <paramref name="content"/> in a <paramref name="width"/> x <paramref name="height"/> view at the
		/// largest zoom that shows all of it, clamped to the range.
		/// </summary>
		public static SceneTransform Fit(RectangleDouble content, double width, double height, double minZoom, double maxZoom)
		{
			double contentWidth = Math.Max(content.Width, 1e-9);
			double contentHeight = Math.Max(content.Height, 1e-9);
			double zoom = ClampZoom(Math.Min(width / contentWidth, height / contentHeight), minZoom, maxZoom);
			Vector2 center = content.Center;
			return new SceneTransform(zoom, new Vector2(width / 2 - zoom * center.X, height / 2 - zoom * center.Y));
		}

		/// <summary>Clamps to the range, accepting it either way round (as agg-gui does).</summary>
		public static double ClampZoom(double zoom, double minZoom, double maxZoom)
		{
			return Math.Clamp(zoom, Math.Min(minZoom, maxZoom), Math.Max(minZoom, maxZoom));
		}
	}
}
