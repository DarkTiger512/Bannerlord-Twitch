using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace BannerlordTwitch.Util
{
    // Diagnostic build only. The worker never reads engine objects or calls the game logger.
    public static class FreezeDiagnostics
    {
        private static readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();
        private static readonly ConcurrentDictionary<long, string> active = new ConcurrentDictionary<long, string>();
        private static long heartbeat, nextId, dropped;
        private static int started, queued, writing;
        private static string context = "startup; no application tick yet";
        private static string directory, stem;
        private static Timer timer;
        private static StreamWriter writer;
        private static int part;
        private static long previousCpu, previousSample;
        private static readonly long frequency = Stopwatch.Frequency;

        public static void Start(string outputDirectory = null)
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            try
            {
                directory = outputDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BLTRefreshed", "Diagnostics");
                Directory.CreateDirectory(directory);
                stem = "freeze-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Process.GetCurrentProcess().Id;
                heartbeat = Stopwatch.GetTimestamp();
                previousSample = heartbeat;
                using (var process = Process.GetCurrentProcess()) previousCpu = process.TotalProcessorTime.Ticks;
                Mark("START build=5.5.7-diagnostics.1 pid=" + Process.GetCurrentProcess().Id + " logicalProcessors=" + Environment.ProcessorCount);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                    a.GetName().Name.StartsWith("TAOM") || a.GetName().Name.StartsWith("BLT") ||
                    a.GetName().Name == "0Harmony" || a.GetName().Name == "BannerlordTwitch"))
                    Mark("ASSEMBLY " + assembly.FullName + " location=" + (assembly.IsDynamic ? "dynamic" : assembly.Location));
                timer = new Timer(_ => Sample(), null, 0, 1000);
            }
            catch { /* Diagnostics must never interrupt the game. */ }
        }

        public static void Pulse(string state)
        {
            Volatile.Write(ref context, state);
            Interlocked.Exchange(ref heartbeat, Stopwatch.GetTimestamp());
        }

        public static void Mark(string message)
        {
            if (Interlocked.Increment(ref queued) > 8192)
            {
                Interlocked.Decrement(ref queued);
                Interlocked.Increment(ref dropped);
                return;
            }
            events.Enqueue(DateTime.UtcNow.ToString("O") + " thread=" + Thread.CurrentThread.ManagedThreadId + " " + message);
        }

        public static IDisposable Trace(string operation)
        {
            var id = Interlocked.Increment(ref nextId);
            if (active.Count < 1024) active[id] = DateTime.UtcNow.ToString("O") + " thread=" + Thread.CurrentThread.ManagedThreadId + " " + operation;
            Mark("ENTER " + id + " " + operation);
            return new Scope(id);
        }
        private sealed class Scope : IDisposable
        {
            private long id;
            public Scope(long value) { id = value; }
            public void Dispose()
            {
                var value = Interlocked.Exchange(ref id, 0);
                if (value == 0) return;
                active.TryRemove(value, out _);
                Mark("EXIT " + value);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalCommitLimit, AvailableCommit, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        private static void Sample()
        {
            if (Interlocked.Exchange(ref writing, 1) != 0) return;
            try
            {
                if (writer == null)
                {
                    writer = new StreamWriter(new FileStream(Path.Combine(directory, stem + "-" + part + ".log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                }
                for (var n = 0; n < 8192 && events.TryDequeue(out var line); n++)
                {
                    Interlocked.Decrement(ref queued);
                    writer.WriteLine(line);
                }
                var now = Stopwatch.GetTimestamp();
                var age = (now - Interlocked.Read(ref heartbeat)) / (double)frequency;
                using (var process = Process.GetCurrentProcess())
                {
                    var cpu = process.TotalProcessorTime.Ticks;
                    var seconds = (now - previousSample) / (double)frequency;
                    var cpuPercent = seconds <= 0 ? 0 : (cpu - previousCpu) / (double)TimeSpan.TicksPerSecond / seconds / Environment.ProcessorCount * 100;
                    previousCpu = cpu; previousSample = now;
                    var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
                    var memoryOk = GlobalMemoryStatusEx(ref memory);
                    writer.WriteLine(DateTime.UtcNow.ToString("O") + " SAMPLE heartbeatAgeSec=" + age.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + " stalled=" + (age >= 10) + " context=" + Volatile.Read(ref context)
                        + " workingSetBytes=" + process.WorkingSet64 + " privateBytes=" + process.PrivateMemorySize64
                        + " managedBytes=" + GC.GetTotalMemory(false) + " processCpuPercent=" + cpuPercent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + " physicalMemoryLoadPercent=" + (memoryOk ? memory.Load.ToString() : "unavailable")
                        + " availablePhysicalBytes=" + memory.AvailablePhysical + " totalPhysicalBytes=" + memory.TotalPhysical
                        + " availableCommitBytes=" + memory.AvailableCommit + " commitLimitBytes=" + memory.TotalCommitLimit
                        + " droppedEvents=" + Interlocked.Read(ref dropped));
                }
                if (age >= 10)
                    foreach (var operation in active.OrderBy(p => p.Key)) writer.WriteLine("ACTIVE " + operation.Key + " " + operation.Value);
                writer.Flush();
                if (writer.BaseStream.Length >= 16 * 1024 * 1024)
                {
                    writer.Dispose(); writer = null; part = (part + 1) % 3;
                    // Only rotate this process's own diagnostic files; never touch other sessions.
                    using (File.Create(Path.Combine(directory, stem + "-" + part + ".log"))) { }
                }
            }
            catch { try { writer?.Dispose(); } catch { } writer = null; }
            finally { Volatile.Write(ref writing, 0); }
        }
    }
}
