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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Agg.Tests.Agg
{
    public class Assert
    {
        public static void Equal(int expectedCount, int count)
        {
            if (expectedCount != count)
            {
                throw new Exception("Expected " + count + " but was " + expectedCount);
            }
        }

        public static void Equal(string expectedName, string name)
        {
            if (expectedName != name)
            {
                throw new Exception("Expected " + name + " but was " + expectedName);

            }
        }

        public static void Equal(object selectedItem1, object selectedItem2)
        {
            if (selectedItem1 == null && selectedItem2 == null)
            {
                return;
            }

            if (selectedItem1 == null || selectedItem2 == null)
            {
                throw new Exception($"Expected {selectedItem2} but was {selectedItem1}");
            }

            if (selectedItem1.GetType() != selectedItem2.GetType())
            {
                throw new Exception($"Expected type {selectedItem2.GetType()} but was {selectedItem1.GetType()}");
            }

            if (selectedItem1 is Array array1 && selectedItem2 is Array array2)
            {
                if (array1.Length != array2.Length)
                {
                    throw new Exception("Array lengths do not match.");
                }

                for (int i = 0; i < array1.Length; i++)
                {
                    Equal(array1.GetValue(i), array2.GetValue(i));
                }
            }
            else if (!selectedItem1.Equals(selectedItem2))
            {
                throw new Exception($"Expected {selectedItem2} but was {selectedItem1}");
            }
        }

        public static void False(bool expectedFalse)
        {
            if (expectedFalse)
            {
                throw new Exception("Expected false but was true");
            }
        }

        public static void IsTrue(bool isConditionTrue, string assertionMessage)
        {
            if (!isConditionTrue)
            {
                throw new Exception($"Expected true but was false, {assertionMessage}");
            }
        }

        public static void NotNull(object nonNullObject)
        {
            if (nonNullObject == null)
            {
                throw new Exception("Expected not null but was null");
            }
        }

        public static void Null(object shouldBeNull)
        {
            if (shouldBeNull != null)
            {
                throw new Exception("Expected null but was not null");
            }
        }

        public static T Single<T>(IEnumerable<T> collection)
        {
            if (collection.Count() != 1)
            {
                throw new Exception("Expected 1 but was " + collection.Count());
            }

            return collection.First();
        }

        public static void True(bool expectedTrue, string assertionMessage)
        {
            if (!expectedTrue)
            {
                throw new Exception("Expected true but was false, " + assertionMessage);
            }
        }

        public static void True(bool expectedTrue)
        {
            if (!expectedTrue)
            {
                throw new Exception("Expected true but was false");
            }
        }

        public static void False(bool v1, string v2)
        {
            if (v1)
            {
                throw new Exception("Expected false but was true, " + v2);
            }
        }

        public static void Null(TreeNode selectedNode)
        {
            if (selectedNode != null)
            {
                throw new Exception("Expected null but was not null");
            }
        }

        public static void IsType<T>(object selectedItem)
        {
            if (!(selectedItem is T))
            {
                throw new Exception("Expected " + typeof(T) + " but was " + selectedItem.GetType());
            }
        }

        public static void IsNotType<T>(object selectedItem)
        {
            if (selectedItem is T)
            {
                throw new Exception("Expected not " + typeof(T) + " but was " + selectedItem.GetType());
            }
        }

        public static void NotEqual(Color expectedColor, Color currentColor)
        {
            if (expectedColor != currentColor)
            {
                throw new Exception($"Colors not equal: expected {expectedColor.ToString()}, have {currentColor.ToString()}");
            }
        }

        public static void Equal(double expected, double actual, double allowedError = 0.001)
        {
            if (Math.Abs(actual - expected) > allowedError)
            {
                throw new Exception($"Expected value {expected} not within allowed delte of actual {actual}.");
            }
        }

        public static async Task ThrowsAsync<T>(Func<Task> value) where T : Exception
        {
            try
            {
                await value();
                throw new Exception("Expected exception of type " + typeof(T).Name + " was not thrown.");
            }
            catch (T)
            {
                // Exception of type T was thrown as expected.
            }
            catch (Exception ex)
            {
                throw new Exception("Expected exception of type " + typeof(T).Name + " but " + ex.GetType().Name + " was thrown.", ex);
            }
        }
    }
}
