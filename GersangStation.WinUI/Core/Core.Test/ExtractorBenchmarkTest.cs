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
        const string fixedExtractRoot = @"E:\Projects\dotnet\GersangStation\GersangStation.WinUI\Core\Core.Test\bin\Debug\net8.0-windows10.0.19041.0\Temp";

        Assert.IsTrue(File.Exists(archivePath), $"Archive file not found: {archivePath}");
        Directory.CreateDirectory(fixedExtractRoot);

        var extractor = new NativeSevenZipExtractor();

        // 요청사항: Temp\{Extractor식별자}\ 경로로 고정
        string destination = Path.Combine(fixedExtractRoot, SanitizeName(extractor.Name));

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
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
            TestContext.WriteLine($"  - destination: {destination}");
            TestContext.WriteLine($"  - elapsed    : {sw.Elapsed}");
            TestContext.WriteLine($"  - files      : {fileCount}");
            TestContext.WriteLine($"  - bytes      : {totalBytes}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            TestContext.WriteLine($"[BENCH][FAIL] {extractor.Name}");
            TestContext.WriteLine($"  - destination: {destination}");
            TestContext.WriteLine($"  - elapsed    : {sw.Elapsed}");
            TestContext.WriteLine($"  - error      : {ex}");
        }
    }

    public TestContext TestContext { get; set; } = default!;

    private static string SanitizeName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }
}
