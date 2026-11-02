using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// Standalone x64 .NET Framework helper. Never attaches a debugger or resumes/kills the game.
internal static class DiagnosticWatchdog
{
    internal const int StallSeconds = 180;
    internal const long StorageBudget = 32L * 1024 * 1024 * 1024;
    [DllImport("kernel32.dll")] static extern uint PssCaptureSnapshot(IntPtr process, uint flags, uint context, out IntPtr snapshot);
    [DllImport("kernel32.dll")] static extern uint PssFreeSnapshot(IntPtr process, IntPtr snapshot);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate bool DumpCallback(IntPtr param, IntPtr input, IntPtr output);
    [StructLayout(LayoutKind.Sequential)] struct CallbackInfo { public DumpCallback Callback; public IntPtr Param; }
    [DllImport("dbghelp.dll", SetLastError = true)] static extern bool MiniDumpWriteDump(IntPtr snapshot, uint pid,
        SafeFileHandle file, uint flags, IntPtr exception, IntPtr streams, ref CallbackInfo callback);
    [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", SetLastError = true)] static extern bool MiniDumpWriteDumpLive(IntPtr process, uint pid,
        SafeFileHandle file, uint flags, IntPtr exception, IntPtr streams, IntPtr callback);

    static void Note(string folder, string message) { try { File.AppendAllText(Path.Combine(folder, "watcher.log"), DateTime.UtcNow.ToString("O") + " " + message + "\r\n"); } catch { } }
    internal static bool ShouldCapture(long pulseTicks, long nowTicks, int captures)
        => pulseTicks > 0 && nowTicks >= pulseTicks && (nowTicks - pulseTicks) / TimeSpan.TicksPerSecond >= StallSeconds && captures < 2;
    internal static bool EnoughSpace(long privateBytes, long freeBytes, long usedBytes)
        => privateBytes >= 0 && privateBytes <= StorageBudget - 2L * 1024 * 1024 * 1024 && usedBytes >= 0
            && privateBytes + 2L * 1024 * 1024 * 1024 <= freeBytes
            && usedBytes <= StorageBudget - privateBytes - 2L * 1024 * 1024 * 1024;
    static long Number(Dictionary<string, string> status, string key) { long value; return status.ContainsKey(key) && long.TryParse(status[key], out value) ? value : 0; }
    static Dictionary<string, string> ReadStatus(string folder)
    {
        var result = new Dictionary<string, string>();
        try { foreach (var line in File.ReadAllLines(Path.Combine(folder, "heartbeat.txt"))) { var split = line.IndexOf('='); if (split > 0) result[line.Substring(0, split)] = line.Substring(split + 1); } } catch { }
        return result;
    }
    static bool SameProcess(Process process, long ticks) { try { return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == ticks; } catch { return false; } }

