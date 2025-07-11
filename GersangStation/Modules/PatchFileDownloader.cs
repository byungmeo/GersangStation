﻿using System.Diagnostics;
using System.IO.Compression;
using System.Net;

namespace GersangStation.Modules;

internal class PatchFileDownloader {
    private const int NUM_RETRY = 15; // 모든 파일 다운로드 실패 시 다운로드 재시도 최대 횟수
    Dictionary<string, string>? list_retry = new();

    public bool DownloadAll(Dictionary<string, string> list, bool quiet) {
        // 리스트의 모든 파일을 다운로드
        Parallel.ForEach(list, new ParallelOptions { MaxDegreeOfParallelism = 10 }, DownloadFile);

        // 아직도 모든 파일을 다운로드 하지 못한 경우
        if(list_retry.Count > 0) {
            foreach(var item in list_retry) Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);

            Trace.WriteLine("다운로드 실패한 파일을 재다운로드 합니다.");

            // NUM_RETRY 만큼 다운로드 재시도
            for(int i = 0; i < NUM_RETRY; i++) {
                if(list_retry.Count == 0) break;
                Trace.WriteLine(i + 1 + "번째 재다운로드 시도... 남은 파일 수 : " + list_retry.Count + "개");
                Parallel.ForEach(list_retry, new ParallelOptions { MaxDegreeOfParallelism = 10 }, RetryDownloadFile);
            }

            // 그럼에도 실패
            if(list_retry.Count > 0) {
                Trace.WriteLine($"{NUM_RETRY}번의 재다운로드 시도 결과 모든 파일을 재다운로드 하는데 실패하였습니다.");
                foreach(var item in list_retry) Trace.WriteLine("다운로드 실패한 파일 주소 : " + item.Key);
                if(quiet != true) MessageBox.Show($"{NUM_RETRY}번의 패치 재시도에도 불구하고\n모든 파일을 다운로드하는데 실패하였습니다.\n인터넷 환경을 확인해주시고 다시 패치를 진행해주세요.", "패치 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                list_retry.Clear();
                return false;
            } else Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");
        } else Trace.WriteLine("모든 파일을 성공적으로 다운로드 하였습니다.");

        Trace.WriteLine("다운로드 종료");
        list_retry.Clear();
        return true;
    }

    private void DownloadFile(KeyValuePair<string, string> item) {
        using (var client = new WebClient()) {
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

            string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

            //내부 폴더 생성
            DirectoryInfo fileInnerDirectory = new DirectoryInfo(new FileInfo(item.Value).DirectoryName);
            if(!fileInnerDirectory.Exists) fileInnerDirectory.Create();

            Trace.WriteLine($"[다운로드 시작] {file_name}");

            try {
                client.DownloadFile(item.Key, item.Value);
                Trace.WriteLine($"[다운로드 성공] {file_name}");
            } catch(WebException e)
            {
                Trace.WriteLine($"[다운로드 실패] {file_name}\n" + e.StackTrace);
                list_retry.Add(item.Key, item.Value);
            }
        }
    }

    private void RetryDownloadFile(KeyValuePair<string, string> item) {
        using(var client = new WebClient()) {
            client.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 8.0)");

            string file_name = item.Value.Substring(item.Value.LastIndexOf('\\') + 1);

            //내부 폴더 생성
            DirectoryInfo fileInnerDirectory = new(path: new FileInfo(item.Value).DirectoryName);
            if(!fileInnerDirectory.Exists) { fileInnerDirectory.Create(); }

            Trace.WriteLine($"[재다운로드 시작] {file_name}");

            try {
                client.DownloadFile(item.Key, item.Value);
                Trace.WriteLine($"[재다운로드 성공] {file_name}");
                list_retry.Remove(item.Key);
            } catch(WebException e) {
                Trace.WriteLine($"[재다운로드 실패] {file_name}\n" + e.StackTrace);
            }
        }
    }

    public void ExtractAll(string patch_dir, string main_dir, string client2_dir, string client3_dir, bool withClients) {
        foreach(string file in Directory.EnumerateFiles(patch_dir, "*.*", SearchOption.AllDirectories)) {
            string dest1 = main_dir + file.Remove(0, patch_dir.Length);
            dest1 = dest1.Remove(dest1.LastIndexOf('\\'));
            Trace.WriteLine(file + " -> " + dest1);
            try {
                ZipFile.ExtractToDirectory(file, dest1, true);
            } catch(Exception ex) {
                Logger.Log("압축 오류 발생 : " + file, ex);
            }

            if(withClients) {
                if(client2_dir != "") {
                    string dest2 = client2_dir + file.Remove(0, patch_dir.Length);
                    dest2 = dest2.Remove(dest2.LastIndexOf('\\'));
                    Trace.WriteLine(file + " -> " + dest2);
                    try {
                        ZipFile.ExtractToDirectory(file, dest2, true);
                    } catch(Exception ex) {
                        Logger.Log("압축 오류 발생 : " + file, ex);
                    }
                }

                if(client3_dir != "") {
                    string dest3 = client3_dir + file.Remove(0, patch_dir.Length);
                    dest3 = dest3.Remove(dest3.LastIndexOf('\\'));
                    Trace.WriteLine(file + " -> " + dest3);
                    try {
                        ZipFile.ExtractToDirectory(file, dest3, true);
                    } catch(Exception ex) {
                        Logger.Log("압축 오류 발생 : " + file, ex);
                    }
                }
            }
        }
    }
}
