using GersangStation.Modules;
using System.Diagnostics;
using System.IO;

namespace IntegrityCheckTester {

    [TestClass]
    public class TestFullClientInfo {
        [TestMethod]
        public void TestCreateOrGetChecker() {
            //Test with valid path
            IntegrityChecker? checker1 = IntegrityChecker.CreateOrGetIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + @"\Temp");
            Assert.IsNotNull(checker1);
            Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp");

            //Test with invalid path
            try {
                IntegrityChecker? checker2 = IntegrityChecker.CreateOrGetIntegrityChecker(Directory.GetCurrentDirectory() + @"\WrongPath", Directory.GetCurrentDirectory() + @"\Temp");
                Assert.IsNull(checker2);
            } catch(DirectoryNotFoundException) {
                //It should be go this this catch block
                Directory.Delete(Directory.GetCurrentDirectory() + @"\Temp");
            } catch(Exception ex) {
                Assert.Fail(ex.Message);
                throw;
            }
        }

        [TestMethod]
        public void TestGetFullClientFileList() {
            IntegrityChecker? checker1 = IntegrityChecker.CreateOrGetIntegrityChecker(Directory.GetCurrentDirectory() + @"\TestClient", Directory.GetCurrentDirectory() + @"\Temp", true);
            Assert.IsNotNull(checker1);

            checker1.GetFullClientFileList();
        }
    }
}