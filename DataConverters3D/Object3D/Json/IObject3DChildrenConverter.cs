﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;

namespace MatterHackers.DataConverters3D
{
	public class IObject3DChildrenConverter : JsonConverter
	{
		// Register type mappings to support deserializing to the IObject3D concrete type - long term hopefully via configuration mapping, short term via IObject3D inheritance
		private Dictionary<string, string> mappingTypesCache;
		private Dictionary<string, string> mappingTypes
		{
			get
			{
				if (mappingTypesCache == null)
				{
					mappingTypesCache = new Dictionary<string, string>();

					foreach (var type in PluginFinder.FindTypes<IObject3D>())
					{
						mappingTypesCache.Add(type.Name, type.AssemblyQualifiedName);
					}
				}

				return mappingTypesCache;
			}
		}

		public override bool CanWrite { get; } = false;

		public override bool CanConvert(Type objectType) => objectType is IObject3D;

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var parentItem = existingValue as IObject3D;

			var items = new List<IObject3D>();

			JArray jArray = JArray.Load(reader);
			foreach (var item in jArray)
			{
				string typeName = item["TypeName"]?.ToString();
				string fullTypeName;

				IObject3D childItem;

				if (string.IsNullOrEmpty(typeName) || typeName == "Object3D" || !mappingTypes.TryGetValue(typeName, out fullTypeName))
				{
					// Use a normal Object3D type if the TypeName field is missing, invalid or has no mapping entry
					childItem = item.ToObject<Object3D>(serializer);
				}
				else
				{
					// If a mapping entry exists, try to find the type for the given entry falling back to Object3D if that fails
					Type type = Type.GetType(fullTypeName) ?? typeof(Object3D);
					childItem = (IObject3D)item.ToObject(type, serializer);
				}

				childItem.Parent = parentItem;

				items.Add(childItem);
			}

			return new SafeList<IObject3D>(items, null);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
		}
	}
}