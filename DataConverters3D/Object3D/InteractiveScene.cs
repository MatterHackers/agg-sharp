using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MeshVisualizer
{
	public class InteractiveScene : Object3D
	{
		public event EventHandler SelectionChanged;

		private IObject3D selectedItem;

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

		public void Save(string mcxPath, string libraryPath, ReportProgressRatio progress = null)
		{
			var itemsWithUnsavedMeshes = from object3D in this.Descendants()
							  where object3D.MeshPath == null &&
									object3D.Mesh != null &&
									object3D.PersistNode == true
							  select object3D;

			string assetsDirectory = Path.Combine(libraryPath, "Assets");
			Directory.CreateDirectory(assetsDirectory);

			Dictionary<int, string> assetFiles = new Dictionary<int, string>();

			try
			{
				// Write each unsaved mesh to disk
				foreach (IObject3D item in itemsWithUnsavedMeshes)
				{
					// Calculate the mesh hash
					int hashCode = item.Mesh.GetHash();

					string assetPath;

					bool savedSuccessfully = true;

					if (!assetFiles.TryGetValue(hashCode, out assetPath))
					{
						// Get an open filename
						string tempStlPath = GetOpenFilePath(libraryPath, ".stl");

						// Save the embedded asset to disk
						savedSuccessfully = MeshFileIo.Save(
							new List<MeshGroup> { new MeshGroup(item.Mesh) },
							tempStlPath,
							new MeshOutputSettings(MeshOutputSettings.OutputType.Binary),
							progress);

						if (savedSuccessfully)
						{
							// There's currently no way to know the actual mesh file hashcode without saving it to disk, thus we save at least once in
							// order to compute the hash but then throw away the duplicate file if an existing copy exists in the assets directory
							string sha1 = MeshFileIo.ComputeSHA1(tempStlPath);
							assetPath = Path.Combine(assetsDirectory, sha1 + ".stl");
							if (!File.Exists(assetPath))
							{
								File.Copy(tempStlPath, assetPath);
							}

							// Remove the temp file
							File.Delete(tempStlPath);

							assetFiles.Add(hashCode, assetPath);
						}
					}

					if (savedSuccessfully && File.Exists(assetPath))
					{
						item.MeshPath = assetPath;
					}
				}

				// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
				File.WriteAllText(
					mcxPath, 
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

		private string GetOpenFilePath(string libraryPath, string extension)
		{
			string filePath;
			do
			{
				filePath = Path.Combine(libraryPath, Path.ChangeExtension(Path.GetRandomFileName(), extension));
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
