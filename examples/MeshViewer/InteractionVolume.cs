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

using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MeshVisualizer
{
	public class InteractionVolume
	{
		private MeshViewerWidget meshViewerToDrawWith;

		public MeshViewerWidget MeshViewerToDrawWith { get { return meshViewerToDrawWith; } }

		private IPrimitive collisionVolume;

		public IPrimitive CollisionVolume { get { return collisionVolume; } set { collisionVolume = value; } }

		public Matrix4X4 TotalTransform = Matrix4X4.Identity;

		public bool MouseDownOnControl;

		private bool mouseOver = false;

		public bool MouseOver
		{
			get
			{
				return mouseOver;
			}

			set
			{
				if (mouseOver != value)
				{
					mouseOver = value;
					Invalidate();
				}
			}
		}

		public bool DrawOnTop { get; protected set; }

		public void Invalidate()
		{
			MeshViewerToDrawWith.Invalidate();
		}

		public InteractionVolume(IPrimitive collisionVolume, MeshViewerWidget meshViewerToDrawWith)
		{
			this.collisionVolume = collisionVolume;
			this.meshViewerToDrawWith = meshViewerToDrawWith;
		}

		public virtual void SetPosition()
		{
		}

		public virtual void Draw2DContent(Agg.Graphics2D graphics2D)
		{
		}

		public virtual void DrawGlContent(EventArgs e)
		{
		}

		public virtual void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			MouseDownOnControl = true;
		}

		public virtual void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
		}

		public virtual void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			MouseDownOnControl = false;
		}
	}
}