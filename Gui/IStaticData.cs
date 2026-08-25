/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// Read-only access to the application's bundled asset tree (the "StaticData" folder): icons,
	/// images, themes, sample parts and text resources. <see cref="StaticData"/> is the disk-backed
	/// implementation; a host that has no filesystem - a browser/WASM host, for instance - installs
	/// its own through <see cref="StaticData.Instance"/> before the first asset is touched.
	/// </summary>
	/// <remarks>
	/// The API is deliberately synchronous. Hundreds of call sites read icons and text inline while
	/// building widgets, and there is no sane place to await in that code. A host without synchronous
	/// I/O is expected to preload its assets (from a manifest, a zip, or a fetch at startup) and serve
	/// them out of memory behind this interface rather than making these members async.
	/// </remarks>
	public interface IStaticData
	{
		/// <summary>
		/// Drops any cached images so that later loads observe changed asset content.
		/// </summary>
		void PurgeCache();

		bool DirectoryExists(string path);

		bool FileExists(string path);

		/// <summary>
		/// Gets the sub-directories of the given asset directory, as paths that may be passed back in.
		/// </summary>
		IEnumerable<string> GetDirectories(string path);

		/// <summary>
		/// Gets the files of the given asset directory as paths relative to the asset root, suitable
		/// for handing straight back to <see cref="LoadImage"/>, <see cref="OpenStream"/> and friends.
		/// </summary>
		IEnumerable<string> GetFiles(string path);

		/// <summary>
		/// Loads the specified file from the Icons asset path, scaled for the current device scale.
		/// </summary>
		ImageBuffer LoadIcon(string path);

		/// <summary>
		/// Loads the specified file from the Icons asset path at the given design size, scaled for the
		/// current device scale.
		/// </summary>
		ImageBuffer LoadIcon(string path,
			int width,
			int height,
			bool invertImage = false,
			Func<ImageBuffer, (ImageBuffer processed, string key)> processSource = null);

		ImageBuffer LoadImage(string path);

		void LoadImageData(Stream imageStream, ImageBuffer destImage);

		void LoadImageSequenceData(Stream stream, ImageSequence sequence);

		/// <summary>
		/// Loads an animation, either from a directory of numbered PNGs or from a single .gif.
		/// </summary>
		ImageSequence LoadSequence(string path);

		Stream OpenStream(string path);

		string[] ReadAllLines(string path);

		string ReadAllText(string path);

		/// <summary>
		/// Turns an asset-relative path into whatever this implementation considers a full path. Only
		/// meaningful to implementations that are backed by real files.
		/// </summary>
		string MapPath(string path);
	}
}
