using System;
using System.Diagnostics;
using System.Threading;

namespace BLTAdoptAHero.Util
{
    internal static class ResourcePreloadWait
    {
        internal const int TimeoutMilliseconds = 15000;

        internal static bool Run(Func<int> poll)
        {
            var clock = Stopwatch.StartNew();
            return Run(poll, () => clock.ElapsedMilliseconds, () => Thread.Sleep(1), TimeoutMilliseconds);
        }

        // A deadline bounds repeated polling, not a native call that itself never returns.
        internal static bool Run(Func<int> poll, Func<long> elapsed, Action yield, int timeoutMilliseconds)
        {
            while (true)
            {
                if (poll() == 0) return true;
                if (elapsed() >= timeoutMilliseconds) return false;
                yield();
            }
        }
    }
}
