namespace GersangStation.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

[TestClass]
[Ignore("Extractor benchmark only run mode")]
public class PureDownloadTests {
    public TestContext TestContext { get; set; }

    private const string DownloadUrl = "http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z";
    private string _tempFolderPath;
    private string _destinationPath;

    [TestInitialize]
    public void Setup() {
        // 실행 파일 위치 기준 Temp 폴더 경로 설정
        _tempFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
        _destinationPath = Path.Combine(_tempFolderPath, "Gersang_Install.7z");

        if(!Directory.Exists(_tempFolderPath)) {
            Directory.CreateDirectory(_tempFolderPath);
        }

        if(File.Exists(_destinationPath)) {
            File.Delete(_destinationPath);
        }

        TestContext.WriteLine($"tempFolderPath : {_tempFolderPath}");
        TestContext.WriteLine($"_destinationPath : {_destinationPath}");
    }

    [Ignore("duration : 3m 51s 755ms")]
    [TestMethod]
    public async Task PureDownloadTest() {
        // Arrange
        using var client = new HttpClient();

        // Act
        using(var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
            response.EnsureSuccessStatusCode();

            using(var stream = await response.Content.ReadAsStreamAsync())
            using(var fileStream = new FileStream(_destinationPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                await stream.CopyToAsync(fileStream);
            }
        }

        // Assert
        Assert.IsTrue(File.Exists(_destinationPath), "파일이 로컬에 생성되지 않았습니다.");

        FileInfo fileInfo = new FileInfo(_destinationPath);
        Assert.IsTrue(fileInfo.Length > 0, "다운로드된 파일의 크기가 0입니다.");

        TestContext.WriteLine($"다운로드 완료: {fileInfo.Length:N0} bytes");
    }
}
