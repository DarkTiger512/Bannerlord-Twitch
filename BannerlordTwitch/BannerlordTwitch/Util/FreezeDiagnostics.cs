using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace BannerlordTwitch.Util
{
    // Observation only. No engine calls, game logger, dialogs or game queue on the writer thread.
    public static class FreezeDiagnostics
    {
        private static readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();
        private static readonly ConcurrentDictionary<long, string> active = new ConcurrentDictionary<long, string>();
        private static long pulse, nextId, dropped, shaderTicks, renderTicks;
        private static int started, queued, mainThread, shaderCount = -1, renderReady = -1, exceptions;
        private static string state = "startup", phase = "startup", folder;
        private static Thread worker;
        private static volatile bool stopping;
        private static long lastProbe;
        public static string SessionDirectory => folder;

        public static void Start(string outputRoot = null)
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            try
            {
                var process = Process.GetCurrentProcess();
                folder = Path.Combine(outputRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BLTRefreshed", "Diagnostics"), "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + process.Id + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "session.txt"), "BLT passive diagnostics v1\r\nrevision=2026-10-21-passive.1\r\nPID=" + process.Id
                    + "\r\nProcessStartUtc=" + process.StartTime.ToUniversalTime().ToString("O")
                    + "\r\nGameDirectory=" + AppDomain.CurrentDomain.BaseDirectory
                    + "\r\nBLTAssembly=" + typeof(FreezeDiagnostics).Assembly.FullName);
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(folder, "fatal.txt"), DateTime.UtcNow.ToString("O") + " terminating=" + e.IsTerminating + "\r\n" + FormatException(e.ExceptionObject as Exception));
                        // Only a terminating crash waits briefly for the external helper to acknowledge.
                        // Never blocks normal gameplay or a recoverable exception.
                        if (e.IsTerminating)
                        {
                            var deadline = Stopwatch.StartNew();
                            while (deadline.ElapsedMilliseconds < 2000 && !File.Exists(Path.Combine(folder, "fatal-ack.txt"))) Thread.Sleep(20);
                        }
                    }
                    catch { }
                };
                AppDomain.CurrentDomain.ProcessExit += (_, __) => { stopping = true; try { File.WriteAllText(Path.Combine(folder, "exit.txt"), DateTime.UtcNow.ToString("O")); } catch { } };
                worker = new Thread(WriteLoop) { IsBackground = true, Name = "BLT passive diagnostic writer", Priority = ThreadPriority.BelowNormal };
                worker.Start();
                var helper = Path.Combine(Path.GetDirectoryName(typeof(FreezeDiagnostics).Assembly.Location), "BLT.Diagnostics.exe");
                if (File.Exists(helper))
                    using (Process.Start(new ProcessStartInfo(helper, "watch " + process.Id + " " + process.StartTime.ToUniversalTime().Ticks + " \"" + folder + "\"")
                        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })) { }
                else Mark("HELPER_MISSING " + helper);
                process.Dispose();
            }
            catch (Exception ex) { Mark("DIAGNOSTIC_START_FAILED " + ex.GetType().Name + " " + ex.Message); }
        }

        public static void Pulse(string context)
        {
            Volatile.Write(ref state, context);
            Volatile.Write(ref mainThread, Thread.CurrentThread.ManagedThreadId);
            Interlocked.Exchange(ref pulse, DateTime.UtcNow.Ticks);
        }
        public static void Phase(string value) => Volatile.Write(ref phase, value);
        public static void ShaderSample(int value) { Volatile.Write(ref shaderCount, value); Interlocked.Exchange(ref shaderTicks, DateTime.UtcNow.Ticks); }
        public static void RenderSample(bool ready) { Volatile.Write(ref renderReady, ready ? 1 : 0); Interlocked.Exchange(ref renderTicks, DateTime.UtcNow.Ticks); }
        public static bool ProbeDue()
        {
            var now = Stopwatch.GetTimestamp();
            if (now - Interlocked.Read(ref lastProbe) < Stopwatch.Frequency) return false;
            Interlocked.Exchange(ref lastProbe, now); return true;
        }
        public static void Mark(string value)
        {
            if (Interlocked.Increment(ref queued) > 2048) { Interlocked.Decrement(ref queued); Interlocked.Increment(ref dropped); return; }
            events.Enqueue(DateTime.UtcNow.ToString("O") + " managedThread=" + Thread.CurrentThread.ManagedThreadId + " " + value);
        }
        public static IDisposable Trace(string operation)
        {
            var id = Interlocked.Increment(ref nextId);
            if (active.Count < 128) active[id] = DateTime.UtcNow.ToString("O") + " managedThread=" + Thread.CurrentThread.ManagedThreadId + " " + operation;
            Mark("ENTER " + id + " " + operation);
            return new Scope(id);
        }
        private sealed class Scope : IDisposable
        {
            private long id;
            public Scope(long value) { id = value; }
            public void Dispose() { var value = Interlocked.Exchange(ref id, 0); if (value == 0) return; active.TryRemove(value, out _); Mark("EXIT " + value); }
        }
        public static void ExceptionObserved(string origin, Exception exception)
        {
            if (exception == null || Interlocked.Increment(ref exceptions) > 32) return;
            try { Mark("EXCEPTION origin=" + origin + "\r\n" + FormatException(exception)); } catch { }
        }
        private static string FormatException(Exception exception)
        {
            if (exception == null) return "Exception object unavailable";
            var result = exception.ToString();
            for (var inner = exception; inner != null; inner = inner.InnerException)
            foreach (var frame in new StackTrace(inner, true).GetFrames() ?? new StackFrame[0])
            {
                var method = frame.GetMethod();
                if (method == null) continue;
                var original = method is MethodInfo mi ? Harmony.GetOriginalMethod(mi) : method;
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                result += "\r\nPATCHES " + method.DeclaringType?.FullName + "." + method.Name;
                foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
                    result += "\r\n owner=" + patch.owner + " method=" + patch.PatchMethod?.DeclaringType?.FullName + "." + patch.PatchMethod?.Name;
            }
            return result.Length > 65536 ? result.Substring(0, 65536) : result;
        }
        private static void WriteLoop()
        {
            StreamWriter writer = null;
            int part = 0, samples = 0;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    try { Mark("ASSEMBLY " + assembly.FullName + " path=" + (assembly.IsDynamic ? "dynamic" : assembly.Location)); } catch { }
                while (!stopping)
                {
                    try
                    {
                        if (writer == null) writer = new StreamWriter(new FileStream(Path.Combine(folder, "events-" + part + ".log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                        for (int i = 0; i < 2048 && events.TryDequeue(out var entry); i++)
                        {
                            Interlocked.Decrement(ref queued); writer.WriteLine(entry);
                            if (entry.Contains(" EXCEPTION origin="))
                            {
                                File.AppendAllText(Path.Combine(folder, "exceptions.txt"), entry + "\r\n");
                                File.WriteAllText(Path.Combine(folder, "exceptions.signal"), DateTime.UtcNow.ToString("O"));
                            }
                        }
                        var now = DateTime.UtcNow;
                        var heartbeat = Interlocked.Read(ref pulse);
                        var status = "utcTicks=" + now.Ticks + "\r\npulseTicks=" + heartbeat + "\r\nphase=" + Volatile.Read(ref phase)
                            + "\r\ncontext=" + Volatile.Read(ref state) + "\r\nmainManagedThread=" + Volatile.Read(ref mainThread)
                            + "\r\nshaders=" + Volatile.Read(ref shaderCount) + "\r\nshaderSampleTicks=" + Interlocked.Read(ref shaderTicks)
                            + "\r\nrenderReady=" + Volatile.Read(ref renderReady) + "\r\nrenderSampleTicks=" + Interlocked.Read(ref renderTicks)
                            + "\r\ndroppedEvents=" + Interlocked.Read(ref dropped);
                        File.WriteAllText(Path.Combine(folder, "heartbeat.next"), status);
                        var destination = Path.Combine(folder, "heartbeat.txt");
                        if (File.Exists(destination)) File.Replace(Path.Combine(folder, "heartbeat.next"), destination, null);
                        else File.Move(Path.Combine(folder, "heartbeat.next"), destination);
                        if (++samples % 10 == 0)
                        {
                            using (var process = Process.GetCurrentProcess()) writer.WriteLine(now.ToString("O") + " SAMPLE " + status.Replace("\r\n", " ")
                                + " privateBytes=" + process.PrivateMemorySize64 + " managedBytes=" + GC.GetTotalMemory(false)
                                + " cpuTicks=" + process.TotalProcessorTime.Ticks + " threads=" + process.Threads.Count
                                + " gc=" + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2));
                            foreach (var operation in active) writer.WriteLine("ACTIVE " + operation.Key + " " + operation.Value);
                        }
                        writer.Flush();
                        if (writer.BaseStream.Length > 4 * 1024 * 1024) { writer.Dispose(); writer = null; part = (part + 1) % 3; File.WriteAllText(Path.Combine(folder, "events-" + part + ".log"), ""); }
                    }
                    catch { try { writer?.Dispose(); } catch { } writer = null; }
                    Thread.Sleep(1000);
                }
            }
            catch { }
            finally { try { writer?.Dispose(); } catch { } }
        }
    }

    internal static class PassiveDiagnosticHooks
    {
        public static void Install()
        {
            var harmony = new Harmony("blt.passive-diagnostics");
            foreach (var name in new[] { "TaleWorlds.MountAndBlade.MissionState:TickLoading", "TaleWorlds.MountAndBlade.Mission:WaitTickCompletion",
                "TaleWorlds.MountAndBlade.Mission:OnPreTick", "TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.KingdomDecisionsVM:HandleDecision",
                "TaleWorlds.ScreenSystem.ScreenManager:Update" })
            {
                try
                {
                    var method = AccessTools.Method(name);
                    if (method == null) { FreezeDiagnostics.Mark("HOOK_UNAVAILABLE " + name); continue; }
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(PassiveDiagnosticHooks), nameof(Before)),
                        finalizer: new HarmonyMethod(typeof(PassiveDiagnosticHooks), nameof(After)) { priority = Priority.First });
                    FreezeDiagnostics.Mark("HOOK_INSTALLED " + name);
                }
                catch (Exception ex) { FreezeDiagnostics.Mark("HOOK_FAILED " + name + " " + ex.GetType().Name); }
            }
            try
            {
                var method = AccessTools.Method("TaleWorlds.Engine.SceneView:ReadyToRender");
                if (method != null && method.ReturnType == typeof(bool))
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(PassiveDiagnosticHooks), nameof(Before)), postfix: new HarmonyMethod(typeof(PassiveDiagnosticHooks), nameof(Rendered)));
                    FreezeDiagnostics.Mark("HOOK_INSTALLED SceneView.ReadyToRender");
                }
                else FreezeDiagnostics.Mark("HOOK_UNAVAILABLE SceneView.ReadyToRender");
            }
            catch (Exception ex) { FreezeDiagnostics.Mark("RENDER_HOOK_FAILED " + ex.GetType().Name); }
        }
        private static void Before(MethodBase __originalMethod) { FreezeDiagnostics.Phase(__originalMethod.DeclaringType?.Name + "." + __originalMethod.Name); }
        private static void After(MethodBase __originalMethod, Exception __exception)
        {
            FreezeDiagnostics.ExceptionObserved(__originalMethod.Name, __exception);
            FreezeDiagnostics.Phase(__originalMethod.DeclaringType?.Name + "." + __originalMethod.Name + ".returned");
        }
        private static void Rendered(bool __result) { FreezeDiagnostics.RenderSample(__result); FreezeDiagnostics.Phase("SceneView.ReadyToRender.returned"); }
    }
}
