using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace GersangStation.Modules {

    internal struct FullClientInfo
    {
        public static string Url = @"http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z";
        public static string FileName = "Gersang_Install.7z";
        public static uint HeaderSize = 32; //32 bytes

        public FullClientInfo() { }
        public static string CreateRange(long start, long end)
        {
            return $"bytes={start}-{end}";
        }
    }

    public class IntegrityChecker
    {
        static string _SevenZipPath = @"\Resources\7zip\7za.exe";

        static string _BaseURL = @"http://akgersang.xdn.kinxcdn.com/";
        static string _ReadMePath = _BaseURL + @"Gersang/Patch/Gersang_Server/Client_Readme/readme.txt";
        static string _ClientInfoFile = _BaseURL + @"Gersang/Patch/Gersang_Server/Client_info_File/"; // + "00000"

        private string _ClientPath = string.Empty;
        private string _TempPath = string.Empty;
        private bool _verbose = false;

        public event EventHandler<ProgressChangedEventArgs> ? ProgressChanged;
        int start = 0;
        int localCheckEnd = 60;
        int webCheckEnd = 90;
        int diffCheckEnd = 100;
        static long progressCounter = 0;


        private IntegrityChecker(string clientPath, string tempPath, bool verbose)
        {
            _ClientPath = clientPath;
            _TempPath = tempPath;
            _verbose = verbose;
        }

        public static IntegrityChecker? CreateIntegrityChecker(string clientPath, string tempPath, bool verbose = false)
        {
            if (Directory.Exists(clientPath) == false) return null;
            try
            {
                if (Directory.Exists(tempPath) == false)
                    Directory.CreateDirectory(tempPath);
            }
            catch (Exception e)
            {
                Trace.WriteLine("IntegrityCheck: Failed to create temp directory");
                Trace.Write(e);
                throw;
            }

            return new IntegrityChecker(clientPath, tempPath, verbose);
        }

        private void WriteFullClientFile(string path)
        {
            long headerSize = 32; //32 bytes
            long footerSize = 1024 * 1024 * 1; //1 mega bytes
            string fullSizeString = string.Empty;
            long fullSize = 0;

            //Create new file
            FileStream? fileStream = null;
            try
            {
                fileStream = File.Open(path, FileMode.CreateNew);
            }
            catch (Exception)
            {
                Trace.WriteLine("Failed to create file");
                throw;
            }

            //Get header, and full size information
            if (_verbose)
            {
                Trace.WriteLine("Header request");
            }
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Make request 
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, FullClientInfo.Url);
                    requestMessage.Headers.Add("Range", FullClientInfo.CreateRange(0, headerSize - 1)); //Get header
                    if (_verbose)
                    {
                        Trace.WriteLine("Created request message");
                        Trace.WriteLine(requestMessage);
                    }

                    using HttpResponseMessage response = client.Send(requestMessage);
                    HttpContentHeaders contentHeaders = response.Content.Headers;
                    if (_verbose)
                    {
                        Trace.WriteLine("Response from server");
                        Trace.WriteLine(contentHeaders);
                    }
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        Trace.WriteLine("Failed");
                        throw new HttpRequestException();
                    }

                    string contentRange = contentHeaders.GetValues("Content-Range").First();
                    fullSizeString = contentRange.Split("/")[1];
                    if (_verbose)
                    {
                        Trace.WriteLine($"Size of Full Client = {fullSizeString}");
                    }

                    byte[] headerBytes = response.Content.ReadAsByteArrayAsync().Result;
                    fileStream.Write(headerBytes, 0, headerBytes.Length);
                }
            }
            catch (Exception)
            {
                throw;
            }
            if (_verbose)
            {
                Trace.WriteLine($"succeeded. file size = {fullSizeString}");
            }

            //Fill dummy data between header and footer
            fullSize = long.Parse(fullSizeString);
            long dummySize = fullSize - (footerSize + headerSize);
            long dummyChunkSize = 1024 * 1024 * 500; //500MB
            Trace.WriteLine(dummySize);
            Trace.WriteLine(dummyChunkSize);

            try
            {
                byte[] b = new byte[dummyChunkSize];
                while (dummySize >= dummyChunkSize)
                {
                    fileStream.Write(b, 0, b.Length);
                    dummySize -= dummyChunkSize;
                }
                if (dummySize > 0)
                {
                    b = new byte[dummySize];
                    fileStream.Write(b, 0, b.Length);
                }
            }
            catch (Exception)
            {
                throw;
            }

            //Get footer information
            if (_verbose)
            {
                Trace.WriteLine("Footer request");
            }
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    //Make request 
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, FullClientInfo.Url);
                    requestMessage.Headers.Add("Range", FullClientInfo.CreateRange(fullSize - footerSize, fullSize - 1)); //Get header
                    if (_verbose)
                    {
                        Trace.WriteLine("Created request message");
                        Trace.WriteLine(requestMessage);
                    }

                    using HttpResponseMessage response = client.Send(requestMessage);
                    HttpContentHeaders contentHeaders = response.Content.Headers;
                    if (_verbose)
                    {
                        Trace.WriteLine("Response from server");
                        Trace.WriteLine(contentHeaders);
                    }
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        Trace.WriteLine("Failed");
                        throw new HttpRequestException();
                    }

                    byte[] footerBytes = response.Content.ReadAsByteArrayAsync().Result;
                    fileStream.Write(footerBytes, 0, footerBytes.Length);
                }
            }
            catch (Exception)
            {
                throw;
            }

            fileStream.Close();
            //Check file size and web info
            FileInfo fileInfo = new FileInfo(path);
            string message = $"Created File Length = {fileInfo.Length}, WebInfo = {fullSize}";
            Trace.WriteLine(message);
            if (fullSize != fileInfo.Length)
            {
                throw new InvalidDataException(message);
            }
        }

        private Dictionary<string, string> GetFileListFromVersion(int version) 
        {
            Dictionary<string, string> output = new Dictionary<string, string>();
            string url = _ClientInfoFile + $"{version}";
            using (HttpClient client = new HttpClient())
            {
                //Make request 
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                using HttpResponseMessage response = client.Send(requestMessage);

                byte[] data = response.Content.ReadAsByteArrayAsync().Result;
                string str = System.Text.Encoding.GetEncoding("EUC-KR").GetString(data);

                if (_verbose)
                {
                    Trace.WriteLine($"Version {version}: ");
                }

                string[] lines = str.Split(System.Environment.NewLine);
                foreach (string line in lines)
                {
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') {
                        continue;
                    }
                    try
                    {
                        string[] columns = line.Split("\t");
                        if (columns.Length != 8) continue;

                        string fullPath = columns[3] + columns[2];
                        fullPath = fullPath.Substring(1, fullPath.Length - 1);

                        string CRC = long.Parse(columns[6]).ToString("X");
                        output.Add(fullPath.StartsWith("Korean")? fullPath.Replace("Korean", "korean") : fullPath , CRC);
                        Trace.WriteLine($"{fullPath} = {CRC}");
                    }
                    catch (Exception)
                    {
                    }
                }
                return output;
            }
        }

        private List<int> GetVersionListFromReadMe() {
            List<int> versionList = new();
            using (HttpClient client = new HttpClient())
            {
                //Make request 
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, _ReadMePath);
                using HttpResponseMessage response = client.Send(requestMessage);

                byte[] rawData = response.Content.ReadAsByteArrayAsync().Result;
                System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                string readmeFile = System.Text.Encoding.GetEncoding("EUC-KR").GetString(rawData);

                if (readmeFile == null) {
                    throw new Exception("Cannot read readme file");
                }
                //if (_verbose) {
                //    Trace.WriteLine("ReadMeFile: ");
                //    Trace.WriteLine(readmeFile);
                //}

                string[] versions = readmeFile.Split("[");
                foreach (var version in versions)
                {
                    string ver = version.Split("]")[0];
                    
                    if(_verbose)
                        Trace.Write($"{ver} -> ");
                    if (ver.Contains("패치") == false) {
                        if (_verbose)
                            Trace.WriteLine("");
                        continue;
                    } 
                    try
                    {
                        string result = Regex.Replace(ver, @"[^0-9]", "");
                        if (_verbose)
                            Trace.WriteLine(result);
                        versionList.Add(int.Parse(result));
                    }
                    catch (Exception)
                    {
                        //Ignore errors
                    }
                }
            }
            return versionList;
        }

        private Dictionary<string, string>? ParsingSevenZipHashValueCheckOutput(string input, string folderName) {
            Dictionary<string, string> output = new();
            string[] body = input.Split("-------- -------------  ------------");
            if (body.Length != 3) {
                throw new InvalidDataException("Hash check format is not correct. Expected split 3, but {body.Length}");
            }
            foreach (string line in body[1].Split(System.Environment.NewLine))
            {
                string[] fileData = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (fileData.Length > 3) {
                    int start = line.IndexOf(fileData[2]);
                    fileData[2] = line.Substring(start);
                }
                else if(fileData.Length != 3) {
                    continue;
                }
                if (fileData[2].EndsWith(".tmp")) continue; //Ignore tmp file

                string p = fileData[2];
                output.Add(p.Substring(folderName.Length + 1), fileData[0]);
            }
            return output;
        }

        
        private Dictionary<string, string>? ParsingSevenZipArchiveContentOutput(string input)
        {
            string[] files = input.Split("Path = ");

            Dictionary<string, string> data = new();
            for (long idx = 2; idx < files.Length; idx++)
            {
                Dictionary<string, string> attr = new();
                string fileRawData = files[idx];
                string[] attrs = fileRawData.Split(System.Environment.NewLine);

                for (long i = 1; i < attrs.Length; i++)
                {
                    string[] keyAndValue = attrs[i].Split("=", StringSplitOptions.TrimEntries);
                    if (keyAndValue.Length != 2) continue;
                    attr.Add(keyAndValue[0], keyAndValue[1]);
                }
                if (!attr.ContainsKey("Size") || !attr.ContainsKey("CRC"))
                    continue; //Ignore size 0 or does not have CRC value
                if (attr["Size"] == "0")
                    continue;
                data.Add(attrs[0].StartsWith("Korean") ? attrs[0].Replace("Korean", "korean") : attrs[0], attr["CRC"]);
            }
            return data;
        }

        //Input: 
        //Output: key = Path, val = CRC
        public Dictionary<string, string>? GetFullClientFileListFromWeb(out int version)
        {
            string path = _TempPath + @"\" + FullClientInfo.FileName;
            WriteFullClientFile(path);

            string SevenZipPath = Directory.GetCurrentDirectory() + _SevenZipPath;
            string args = $"l \"{path}\" -slt";
            if (_verbose)
            {
                Trace.WriteLine($"Run {SevenZipPath} {args}");
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = SevenZipPath;
            psi.Arguments = args;
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;

            Process? FullClientListCheckProcess = Process.Start(psi);
            if (FullClientListCheckProcess == null)
            {
                throw new Exception("Failed to get 7zip process");
            }
            string FullClientListProcessOutput = FullClientListCheckProcess.StandardOutput.ReadToEnd();
            if (_verbose)
            {
                Trace.WriteLine(FullClientListProcessOutput);
            }
            var files = ParsingSevenZipArchiveContentOutput(FullClientListProcessOutput);
            if (_verbose)
            {
                Trace.WriteLine(files);
            }

            string webClientCRC = "";
            if (files.TryGetValue(@"Online\vsn.dat", out webClientCRC) == false) {
                throw new Exception("Failed to find version data in FullClient");
            }

            List<int> versions = GetVersionListFromReadMe();
            //versions = versions.OrderByDescending(i => i).ToList();
            if (_verbose) {
                Trace.WriteLine("Detected Version List");
                foreach (var ver in versions) {
                    Trace.WriteLine(ver);
                }
            }

            int idx = 0;
            version = 0;
            for (idx = 0; idx < versions.Count; idx++)
            {
                int ver = versions[idx];
                Dictionary<string, string> fileList = GetFileListFromVersion(ver);
                string versionDataCRC = "";
                if (fileList.TryGetValue(@"Online\vsn.dat", out versionDataCRC)) {
                    if (versionDataCRC == webClientCRC) {
                        Trace.WriteLine($"Detected web full client version : {ver}");
                        version = ver;
                        break;
                    }
                }
            }
            if (versions.Contains(version) == false) {
                throw new Exception($"Failed to find match version: currentCRC = {webClientCRC}");
            }

            //Update CRC or files based on patch
            idx--;
            for (; idx >= 0; idx--) {
                int ver = versions[idx];
                Dictionary<string, string> fileList = GetFileListFromVersion(ver);
                Trace.WriteLine($"Update based on {ver}");
                foreach (var file in fileList) {
                    string strPath = file.Key;
                    string CRC = file.Value;

                    if (files.ContainsKey(strPath)) {
                        Trace.WriteLine($"  {strPath} = {files[strPath]} -> {CRC}");
                        files[strPath] = CRC;
                    }
                    else
                    {
                        Trace.WriteLine($"  {strPath} = (New) -> {CRC}");
                        files.Add(strPath, CRC);
                    }
                }
            }


            return files;
        }

        public async Task<string[]?> GetCRC32FromLocalFile(string path) {
            try
            {
                if (!File.Exists(path)) {
                    Interlocked.Add(ref progressCounter, 1);
                    return null;
                } 
                System.IO.Hashing.Crc32 crc32 = new();
                byte[] file = await File.ReadAllBytesAsync(path);
                crc32.Append(file);

                string[] result = { path, BitConverter.ToInt32(crc32.GetCurrentHash()).ToString("X") };
                Interlocked.Add(ref progressCounter, 1);
                return result;
            }
            catch
            {
                throw;
            }
            finally {
            }
        }

        //Input: Client Path
        //Output: key = Path, val = CRC32
        public Dictionary<string, string>? GetFullClientFileListFromLocal(string localClientPath)
        {
            var output = new Dictionary<string, string>();
            progressCounter = 0;

            string[] files = Directory.GetFiles(localClientPath, "*.*", SearchOption.AllDirectories);

            List < Task<string[]?>> tasks = new List<Task<string[]?>>();
            foreach (string file in files)
            {
                tasks.Add(Task.Run(() => GetCRC32FromLocalFile(file)));
            }

            Trace.WriteLine($"Run {tasks.Count} files");

            int whileCount = 0;
            while (whileCount < 3000) { //Wait 3000 = 300s = 5mins
                long currCount = Interlocked.Read(ref progressCounter);
                int currProgress = ((int)currCount * (localCheckEnd - start) / tasks.Count) + start;
                Trace.WriteLine(currProgress);
                if (currCount == tasks.Count) break;
                if(ProgressChanged != null)
                    ProgressChanged(this, new ProgressChangedEventArgs(currProgress, $"클라이언트의 파일을 읽어오는 중 입니다.  ({currCount} / {tasks.Count})"));
                Thread.Sleep(100);
                whileCount++;
            }

            Task.WaitAll(tasks.ToArray());
            foreach (Task<string[]?> task in tasks) {
                string[]? result = task.Result;
                if (_verbose)
                    Trace.WriteLine($"Path = {result[0].Substring(localClientPath.Length + 1)} \r\nCRC ={result[1]}");
                if (result != null) output.Add(result[0].Substring(localClientPath.Length+1), result[1]);
            }
            return output;
        }


        public void Run(out string reportFileName, ref Dictionary<string, string> output) {
            //Dictionary<string, string> output = new();
            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(start, "클라이언트의 파일을 읽어오는 중 입니다."));

            var localFiles = this.GetFullClientFileListFromLocal(_ClientPath);
            Trace.WriteLine($"In local, {localFiles.Count} files detected.");

            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(localCheckEnd, "서버로부터 파일을 가져오는 중 입니다."));

            int versionInfo = 0;
            var webFiles = this.GetFullClientFileListFromWeb(out versionInfo);
            Trace.WriteLine($"In web, {webFiles.Count} files detected.");

            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(webCheckEnd, "파일을 확인중입니다."));

            Dictionary<string, bool> excludedFiles = new Dictionary<string, bool>();
            {
                string[] lines = File.ReadAllLines(Directory.GetCurrentDirectory() + @"\Resources\IntegrityCheckExcludes.txt");
                foreach (var line in lines)
                {
                    if (line.Trim().Length == 0) continue;
                    excludedFiles.Add(line, true);
                }
            }

            List<string> excludedFolders = new List<string>();
            {
                string[] lines = File.ReadAllLines(Directory.GetCurrentDirectory() + @"\Resources\IntegrityCheckExcludeFolders.txt");
                foreach (var line in lines)
                {
                    if (line.Trim().Length == 0) continue;
                    excludedFolders.Add(line);
                }
            }


            string report = "";
            //Local to Web check

            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(webCheckEnd, "파일을 확인중입니다."));

            report += ("== Check report ==") + System.Environment.NewLine;
            foreach (var file in localFiles)
            {
                string? crc = null;
                if (excludedFiles.ContainsKey(file.Key)) continue;
                bool isInExcludedFolder = false;
                foreach (var dir in excludedFolders) {
                    if (file.Key.StartsWith(dir)) {
                        isInExcludedFolder = true;
                        break;
                    }
                }
                if (isInExcludedFolder) continue;

                if (webFiles.TryGetValue(file.Key, out crc))
                {
                    if (Convert.ToInt32(crc, 16) != Convert.ToInt32(file.Value, 16))
                    {
                        output.Add(file.Key, " 파일이 일치하지 않습니다.");
                        report += ($"ERROR: File {file.Key} not match = web({crc}) local({file.Value})") + System.Environment.NewLine;
                    }
                    excludedFiles.Add(file.Key, false);
                }
                else
                {
                    report += ($"WARNING: File {file.Key} is not exist in web full client") + System.Environment.NewLine;
                }
                webFiles.Remove(file.Key);
            }

            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(95, "파일을 확인중입니다."));

            //Web to Local
            foreach (var file in webFiles)
            {
                string? crc = null;
                if (excludedFiles.ContainsKey(file.Key)) continue;
                bool isInExcludedFolder = false;
                foreach (var dir in excludedFolders)
                {
                    if (file.Key.StartsWith(dir))
                    {
                        isInExcludedFolder = true;
                        break;
                    }
                }
                if (isInExcludedFolder) continue;
                if (localFiles.TryGetValue(file.Key, out crc))
                {
                    if (Convert.ToInt32(crc, 16) != Convert.ToInt32(file.Value, 16))
                    {
                        output.Add(file.Key, " 파일이 일치하지 않습니다.");
                        report += ($"ERROR: File {file.Key} not match = web({file.Value}) local({crc})") + System.Environment.NewLine;
                        excludedFiles.Add(file.Key, false);
                    }
                }
                else
                {
                    output.Add(file.Key, " 파일이 로컬 클라이언트에 존재하지 않습니다.");
                    report += ($"ERROR: File {file.Key} is not exist in local client") + System.Environment.NewLine;
                    excludedFiles.Add(file.Key, false);
                }
            }

            Trace.WriteLine("Writing report files... ");

            string date = "" + DateTime.Now;
            date = date.Replace(":", "");
            reportFileName = Directory.GetCurrentDirectory() + @"\IntegrityReport_" + date + ".txt";
            File.WriteAllText(reportFileName, report);

            Trace.WriteLine("Done");
            
            if (ProgressChanged != null)
                ProgressChanged(this, new ProgressChangedEventArgs(100, ""));

            string detailReport;
            if (output.Count == 0)
            {
                detailReport = "모든 파일이 정상입니다.";
            }
            else {
                detailReport = $"{output.Count}개의 파일이 다릅니다.\n\r\n\r";
                foreach (var item in output)
                {
                    detailReport += $"{item.Key}\r\n";
                }
            }
            Trace.WriteLine(detailReport);

            MessageBox.Show("유효성 검사를 완료했습니다. \r\n자세히 보기를 참조해주세요.");

            Trace.WriteLine("Exit");
        }

        private void MessageBoxClicked(object sender, EventArgs e) { 
        
        }
    }
}

