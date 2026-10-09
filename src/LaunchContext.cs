using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VsDebugEnv
{
    public sealed class LaunchContext
    {
        public string WorkspaceId { get; set; }
        public string WorkspaceRoot { get; set; }
        public string ProjectId { get; set; }
        public string ProjectDirectory { get; set; }
        public string DisplayName { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public string ExecutablePath { get; set; }
        public string BuildDirectory { get; set; }
        public bool IsFolder { get; set; }
        public string[] Configurations { get; set; } = new string[0];
        public LaunchContext Copy()
        {
            var copy = (LaunchContext)MemberwiseClone();
            copy.Configurations = (string[])Configurations.Clone();
            return copy;
        }
        public string ConfigurationKey => string.IsNullOrEmpty(Platform) ? Configuration : Configuration + "|" + Platform;
        public static LaunchContext Match(IEnumerable<LaunchContext> contexts, string executable)
        {
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable)) return null;
            var matches = contexts.Where(c => !string.IsNullOrEmpty(c.ExecutablePath)
                && string.Equals(Path.GetFullPath(c.ExecutablePath), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new DotEnvException("Several targets launch this executable. Give them distinct output paths before debugging.");
            return matches.SingleOrDefault();
        }
    }
    public sealed class DotEnvException : Exception { public DotEnvException(string message) : base(message) { } }
    public sealed class LoadResult
    {
        public string FilePath { get; set; }
        public string Reason { get; set; }
        public string EnvironmentBlock { get; set; }
    }
}
