/*
Copyright (c) 2017, John Lewin
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
using System.Reflection;
using MatterHackers.Agg;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MatterHackers.DataConverters3D
{
	public class IObject3DContractResolver : DefaultContractResolver
	{
		private static Type IObject3DType = typeof(IObject3D);

		private static Type RGBA_BtyesType = typeof(Color);

		protected override JsonObjectContract CreateObjectContract(Type objectType)
		{
			var result = base.CreateObjectContract(objectType);

			if (IObject3DType.IsAssignableFrom(objectType)
				&& result is JsonObjectContract contract)
			{
				// Add a post deserialization callback to set Parent 
				contract.OnDeserializedCallbacks.Add((o, context) =>
				{
					if (o is IObject3D object3D)
					{
						foreach (var child in object3D.Children)
						{
							child.Parent = object3D;
						}

						object3D.Children.StoreParent(object3D);
					}
				});
			}

			return result;
		}

		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			// Conditionally serialize .Children only if .MeshPath is empty. Currently having a Mesh precludes having children - looks like a 
			// feature for AMF that needs to be reconsidered or constrained to specific scenarios
			JsonProperty property = base.CreateProperty(member, memberSerialization);
			if (property.PropertyName == "Children" && IObject3DType.IsAssignableFrom(property.DeclaringType))
			{
				// TODO: Needs review - clipping the Children property when MeshPath is non-null works for AMF but isn't appropriate for many use cases
				property.ShouldSerialize = instance => {
					IObject3D item = (IObject3D)instance;
					return string.IsNullOrEmpty(item.MeshPath);
				};
			}

			if (property.PropertyName == "Color" && RGBA_BtyesType.IsAssignableFrom(property.PropertyType))
			{
				property.ShouldSerialize = instance =>
				{
					return instance is IObject3D object3D && object3D.Color != Color.Transparent;
				};
			}

			return property;
		}
	}
}