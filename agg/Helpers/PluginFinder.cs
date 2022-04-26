/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Linq;
using System.Reflection;

namespace MatterHackers.Agg
{
	public static class PluginFinder
	{
		private static Dictionary<Assembly, List<Type>> assemblyAndTypes = new Dictionary<Assembly, List<Type>>();

		public static void LoadTypesFromAssembly(Assembly assembly)
		{
			var assemblyTypes = new List<Type>();

			foreach (var type in assembly.GetTypes())
			{
				try
				{
					if (type == null || !type.IsClass || !type.IsPublic)
					{
						continue;
					}

					assemblyTypes.Add(type);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error adding type: " + ex.Message);
				}
			}

			assemblyAndTypes.Add(assembly, assemblyTypes);
		}

		public static IEnumerable<Type> FindTypes<T>()
		{
			Type targetType = typeof(T);

			return assemblyAndTypes?.SelectMany(kvp => kvp.Value)
						.Where(type => targetType.IsAssignableFrom(type));
		}

		public static List<T> CreateInstancesOf<T>()
		{
			List<T> constructedTypes = new List<T>();
			foreach (var keyValue in assemblyAndTypes)
			{
				try
				{
					Type targetType = typeof(T);

					foreach (var type in keyValue.Value)
					{
						if (targetType.IsInterface && targetType.IsAssignableFrom(type) 
							|| type.BaseType == typeof(T))
						{
							constructedTypes.Add((T)Activator.CreateInstance(type));
						}
					}
				}
				//catch (ReflectionTypeLoadException)	{ }
				//catch (BadImageFormatException) { }
				//catch (NotSupportedException) {	}
				catch(Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading types: " + ex.Message);
				}
			}

			return constructedTypes;
		}
	}
}