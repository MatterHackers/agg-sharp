using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
    /// <summary>
    /// Covers the reference-identity guarantee of <see cref="StyledTypeFaceImageCache"/>: for a given
    /// (TypeFace, color, size, character) the cache retains exactly one <see cref="ImageBuffer"/> instance,
    /// and every caller that raced to render that character is handed the retained instance rather than its
    /// own throwaway copy.
    /// </summary>
    /// <remarks>
    /// Identity matters because consumers key off the returned instance - <c>LcdMaskCache</c> deliberately
    /// guarantees identity, and a texture cache keyed on the glyph image would silently double its entries
    /// (or miss entirely) if a character's image instance could change under it.
    /// <para>
    /// Like <see cref="FontCacheEvictionTests"/> these are keyless <c>[NotInParallel]</c>: the cache is
    /// process-wide, these tests clear it and depend on missing it, and any other test in the assembly that
    /// renders text would populate it underneath them. Exclusive, not merely serialized against siblings.
    /// </para>
    /// </remarks>
    public class FontCacheIdentityTests
    {
        [Test]
        [NotInParallel]
        public async Task StoringAnAlreadyCachedCharacterKeepsTheFirstInstance()
        {
            try
            {
                var styled = new StyledTypeFace(LiberationSansFont.Instance, 12);

                // Two genuinely rendered images of the same glyph, obtained the way production makes them.
                // Clearing between the two renders is what makes the second a distinct instance.
                StyledTypeFaceImageCache.Clear();
                ImageBuffer firstRender = styled.GetImageForCharacter('A', 0, 0, Color.Black);
                StyledTypeFaceImageCache.Clear();
                ImageBuffer secondRender = styled.GetImageForCharacter('A', 0, 0, Color.Black);
                await Assert.That(ReferenceEquals(firstRender, secondRender)).IsFalse();

                // Replay the concurrent first-render race: both threads missed, both rendered, both store.
                StyledTypeFaceImageCache.Clear();
                StyledTypeFaceImageCache.StoreImage(styled.TypeFace, Color.Black, styled.EmSizeInPixels, 'A', firstRender);
                ImageBuffer retained = StyledTypeFaceImageCache.StoreImage(styled.TypeFace, Color.Black, styled.EmSizeInPixels, 'A', secondRender);

                // The loser's image is discarded, not swapped in, and the loser is told which instance won
                await Assert.That(ReferenceEquals(retained, firstRender)).IsTrue();

                StyledTypeFaceImageCache.TryGetImage(styled.TypeFace, Color.Black, styled.EmSizeInPixels, 'A', out ImageBuffer found);
                await Assert.That(ReferenceEquals(found, firstRender)).IsTrue();
                await Assert.That(StyledTypeFaceImageCache.CachedImageCount).IsEqualTo(1);
            }
            finally
            {
                StyledTypeFaceImageCache.Clear();
            }
        }

        [Test]
        [NotInParallel]
        public async Task ConcurrentFirstRenderOfSameCharacterHandsEveryThreadOneInstance()
        {
            const int threadCount = 8;

            try
            {
                var characters = Enumerable.Range('A', 26).Select(value => (char)value).ToList();
                var exceptions = new ConcurrentBag<Exception>();

                // [character index][thread index] - every thread's view of every raced character
                var rendered = characters.Select(_ => new ImageBuffer[threadCount]).ToList();

                // Clearing in the post-phase action is what forces a real race: the cache is emptied while
                // every thread is parked at the barrier, so all of them miss the next character together.
                using (var barrier = new Barrier(threadCount, _ => StyledTypeFaceImageCache.Clear()))
                {
                    var threads = Enumerable.Range(0, threadCount)
                        .Select(threadIndex => new Thread(() =>
                        {
                            try
                            {
                                var styled = new StyledTypeFace(LiberationSansFont.Instance, 12);
                                for (int characterIndex = 0; characterIndex < characters.Count; characterIndex++)
                                {
                                    // Timeout so a thread that faults before signaling cannot hang the rest
                                    barrier.SignalAndWait(TimeSpan.FromSeconds(30));
                                    rendered[characterIndex][threadIndex] = styled.GetImageForCharacter(characters[characterIndex], 0, 0, Color.Black);
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

                var charactersWithSplitIdentity = new List<char>();
                for (int characterIndex = 0; characterIndex < characters.Count; characterIndex++)
                {
                    ImageBuffer[] images = rendered[characterIndex];
                    if (images.Any(image => image == null)
                        || images.Any(image => !ReferenceEquals(image, images[0])))
                    {
                        charactersWithSplitIdentity.Add(characters[characterIndex]);
                    }
                }

                await Assert.That(charactersWithSplitIdentity).IsEmpty();

                // The last character was never cleared out from under the threads, so the cache still holds
                // the instance they were all handed
                var probe = new StyledTypeFace(LiberationSansFont.Instance, 12);
                StyledTypeFaceImageCache.TryGetImage(probe.TypeFace, Color.Black, probe.EmSizeInPixels, characters[characters.Count - 1], out ImageBuffer cached);
                await Assert.That(ReferenceEquals(cached, rendered[characters.Count - 1][0])).IsTrue();
            }
            finally
            {
                StyledTypeFaceImageCache.Clear();
            }
        }
    }
}
