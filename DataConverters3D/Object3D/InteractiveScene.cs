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

		public void AddToSelection(IObject3D itemToAdd)
		{
			if (HasSelection)
			{
				ModifyChildren(children =>
				{
					// We're adding a new item to the selection. To do so we wrap the selected item
					// in a new group and with the new item. The selection will continue to grow in this
					// way until it's applied, due to a loss of focus or until a group operation occurs
					var newSelectionGroup = new Object3D
					{
						ItemType = Object3DTypes.SelectionGroup,
						MeshGroup = new MeshGroup()
					};

					newSelectionGroup.Children.Add(SelectedItem);
					newSelectionGroup.Children.Add(itemToAdd);

					// Swap items
					children.Remove(SelectedItem);
					children.Remove(itemToAdd);
					children.Add(newSelectionGroup);

					this.Select(newSelectionGroup);
				});
			}
			else if (Children.Contains(itemToAdd))
			{
				SelectedItem = itemToAdd;
			}
			else
			{
				throw new Exception("Unable to select external object. Item must be in the scene be selected.");
			}
		}
	}
}
