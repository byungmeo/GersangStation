using System.Diagnostics;
using System.IO.Compression;
using System.Net;

namespace GersangStation.Modules;

internal class VersionChecker
{
    // private static bool isDownloadedVsn = false; //vsn파일 다운로드 남용을 차단

    public static string GetCurrentVersion(Form owner, string path_main)
    {
        //Logger.Log("Log : (" + "*static*_Form_ClientSetting" + ") " + "현재 거상 본클라 버전 확인 시도");
        string version;
        try
        {
            FileStream fs = File.OpenRead(path_main + @"\Online\vsn.dat");
            BinaryReader br = new BinaryReader(fs);
            version = (-(br.ReadInt32() + 1)).ToString();
            fs.Close();
            br.Close();
        }
        catch (Exception e)
        {
            MessageBox.Show(owner, "현재 거상 버전 확인 중 오류가 발생하였습니다.\n문의해주세요." + e.Message
                , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return "";
        }

        return version;
    }

    public static string GetLatestVersion(Form owner, string url_vsn)
    {
        //Logger.Log("Log : (" + "*static*_Form_ClientSetting" + ") " + "현재 거상 최신 버전 확인 시도");
        try
        {
            Trace.WriteLine("vsn파일을 다운로드 합니다.");
            return CheckServerVsn(url_vsn);
        }
        catch (Exception e)
        {
            MessageBox.Show(owner, "거상 최신 버전 확인 중 오류가 발생하였습니다.\n문의해주세요.\n" + e.Message
                , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return "";
        }
    }

    //게임 시작 시 마다 vsn.dat을 받는 것은 부담이 될 수 있으므로,
    //1회 게임 시작 시 vsn.dat을 받으면 그 이후엔 받지 않도록 함.
    /*
    public static string GetLatestVersion_Safe(Form owner, string url_vsn) {
        //Logger.Log("Log : (" + "*static*_Form_ClientSetting" + ") " + "현재 거상 최신 버전 확인 시도_Safe");
        string version;

        if (true == isDownloadedVsn) {
            try {
                //Logger.Log("Log : (" + "*static*_Form_ClientSetting" + ") " + "현재 거상 최신 버전 확인 시도_Safe -> 체크하지 않음");
                Trace.WriteLine("vsn파일을 다운로드 하지 않습니다.");
                return CheckServerVsn(url_vsn, false);
            }
            catch (Exception e) {
                MessageBox.Show(owner, "거상 최신 버전 확인 중 오류가 발생하였습니다.\n문의해주세요. (Safe)" + e.Message
                , "거상 경로 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }
        }
        else {
            version = GetLatestVersion(owner, url_vsn);
            if (version != "") { isDownloadedVsn = true; }
            return version;
        }
    }
    */

    private static string CheckServerVsn(string url_vsn)
    {
        string version;
        DirectoryInfo binDirectory = new DirectoryInfo(Application.StartupPath + @"\bin");

        //현재 거상 최신 버전을 확인합니다
        using (WebClient client = new())
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            //마이크로소프트 권장 사항 : 보안프로토콜의 결정은 운영체제에게 맡겨야 한다.
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

            if (!binDirectory.Exists) { binDirectory.Create(); }
            else
            {
                foreach (FileInfo file in binDirectory.GetFiles())
                {
                    if (file.Name.Equals("vsn.dat"))
                    {
                        file.Delete();
                    }
                }
            }

            client.DownloadFile(new Uri(url_vsn), Application.StartupPath + @"\bin\vsn.dat.gsz");

            Trace.WriteLine("vsn.dat.gsz 파일 다운로드 완료");
            ZipFile.ExtractToDirectory(binDirectory.FullName + @"\vsn.dat.gsz", binDirectory.FullName);
            Trace.WriteLine("vsn.dat 파일 압축 해제 완료");
        }

        FileStream fs = File.OpenRead(binDirectory.FullName + @"\vsn.dat");
        BinaryReader br = new BinaryReader(fs);
        version = (-(br.ReadInt32() + 1)).ToString();
        Trace.WriteLine("서버에 게시된 거상 최신 버전 : " + version);
        fs.Close();
        br.Close();
        return version;
    }
}
