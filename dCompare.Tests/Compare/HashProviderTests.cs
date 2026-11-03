using dCompare.Compare;

namespace dCompare.Tests.Compare
{
    public class HashProviderTests : IDisposable
    {
        private readonly HashProvider _provider = new();
        private readonly string _dir;

        public HashProviderTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dCompareTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void ComputeXxHash64_PartialRatio_IgnoresBytesBeyondRatio()
        {
            // 100 bytes; first 40 identical, rest differs. At ratio 0.4 the hashes must match;
            // at ratio 1.0 they must not.
            var shared = new byte[40];
            for (var i = 0; i < shared.Length; i++) shared[i] = (byte)i;
            var a = new byte[100];
            var b = new byte[100];
            shared.CopyTo(a, 0);
            shared.CopyTo(b, 0);
            for (var i = 40; i < 100; i++) { a[i] = 0; b[i] = (byte)(i + 1); }

            Assert.Equal(
                _provider.ComputeXxHash64(new MemoryStream(a), ratio: 0.4),
                _provider.ComputeXxHash64(new MemoryStream(b), ratio: 0.4));
            Assert.NotEqual(
                _provider.ComputeXxHash64(new MemoryStream(a), ratio: 1.0),
                _provider.ComputeXxHash64(new MemoryStream(b), ratio: 1.0));
        }

        [Fact]
        public void ComputeMD5_PartialRatio_BoundaryIsTruncatedLength()
        {
            // Length 10, ratio 0.3 -> reads exactly 3 bytes. A difference at byte 4+ is invisible
            // at 0.3 but visible at 0.4.
            var a = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11 }; // differs only in the tail

            Assert.Equal(
                _provider.ComputeMD5(new MemoryStream(a), ratio: 0.3),
                _provider.ComputeMD5(new MemoryStream(b), ratio: 0.3));
            Assert.NotEqual(
                _provider.ComputeMD5(new MemoryStream(a), ratio: 1.0),
                _provider.ComputeMD5(new MemoryStream(b), ratio: 1.0));
        }

        [Fact]
        public void ComputeXxHash64_SameContent_SameHash()
        {
            var bytes = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
            Assert.Equal(
                _provider.ComputeXxHash64(new MemoryStream(bytes)),
                _provider.ComputeXxHash64(new MemoryStream(bytes)));
        }
    }
}
