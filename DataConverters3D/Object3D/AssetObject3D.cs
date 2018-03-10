/*
Copyright (c) 2018, John Lewin
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.DataConverters3D
{
	public interface IAssetObject
	{
		string AssetPath { get; set; }

		string AssetID { get; set; }

		Task<Stream> LoadAsset(CancellationToken cancellationToken, Action<double, string> progress);
	}

	public interface IAssetManager
	{
		Task<Stream> LoadAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress);

		Task AcquireAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress);

		Task StoreAsset(IAssetObject assetObject, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

		Task StoreMesh(IObject3D object3D, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

		/// <summary>
		/// Ensures the given file is stored in the asset system
		/// </summary>
		/// <param name="filePath">The full path to the source file</param>
		/// <param name="cancellationToken"></param>
		/// <param name="progress"></param>
		/// <returns>The new asset file name</returns>
		Task<string> StoreFile(string filePath, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress);

		/// <summary>
		/// Computes and writes the MCX file to the assets system
		/// </summary>
		/// <param name="object3D">The source MCX file</param>
		/// <returns></returns>
		Task<string> StoreMcx(IObject3D object3D, bool publishAfterSave);
	}

	public class AssetObject3D : Object3D, IAssetObject
	{
		// Collector
		public static IAssetManager AssetManager { get; set; }

		public string AssetPath { get; set; }

		public string AssetID { get; set; }

		// Load
		public Task<Stream> LoadAsset(CancellationToken cancellationToken, Action<double, string> progress)
		{
			return AssetManager.LoadAsset(this, cancellationToken, progress);
		}
	}

	public class AssetManager : IAssetManager
	{
		public virtual Task AcquireAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress)
		{
			return Task.CompletedTask;
		}

		public Task<Stream> LoadAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress)
		{
			// Natural path
			string filePath = assetObject.AssetPath;

			// Is relative asset path only, no directory
			if (Path.GetDirectoryName(filePath) == "")
			{
				filePath = Path.Combine(Object3D.AssetsPath, filePath);

				// Prime cache
				if (!File.Exists(filePath))
				{
					AcquireAsset(assetObject.AssetPath, cancellationToken, progress);
				}
			}

			if (!File.Exists(filePath))
			{
				// Not at natural path, not in local assets, not in remote assets
				return Task.FromResult<Stream>(null);
			}

			return Task.FromResult<Stream>(File.OpenRead(filePath));
		}

		public virtual async Task StoreAsset(IAssetObject assetObject, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress)
		{
			// Natural path
			string filePath = assetObject.AssetPath;

			// Is full path and file exists, import as Asset
			if (Path.GetDirectoryName(filePath) != ""
				&& File.Exists(filePath))
			{
				using (var sourceStream = File.OpenRead(assetObject.AssetPath))
				{
					// ComputeSha1 -> Save asset
					//string assetName = await this.StoreStream(sourceStream, Path.GetExtension(assetObject.AssetPath), cancellationToken, progress);
					string assetName = await this.StoreFile(assetObject.AssetPath, publishAfterSave, cancellationToken, progress);

					// Update AssetID
					assetObject.AssetID = Path.GetFileNameWithoutExtension(assetName);
					assetObject.AssetPath = assetName;
				}
			}
		}

		public virtual Task<string> StoreFile(string filePath, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress)
		{
			// Compute SHA1
			string sha1 = Object3D.ComputeSHA1(filePath);

			string assetPath = Path.Combine(Object3D.AssetsPath, sha1 + Path.GetExtension(filePath).ToLower());

			// Load cache
			if (!File.Exists(assetPath))
			{
				File.Copy(filePath, assetPath);
			}

			return Task.FromResult(Path.GetFileName(assetPath));
		}

		public Task<string> StoreMcx(IObject3D object3D, bool publishAfterSave)
		{
			// TODO: Track SHA1 of persisted asset
			// TODO: Skip if cached sha1 exists in assets

			// Serialize object3D to in memory mcx/json stream
			using (var memoryStream = new MemoryStream())
			{
				// Write JSON
				object3D.SaveTo(memoryStream);

				// Reposition
				memoryStream.Position = 0;

				// Calculate
				string sha1 = Object3D.ComputeSHA1(memoryStream);

				Directory.CreateDirectory(Object3D.AssetsPath);

				string assetPath = Path.Combine(Object3D.AssetsPath, sha1 + ".stl");
				if (!File.Exists(assetPath))
				{
					memoryStream.Position = 0;

					using (var outStream = File.Create(assetPath))
					{
						memoryStream.CopyTo(outStream);
					}
				}

				return Task.FromResult(assetPath);
			}
		}

		public async Task StoreMesh(IObject3D object3D, bool publishAfterSave, CancellationToken cancellationToken, Action<double, string> progress = null)
		{
			// Get an open filename
			string tempStlPath = CreateNewLibraryPath(".stl");

			// Save the embedded asset to disk
			bool savedSuccessfully = MeshFileIo.Save(
				object3D.Mesh,
				tempStlPath,
				CancellationToken.None,
				new MeshOutputSettings(MeshOutputSettings.OutputType.Binary),
				progress);

			if (savedSuccessfully)
			{
				// There's currently no way to know the actual mesh file hashcode without saving it to disk, thus we save at least once in
				// order to compute the hash but then throw away the duplicate file if an existing copy exists in the assets directory
				string assetPath = await this.StoreFile(tempStlPath, publishAfterSave, cancellationToken, progress);

				// Remove the temp file
				File.Delete(tempStlPath);

				// Update MeshPath with Assets relative filename
				object3D.MeshPath = Path.GetFileName(assetPath);
			}
		}

		/// <summary>
		/// Creates a new non-colliding library file path to write library contents to
		/// </summary>
		/// <param name="extension">The file extension to use</param>
		/// <returns>A new unique library path</returns>
		private static string CreateNewLibraryPath(string extension)
		{
			string filePath;
			do
			{
				filePath = Path.Combine(Object3D.AssetsPath, Path.ChangeExtension(Path.GetRandomFileName(), extension));
			} while (File.Exists(filePath));

			return filePath;
		}
	}
}