using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

namespace VsDebugEnv
{
    // Per-IDE immutable snapshot. The debugger request thread must never wait on UI services.
    public static class LaunchSnapshot
    {
        private sealed class Snapshot
        {
            internal string Folder;
            internal LaunchContext[] Contexts;
            internal string Error;
        }
        private static Snapshot current;
        private static readonly ConcurrentQueue<string> diagnostics = new ConcurrentQueue<string>();
        public static void Publish(string folder, LaunchContext[] contexts, string error = null) =>
            Interlocked.Exchange(ref current, new Snapshot { Folder = folder, Contexts = contexts?.Select(c => c.Copy()).ToArray() ?? new LaunchContext[0], Error = error });
        public static LaunchContext Resolve(string executable)
        {
            var snapshot = Volatile.Read(ref current);
            if (snapshot == null) throw new DotEnvException("DotEnv launch context is not initialized. Restart Visual Studio and retry F5.");
            if (snapshot.Error != null) throw new DotEnvException(snapshot.Error);
            if (snapshot.Folder != null)
            {
                var context = LaunchContext.Match(snapshot.Contexts, executable)?.Copy() ?? CMakeContext.Resolve(snapshot.Folder, executable);
                if (context == null) throw new DotEnvException("DotEnv could not associate this executable with a CMake target. Reconfigure CMake and select a native executable target.");
                return context;
            }
            var match = LaunchContext.Match(snapshot.Contexts, executable);
            if (match == null && snapshot.Contexts.Length > 0) throw new DotEnvException("DotEnv could not associate the executable with a C++ project. Check the startup project's Debugging Command.");
            return match?.Copy();
        }
        public static void Report(string message) => diagnostics.Enqueue(message);
        public static bool TryReadDiagnostic(out string message) => diagnostics.TryDequeue(out message);
    }
}
