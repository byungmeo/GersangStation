using Core.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Core.Test;

[TestClass]
public sealed class PatchMetadataParseTest
{
    [TestMethod]
    public void ParseClientInfoRows_ParsesDataLinesOnly()
    {
        const string content = """
;\tBased on Gersang Text DB Common Parser System V1.0
;인덱스\t압축 파일명\t원본 파일명\t파일 경로(오로지 경로만)\t압축 파일 체크섬\t원본 파일 체크섬\t압축 파일 압축CRC\t파일옵션
#INDEX\tZIP FileName\tOrigin FileName\tFile Path\tZIP File CheckSum\tOrigin File CheckSum\tZIP CRC\tFile Option
1\tvsn.dat.gsz\tvsn.dat\t\\Online\\\t894037877\t562532317\t2267779679\t1
2\t_patchdata_34001.gsz\t_patchdata_34001\t\\\t405490937\t0\t3993170318\t6
;EOF
""";

        var rows = PatchPipeline.ParseClientInfoRows(content);

        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual("vsn.dat.gsz", rows[0][1]);
        Assert.AreEqual("\\Online\\", rows[0][3]);
        Assert.AreEqual("894037877", rows[0][4]);
        Assert.AreEqual("_patchdata_34001.gsz", rows[1][1]);
        Assert.AreEqual("405490937", rows[1][4]);
    }

    [TestMethod]
    public void DecodeLatestVersionFromVsnDat_ReadsOnesComplement()
    {
        const int expectedVersion = 32806;
        int encoded = ~expectedVersion;
        byte[] bytes = BitConverter.GetBytes(encoded);

        int decoded = PatchPipeline.DecodeLatestVersionFromVsnDat(bytes);

        Assert.AreEqual(expectedVersion, decoded);
    }

    [TestMethod]
    public void DecodeLatestVersionFromVsnDat_Throws_WhenTooShort()
    {
        var ex = Assert.ThrowsException<InvalidDataException>(
            () => PatchPipeline.DecodeLatestVersionFromVsnDat(Encoding.ASCII.GetBytes("A")));

        StringAssert.Contains(ex.Message, "vsn.dat size is too small");
    }
}
