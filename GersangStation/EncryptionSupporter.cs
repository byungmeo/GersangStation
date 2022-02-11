using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GersangStation {
    public class EncryptionSupporter {
        public static string Protect(string origin) {
            string PasswordProtect = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(origin), null, DataProtectionScope.CurrentUser));
            return PasswordProtect;
        }
        public static string Unprotect(string origin) {
            if (origin == "") {
                return "";
            }

            var PasswordUnprotect = "";

            try {
                PasswordUnprotect = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(origin), null, DataProtectionScope.CurrentUser));
            } catch (CryptographicException e) {
                throw e;
            } catch (Exception e) {
                Trace.WriteLine(e.Message);
                MessageBox.Show("패스워드를 복호화 하던 도중 예외가 발생하였습니다.\n관리자에게 문의해주세요.", "패스워드 복호화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return PasswordUnprotect;
        }
    }
}
