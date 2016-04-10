using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MeshVisualizer
{
	public class InteractiveScene : Object3D
	{
		public event EventHandler SelectionChanged;

		IObject3D selectedItem;

		[JsonIgnore]
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

		[JsonIgnore]
		public bool HasSelection => HasChildren && SelectedItem != null;

		public bool IsSelected(Object3DTypes objectType) => HasSelection && SelectedItem.ItemType == objectType;

		public void Save(string filePath, string assetsPath, ReportProgressRatio progress = null)
		{
			var itemsWithUnsavedMeshes = from object3D in this.Descendants()
							  where object3D.MeshPath == null &&
									object3D.Mesh != null &&
									object3D.PersistNode == true
							  select object3D;

			try
			{
				// Save each unpersisted mesh
				foreach (IObject3D item in itemsWithUnsavedMeshes)
				{
					// Get an open filename
					string amfPath = GetOpenAmfPath(assetsPath);

					// Save the embedded asset to disk
					bool savedSuccessfully = MeshFileIo.Save(
						new List<MeshGroup> { new MeshGroup(item.Mesh) },
						amfPath,
						new MeshOutputSettings(
							MeshOutputSettings.OutputType.Binary,
							new string[] { "Created By", "MatterControl", "BedPosition", "Absolute" }),
						progress);

					if (savedSuccessfully && File.Exists(amfPath))
					{
						item.MeshPath = amfPath;
					}
				}

				// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
				File.WriteAllText(
					filePath, 
					JsonConvert.SerializeObject(
						this, 
						Formatting.Indented, 
						new JsonSerializerSettings { ContractResolver = new IObject3DContractResolver() }));
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		private string GetOpenAmfPath(string assetsPath)
		{
			string filePath;
			do
			{
				filePath = Path.Combine(assetsPath, Path.ChangeExtension(Path.GetRandomFileName(), ".amf"));
			} while (File.Exists(filePath));

			return filePath;
		}

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
			if (itemToAdd == SelectedItem || SelectedItem?.Children?.Contains(itemToAdd) == true)
			{
				return;
			}

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
				throw new Exception("Unable to select external object. Item must be in the scene to be selected.");
			}
		}
	}
}
