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

namespace MatterHackers.DataConverters3D
{
	public interface IAssetObject
	{
		string AssetPath { get; set; }

		string AssetID { get; set; }

		Task<Stream> LoadAsset(CancellationToken cancellationToken, Action<double, string> progress);

		//Task StoreAsset(string filePath, CancellationToken cancellationToken, Action<double, string> progress);

		//Task StoreAsset(Stream stream, CancellationToken cancellationToken, Action<double, string> progress);
	}

	public interface IAssetManager
	{
		//Task AcquireAsset(IAssetObject assetObject);
		Task<Stream> LoadAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress);

		Task AcquireAsset(string sha1PlusExtension, CancellationToken cancellationToken, Action<double, string> progress);

		//Task StoreAsset(IAssetObject assetObject, Stream stream, CancellationToken cancellationToken, Action<double, string> progress);

		//Task StoreAsset(IAssetObject assetObject, string filePath, CancellationToken cancellationToken, Action<double, string> progress);

		Task StoreAsset(IAssetObject assetObject, CancellationToken cancellationToken, Action<double, string> progress);

		/// <summary>
		/// Ensures the given file is stored in the asset system
		/// </summary>
		/// <param name="filePath">The full path to the source file</param>
		/// <param name="cancellationToken"></param>
		/// <param name="progress"></param>
		/// <returns>The new asset file name</returns>
		Task<string> StoreFile(string filePath, CancellationToken cancellationToken, Action<double, string> progress);

		/// <summary>
		/// Computes and writes the MCX file to the assets system
		/// </summary>
		/// <param name="object3D">The source MCX file</param>
		/// <returns></returns>
		Task<string> StoreMcx(IObject3D object3D);
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

		//public Task StoreAsset(string filePath, CancellationToken cancellationToken, Action<double, string> progress)
		//{
		//	return AssetManager.StoreAsset(this, filePath, cancellationToken, progress);
		//}

		//public Task StoreAsset(Stream stream, CancellationToken cancellationToken, Action<double, string> progress)
		//{
		//	return AssetManager.StoreAsset(this, stream, cancellationToken, progress);
		//}
	}
}