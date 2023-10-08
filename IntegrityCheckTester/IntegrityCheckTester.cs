using GersangStation.Modules;
using System.Diagnostics;
using System.IO;

namespace IntegrityCheckTester
{

    [TestClass]
    public class IntegrityCheckTester
    {
        [TestMethod]
        public void TestCreateOrGetChecker()
        {
            //Test with valid path
            try
            {
                IntegrityChecker? checker1 = IntegrityChecker.CreateIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + @"\Temp1");
                Assert.IsNotNull(checker1);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp1");
            }

            //Test with invalid path
            try
            {
                IntegrityChecker? checker2 = IntegrityChecker.CreateIntegrityChecker(Directory.GetCurrentDirectory() + @"\WrongPath", Directory.GetCurrentDirectory() + @"\Temp2");
                Assert.IsNull(checker2);
            }
            catch (DirectoryNotFoundException)
            {
                //It must go to this catch block
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
                throw;
            }
        }

        [TestMethod]
        public void TestGetFullClientFileListFromWeb()
        {

            try
            {
                IntegrityChecker? checker1 = IntegrityChecker.CreateIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + @"\Temp", true);
                Assert.IsNotNull(checker1);

                int versionInfo = 0;
                var fileList = checker1.GetFullClientFileListFromWeb(out versionInfo);
                Trace.WriteLine($"{fileList.Count} files detected.");
                foreach (var item in fileList)
                {
                    Trace.WriteLine($"Path = {item.Key}, CRC = {item.Value}");
                }

                Assert.AreNotEqual(0, fileList.Count);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp", true);
            }
        }

        [TestMethod]
        public void TestGetFullClientFileListFromLocal()
        {
            string TempPath = @"\TempLocal";
            try
            {
                IntegrityChecker? checker1 = IntegrityChecker.CreateIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + TempPath, true);
                Assert.IsNotNull(checker1);

                var result = checker1.GetFullClientFileListFromLocal(@"C:\AKInteractive\Gersang\");
                Trace.WriteLine($"{result.Count} files detected.");
                foreach (var item in result)
                {
                    Trace.WriteLine($"Path = {item.Key}, CRC32 = {item.Value}");
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Directory.Delete(Directory.GetCurrentDirectory() + TempPath, true);
            }
        }

        [TestMethod]
        public void TestAllFlow() {
            string TempPath = @"\TempFlow";
            try
            {
                IntegrityChecker? checker1 = IntegrityChecker.CreateIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + TempPath, true);
                Assert.IsNotNull(checker1);

                var localFiles = checker1.GetFullClientFileListFromLocal(@"C:\AKInteractive\Gersang\");
                Trace.WriteLine($"In local, {localFiles.Count} files detected.");

                int versionInfo = 0;
                var webFiles = checker1.GetFullClientFileListFromWeb(out versionInfo);
                Trace.WriteLine($"In web, {webFiles.Count} files detected.");

                Dictionary<string, bool> excludedFiles = new Dictionary<string, bool>();
                string[] lines = File.ReadAllLines(Directory.GetCurrentDirectory() + @"\Resources\IntegrityCheckExcludes.txt");
                foreach (var line in lines) {
                    excludedFiles.Add(line, true);
                }


                //Local to Web check
                Console.WriteLine("== Check report ==");
                foreach ( var file in localFiles)
                {
                    string? crc = null;
                    if (excludedFiles.ContainsKey(file.Key)) continue;
                    if (webFiles.TryGetValue(file.Key, out crc))
                    {
                        if (Convert.ToInt32(crc, 16) != Convert.ToInt32(file.Value, 16)) {
                            Console.WriteLine($"ERROR: File {file.Key} not match = web({crc}) local({file.Value})");
                        }
                        excludedFiles.Add(file.Key, false);
                    }
                    else {
                        Console.WriteLine($"WARNING: File {file.Key} is not exist in web full client");
                    }
                }

                //Web to Local
                foreach (var file in webFiles)
                {
                    string? crc = null;
                    if (excludedFiles.ContainsKey(file.Key)) continue;
                    if (localFiles.TryGetValue(file.Key, out crc))
                    {
                        if (Convert.ToInt32(crc, 16) != Convert.ToInt32(file.Value, 16))
                        {
                            Console.WriteLine($"ERROR: File {file.Key} not match = web({file.Value}) local({crc})");
                            excludedFiles.Add(file.Key, false);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: File {file.Key} is not exist in local client");
                        excludedFiles.Add(file.Key, false);
                    }
                }
            }
            catch (Exception) {
                throw;
            }
            finally
            {
                Directory.Delete(Directory.GetCurrentDirectory() + TempPath, true);
            }

        }

    }
}