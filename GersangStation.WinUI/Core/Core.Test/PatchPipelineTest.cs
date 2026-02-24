using Core.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace Core.Test;

[TestClass]
public sealed class PatchPipelineTest
{
    [TestMethod]
    public async Task RunPatchAsync_DownloadAndExtract_ToTempGameFolder()
    {
        // Arrange
        const int currentClientVersion = 34000;

        var patchBaseUri = new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/");

        string tempRoot = Path.Combine(Path.GetTempPath(), "GersangStation2_PatchPipelineTest_" + Guid.NewGuid().ToString("N"));
        string installRoot = Path.Combine(tempRoot, "Game");
        string tempPatchRoot = Path.Combine(tempRoot, "TempPatch");

        Directory.CreateDirectory(installRoot);

        Debug.WriteLine($"installRoot : {installRoot}");
        Debug.WriteLine($"tempPatch   : {tempPatchRoot}");
        Debug.WriteLine($"baseUri     : {patchBaseUri}");

        var sw = Stopwatch.StartNew();

        try
        {
            // Act
            await PatchPipeline.RunPatchAsync(
                currentClientVersion: currentClientVersion,
                patchBaseUri: patchBaseUri,
                installRoot: installRoot,
                tempRoot: tempPatchRoot,
                maxConcurrency: 2,
                maxExtractRetryCount: 2,
                ct: CancellationToken.None);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Debug.WriteLine($"[FAIL] RunPatchAsync threw after {sw.Elapsed}.");
            Debug.WriteLine(ex.ToString());

            Debug.WriteLine("---- installRoot snapshot ----");
            DumpDir(installRoot, maxLines: 80);

            Debug.WriteLine("---- tempPatch snapshot ----");
            DumpDir(tempPatchRoot, maxLines: 120);

            throw;
        }
        finally
        {
            sw.Stop();
            Debug.WriteLine($"RunPatchAsync elapsed: {sw.Elapsed}");
        }

        // Assert
        int extractedCount = Directory.Exists(installRoot)
            ? Directory.EnumerateFiles(installRoot, "*", SearchOption.AllDirectories).Count()
            : 0;

        Debug.WriteLine($"Extracted file count: {extractedCount}");
        Debug.WriteLine("---- installRoot (first 50 files) ----");
        DumpFiles(installRoot, maxFiles: 50);

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
}