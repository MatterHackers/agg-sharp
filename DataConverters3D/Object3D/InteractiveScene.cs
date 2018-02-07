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
using System.Threading;
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

		public void Save(Stream stream, Action<double, string> progress = null)
		{
			// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
			try
			{
				this.PersistAssets(progress);

				// Clear the selection before saving
				List<IObject3D> selectedItems = new List<IObject3D>();

				if (this.SelectedItem != null)
				{
					if (this.SelectedItem is SelectionGroup selectionGroup)
					{
						foreach (var item in selectionGroup.Children)
						{
							selectedItems.Add(item);
						}
					}
					else
					{
						selectedItems.Add(this.SelectedItem);
					}
				}

				var streamWriter = new StreamWriter(stream);
				streamWriter.Write(this.ToJson());
				streamWriter.Flush();

				// Restore the selection after saving
				foreach (var item in selectedItems)
				{
					this.AddToSelection(item);
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		public void Save(string mcxPath, Action<double, string> progress = null)
		{
			using (var stream = new FileStream(mcxPath, FileMode.Create, FileAccess.Write))
			{
				this.Save(stream, progress);
			}
		}

		public void PersistAssets(Action<double, string> progress = null)
		{
			var itemsWithUnsavedMeshes = from object3D in this.Descendants()
										 where object3D.Persistable &&
											   object3D.MeshPath == null &&
											   object3D.Mesh != null
										 select object3D;

			string assetsDirectory = Object3D.AssetsPath;
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
						string tempStlPath = GetOpenFilePath(Object3D.AssetsPath, ".stl");

						// Save the embedded asset to disk
						savedSuccessfully = MeshFileIo.Save(
							new List<MeshGroup> { new MeshGroup(item.Mesh) },
							tempStlPath,
							CancellationToken.None,
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
						// add the seletionGroup as the first item so we can hit it first
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

		public void Load(IObject3D sourceItem)
		{
			if (sourceItem != null)
			{
				sourceItem.Invalidated -= SourceItem_Invalidated;
			}

			this.sourceItem = sourceItem;
			sourceItem.Invalidated += SourceItem_Invalidated;
		}

		private void SourceItem_Invalidated(object sender, EventArgs e)
		{
			this.Invalidated(this, e);
		}

		#region IObject3D

		private IObject3D sourceItem = new Object3D();

		public string ActiveEditor { get => sourceItem.ActiveEditor; set => sourceItem.ActiveEditor = value; }
		public string OwnerID { get => sourceItem.OwnerID; set => sourceItem.OwnerID = value; }
		public SafeList<IObject3D> Children { get => sourceItem.Children; set => sourceItem.Children = value; }
		public IObject3D Parent { get => sourceItem.Parent; set => sourceItem.Parent = value; }
		public Color Color { get => sourceItem.Color; set => sourceItem.Color = value; }
		public int MaterialIndex { get => sourceItem.MaterialIndex; set => sourceItem.MaterialIndex = value; }
		public PrintOutputTypes OutputType { get => sourceItem.OutputType; set => sourceItem.OutputType = value; }
		public Matrix4X4 Matrix { get => sourceItem.Matrix; set => sourceItem.Matrix = value; }
		public string TypeName => sourceItem.TypeName;
		public Mesh Mesh { get => sourceItem.Mesh; set => sourceItem.Mesh = value; }
		public string MeshPath { get => sourceItem.MeshPath; set => sourceItem.MeshPath = value; }
		public string Name { get => sourceItem.Name; set => sourceItem.Name = value; }
		public bool Persistable => sourceItem.Persistable;
		public bool Visible { get => sourceItem.Visible; set => sourceItem.Visible = value; }
		public string ID { get => sourceItem.ID; set => sourceItem.ID = value; }

		public IObject3D Clone() => sourceItem.Clone();

		public string ToJson() => sourceItem.ToJson();

		public long GetLongHashCode() => sourceItem.GetLongHashCode();

		public IPrimitive TraceData() => sourceItem.TraceData();

		public void SetMeshDirect(Mesh mesh) => sourceItem.SetMeshDirect(mesh);

		public void Invalidate()
		{
			this.Invalidated?.Invoke(this, null);
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix, bool requirePrecision = false)
		{
			return sourceItem.GetAxisAlignedBoundingBox(matrix, requirePrecision);
		}

		/// <summary>
		/// Wrap the current selection with the object passed, 
		/// then add the object to the scene,
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
