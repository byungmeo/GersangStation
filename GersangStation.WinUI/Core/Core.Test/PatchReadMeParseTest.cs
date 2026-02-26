using Core.Patch;

namespace Core.Test;

[TestClass]
public sealed class PatchReadMeParseTest
{
    [TestMethod]
    public void ParseRecentPatchNotes_ReturnsRecentFivePatchNotes()
    {
        string readMe = """
-2026.02.24-
[정식 패치 V34020]
1. 첫 번째 패치 내용입니다.
- 항목 A

-2026.02.12-
[정식 패치 V34014]
1. 두 번째 패치 내용입니다.

-2026.01.28-
[정식 패치 V34013]
1. 세 번째 패치 내용입니다.

-2026.01.28-
[정식 패치 V34012]
1. 네 번째 패치 내용입니다.

-2026.01.10-
[정식 패치 V34011]
1. 다섯 번째 패치 내용입니다.

-2025.12.31-
[정식 패치 V34010]
1. 여섯 번째 패치 내용입니다.
""";

        IReadOnlyList<PatchNote> notes = PatchClientApi.ParseRecentPatchNotes(readMe, takeCount: 5).ToList();

        Assert.AreEqual(5, notes.Count);
        Assert.AreEqual("2026.02.24", notes[0].DateStr);
        Assert.AreEqual("[정식 패치 V34020]", notes[0].Title);
        StringAssert.Contains(notes[0].Note, "첫 번째 패치 내용입니다.");

        Assert.AreEqual("2026.01.10", notes[4].DateStr);
        Assert.AreEqual("[정식 패치 V34011]", notes[4].Title);
    }
}
