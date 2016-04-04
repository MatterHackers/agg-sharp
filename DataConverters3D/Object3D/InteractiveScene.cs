using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MeshVisualizer
{
	public class InteractiveScene : Object3D
	{
		public event EventHandler SelectionChanged;

		IObject3D selectedItem;
		public IObject3D SelectedItem
		{
			get
			{
				return selectedItem;
			}

			set
			{
				if (selectedItem != value)
				{
					selectedItem = value;
					SelectionChanged?.Invoke(this, null);
				}
			}
		}

		public bool HasSelection => HasChildren && SelectedItem != null;

		public bool IsSelected(Object3DTypes objectType) => HasSelection && SelectedItem.ItemType == objectType;

		public void SelectLastChild()
		{
			if (Children.Count > 0)
			{
				SelectedItem = Children.Last();
			}
		}

		public void SelectFirstChild()
		{
			if (Children.Count > 0)
			{
				SelectedItem = Children.First();
			}
		}

		public void Select(IObject3D item)
		{
			SelectedItem = item;
		}

		public void ModifyChildren(Action<List<IObject3D>> modifier)
		{
			// Copy the child items
			var clonedChildren = new List<IObject3D>(Children);

			// Pass them to the action
			modifier(clonedChildren);

			// Swap the modified list into place
			Children = clonedChildren;
		}

		public void ClearSelection()
		{
			if (HasSelection)
			{
				SelectedItem = null;
			}
		}
	}
}
