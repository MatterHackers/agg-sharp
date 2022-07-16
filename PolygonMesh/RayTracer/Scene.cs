// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
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

using MatterHackers.Agg;
using MatterHackers.RayTracer.Light;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.RayTracer
{
	/// <summary>
	/// a scene is defined by:
	/// - lights
	/// - a camera, of viewpoint from which the scene is observed
	/// - a background
	/// - the objects in the scene, called the shapes.
	/// </summary>
	public class Scene
	{
		public Background background;
		public ICamera camera;
		public List<ITraceable> shapes;
		public List<ILight> lights;

		public Scene(ICamera camera = null)
		{
			if (camera == null)
			{
				camera = new SimpleCamera(512, 512, MathHelper.DegreesToRadians(40));
				((SimpleCamera)camera).Origin = new Vector3(0, 0, -5);
			}
			this.camera = camera;
			shapes = new List<ITraceable>();
			lights = new List<ILight>();
			background = new Background(new ColorF(0, 0, 0, 0), 0.2);
		}

		/// <summary>
		/// This will remove the shapes from 'Shapes' and add them to a Bounding Volume Hierarchy.  Then add that at a single element
		/// to 'Shapes'.  You could also create a list of 'List<IPrimitive>' and put that directly into a BVH and then add that
		/// to the Shapes list (there could be more than 1 BVH in the 'Shapes' list.
		/// </summary>
		public ITraceable MoveShapesIntoBoundingVolumeHierachy()
		{
			var rootObject = BoundingVolumeHierarchy.CreateNewHierachy(shapes);
			if (rootObject != null)
			{
				shapes.Clear();
				shapes.Add(rootObject);
			}

			return rootObject;
		}
	}
}