using System;

namespace GerSDK.Tests;

public class Tests
{
    private const uint ERROR_PATH_NOT_FOUND = 3;

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test_IsSupportedSymlink()
    {
        bool result = GamePathChecker.IsSupportedSymlink("F:\\Games\\AKInteractive\\Gersang");
        Assert.That(result, Is.True);

        // 상대 경로가 포함된 경로
        result = GamePathChecker.IsSupportedSymlink("F:\\Games\\AKInteractive\\Gersang..\\Gersang2");
        Assert.That(result, Is.True);

        // F:\에서 \를 빼먹어 F:로 적는 경우
        Assert.Throws<ArgumentException>(() => GamePathChecker.IsSupportedSymlink("F:Games\\AKInteractive\\Gersang"));

        // 드라이브 문자 없이 상대경로로 쓴 경우
        Assert.Throws<ArgumentException>(() => GamePathChecker.IsSupportedSymlink("..\\Games\\AKInteractive\\Gersang"));

        // 존재하지 않는 드라이브 경로일 경우에는 실패
        var ex2 = Assert.Throws<System.ComponentModel.Win32Exception>(() => GamePathChecker.IsSupportedSymlink("Z:\\Games\\AKInteractive\\Gersang"));
        Assert.That(ex2.NativeErrorCode, Is.EqualTo(ERROR_PATH_NOT_FOUND));

        // 존재하지 않는 경로라도 드라이브 정보만 유효하다면 판단 가능
        result = GamePathChecker.IsSupportedSymlink("C:\\Games\\AKInteractive\\Gersang\\UnknownDirentory");
        Assert.That(result, Is.True);

        // 존재하지 않는 파일 경로라도 드라이브 정보만 유효하다면 판단 가능
        result = GamePathChecker.IsSupportedSymlink("C:\\Games\\AKInteractive\\Gersang\\UnknownFile.exe");
        Assert.That(result, Is.True);

        // 경로가 비어있거나 null인 경우
        Assert.Throws<ArgumentNullException>(() => GamePathChecker.IsSupportedSymlink(string.Empty));
    }
}
