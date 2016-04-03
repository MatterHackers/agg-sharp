/*
Copyright (c) 2016, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.PolygonMesh.Processors
{
	public class MeshOutputSettings
	{
		public enum CsgOption { SimpleInsertVolumes, DoCsgMerge }

		public enum OutputType { Ascii, Binary };

		public OutputType OutputTypeSetting = OutputType.Binary;
		public Dictionary<string, string> MetaDataKeyValue = new Dictionary<string, string>();
		public List<int> MaterialIndexsToSave = null;
		public CsgOption CsgOptionState = CsgOption.SimpleInsertVolumes;

		public ReportProgressRatio ReportProgress
		{
			get; set;
		}

		public MeshOutputSettings()
		{
		}

		public MeshOutputSettings(CsgOption csgOption)
		{
			this.CsgOptionState = csgOption;
		}

		public MeshOutputSettings(OutputType outputTypeSetting, string[] metaDataKeyValuePairs = null, ReportProgressRatio reportProgress = null)
		{
			this.ReportProgress = reportProgress;

			this.OutputTypeSetting = outputTypeSetting;
			if (metaDataKeyValuePairs != null)
			{
				for (int i = 0; i < metaDataKeyValuePairs.Length / 2; i++)
				{
					MetaDataKeyValue.Add(metaDataKeyValuePairs[i * 2], metaDataKeyValuePairs[i * 2 + 1]);
				}
			}
		}
	}

}
