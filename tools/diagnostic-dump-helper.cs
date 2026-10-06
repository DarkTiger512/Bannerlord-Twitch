using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// Run out-of-process; never suspends or terminates the game explicitly.
internal static class DumpCapture
{
    [DllImport("Dbghelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(IntPtr process, uint pid, SafeFileHandle file,
        uint type, IntPtr exception, IntPtr streams, IntPtr callback);

    public static int Main(string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[0], out var pid)) return 2;
        string destination = Path.GetFullPath(args[1]);
        string status = destination + ".status.txt";
        try
        {
            using (var mutex = new Mutex(false, "Local\\BLT-Diagnostic-Dump-" + pid))
            {
                if (!mutex.WaitOne(0)) { File.WriteAllText(status, "Skipped: another capture is active."); return 3; }
                try
                {
                    // Bound capture duration; leave a clearly named partial file on timeout.
                    using (var timeout = new Timer(_ => Environment.Exit(4), null, 60000, Timeout.Infinite))
                    using (var target = Process.GetProcessById(pid))
                    {
                        File.WriteAllText(status, DateTime.UtcNow.ToString("O") + " START pid=" + pid);
                        if (new DriveInfo(Path.GetPathRoot(destination)).AvailableFreeSpace < 1024L * 1024 * 1024)
                            throw new IOException("Less than 1 GiB free; capture skipped.");
                        using (var file = new FileStream(destination + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            // Stack memory, thread info, unloaded modules, process/thread data,
                            // and memory-region metadata. Deliberately excludes full process memory.
                            if (!MiniDumpWriteDump(target.Handle, (uint)pid, file.SafeFileHandle,
                                0x1920, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        }
                        File.Move(destination + ".partial", destination);
                        File.AppendAllText(status, "\r\n" + DateTime.UtcNow.ToString("O") + " SUCCESS bytes=" + new FileInfo(destination).Length);
                    }
                    return 0;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(status, "\r\nFAILED " + ex); } catch { }
            return 1;
        }
    }
}
