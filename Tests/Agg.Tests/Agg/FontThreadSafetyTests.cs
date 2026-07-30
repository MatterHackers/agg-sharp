using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
    public class FontThreadSafetyTests
    {
        private const int ThreadCount = 8;

        private static TypeFace[] ReadInstanceFromManyThreads(Func<TypeFace> getInstance)
        {
            var results = new TypeFace[ThreadCount];
            using (var barrier = new Barrier(ThreadCount))
            {
                var threads = Enumerable.Range(0, ThreadCount)
                    .Select(i => new Thread(() =>
                    {
                        // Maximize contention by releasing all threads at once
                        barrier.SignalAndWait();
                        results[i] = getInstance();
                    }))
                    .ToList();

                threads.ForEach(thread => thread.Start());
                threads.ForEach(thread => thread.Join());
            }

            return results;
        }

        [Test]
        public async Task LiberationSansFontInstanceIsSingleFullyConstructedInstanceAcrossThreads()
        {
            TypeFace[] results = ReadInstanceFromManyThreads(() => LiberationSansFont.Instance);

            foreach (TypeFace typeFace in results)
            {
                await Assert.That(typeFace).IsNotNull();
                await Assert.That(ReferenceEquals(typeFace, results[0])).IsTrue();
                // A torn publish would expose a TypeFace before ReadSVG populated it
                await Assert.That(typeFace.UnitsPerEm).IsGreaterThan(0);
                var styled = new StyledTypeFace(typeFace, 12);
                await Assert.That(styled.GetGlyphForCharacter('A')).IsNotNull();
            }
        }

        [Test]
        public async Task LiberationSansBoldFontInstanceIsSingleFullyConstructedInstanceAcrossThreads()
        {
            TypeFace[] results = ReadInstanceFromManyThreads(() => LiberationSansBoldFont.Instance);

            foreach (TypeFace typeFace in results)
            {
                await Assert.That(typeFace).IsNotNull();
                await Assert.That(ReferenceEquals(typeFace, results[0])).IsTrue();
                await Assert.That(typeFace.UnitsPerEm).IsGreaterThan(0);
                var styled = new StyledTypeFace(typeFace, 12);
                await Assert.That(styled.GetGlyphForCharacter('A')).IsNotNull();
            }
        }

        [Test]
        public async Task ConcurrentGetImageForCharacterAcrossTypeFacesDoesNotThrow()
        {
            // Two distinct TypeFaces sharing the one static image cache
            var typeFaces = new[] { LiberationSansFont.Instance, LiberationSansBoldFont.Instance };
            var colors = new[] { Color.Black, Color.Red };
            var exceptions = new ConcurrentBag<Exception>();
            var nullImages = 0;

            using (var barrier = new Barrier(ThreadCount))
            {
                var threads = Enumerable.Range(0, ThreadCount)
                    .Select(i => new Thread(() =>
                    {
                        try
                        {
                            var styled = new StyledTypeFace(typeFaces[i % typeFaces.Length], 12);
                            var color = colors[i % colors.Length];
                            // Timeout so a thread that faults before signaling cannot hang the rest
                            barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                            for (char character = 'A'; character <= 'Z'; character++)
                            {
                                ImageBuffer image = styled.GetImageForCharacter(character, 0, 0, color);
                                if (image == null)
                                {
                                    Interlocked.Increment(ref nullImages);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }))
                    .ToList();

                threads.ForEach(thread => thread.Start());
                threads.ForEach(thread => thread.Join());
            }

            await Assert.That(exceptions).IsEmpty();
            await Assert.That(nullImages).IsEqualTo(0);
        }
    }
}