    public static int Main(string[] args)
    {
        try
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            if (args.Length == 4 && args[0] == "capture") return Capture(int.Parse(args[1]), long.Parse(args[2]), Path.GetFullPath(args[3]));
            if (args.Length != 4 || args[0] != "watch") return 2;
            var pid = int.Parse(args[1]); var start = long.Parse(args[2]); var folder = Path.GetFullPath(args[3]);
            using (var mutex = new Mutex(false, "Local\\BLT-Passive-Watcher-" + pid))
            {
                if (!mutex.WaitOne(0)) return 0;
                try { return Watch(pid, start, folder); } finally { mutex.ReleaseMutex(); }
            }
        }
        catch { return 1; }
    }

    static int Watch(int pid, long start, string folder)
    {
        using (var target = Process.GetProcessById(pid))
        {
            if (!SameProcess(target, start)) return 3;
            try { File.WriteAllText(Path.Combine(folder, "game-bin.txt"), Path.GetDirectoryName(target.MainModule.FileName)); } catch { }
            Note(folder, "WATCH started; heartbeat threshold=180s; max=2 snapshot dumps; no gameplay intervention");
            Inventory(target, folder, false);
            int captures = 0; bool collected = false, exceptionCollected = false;
            DateTime nextCapture = DateTime.MinValue, nextThreadSample = DateTime.MinValue;
            Process child = null; DateTime childStarted = DateTime.MinValue;
            while (SameProcess(target, start))
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if (child != null && (child.HasExited || (now - childStarted).TotalSeconds > 120))
                    {
                        if (!child.HasExited) { child.Kill(); Note(folder, "Capture helper exceeded 120s; stopped helper only; .partial file retained"); }
                        else Note(folder, "Capture child exit=" + child.ExitCode);
                        child.Dispose(); child = null;
                        CopyLogs(target, folder, pid);
                        Bundle(folder);
                        nextCapture = now.AddSeconds(20);
                    }
                    var status = ReadStatus(folder);
                    var pulse = Number(status, "pulseTicks");
                    if (pulse == 0 && (now.Ticks - start) / TimeSpan.TicksPerSecond >= 600) pulse = start;
                    bool fatal = File.Exists(Path.Combine(folder, "fatal.txt"));
                    bool stale = ShouldCapture(pulse, now.Ticks, captures);
                    if ((stale || fatal) && captures < 2 && child == null && now >= nextCapture)
                    {
                        Note(folder, "INCIDENT " + (fatal ? "terminating-managed-exception" : "stale-main-thread-heartbeat")
                            + " heartbeatAgeSeconds=" + (pulse == 0 ? -1 : (now.Ticks - pulse) / TimeSpan.TicksPerSecond)
                            + " shaderSampleAgeSeconds=" + (Number(status, "shaderSampleTicks") == 0 ? -1 : (now.Ticks - Number(status, "shaderSampleTicks")) / TimeSpan.TicksPerSecond));
                        File.WriteAllText(Path.Combine(folder, "incident-state-" + (captures + 1) + ".txt"), string.Join("\r\n", status.Select(p => p.Key + "=" + p.Value)));
                        var destination = Path.Combine(folder, "capture" + (++captures) + ".dmp");
                        child = Process.Start(new ProcessStartInfo(typeof(DiagnosticWatchdog).Assembly.Location,
                            "capture " + pid + " " + start + " \"" + destination + "\"")
                            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                        childStarted = now;
                        // The child acknowledges once a snapshot exists, before dumping its memory to disk.
                    }
                    if ((stale || fatal) && !collected)
                    {
                        collected = true;
                        CopyLogs(target, folder, pid);
                        Inventory(target, folder, true);
                    }
                    if (File.Exists(Path.Combine(folder, "exceptions.signal")) && !exceptionCollected)
                    { exceptionCollected = true; CopyLogs(target, folder, pid); }
                    if ((stale || fatal) && now >= nextThreadSample)
                    {
                        nextThreadSample = now.AddSeconds(10);
                        foreach (ProcessThread thread in target.Threads)
                        {
                            try { Note(folder, "THREAD id=" + thread.Id + " cpuTicks=" + thread.TotalProcessorTime.Ticks + " state=" + thread.ThreadState
                                + " wait=" + (thread.ThreadState == System.Diagnostics.ThreadState.Wait ? thread.WaitReason.ToString() : "none")); } catch { } finally { thread.Dispose(); }
                        }
                    }
                }
                catch (Exception ex) { Note(folder, "WATCH_ERROR " + ex.GetType().Name + " " + ex.Message); }
                Thread.Sleep(250);
            }
            if (child != null) { if (!child.WaitForExit(10000)) Note(folder, "Capture child continues after target exit"); child.Dispose(); }
            Note(folder, "PROCESS_EXIT observed; gracefulMarker=" + File.Exists(Path.Combine(folder, "exit.txt"))
                + "; fatalMarker=" + File.Exists(Path.Combine(folder, "fatal.txt")) + "; exit alone does not identify a native crash");
            // Native crashes may not run managed handlers. Preserve available logs after the process exits too.
            CopyLogs(null, folder, pid);
            if (collected || exceptionCollected || File.Exists(Path.Combine(folder, "fatal.txt")) || !File.Exists(Path.Combine(folder, "exit.txt"))) Bundle(folder);
            else Note(folder, "No detected incident; bounded session log retained");
        }
        return 0;
    }

    static int Capture(int pid, long start, string destination)
    {
        var folder = Path.GetDirectoryName(destination);
        IntPtr snapshot = IntPtr.Zero;
        try
        {
            using (var timeout = new Timer(_ => Environment.Exit(4), null, 110000, Timeout.Infinite))
            using (var target = Process.GetProcessById(pid))
            {
                if (!SameProcess(target, start)) return 3;
                long used = Directory.EnumerateFiles(Directory.GetParent(folder).FullName, "*.dmp*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length);
                var free = new DriveInfo(Path.GetPathRoot(destination)).AvailableFreeSpace;
                if (!EnoughSpace(target.PrivateMemorySize64, free, used))
                { Note(folder, "Full snapshot skipped: insufficient disk headroom or 32GiB budget; trying small stack capture"); return CaptureStacks(target, destination, free, used); }
                // Clone the process address space. Dump I/O runs against the snapshot, not the live game.
                uint error = PssCaptureSnapshot(target.Handle, 0x00000985, 0x0010001F, out snapshot);
                if (error != 0) { Note(folder, "PssCaptureSnapshot error=" + error + "; trying small stack capture"); return CaptureStacks(target, destination, free, used); }
                File.WriteAllText(Path.Combine(folder, "fatal-ack.txt"), "snapshot created " + DateTime.UtcNow.ToString("O"));
                Note(folder, "SNAPSHOT_CREATED file=" + Path.GetFileName(destination));
                var callback = new CallbackInfo { Callback = (param, input, output) =>
                    { if (Marshal.ReadInt32(input, 12) == 16) Marshal.WriteInt32(output, 1); return true; } };
                using (var file = new FileStream(destination + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    // Full memory + thread/unloaded-module/region metadata. Snapshot handle via callback type 16.
                    if (!MiniDumpWriteDump(snapshot, (uint)pid, file.SafeFileHandle, 0x00001926, IntPtr.Zero, IntPtr.Zero, ref callback))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                File.Move(destination + ".partial", destination);
                Note(folder, "DUMP_SUCCESS file=" + Path.GetFileName(destination) + " bytes=" + new FileInfo(destination).Length);
                return 0;
            }
        }
        catch (Exception ex) { Note(folder, "DUMP_FAILED " + ex); return 1; }
        finally { if (snapshot != IntPtr.Zero) PssFreeSnapshot(GetCurrentProcess(), snapshot); }
    }

    static int CaptureStacks(Process target, string destination, long free, long used)
    {
        var folder = Path.GetDirectoryName(destination);
        if (free < 512L * 1024 * 1024 || used >= StorageBudget - 256L * 1024 * 1024)
        { Note(folder, "DUMP_SKIPPED stack capture also exceeds storage/headroom limit"); return 5; }
        using (var file = new FileStream(destination + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            if (!MiniDumpWriteDumpLive(target.Handle, (uint)target.Id, file.SafeFileHandle, 0x1920, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        File.Move(destination + ".partial", destination);
        File.WriteAllText(Path.Combine(folder, "fatal-ack.txt"), "stack capture saved " + DateTime.UtcNow.ToString("O"));
        Note(folder, "STACK_ONLY_DUMP_SUCCESS file=" + Path.GetFileName(destination) + "; full memory unavailable; game not explicitly suspended");
        return 0;
    }

    static void Inventory(Process target, string folder, bool copyBinaries)
    {
        try
        {
            using (var writer = new StreamWriter(Path.Combine(folder, copyBinaries ? "incident-modules.txt" : "modules.txt")))
            foreach (ProcessModule module in target.Modules)
            {
                try
                {
                    var path = module.FileName;
                    writer.WriteLine(module.ModuleName + " base=0x" + module.BaseAddress.ToInt64().ToString("x") + " size=" + module.ModuleMemorySize
                        + " version=" + module.FileVersionInfo.FileVersion + " path=" + path);
                    bool relevant = Regex.IsMatch(module.ModuleName, "^(BLT.*|BannerlordTwitch|TAOM|TaleWorlds\\.(Native|MountAndBlade|CampaignSystem|CampaignSystem\\.ViewModelCollection)|0Harmony)\\.dll$", RegexOptions.IgnoreCase);
                    if (!relevant) continue;
                    using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) writer.WriteLine(" SHA256=" + BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""));
                    if (copyBinaries)
                    {
                        var destination = Path.Combine(folder, "binaries"); Directory.CreateDirectory(destination);
                        File.Copy(path, Path.Combine(destination, module.ModuleName), true);
                        var pdb = Path.ChangeExtension(path, ".pdb");
                        if (File.Exists(pdb) && (module.ModuleName.StartsWith("BLT", StringComparison.OrdinalIgnoreCase) || module.ModuleName == "BannerlordTwitch.dll")) File.Copy(pdb, Path.Combine(destination, Path.GetFileName(pdb)), true);
                    }
                }
                catch (Exception ex) { writer.WriteLine("MODULE_ERROR " + ex.GetType().Name); }
                finally { module.Dispose(); }
            }
        }
        catch (Exception ex) { Note(folder, "INVENTORY_FAILED " + ex.GetType().Name); }
    }
    static void CopyLogs(Process target, string folder, int pid)
    {
        var destination = Path.Combine(folder, "logs"); Directory.CreateDirectory(destination);
        var roots = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord", "logs") };
        if (target != null)
        {
            try
            {
                var bin = Path.GetDirectoryName(target.MainModule.FileName);
                File.WriteAllText(Path.Combine(folder, "game-bin.txt"), bin);
            }
            catch { }
        }
        try { roots.Add(Path.Combine(File.ReadAllText(Path.Combine(folder, "game-bin.txt")), "Logs")); } catch { }
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var path in Directory.EnumerateFiles(root).Where(p => Regex.IsMatch(Path.GetFileName(p), "^(rgl_log(_errors)?_" + pid + "\\.txt|taom_debug_.*\\.log)$", RegexOptions.IgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(4))
            {
                try
                {
                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        input.Position = Math.Max(0, input.Length - 16 * 1024 * 1024);
                        using (var reader = new StreamReader(input))
                        {
                            var text = reader.ReadToEnd();
                            text = Regex.Replace(text, @"(?i)(oauth:|bearer\s+)[a-z0-9_\-\.]+", "$1[REDACTED]");
                            text = Regex.Replace(text, @"(?im)((?:access_token|refresh_token|client_secret|authorization)\s*[:=]\s*[""']?)[^\s,""'}]+", "$1[REDACTED]");
                            File.WriteAllText(Path.Combine(destination, Path.GetFileName(path)), text);
                        }
                    }
                }
                catch (Exception ex) { Note(folder, "LOG_COPY_FAILED " + Path.GetFileName(path) + " " + ex.GetType().Name); }
            }
        }
    }
    static void Bundle(string folder)
    {
        try
        {
            // Full dumps stay separate: avoid compressing tens of gigabytes while the game is running.
            var destination = Path.Combine(folder, "BLT-incident.zip");
            var temporary = destination + ".partial";
            if (File.Exists(temporary)) File.Delete(temporary);
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) || file == destination) continue;
                // The game keeps its ring log open; CreateEntryFromFile's sharing mode conflicts with that writer.
                var entry = zip.CreateEntry(file.Substring(folder.Length + 1), CompressionLevel.Fastest);
                using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var output = entry.Open()) input.CopyTo(output);
            }
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
            Note(folder, "BUNDLE_READY BLT-incident.zip; full dumps are adjacent files, not inside ZIP");
        }
        catch (Exception ex) { Note(folder, "BUNDLE_FAILED " + ex.GetType().Name); }
    }
}
