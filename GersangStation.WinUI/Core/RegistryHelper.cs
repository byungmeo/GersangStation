using Core.Models;
using Microsoft.Win32;
using System.Diagnostics;

namespace Core
{
    public static class RegistryHelper
    {
        public static string? GetInstallPathFromRegistry(GameServer gameServerContext)
        {
            try
            {
                using RegistryKey? k = Registry.CurrentUser.OpenSubKey(@"Software\JOYON\Gersang\Korean", false);
                string? installPath = k?.GetValue(GameServerHelper.GetInstallPathRegKey(gameServerContext))?.ToString();
                k?.Close();
                return installPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static string? GetGersangStarterPathFromRegistry()
        {
            try
            {
                using RegistryKey? k = Registry.ClassesRoot.OpenSubKey(@"Gersang\shell\open\command", false);
                string? wrappedPath = k?.GetValue("")?.ToString();
                k?.Close();
                if (string.IsNullOrWhiteSpace(wrappedPath)) return null;
                string exePath = ExtractPathFromWrapped(wrappedPath);
                if (string.IsNullOrWhiteSpace(exePath)) return null;
                return exePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static void SetInstallPathToRegistry(GameServer gameServerContext, string installPath)
        {
            RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\JOYON\Gersang\Korean", RegistryKeyPermissionCheck.ReadWriteSubTree);
            registryKey?.SetValue(GameServerHelper.GetInstallPathRegKey(gameServerContext), installPath);
            registryKey?.Close();
        }

        // " 로 감싸져 있는 경로를 추출
        private static string ExtractPathFromWrapped(string wrappedPath)
        {
            wrappedPath = wrappedPath.Trim();
            if (wrappedPath.Length == 0) return "";

            if (wrappedPath[0] == '"')
            {
                int end = wrappedPath.IndexOf('"', 1);
                return end > 1 ? wrappedPath.Substring(1, end - 1) : "";
            }

            int space = wrappedPath.IndexOf(' ');
            return space > 0 ? wrappedPath.Substring(0, space) : wrappedPath;
        }
    }
}
