using Core.Extract;
using System.IO.Compression;

namespace Core.Test;

[TestClass]
public sealed class ExtractorSupportProbeTest
{
    [TestMethod]
    public void ZipFileExtractor_ProbeSupport_ReturnsTrue_ForValidZipArchive()
    {
        string root = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string archivePath = Path.Combine(root, "sample.zip");

        try
        {
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("hello.txt");
                using StreamWriter writer = new(entry.Open());
                writer.Write("hello");
            }

            ZipFileExtractor extractor = new();

            ExtractorSupportProbeResult result = extractor.ProbeSupport(archivePath);

            Assert.IsTrue(result.CanHandle, result.Reason);
            Assert.AreEqual(string.Empty, result.Reason);
            Assert.IsNull(result.Exception);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ZipFileExtractor_ProbeSupport_ReturnsInvalidData_ForCorruptArchive()
    {
        string root = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string archivePath = Path.Combine(root, "corrupt.zip");

        try
        {
            File.WriteAllText(archivePath, "this is not a zip file");

            ZipFileExtractor extractor = new();

            ExtractorSupportProbeResult result = extractor.ProbeSupport(archivePath);

            Assert.IsFalse(result.CanHandle);
            StringAssert.Contains(result.Reason, "valid zip file");
            Assert.IsInstanceOfType<InvalidDataException>(result.Exception);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void NativeSevenZipExtractor_ProbeSupport_ReturnsMissingArchiveReason()
    {
        string root = Path.Combine(Path.GetTempPath(), "GersangStation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string fakeExePath = Path.Combine(root, "7za.exe");
        File.WriteAllText(fakeExePath, string.Empty);

        try
        {
            NativeSevenZipExtractor extractor = new(fakeExePath);

            ExtractorSupportProbeResult result = extractor.ProbeSupport(Path.Combine(root, "missing.7z"));

            Assert.IsFalse(result.CanHandle);
            StringAssert.Contains(result.Reason, "does not exist");
            Assert.IsNull(result.Exception);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
