namespace Core.Test;

[TestClass]
public sealed class PathWriteProbeTest
{
    [TestMethod]
    public void TryProbeDirectoryWriteAccess_ReturnsWritableForExistingDirectory()
    {
        string root = CreateTempRoot();

        try
        {
            Directory.CreateDirectory(root);

            DirectoryWriteProbeResult result = PathWriteProbe.TryProbeDirectoryWriteAccess(root);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.CanWrite);
            Assert.AreEqual(Path.GetFullPath(root), result.TargetPath);
            Assert.AreEqual(Path.GetFullPath(root), result.ProbePath);
            Assert.IsNull(result.Exception);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [TestMethod]
    public void TryProbeDirectoryWriteAccess_UsesNearestExistingParentForPlannedDirectory()
    {
        string root = CreateTempRoot();
        string plannedPath = Path.Combine(root, "AKInteractive", "Gersang");

        try
        {
            Directory.CreateDirectory(root);

            DirectoryWriteProbeResult result = PathWriteProbe.TryProbeDirectoryWriteAccess(plannedPath);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.CanWrite);
            Assert.AreEqual(Path.GetFullPath(plannedPath), result.TargetPath);
            Assert.AreEqual(Path.GetFullPath(root), result.ProbePath);
            Assert.IsNull(result.Exception);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), "GersangStation", nameof(PathWriteProbeTest), Guid.NewGuid().ToString("N"));

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
