using Core.Patch;
using SharpCompress.Archives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Core.Test;

[TestClass]
public sealed class PatchMetadataParseTest
{
    [TestMethod]
    public async Task DecodeLatestVersionFromVsnDat_FromRealServerVsnArchive()
    {
        const string url = "https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/Client_Patch_File/Online/vsn.dat.gsz";

        using var http = new HttpClient();
        byte[] archiveBytes = await http.GetByteArrayAsync(url);

        string root = Path.Combine(Path.GetTempPath(), "GersangStation_vsn_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            string archivePath = Path.Combine(root, "vsn.dat.gsz");
            string extractRoot = Path.Combine(root, "extract");

            await File.WriteAllBytesAsync(archivePath, archiveBytes);
            Directory.CreateDirectory(extractRoot);

            using (var archive = ArchiveFactory.OpenArchive(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(extractRoot, new SharpCompress.Common.ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }

            var files = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).ToArray();
            Assert.AreEqual(1, files.Length, "vsn.dat.gsz should contain exactly one file.");

            await using var stream = File.OpenRead(files[0]);
            int version = PatchPipeline.DecodeLatestVersionFromVsnDat(stream);

            Assert.IsTrue(version >= 34014, $"decoded version should be at least 34014. actual={version}");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
