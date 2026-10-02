using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Managers;
using NAudio.Wave;

namespace MySoundBoard.Tests.Managers
{
    [TestClass]
    public class LoopingSampleProviderTests
    {
        private static (LoopingSampleProvider provider, WaveStream stream) Create(float[] samples, Func<bool> loop)
        {
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            var stream = new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
            return (new LoopingSampleProvider(stream.ToSampleProvider(), stream, loop), stream);
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
