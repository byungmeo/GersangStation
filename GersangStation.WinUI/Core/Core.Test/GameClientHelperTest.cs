namespace Core.Test;

[TestClass]
public sealed class GameClientHelperTest
{
    [TestMethod]
    public void TryIsSymbolFile_ReturnsTrue_ForSymbolicFile()
    {
        string root = CreateTempRoot();

        try
        {
            EnsureSymbolicLinksAvailable(root);

            string targetFilePath = Path.Combine(root, "target.txt");
            string symbolFilePath = Path.Combine(root, "symbol.txt");
            File.WriteAllText(targetFilePath, "target");
            File.CreateSymbolicLink(symbolFilePath, targetFilePath);

            GameClientHelper.SymbolFileProbeResult result = GameClientHelper.TryIsSymbolFile(symbolFilePath);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsSymbolFile);
            Assert.IsNull(result.FailureStage);
            Assert.IsNull(result.Exception);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [TestMethod]
    public void TryCreateSymbolMultiClient_ReplacesDestinationSymbolicFileBeforeCopy()
    {
        string root = CreateTempRoot();
        string sourcePath = Path.Combine(root, "Main");
        string destPath = Path.Combine(root, "Clone2");

        try
        {
            EnsureSymbolicLinksAvailable(root);
            CreateMinimalClient(sourcePath, version: 34100, "source-version");
            PrepareExistingCloneWithSymbolicFile(sourcePath, destPath);

            CreateSymbolMultiClientArgs args = new()
            {
                InstallPath = sourcePath,
                DestPath2 = destPath,
                LayoutPolicy = GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
            };

            CreateSymbolMultiClientResult result = GameClientHelper.TryCreateSymbolMultiClient(args);

            Assert.IsTrue(result.Success, result.Reason);

            string copiedFilePath = Path.Combine(destPath, "Online", "vsn.dat");
            Assert.IsTrue(File.Exists(copiedFilePath));
            Assert.IsFalse(GameClientHelper.IsSymbolFile(copiedFilePath));
            Assert.AreEqual("source-version", File.ReadAllText(copiedFilePath));
            Assert.IsFalse(Directory.Exists(Path.Combine(destPath, "PatchTemp")));
            Assert.IsFalse(Directory.Exists(Path.Combine(destPath, "GersangDown")));
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), "GersangStation", nameof(GameClientHelperTest), Guid.NewGuid().ToString("N"));

    private static void EnsureSymbolicLinksAvailable(string anyPathInDrive)
    {
        if (!GameClientHelper.CanUseSymbol(anyPathInDrive, out string resolvedFormat))
            Assert.Inconclusive($"Symbolic links are not supported on this drive. Format={resolvedFormat}");

        Directory.CreateDirectory(anyPathInDrive);

        string probeTargetPath = Path.Combine(anyPathInDrive, "probe-target.txt");
        string probeSymbolPath = Path.Combine(anyPathInDrive, "probe-symbol.txt");
        File.WriteAllText(probeTargetPath, "probe");

        try
        {
            File.CreateSymbolicLink(probeSymbolPath, probeTargetPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Unable to create symbolic links in the test environment: {ex.Message}");
        }
    }

    private static void CreateMinimalClient(string root, int version, string vsnContents)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Run.exe"), "run");
        File.WriteAllText(Path.Combine(root, "config.ln"), "config");

        string onlinePath = Path.Combine(root, "Online");
        Directory.CreateDirectory(Path.Combine(onlinePath, "Map"));
        File.WriteAllText(Path.Combine(onlinePath, "vsn.dat"), vsnContents);
        File.WriteAllBytes(Path.Combine(onlinePath, "version.bin"), PatchManager.EncodeVersionToVsn(version));

        string xigncodePath = Path.Combine(root, "XIGNCODE");
        Directory.CreateDirectory(xigncodePath);
        File.WriteAllText(Path.Combine(xigncodePath, "xigncode.bin"), "xign");

        string patchTempPath = Path.Combine(root, "PatchTemp");
        Directory.CreateDirectory(patchTempPath);
        File.WriteAllText(Path.Combine(patchTempPath, "patch.tmp"), "patch");

        string gersangDownPath = Path.Combine(root, "GersangDown");
        Directory.CreateDirectory(gersangDownPath);
        File.WriteAllText(Path.Combine(gersangDownPath, "download.tmp"), "download");
    }

    private static void PrepareExistingCloneWithSymbolicFile(string sourcePath, string destPath)
    {
        string destOnlinePath = Path.Combine(destPath, "Online");
        Directory.CreateDirectory(destOnlinePath);

        string sourceMapPath = Path.Combine(sourcePath, "Online", "Map");
        string destMapPath = Path.Combine(destOnlinePath, "Map");
        Directory.CreateSymbolicLink(destMapPath, sourceMapPath);

        string oldTargetPath = Path.Combine(destPath, "old-vsn.txt");
        File.WriteAllText(oldTargetPath, "old-version");
        File.CreateSymbolicLink(Path.Combine(destOnlinePath, "vsn.dat"), oldTargetPath);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string filePath in Directory.GetFiles(path))
            File.Delete(filePath);

        foreach (string directoryPath in Directory.GetDirectories(path))
        {
            FileAttributes attributes = File.GetAttributes(directoryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(directoryPath);
                continue;
            }

            DeleteDirectoryIfExists(directoryPath);
        }

        Directory.Delete(path);
    }
}
