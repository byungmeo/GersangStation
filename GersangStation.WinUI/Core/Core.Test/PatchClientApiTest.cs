using Core.Patch;

namespace Core.Test;

[TestClass]
[Ignore("Extractor benchmark only run mode")]
public sealed class PatchClientApiTest
{
    [Ignore]
    [TestMethod]
    public void GetCurrentClientVersion_ReadsOnlineVsnDat_FromConfiguredInstallRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "GersangStation_PatchClientApiTest_" + Guid.NewGuid().ToString("N"));
        string onlineDir = Path.Combine(root, "Online");
        Directory.CreateDirectory(onlineDir);

        try
        {
            string sourceVsn = Path.Combine(AppContext.BaseDirectory, "SampleData", "vsn.dat");
            Assert.IsTrue(File.Exists(sourceVsn), "SampleData/vsn.dat not found.");

            string destVsn = Path.Combine(onlineDir, "vsn.dat");
            File.Copy(sourceVsn, destVsn);

            PatchClientApi.SetClientInstallRoot(root);

            int version = PatchClientApi.GetCurrentClientVersion();
            Assert.AreEqual(34001, version);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Ignore]
    [TestMethod]
    public void FixedSuffixAndUri_AreExpected()
    {
        Assert.AreEqual("Client_Readme/readme.txt", PatchClientApi.ReadMeSuffix);
        Assert.AreEqual("Client_Patch_File/Online/vsn.dat.gsz", PatchClientApi.LatestVersionArchiveSuffix);
        Assert.AreEqual("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/", PatchClientApi.PatchBaseUri.ToString());
        Assert.AreEqual("http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z", PatchClientApi.FullClientUri.ToString());
    }
}
