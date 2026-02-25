using Core.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archives;
using System.Diagnostics;

namespace Core.Test;

[TestClass]
[Ignore("Extractor benchmark only run mode")]
public sealed class PatchPipelineTest
{
    [Ignore]
    [TestMethod]
    public async Task RunPatchAsync_DownloadAndExtract_ToTempGameFolder()
    {
        // Arrange
        var patchBaseUri = new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/");

        string tempRoot = Path.Combine(Path.GetTempPath(), "GersangStation2_PatchPipelineTest_" + Guid.NewGuid().ToString("N"));
        string installRoot = Path.Combine(tempRoot, "Game");
        string tempPatchRoot = Path.Combine(tempRoot, "TempPatch");

        Directory.CreateDirectory(installRoot);

        Debug.WriteLine("[TEST][BEGIN] PatchPipelineTest.RunPatchAsync_DownloadAndExtract_ToTempGameFolder");
        Debug.WriteLine($"[TEST] installRoot : {installRoot}");
        Debug.WriteLine($"[TEST] tempPatch   : {tempPatchRoot}");
        Debug.WriteLine($"[TEST] tempRoot    : {tempRoot}");
        Debug.WriteLine($"[TEST] baseUri     : {patchBaseUri}");

        int currentClientVersion = await ReadCurrentVersionFromSampleDataAsync();
        Debug.WriteLine($"[TEST] currentClientVersion(from SampleData/vsn.dat): {currentClientVersion}");

        int latestServerVersion = await ReadLatestServerVersionFromServerAsync(patchBaseUri);
        Debug.WriteLine($"[TEST] latestServerVersion(from server): {latestServerVersion}");

        Assert.IsTrue(
            latestServerVersion >= currentClientVersion,
            $"latestServerVersion({latestServerVersion}) should be >= currentClientVersion({currentClientVersion})");

        var sw = Stopwatch.StartNew();

        try
        {
            // Act
            Debug.WriteLine("[TEST][STEP] RunPatchAsync start");
            await PatchPipeline.RunPatchAsync(
                currentClientVersion: currentClientVersion,
                patchBaseUri: patchBaseUri,
                installRoot: installRoot,
                tempRoot: tempPatchRoot,
                maxConcurrency: 2,
                maxExtractRetryCount: 2,
                ct: CancellationToken.None,
                cleanupTemp: false);
            Debug.WriteLine("[TEST][STEP] RunPatchAsync end");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Debug.WriteLine($"[FAIL] RunPatchAsync threw after {sw.Elapsed}.");
            Debug.WriteLine(ex.ToString());

            Debug.WriteLine("---- installRoot snapshot ----");
            DumpDir(installRoot, maxLines: 120);

            Debug.WriteLine("---- tempPatch snapshot ----");
            DumpDir(tempPatchRoot, maxLines: 200);

            throw;
        }
        finally
        {
            sw.Stop();
            Debug.WriteLine($"[TEST][END] RunPatchAsync elapsed: {sw.Elapsed}");
            Debug.WriteLine($"[TEST][END] installRoot exists: {Directory.Exists(installRoot)}");
            Debug.WriteLine($"[TEST][END] tempPatchRoot exists: {Directory.Exists(tempPatchRoot)}");

            // 결과 확인을 위해 삭제하지 않음(요청사항)
            // if (Directory.Exists(tempRoot))
            //     Directory.Delete(tempRoot, recursive: true);
        }

        // Assert
        int extractedCount = Directory.Exists(installRoot)
            ? Directory.EnumerateFiles(installRoot, "*", SearchOption.AllDirectories).Count()
            : 0;

        int downloadedCount = Directory.Exists(tempPatchRoot)
            ? Directory.EnumerateFiles(tempPatchRoot, "*.gsz", SearchOption.AllDirectories).Count()
            : 0;

        Debug.WriteLine($"[TEST][ASSERT] Extracted file count: {extractedCount}");
        Debug.WriteLine($"[TEST][ASSERT] Downloaded gsz count: {downloadedCount}");
        Debug.WriteLine("---- installRoot (first 120 files) ----");
        DumpFiles(installRoot, maxFiles: 120);

        Debug.WriteLine("---- tempPatchRoot (first 80 files) ----");
        DumpFiles(tempPatchRoot, maxFiles: 80);

        Assert.IsTrue(extractedCount > 0, $"Extract 결과가 비어있음: {installRoot}");

        static void DumpFiles(string root, int maxFiles)
        {
            if (!Directory.Exists(root))
            {
                Debug.WriteLine("(dir not found)");
                return;
            }

            int i = 0;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                Debug.WriteLine(file);
                if (++i >= maxFiles) break;
            }
        }

        static void DumpDir(string root, int maxLines)
        {
            if (!Directory.Exists(root))
            {
                Debug.WriteLine("(dir not found)");
                return;
            }

            int lines = 0;

            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                Debug.WriteLine("[D] " + dir);
                if (++lines >= maxLines) return;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                Debug.WriteLine("[F] " + file);
                if (++lines >= maxLines) return;
            }
        }
    }

    private static async Task<int> ReadCurrentVersionFromSampleDataAsync()
    {
        string vsnPath = Path.Combine(AppContext.BaseDirectory, "SampleData", "vsn.dat");
        Debug.WriteLine($"[TEST][STEP] Read current version from: {vsnPath}");

        Assert.IsTrue(File.Exists(vsnPath), $"SampleData/vsn.dat not found: {vsnPath}");

        await using var stream = File.OpenRead(vsnPath);
        int version = PatchPipeline.DecodeLatestVersionFromVsnDat(stream);
        return version;
    }

    private static async Task<int> ReadLatestServerVersionFromServerAsync(Uri patchBaseUri)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "GersangStation2_PatchPipelineTest_VsnProbe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        string archivePath = Path.Combine(tempRoot, "vsn.dat.gsz");
        string extractRoot = Path.Combine(tempRoot, "extract");
        Directory.CreateDirectory(extractRoot);

        var url = new Uri(patchBaseUri, "Client_Patch_File/Online/vsn.dat.gsz");
        Debug.WriteLine($"[TEST][STEP] Download latest version metadata from: {url}");

        using var http = new HttpClient();
        byte[] archiveBytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(archivePath, archiveBytes);
        Debug.WriteLine($"[TEST] vsn.dat.gsz size: {archiveBytes.Length} bytes");

        using (var archive = ArchiveFactory.OpenArchive(archivePath))
        {
            archive.WriteToDirectory(extractRoot);
        }

        var files = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).ToArray();
        Debug.WriteLine($"[TEST] extracted vsn files: {files.Length}");
        Assert.AreEqual(1, files.Length, "vsn.dat.gsz should contain exactly one file.");

        await using var stream = File.OpenRead(files[0]);
        int version = PatchPipeline.DecodeLatestVersionFromVsnDat(stream);

        // 결과 확인을 위해 probe 폴더도 삭제하지 않음(요청사항)
        Debug.WriteLine($"[TEST] keep vsn probe root for inspection: {tempRoot}");

        return version;
    }
}
