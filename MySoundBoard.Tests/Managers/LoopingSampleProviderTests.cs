using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Managers;
using NAudio.Wave;

namespace MySoundBoard.Tests.Managers
{
    [TestClass]
    public class LoopingSampleProviderTests
    {
        // startSample/endSample mark the trim region in mono float samples; endSample 0 means "to the end".
        private static (LoopingSampleProvider provider, WaveStream stream) Create(
            float[] samples, Func<bool> loop, int startSample = 0, int endSample = 0)
        {
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            var stream = new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            stream.Position = startSample * sizeof(float);
            var provider = new LoopingSampleProvider(stream.ToSampleProvider(), stream, loop,
                startSample * sizeof(float), endSample * sizeof(float));
            return (provider, stream);
        }

        [TestMethod]
        public void Read_WithTrimEnd_StopsAtRegionEnd()
        {
            var (provider, stream) = Create(new[] { 1f, 2f, 3f, 4f, 5f }, () => false, endSample: 3);
            using (stream)
            {
                var buffer = new float[8];
                Assert.AreEqual(3, provider.Read(buffer, 0, buffer.Length));
                CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, buffer.Take(3).ToArray());
                Assert.AreEqual(0, provider.Read(buffer, 0, buffer.Length));
            }
        }

        [TestMethod]
        public void Read_WithTrimAndLoop_LoopsOnlyTheRegion()
        {
            var (provider, stream) = Create(new[] { 1f, 2f, 3f, 4f, 5f }, () => true, startSample: 1, endSample: 3);
            using (stream)
            {
                var buffer = new float[6];
                Assert.AreEqual(6, provider.Read(buffer, 0, buffer.Length));
                CollectionAssert.AreEqual(new[] { 2f, 3f, 2f, 3f, 2f, 3f }, buffer);
            }
        }

        [TestMethod]
        public void Read_WithTrimEndPastStreamLength_PlaysToStreamEnd()
        {
            var (provider, stream) = Create(new[] { 1f, 2f }, () => false, endSample: 50);
            using (stream)
                Assert.AreEqual(2, provider.Read(new float[8], 0, 8));
        }

        [TestMethod]
        public void Read_WithLoop_WrapsAroundToFillBuffer()
        {
            var (provider, stream) = Create(new[] { 1f, 2f, 3f }, () => true);
            using (stream)
            {
                var buffer = new float[8];
                int read = provider.Read(buffer, 0, buffer.Length);
                Assert.AreEqual(8, read);
                CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 1f, 2f, 3f, 1f, 2f }, buffer);
            }
        }

        [TestMethod]
        public void Read_WithoutLoop_StopsAtEndOfStream()
        {
            var (provider, stream) = Create(new[] { 1f, 2f, 3f }, () => false);
            using (stream)
            {
                var buffer = new float[8];
                Assert.AreEqual(3, provider.Read(buffer, 0, buffer.Length));
                Assert.AreEqual(0, provider.Read(buffer, 0, buffer.Length));
            }
        }

        [TestMethod]
        public void Read_LoopToggledOffMidway_StopsAtNextEnd()
        {
            bool loop = true;
            var (provider, stream) = Create(new[] { 1f, 2f }, () => loop);
            using (stream)
            {
                var buffer = new float[3];
                Assert.AreEqual(3, provider.Read(buffer, 0, 3));
                loop = false;
                Assert.AreEqual(1, provider.Read(buffer, 0, 3));
                Assert.AreEqual(0, provider.Read(buffer, 0, 3));
            }
        }

        [TestMethod]
        public void Read_EmptyStream_ReturnsZeroInsteadOfSpinning()
        {
            var (provider, stream) = Create(Array.Empty<float>(), () => true);
            using (stream)
                Assert.AreEqual(0, provider.Read(new float[4], 0, 4));
        }
    }
}
