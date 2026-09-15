using System.Diagnostics;
using System.Reflection;

namespace GersangStation.Modules;

/// <summary>Best-effort maintenance of the updater shipped inside this application.</summary>
internal static class MiniUpdaterMaintenance {
    private const string UpdaterName = "GersangStationMiniUpdator";
    private static readonly string[] PayloadFiles = [
        UpdaterName + ".exe", UpdaterName + ".dll",
        UpdaterName + ".deps.json", UpdaterName + ".runtimeconfig.json"
    ];
    private static readonly object LaunchGate = new();
    private static int started;
    private static bool updaterLaunched;

    internal static void Start() {
        if(Interlocked.Exchange(ref started, 1) != 0) return;

        // Thread-pool work does not keep the application alive. Never await it at shutdown.
        var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Application.ApplicationExit += (_, _) => lifetime.Cancel();
        _ = Task.Run(() => MaintainAsync(Application.StartupPath, lifetime.Token));
    }

    internal static void StartUpdater(ProcessStartInfo startInfo) {
        lock(LaunchGate) {
            // Only the short directory commit shares this gate with the UI launch path.
            // Once an update has been requested, defer maintenance until the next app launch.
            updaterLaunched = true;
            using Process? process = Process.Start(startInfo);
        }
    }

    internal static async Task MaintainAsync(string appDirectory, CancellationToken cancellationToken) {
        string target = Path.Combine(appDirectory, "Updator");
        string pending = Path.Combine(appDirectory, ".Updator.pending");
        string backup = Path.Combine(appDirectory, ".Updator.previous");
        // Also serialize maintenance across processes using the same installation.
        FileStream? lease = null;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            lease = new FileStream(Path.Combine(appDirectory, ".Updator.maintenance.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);

            lock(LaunchGate) {
                if(updaterLaunched) return;
                RecoverInterruptedCommit(target, backup);
            }
            DeleteOwnedDirectory(backup);
            DeleteOwnedDirectory(pending);
            Directory.CreateDirectory(pending);
            Assembly assembly = typeof(MiniUpdaterMaintenance).Assembly;
            foreach(string file in PayloadFiles) {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream source = assembly.GetManifestResourceStream("MiniUpdater." + file)
                    ?? throw new InvalidDataException("Missing embedded updater payload: " + file);
                using var destination = File.Create(Path.Combine(pending, file));
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            Version available = ReadVersion(Path.Combine(pending, UpdaterName + ".exe"));
            if(!NeedsUpdate(target, available)) return;

            while(true) {
                cancellationToken.ThrowIfCancellationRequested();
                if(Volatile.Read(ref updaterLaunched)) return;
                if(!IsUpdaterRunning(target) && CanOpenPayloadExclusively(target)) break;
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }

            // Preserve any additional files in the updater directory, without following links.
            if(Directory.Exists(target)) {
                CopyAdditionalFiles(target, pending, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            lock(LaunchGate) {
                if(updaterLaunched || cancellationToken.IsCancellationRequested) return;
                // Recheck after staging; another process may have started the updater meanwhile.
                if(IsUpdaterRunning(target) || !CanOpenPayloadExclusively(target)) return;
                if(!NeedsUpdate(target, available)) return;
                Commit(target, pending, backup);
            }
            DeleteOwnedDirectory(backup);
        } catch(OperationCanceledException) {
            // A busy updater or app exit is normal. Try again on the next startup.
        } catch(Exception ex) {
            // This optional task must not enter the application's UI/global error handlers.
            Trace.WriteLine($"Mini updater maintenance skipped: {ex}");
        } finally {
            if(lease != null) {
                try { DeleteOwnedDirectory(pending); }
                catch(Exception ex) { Trace.WriteLine($"Mini updater staging cleanup skipped: {ex}"); }
                try { lease.Dispose(); }
                catch(Exception ex) { Trace.WriteLine($"Mini updater maintenance lock cleanup skipped: {ex}"); }
            }
        }
    }

    private static Version ReadVersion(string path) {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }

    private static bool NeedsUpdate(string target, Version available) {
        string exe = Path.Combine(target, UpdaterName + ".exe");
        if(!File.Exists(exe)) return true;
        Version installed = ReadVersion(exe);
        if(installed > available) return false;
        return installed < available || PayloadFiles.Any(file => !File.Exists(Path.Combine(target, file)));
    }

    private static bool IsUpdaterRunning(string target) {
        string expected = Path.GetFullPath(Path.Combine(target, UpdaterName + ".exe"));
        Process[] processes = Process.GetProcessesByName(UpdaterName);
        try {
            foreach(Process process in processes) {
                try {
                    if(process.HasExited) continue;
                    string? path = process.MainModule?.FileName;
                    if(path == null || string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase))
                        return true;
                } catch(InvalidOperationException) {
                    // Process exited between enumeration and inspection.
                } catch(System.ComponentModel.Win32Exception) {
                    // Do not replace files when the identity of a running updater is unknown.
                    return true;
                }
            }
            return false;
        } finally {
            foreach(Process process in processes) process.Dispose();
        }
    }

    private static bool CanOpenPayloadExclusively(string target) {
        try {
            foreach(string file in PayloadFiles) {
                string path = Path.Combine(target, file);
                if(File.Exists(path)) {
                    using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
            }
            return true;
        } catch(IOException) {
            return false;
        }
    }

    private static void CopyAdditionalFiles(string source, string destination, CancellationToken token) {
        EnsureNotLinked(source);
        foreach(string file in Directory.EnumerateFiles(source)) {
            token.ThrowIfCancellationRequested();
            EnsureNotLinked(file);
            string targetFile = Path.Combine(destination, Path.GetFileName(file));
            if(!File.Exists(targetFile)) File.Copy(file, targetFile);
        }
        foreach(string directory in Directory.EnumerateDirectories(source)) {
            token.ThrowIfCancellationRequested();
            string targetDirectory = Path.Combine(destination, Path.GetFileName(directory));
            Directory.CreateDirectory(targetDirectory);
            CopyAdditionalFiles(directory, targetDirectory, token);
        }
    }

    private static void RecoverInterruptedCommit(string target, string backup) {
        if(!Directory.Exists(backup)) return;
        EnsureNotLinked(backup);
        if(!Directory.Exists(target)) Directory.Move(backup, target);
        else if(!PayloadFiles.All(file => File.Exists(Path.Combine(target, file))))
            throw new IOException("Incomplete updater installation; preserving its backup.");
    }

    private static void Commit(string target, string pending, string backup) {
        if(Directory.Exists(target)) {
            EnsureNotLinked(target);
            Directory.Move(target, backup);
        }
        try {
            Directory.Move(pending, target);
        } catch {
            if(!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
    }

    private static void EnsureNotLinked(string path) {
        if((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Updater maintenance does not follow filesystem links.");
    }

    private static void DeleteOwnedDirectory(string directory) {
        if(!Directory.Exists(directory)) return;
        EnsureNotLinked(directory);
        foreach(string child in Directory.EnumerateDirectories(directory)) DeleteOwnedDirectory(child);
        foreach(string file in Directory.EnumerateFiles(directory)) {
            EnsureNotLinked(file);
            File.Delete(file);
        }
        Directory.Delete(directory);
    }
}
