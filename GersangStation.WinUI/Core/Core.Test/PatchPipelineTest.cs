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
        const int latestServerVersion = 34014;

        var patchBaseUri = new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/");

        string tempRoot = Path.Combine(Path.GetTempPath(), "GersangStation2_PatchPipelineTest_" + Guid.NewGuid().ToString("N"));
        string installRoot = Path.Combine(tempRoot, "Game");
        string tempPatchRoot = Path.Combine(tempRoot, "TempPatch");

        Directory.CreateDirectory(installRoot);

        Debug.WriteLine($"installRoot : {installRoot}");
        Debug.WriteLine($"tempPatch   : {tempPatchRoot}");
        Debug.WriteLine($"baseUri     : {patchBaseUri}");

        IReadOnlyDictionary<int, List<string[]>> entriesByVersion = new Dictionary<int, List<string[]>>
        {
            [34001] = [MakeRow("_patchdata_34001.gsz", "")],
            [34002] = [MakeRow("_patchdata_34002.gsz", "")],
            [34003] = [MakeRow("_patchdata_34003.gsz", "")],
            [34004] = [MakeRow("_patchdata_34004.gsz", "")],
            [34005] = [MakeRow("_patchdata_34005.gsz", "")],
            [34006] = [MakeRow("_patchdata_34006.gsz", "")],
            [34007] = [MakeRow("_patchdata_34007.gsz", "")],
            [34008] = [MakeRow("_patchdata_34008.gsz", "")],
            [34009] = [MakeRow("_patchdata_34009.gsz", "")],
            [34011] = [MakeRow("_patchdata_34011.gsz", "")],
            [34012] = [MakeRow("_patchdata_34012.gsz", "")],
            [34013] = [MakeRow("_patchdata_34013.gsz", "")],
            [34014] = [MakeRow("_patchdata_34014.gsz", "")],
        };

        // (선택) plan을 미리 만들어서 "버전별로 뭐가 실행될지"를 로그로 확인
        var plan = PatchPlanBuilder_StringRows.BuildExtractPlan(entriesByVersion);
        LogPlanSummary(plan);

        var sw = Stopwatch.StartNew();

        try
        {
            // Act
            await PatchPipeline.RunPatchAsync(
                currentClientVersion: currentClientVersion,
                latestServerVersion: latestServerVersion,
                entriesByVersion: entriesByVersion,
                patchBaseUri: patchBaseUri,
                installRoot: installRoot,
                tempRoot: tempPatchRoot,
                maxConcurrency: 2,
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

        // local helper
        static string[] MakeRow(string compressedFileName, string relativeDir)
        {
            var row = new string[4];
            row[1] = compressedFileName;
            row[3] = relativeDir;
            return row;
        }

        static void LogPlanSummary(PatchExtractPlan plan)
        {
            int versions = plan.ByVersion.Count;
            int files = plan.ByVersion.Values.Sum(v => v.Count);

            Debug.WriteLine($"Plan versions: {versions}, total files: {files}");
            foreach (var kv in plan.ByVersion) // 오름차순 보장 가정(구현에 따라 다르면 정렬해서 찍어도 됨)
            {
                Debug.WriteLine($"  v{kv.Key}: {kv.Value.Count} file(s)");
                foreach (var f in kv.Value)
                    Debug.WriteLine($"    - dir='{f.RelativeDir}', gsz='{f.CompressedFileName}'");
            }
        }

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