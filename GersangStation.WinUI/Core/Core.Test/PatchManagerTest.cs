namespace Core.Test;

[TestClass]
public sealed class PatchManagerTest
{
    [TestMethod]
    public void EncodeVersionToVsn_RoundTripsWithDecode()
    {
        const int version = 34012;

        byte[] bytes = PatchManager.EncodeVersionToVsn(version);
        int decodedVersion = PatchManager.DecodeVersionFromVsn(bytes);

        Assert.AreEqual(version, decodedVersion);
    }

    [TestMethod]
    public void WriteClientVersion_WritesReadableVsnFile()
    {
        string clientPath = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));

        try
        {
            const int version = 32123;

            PatchManager.WriteClientVersion(clientPath, version);

            string vsnPath = Path.Combine(clientPath, "Online", "vsn.dat");
            Assert.IsTrue(File.Exists(vsnPath), $"vsn.dat was not created. path={vsnPath}");
            Assert.AreEqual(version, PatchManager.GetCurrentClientVersion(clientPath));
        }
        finally
        {
            if (Directory.Exists(clientPath))
                Directory.Delete(clientPath, recursive: true);
        }
    }

    [TestMethod]
    public void TryGetCurrentClientVersion_ReturnsMissingFileStage_WhenVsnDoesNotExist()
    {
        string clientPath = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(clientPath);

            ClientVersionReadResult result = PatchManager.TryGetCurrentClientVersion(clientPath);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Version);
            Assert.AreEqual(ClientVersionReadFailureStage.OpenVsnFile, result.FailureStage);
            Assert.IsInstanceOfType<FileNotFoundException>(result.Exception);
            Assert.IsFalse(result.FileExists);
        }
        finally
        {
            if (Directory.Exists(clientPath))
                Directory.Delete(clientPath, recursive: true);
        }
    }

    [TestMethod]
    public void TryGetCurrentClientVersion_ReturnsDecodeStage_ForTruncatedVsn()
    {
        string clientPath = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));

        try
        {
            string onlinePath = Path.Combine(clientPath, "Online");
            Directory.CreateDirectory(onlinePath);
            string vsnPath = Path.Combine(onlinePath, "vsn.dat");
            File.WriteAllBytes(vsnPath, [0x01, 0x02]);

            ClientVersionReadResult result = PatchManager.TryGetCurrentClientVersion(clientPath);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Version);
            Assert.AreEqual(ClientVersionReadFailureStage.DecodeVsnContents, result.FailureStage);
            Assert.IsInstanceOfType<InvalidDataException>(result.Exception);
            Assert.IsTrue(result.FileExists);
        }
        finally
        {
            if (Directory.Exists(clientPath))
                Directory.Delete(clientPath, recursive: true);
        }
    }
}
