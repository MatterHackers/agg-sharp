﻿/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;

namespace MatterHackers.DataConverters3D.UndoCommands
{
	public class ReplaceCommand : IUndoRedoCommand
	{
		private IEnumerable<IObject3D> removeItems;
		private IEnumerable<IObject3D> addItems;

		public ReplaceCommand(IEnumerable<IObject3D> removeItems, IEnumerable<IObject3D> addItems)
		{
			var firstParent = removeItems.First().Parent;
			if (firstParent == null)
			{
				throw new Exception("The remove item(s) must already be in the scene (have a parent).");
			}
			if (removeItems.Any())
			{
				foreach(var removeItem in removeItems)
				{
					if (firstParent != removeItem.Parent)
					{
						throw new Exception("All the remove items must be siblings");
					}
				}
			}
			this.removeItems = removeItems;
			this.addItems = addItems;
		}

		public void Do()
		{
			var firstParent = removeItems.First().Parent;
			firstParent.Children.Modify(list =>
			{
				foreach (var child in removeItems)
				{
					list.Remove(child);
				}
				list.AddRange(addItems);
				firstParent.Invalidate();
			});
		}

		public void Undo()
		{
			var firstParent = removeItems.First().Parent;
			firstParent.Children.Modify(list =>
			{
				foreach (var child in addItems)
				{
					list.Remove(child);
				}
				list.AddRange(removeItems);
				firstParent.Invalidate();
			});
		}
	}
}