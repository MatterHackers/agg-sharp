/*
Copyright (c) 2026, Lars Brubaker
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
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Threading.Tasks;
using Newtonsoft.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// ColorJsonConverter has to read every shape a Color has ever been written in: the "#RRGGBB" string,
	/// a named color, the full object with an Html property, and the degenerate values real customer files
	/// carry - an empty string, a null, or something structural like an array. The empty string is the one
	/// that used to throw, because a non-"#" string fell through to JObject.Load() on a reader parked on a
	/// String token. The After property on the holder pins reader positioning: if the converter leaves the
	/// reader partway inside the value it just read, the next property does not survive.
	/// </summary>
	public class ColorJsonConverterTests
	{
		/// <summary>
		/// No [JsonConverter] attribute here on purpose - the Color struct itself carries it, which is the
		/// path customer files actually take.
		/// </summary>
		private class ColorHolder
		{
			public Color Color { get; set; }

			public int After { get; set; }
		}

		[Test]
		public async Task EmptyStringReadsAsTransparent()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": \"\", \"After\": 7}");

			await Assert.That(holder.Color).IsEqualTo(Color.Transparent);
			await Assert.That(holder.After).IsEqualTo(7);
		}

		[Test]
		public async Task HtmlStringReadsAsThatColor()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": \"#FF0000\"}");

			await Assert.That(holder.Color).IsEqualTo(Color.Red);
		}

		[Test]
		public async Task NamedColorStringReadsAsThatColor()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": \"red\"}");

			await Assert.That(holder.Color).IsEqualTo(Color.Red);
		}

		[Test]
		public async Task ObjectWithHtmlPropertyReadsAsThatColor()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": {\"Html\": \"#00FF00\"}, \"After\": 7}");

			await Assert.That(holder.Color).IsEqualTo(new Color("#00FF00"));
			await Assert.That(holder.After).IsEqualTo(7);
		}

		[Test]
		public async Task NullReadsAsTransparent()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": null}");

			await Assert.That(holder.Color).IsEqualTo(Color.Transparent);
		}

		[Test]
		public async Task ArrayReadsAsTransparentAndIsSkipped()
		{
			var holder = JsonConvert.DeserializeObject<ColorHolder>("{\"Color\": [1, 2, 3], \"After\": 7}");

			await Assert.That(holder.Color).IsEqualTo(Color.Transparent);
			await Assert.That(holder.After).IsEqualTo(7);
		}
	}
}
