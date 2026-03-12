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
}
