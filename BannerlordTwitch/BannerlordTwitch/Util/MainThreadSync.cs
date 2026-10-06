using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BannerlordTwitch.Util
{
    public static class MainThreadSync
    {
        private static readonly ConcurrentQueue<(Action action, EventWaitHandle completeEvent)> actions = new();
        private static int MainThreadId;

        internal static void InitMainThread()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        internal static void RunQueued()
        {
            // Module construction can run on a different thread from application ticks.
            // Only the thread actually draining this queue is the game-thread owner.
            Volatile.Write(ref MainThreadId, Thread.CurrentThread.ManagedThreadId);
            var st = new Stopwatch();
            st.Start();
            while (actions.TryDequeue(out var action))
            {
                using (FreezeDiagnostics.Trace("main-thread.queued-action"))
                {
                    try { action.action(); }
                    catch (Exception ex) { Log.Exception("Queued game-thread action", ex); }
                    finally { action.completeEvent?.Set(); }
                }
                if (st.ElapsedMilliseconds > 2)
                {
                    // if (st.ElapsedMilliseconds > 10)
                    // {
                    //     Log.Info($"Action took {st.ElapsedMilliseconds}ms to Enqueue, this is too slow!");
                    // }
                    break;
                }
            }
        }

        public static EventWaitHandle Run(Action action)
        {
            var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref MainThreadId))
            {
                action();
                waitHandle.Set();
            }
            else
            {
                actions.Enqueue((action, waitHandle));
            }

            return waitHandle;
        }

        public static Task RunWaitAsync(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref MainThreadId))
            {
                try { action(); return Task.CompletedTask; }
                catch (Exception ex) { return Task.FromException(ex); }
            }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            actions.Enqueue((() =>
            {
                try { action(); completion.TrySetResult(true); }
                catch (Exception ex) { completion.TrySetException(ex); }
            }, null));
            return completion.Task;
        }
    }
}
