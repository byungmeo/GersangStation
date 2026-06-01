using Core.Models;

namespace Core.Test;

[TestClass]
public sealed class ClientSettingsTest
{
    [TestMethod]
    public void MultiClientPaths_UseStationManagedSuffix()
    {
        ClientSettings settings = new()
        {
            InstallPath = @"C:\Games\Gersang"
        };

        Assert.AreEqual(@"C:\Games\Gersang2_CreatedByStation", settings.Client2Path);
        Assert.AreEqual(@"C:\Games\Gersang3_CreatedByStation", settings.Client3Path);
    }

    [TestMethod]
    public void MultiClientPaths_TrimTrailingDirectorySeparator()
    {
        ClientSettings settings = new()
        {
            InstallPath = @"C:\Games\Gersang\"
        };

        Assert.AreEqual(@"C:\Games\Gersang2_CreatedByStation", settings.Client2Path);
        Assert.AreEqual(@"C:\Games\Gersang3_CreatedByStation", settings.Client3Path);
    }
}
