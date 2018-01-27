/*
Copyright (c) 2014, John Lewin, Lars Brubaker
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.DataConverters3D
{
	public class InteractiveScene : IObject3D
	{
		public event EventHandler SelectionChanged;
		public event EventHandler Invalidated;

		private IObject3D selectedItem;

		public InteractiveScene()
		{
		}

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
					if (SelectedItem is SelectionGroup)
					{
						// If the selected item is a SelectionGroup, collapse its contents into the root
						// of the scene when it loses focus
						Children.Modify(list =>
						{
							SelectedItem.CollapseInto(list);
						});
					}

					selectedItem = value;
					SelectionChanged?.Invoke(this, null);
				}
			}
		}

		[JsonIgnore]
		public UndoBuffer UndoBuffer { get; } = new UndoBuffer();

		[JsonIgnore]
		public bool HasSelection => this.HasChildren() && SelectedItem != null;

		[JsonIgnore]
		public bool ShowSelectionShadow { get; set; } = true;

		public bool IsSelected(IObject3D item) => HasSelection && SelectedItem == item;

		public void Save(string mcxPath, string libraryPath, Action<double, string> progress = null)
		{
			try
			{
				this.PersistAssets(libraryPath, progress);

				// Clear the selection before saving
				List<IObject3D> selectedItems = new List<IObject3D>();

				if(this.SelectedItem != null)
				{
					if (this.SelectedItem is SelectionGroup selectionGroup)
					{
						foreach(var item in selectionGroup.Children)
						{
							selectedItems.Add(item);
						}
					}
					else
					{
						selectedItems.Add(this.SelectedItem);
					}
				}

				// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
				File.WriteAllText(mcxPath, this.ToJson());


				// Restore the selection after saving
				foreach(var item in selectedItems)
				{
					this.AddToSelection(item);
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		public void PersistAssets(string libraryPath, Action<double, string> progress = null)
		{
			var itemsWithUnsavedMeshes = from object3D in this.Descendants()
										 where object3D.Persistable &&
											   object3D.MeshPath == null &&
											   object3D.Mesh != null
										 select object3D;

			string assetsDirectory = Path.Combine(libraryPath, "Assets");
			Directory.CreateDirectory(assetsDirectory);

			var assetFiles = new Dictionary<int, string>();

			try
			{
				// Write each unsaved mesh to disk
				foreach (IObject3D item in itemsWithUnsavedMeshes)
				{
					// Calculate the mesh hash
					int hashCode = (int)item.Mesh.GetLongHashCode();

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
						// Assets should be stored relative to the Asset folder
						item.MeshPath = Path.GetFileName(assetPath);
					}
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		[JsonIgnore]
		// TODO: Remove from InteractiveScene - coordinate debug details between MeshViewer and Inspector directly 
		public IObject3D DebugItem { get; set; }

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

			if (this.HasSelection)
			{
				if(SelectedItem is SelectionGroup)
				{
					// Remove from the scene root
					this.Children.Modify(list => list.Remove(itemToAdd));

					// Move into the SelectionGroup
					SelectedItem.Children.Modify(list => list.Add(itemToAdd));
				}
				else // add a new selection group and add to its children
				{
					// We're adding a new item to the selection. To do so we wrap the selected item
					// in a new group and with the new item. The selection will continue to grow in this
					// way until it's applied, due to a loss of focus or until a group operation occurs
					var newSelectionGroup = new SelectionGroup();

					newSelectionGroup.Children.Modify(list =>
					{
						list.Add(SelectedItem);
						list.Add(itemToAdd);
					});

					this.Children.Modify(list =>
					{
						list.Remove(itemToAdd);
						list.Remove(SelectedItem);
						// add the seletionngroup as the first item so we can hit it first
						list.Insert(0, newSelectionGroup);
					});

					SelectedItem = newSelectionGroup;
				}
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

		public void Load(IObject3D rootItem)
		{
			if (this.RootItem != null)
			{
				this.RootItem.Invalidated -= RootItem_Invalidated;
			}

			this.RootItem = rootItem;
			this.RootItem.Invalidated += RootItem_Invalidated;
		}

		private void RootItem_Invalidated(object sender, EventArgs e)
		{
			this.Invalidated(this, e);
		}

		#region IObject3D

		public IObject3D RootItem { get; private set; } = new Object3D();

		public string ActiveEditor { get => RootItem.ActiveEditor; set => RootItem.ActiveEditor = value; }
		public string OwnerID { get => RootItem.OwnerID; set => RootItem.OwnerID = value; }
		public SafeList<IObject3D> Children { get => RootItem.Children; set => RootItem.Children = value; }
		public IObject3D Parent { get => RootItem.Parent; set => RootItem.Parent = value; }
		public Color Color { get => RootItem.Color; set => RootItem.Color = value; }
		public int MaterialIndex { get => RootItem.MaterialIndex; set => RootItem.MaterialIndex = value; }
		public PrintOutputTypes OutputType { get => RootItem.OutputType; set => RootItem.OutputType = value; }
		public Matrix4X4 Matrix { get => RootItem.Matrix; set => RootItem.Matrix = value; }
		public string TypeName => RootItem.TypeName;
		public Mesh Mesh { get => RootItem.Mesh; set => RootItem.Mesh = value; }
		public string MeshPath { get => RootItem.MeshPath; set => RootItem.MeshPath = value; }
		public string Name { get => RootItem.Name; set => RootItem.Name = value; }
		public bool Persistable => RootItem.Persistable;
		public bool Visible { get => RootItem.Visible; set => RootItem.Visible = value; }
		public string ID { get => RootItem.ID; set => RootItem.ID = value; }

		public IObject3D Clone() => RootItem.Clone();

		public string ToJson() => RootItem.ToJson();

		public long GetLongHashCode() => RootItem.GetLongHashCode();

		public IPrimitive TraceData()
		{
			var curMatrix = RootItem.Matrix;
			RootItem.Matrix = Matrix4X4.Identity;
			var rootTraceData = RootItem.TraceData();
			RootItem.Matrix = curMatrix;

			return rootTraceData;
		}

		public void SetMeshDirect(Mesh mesh) => RootItem.SetMeshDirect(mesh);

		public void Invalidate()
		{
			this.Invalidated?.Invoke(this, null);
		}

		public MeshGroup Flatten(Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = null, Predicate<IObject3D> filter = null)
		{
			return RootItem.Flatten(meshPrintOutputSettings);
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix, bool requirePrecision = false)
		{
			return RootItem.GetAxisAlignedBoundingBox(matrix, requirePrecision);
		}

		/// <summary>
		/// Wrap the current selection with the object passed, 
		/// then add the object to the sceen,
		/// then select the newly added object
		/// </summary>
		/// <param name="itemToWrapWith">Item to wrap selection and add</param>
		public void WrapSelection(Object3D itemToWrapWith)
		{
			if (this.HasSelection)
			{
				IObject3D item;

				List<IObject3D> itemsToRestoreOnUndo;

				if (this.SelectedItem is SelectionGroup selectionGroup)
				{
					item = new Object3D();
					itemsToRestoreOnUndo = selectionGroup.Children.ToList();
					item.Children.Modify((list) =>
					{
						var clone = selectionGroup.Clone();
						list.AddRange(clone.Children);
					});
				}
				else
				{
					itemsToRestoreOnUndo = new List<IObject3D> { this.SelectedItem };
					item = this.SelectedItem.Clone();
				}

				this.SelectedItem = null;

				itemToWrapWith.Children.Add(item);

				itemToWrapWith.MakeNameNonColliding();

				this.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						itemsToRestoreOnUndo,
						new List<IObject3D> { itemToWrapWith }));

				// Make the object have an identity matrix and keep its position in our new object
				itemToWrapWith.Matrix = item.Matrix;
				item.Matrix = Matrix4X4.Identity;

				this.SelectedItem = itemToWrapWith;
			}
		}


		#endregion

	}
}
