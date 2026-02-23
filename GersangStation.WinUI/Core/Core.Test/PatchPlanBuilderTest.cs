using Core.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Core.Test;

[TestClass]
public sealed class PatchPlanBuilderTest
{
    [TestMethod]
    public void BuildExtractPlan_StoresArchiveChecksum()
    {
        IReadOnlyDictionary<int, List<string[]>> entriesByVersion = new Dictionary<int, List<string[]>>
        {
            [34001] = [MakeRow("_patchdata_34001.gsz", "a1b2c3d4", "")]
        };

        var plan = PatchPlanBuilder_StringRows.BuildExtractPlan(entriesByVersion);

        Assert.AreEqual(1, plan.ByVersion.Count);
        var file = plan.ByVersion[34001].Single();
        Assert.AreEqual("a1b2c3d4", file.ArchiveChecksum);

        static string[] MakeRow(string compressedFileName, string checksum, string relativeDir)
        {
            var row = new string[4];
            row[1] = compressedFileName;
            row[2] = checksum;
            row[3] = relativeDir;
            return row;
        }
    }
}
