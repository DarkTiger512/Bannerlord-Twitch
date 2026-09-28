using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace BannerlordTwitch
{
    // TAOM can resolve TwitchLib's older logging references to its own logging
    // assembly. Resolve constructors from the library itself instead of emitting
    // a signature containing BLT's different ILogger assembly identity.
    internal static class TwitchLibraryFactory
    {
        internal static T Create<T>(params object[] arguments)
        {
            var constructor = typeof(T).GetConstructors().Single(c =>
                c.GetParameters().Length == arguments.Length);
            try { return (T)constructor.Invoke(arguments); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
