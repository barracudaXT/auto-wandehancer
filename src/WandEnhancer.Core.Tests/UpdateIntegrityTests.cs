using System.IO;
using System.Text;
using NUnit.Framework;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class UpdateIntegrityTests
    {
        [Test]
        public void ComputeSha256_MatchesKnownVector()
        {
            // FIPS 180-2 test vector for "abc".
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc")))
            {
                Assert.AreEqual(
                    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                    UpdateChecker.ComputeSha256(stream));
            }
        }

        [Test]
        public void ComputeSha256_DetectsTamperedPayload()
        {
            // A single flipped byte must change the digest — this is what lets
            // DownloadAndVerifyAsync refuse a substituted installer.
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc")))
            {
                var original = UpdateChecker.ComputeSha256(stream);
                using (var tampered = new MemoryStream(Encoding.ASCII.GetBytes("abd")))
                {
                    Assert.AreNotEqual(original, UpdateChecker.ComputeSha256(tampered));
                }
            }
        }

        [Test]
        public void ParseDigest_StripsGithubPrefixAndLowercases()
        {
            Assert.AreEqual(
                "e2bcda7beb9d90a333cb43c499db81aa94048ec60d9d104db563253efaf4e6fe",
                UpdateChecker.ParseDigest("sha256:E2BCDA7BEB9D90A333CB43C499DB81AA94048EC60D9D104DB563253EFAF4E6FE"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ParseDigest_ReturnsNullWhenAbsent(string digest)
        {
            // A release with no digest must fail closed, not install unverified.
            Assert.IsNull(UpdateChecker.ParseDigest(digest));
        }

        [Test]
        public void ParseDigest_AcceptsBareHex()
        {
            Assert.AreEqual("abc123", UpdateChecker.ParseDigest("abc123"));
        }
    }
}
