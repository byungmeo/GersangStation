using Core.Extractor;
using System.Diagnostics;

namespace Core.Test;

[TestClass]
public sealed class ExtractorBenchmarkTest
{
    [TestMethod]
    public async Task CompareExtractors_WithSingleArchivePath()
    {
        const string archivePath = @"E:\Projects\dotnet\GersangStation\GersangStation.WinUI\Core\Core.Test\bin\Debug\net8.0-windows10.0.19041.0\Temp\Gersang_Install.7z";
        Assert.IsTrue(File.Exists(archivePath), $"Archive file not found: {archivePath}");

        var extractors = new IExtractor[]
        {
            new ZipFileExtractor(),
            new NativeSevenZipExtractor()
        };

        string benchmarkRoot = Path.Combine(Path.GetTempPath(), "GersangStation", "ExtractorBenchmark", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(benchmarkRoot);

        try
        {
            foreach (var extractor in extractors)
            {
                string destination = Path.Combine(benchmarkRoot, SanitizeName(extractor.Name));
                Directory.CreateDirectory(destination);

                var sw = Stopwatch.StartNew();

                try
                {
                    await extractor.ExtractAsync(archivePath, destination, progress: null, ct: CancellationToken.None);
                    sw.Stop();

                    int fileCount = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Count();
                    long totalBytes = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                        .Sum(path => new FileInfo(path).Length);

                    TestContext.WriteLine($"[BENCH][PASS] {extractor.Name}");
                    TestContext.WriteLine($"  - elapsed : {sw.Elapsed}");
                    TestContext.WriteLine($"  - files   : {fileCount}");
                    TestContext.WriteLine($"  - bytes   : {totalBytes}");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    TestContext.WriteLine($"[BENCH][FAIL] {extractor.Name}");
                    TestContext.WriteLine($"  - elapsed : {sw.Elapsed}");
                    TestContext.WriteLine($"  - error   : {ex}");
                }
            }
        }
        finally
        {
            if (Directory.Exists(benchmarkRoot))
                Directory.Delete(benchmarkRoot, recursive: true);
        }
    }

    public TestContext TestContext { get; set; } = default!;

    private static string SanitizeName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }
}
