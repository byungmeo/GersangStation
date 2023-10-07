using Microsoft.VisualBasic.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace GersangStation.Modules {
    internal struct FullClientInfo {
        public static string Url = "http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z";
        public static uint HeaderSize = 32; //32 bytes

        public FullClientInfo() {}
        public static string CreateRange(uint start, uint end) {
            return $"bytes={start}-{end}";
        }
    }

    public class IntegrityChecker {
        private string _ClientPath = string.Empty;
        private string _TempPath = string.Empty;
        private bool _verbose = false;

        private IntegrityChecker(string clientPath, string tempPath, bool verbose ) {
            _ClientPath = clientPath;
            _TempPath = tempPath;
            _verbose = verbose;
        }

        public static IntegrityChecker? CreateOrGetIntegrityChecker(string clientPath, string tempPath, bool verbose = false) {
            if(Directory.Exists(clientPath) == false) return null;
            try {
                if(Directory.Exists(tempPath) == false)
                    Directory.CreateDirectory(tempPath);
            } catch(Exception e) {
                Trace.WriteLine("IntegrityCheck: Failed to create temp directory");
                Trace.Write(e);
                throw;
            }

            return new IntegrityChecker(clientPath, tempPath, verbose);
        }

        public void GetFullClientFileList() {
            if(_verbose)
                Trace.WriteLine("Trying to get header information... ");
            
            string fullSizeOfClient = string.Empty;
            try {
                FullClientInfo info = new FullClientInfo();
                using(HttpClient client = new HttpClient()) {
                    //Make request 
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, FullClientInfo.Url);
                    requestMessage.Headers.Add("Range", FullClientInfo.CreateRange(0, 100)); //Get 100 bytes from server first
                    if(_verbose) {
                        Trace.WriteLine("Created request message");
                        Trace.WriteLine(requestMessage);
                    }

                    using HttpResponseMessage response = client.Send(requestMessage);
                    HttpContentHeaders contentHeaders = response.Content.Headers;
                    if(_verbose) {
                        Trace.WriteLine("Response from server");
                        Trace.WriteLine(contentHeaders);
                    }
                    if(response.StatusCode != HttpStatusCode.PartialContent) {
                        Trace.WriteLine("Failed");
                        throw new HttpRequestException();
                    }

                    string contentRange = contentHeaders.GetValues("Content-Range").First();
                    fullSizeOfClient = contentRange.Split("/")[1];
                    if(_verbose) {
                        Trace.WriteLine($"Size of Full Client = {fullSizeOfClient}");
                    }
                }
            } catch(Exception) {
                throw;
            }
            if(_verbose) {
                Trace.WriteLine($"succeeded. file size = {fullSizeOfClient}");
            }
        }
    }
}
