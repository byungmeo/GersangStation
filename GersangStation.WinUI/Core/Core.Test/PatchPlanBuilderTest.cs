using Core.Patch;

namespace Core.Test;

[TestClass]
[Ignore("Extractor benchmark only run mode")]
public sealed class PatchPlanBuilderTest
{
    [Ignore]
    [TestMethod]
    public void BuildExtractPlan_StoresFirstEntryChecksum_FromRow6First()
    {
        IReadOnlyDictionary<int, List<string[]>> entriesByVersion = new Dictionary<int, List<string[]>>
        {
            [34001] = [MakeRow("_patchdata_34001.gsz", row4: "ignored", row6: "3993170318", "")]
        };

        var plan = PatchPlanBuilder.BuildExtractPlan(entriesByVersion);

        Assert.AreEqual(1, plan.ByVersion.Count);
        var file = plan.ByVersion[34001].Single();
        Assert.AreEqual("3993170318", file.FirstEntryChecksum);

        static string[] MakeRow(string compressedFileName, string row4, string row6, string relativeDir)
        {
            var row = new string[7];
            row[1] = compressedFileName;
            row[3] = relativeDir;
            row[4] = row4;
            row[6] = row6;
            return row;
        }
    }

    [Ignore]
    [TestMethod]
    public void LegacyPatchPlanBuilderStringRows_DelegatesToNewBuilder()
    {
        IReadOnlyDictionary<int, List<string[]>> entriesByVersion = new Dictionary<int, List<string[]>>
        {
            [34001] = [MakeRow("_patchdata_34001.gsz", row4: "ignored", row6: "3993170318", "\\Online\\")]
        };

        var plan = PatchPlanBuilder_StringRows.BuildExtractPlan(entriesByVersion);

        Assert.AreEqual(1, plan.ByVersion.Count);
        Assert.AreEqual("_patchdata_34001.gsz", plan.ByVersion[34001].Single().CompressedFileName);

        static string[] MakeRow(string compressedFileName, string row4, string row6, string relativeDir)
        {
            var row = new string[7];
            row[1] = compressedFileName;
            row[3] = relativeDir;
            row[4] = row4;
            row[6] = row6;
            return row;
        }
    }

}
