/*
Copyright (c) 2018, John Lewin, Lars Brubaker
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

namespace MatterHackers.DataConverters3D
{
	public class SelectionMaintainer : IDisposable
	{
		InteractiveScene scene;
		private IObject3D selectedItem;
		private List<IObject3D> childrenBeforUndo;

		public SelectionMaintainer(InteractiveScene scene)
		{
			this.scene = scene;
			selectedItem = scene.SelectedItem;
			scene.SelectedItem = null;
			childrenBeforUndo = scene.Children.ToList();
		}

		public void Dispose()
		{
			if(selectedItem == null)
			{
				return;
			}

			// if the item we had selected is still in the scene, re-select it
			if (scene.Children.Contains(selectedItem))
			{
				scene.SelectedItem = selectedItem;
				return;
			}

			// if the previously selected item is not in the scene
			if (!scene.Children.Contains(selectedItem))
			{
				// and we have only added one new item to the scene
				var newItems = scene.Children.Where(c => !childrenBeforUndo.Contains(c));
				// select it
				if (newItems.Count() == 1)
				{
					scene.SelectedItem = newItems.First();
					return;
				}
			}

			// set the root item to the selection and then to the new item
			var rootItem = selectedItem.Ancestors().Where(i => scene.Children.Contains(i)).FirstOrDefault();
			if (rootItem != null)
			{
				scene.SelectedItem = rootItem;
				scene.SelectedItem = selectedItem;
			}
		}
	}
}
