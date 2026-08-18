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
    /// <summary>
    /// Covers the bound on <see cref="StyledTypeFaceImageCache"/>: repeat lookups below the cap share the
    /// cached instance, inserts past the cap evict and stay bounded, and neither path throws under
    /// concurrent lookups.
    /// </summary>
    /// <remarks>
    /// The cache and its cap are process-wide, so every test here is a keyless <c>[NotInParallel]</c> - it
    /// has to run exclusively, not merely serialized against its siblings. A constraint key was not enough:
    /// <c>FontThreadSafetyTests.ConcurrentGetImageForCharacterAcrossTypeFacesDoesNotThrow</c> renders the
    /// same (LiberationSans, 12pt, Black) glyphs from eight threads with no parallel constraint of its own,
    /// and a store that began before ours landed overwrites the entry last-write-wins - so the second lookup
    /// in <see cref="RepeatLookupsBelowCapReturnSameImageInstance"/> came back a different instance. Any
    /// other test in the assembly that renders text can do the same, hence exclusive rather than keyed.
    /// </remarks>
    public class FontCacheEvictionTests
    {
        [Test]
        [NotInParallel]
        public async Task RepeatLookupsBelowCapReturnSameImageInstance()
        {
            int originalCap = StyledTypeFaceImageCache.MaxCachedImages;
            try
            {
                StyledTypeFaceImageCache.MaxCachedImages = 8192;
                StyledTypeFaceImageCache.Clear();

                var styled = new StyledTypeFace(LiberationSansFont.Instance, 12);
                for (char character = 'A'; character <= 'Z'; character++)
                {
                    ImageBuffer first = styled.GetImageForCharacter(character, 0, 0, Color.Black);
                    ImageBuffer second = styled.GetImageForCharacter(character, 0, 0, Color.Black);
                    await Assert.That(first).IsNotNull();
                    await Assert.That(ReferenceEquals(first, second)).IsTrue();
                }
            }
            finally
            {
                StyledTypeFaceImageCache.MaxCachedImages = originalCap;
            }
        }

        [Test]
        [NotInParallel]
        public async Task ExceedingCapEvictsAndKeepsCacheBoundedWithValidImages()
        {
            int originalCap = StyledTypeFaceImageCache.MaxCachedImages;
            try
            {
                const int cap = 8;
                StyledTypeFaceImageCache.MaxCachedImages = cap;
                StyledTypeFaceImageCache.Clear();

                var styled = new StyledTypeFace(LiberationSansFont.Instance, 12);
                for (char character = 'A'; character <= 'Z'; character++)
                {
                    ImageBuffer image = styled.GetImageForCharacter(character, 0, 0, Color.Black);
                    await Assert.That(image).IsNotNull();
                    await Assert.That(image.Width).IsGreaterThan(0);
                    await Assert.That(image.Height).IsGreaterThan(0);
                    await Assert.That(StyledTypeFaceImageCache.CachedImageCount).IsLessThanOrEqualTo(cap);
                }

                // 26 inserts against a cap of 8 must have evicted at least once
                await Assert.That(StyledTypeFaceImageCache.CachedImageCount).IsLessThanOrEqualTo(cap);

                // Lookups after eviction still produce valid images
                ImageBuffer after = styled.GetImageForCharacter('A', 0, 0, Color.Black);
                await Assert.That(after).IsNotNull();
                await Assert.That(after.Width).IsGreaterThan(0);
            }
            finally
            {
                StyledTypeFaceImageCache.MaxCachedImages = originalCap;
                StyledTypeFaceImageCache.Clear();
            }
        }

        [Test]
        [NotInParallel]
        public async Task ConcurrentLookupsUnderEvictionPressureDoNotThrow()
        {
            int originalCap = StyledTypeFaceImageCache.MaxCachedImages;
            try
            {
                const int cap = 4;
                const int threadCount = 8;
                StyledTypeFaceImageCache.MaxCachedImages = cap;
                StyledTypeFaceImageCache.Clear();

                var typeFaces = new[] { LiberationSansFont.Instance, LiberationSansBoldFont.Instance };
                var colors = new[] { Color.Black, Color.Red };
                var exceptions = new ConcurrentBag<Exception>();
                var nullImages = 0;

                using (var barrier = new Barrier(threadCount))
                {
                    var threads = Enumerable.Range(0, threadCount)
                        .Select(i => new Thread(() =>
                        {
                            try
                            {
                                var styled = new StyledTypeFace(typeFaces[i % typeFaces.Length], 12);
                                var color = colors[i % colors.Length];
                                // Timeout so a thread that faults before signaling cannot hang the rest
                                barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                                for (int pass = 0; pass < 3; pass++)
                                {
                                    for (char character = 'A'; character <= 'Z'; character++)
                                    {
                                        ImageBuffer image = styled.GetImageForCharacter(character, 0, 0, color);
                                        if (image == null)
                                        {
                                            Interlocked.Increment(ref nullImages);
                                        }
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
                await Assert.That(StyledTypeFaceImageCache.CachedImageCount).IsLessThanOrEqualTo(cap);
            }
            finally
            {
                StyledTypeFaceImageCache.MaxCachedImages = originalCap;
                StyledTypeFaceImageCache.Clear();
            }
        }
    }
}
