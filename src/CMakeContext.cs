using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace VsDebugEnv
{
    // Reads CMake's generated file API, never infers target identity from an executable basename.
    public static class CMakeContext
    {
        public static LaunchContext Resolve(string workspaceRoot, string executable)
        {
            for (var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(executable))); dir != null; dir = dir.Parent)
            {
                var cache = Path.Combine(dir.FullName, "CMakeCache.txt");
                if (!File.Exists(cache)) continue;
                var home = File.ReadLines(cache).FirstOrDefault(l => l.StartsWith("CMAKE_HOME_DIRECTORY:INTERNAL=", StringComparison.Ordinal));
                if (home == null) return null;
                var source = Path.GetFullPath(home.Substring(home.IndexOf('=') + 1));
                var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar);
                if (!source.Equals(root, StringComparison.OrdinalIgnoreCase) && !source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
                var reply = Path.Combine(dir.FullName, ".cmake", "api", "v1", "reply");
                if (!Directory.Exists(reply)) throw new DotEnvException("CMake target information is not ready. Configure the project in Visual Studio, then retry F5.");
                var indexFile = Directory.GetFiles(reply, "index-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (indexFile == null) throw new DotEnvException("CMake has not generated its file API index. Reconfigure the project.");
                var index = Read<Index>(indexFile);
                var modelRef = index.objects?.FirstOrDefault(o => o.kind == "codemodel");
                if (modelRef == null) throw new DotEnvException("CMake codemodel is unavailable. Reconfigure the project in Visual Studio.");
                var model = Read<Model>(SafeReplyPath(reply, modelRef.jsonFile));
                var matches = new List<LaunchContext>();
                foreach (var config in model.configurations ?? new Configuration[0])
                foreach (var targetRef in config.targets ?? new TargetReference[0])
                {
                    var target = Read<Target>(SafeReplyPath(reply, targetRef.jsonFile));
                    if (target.type != "EXECUTABLE") continue;
                    if (!(target.artifacts ?? new Artifact[0]).Any(a => string.Equals(Path.GetFullPath(Path.Combine(dir.FullName, a.path)), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))) continue;
                    matches.Add(new LaunchContext { WorkspaceId = root, WorkspaceRoot = root, ProjectDirectory = root,
                        ProjectId = targetRef.id, DisplayName = target.name, IsFolder = true,
                        Configuration = config.name, ExecutablePath = executable, BuildDirectory = dir.FullName,
                        Configurations = model.configurations.Select(c => c.name).ToArray() });
                }
                return LaunchContext.Match(matches, executable);
            }
            return null;
        }
        private static string SafeReplyPath(string reply, string name)
        {
            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name) throw new DotEnvException("Invalid CMake file API reference. Reconfigure the project.");
            return Path.Combine(reply, name);
        }
        private static T Read<T>(string path)
        {
            try { using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            catch (Exception ex) when (ex is IOException || ex is SerializationException) { throw new DotEnvException("Cannot read CMake target information. Reconfigure the project."); }
        }
        [DataContract] private sealed class Index { [DataMember] public ObjectReference[] objects { get; set; } }
        [DataContract] private sealed class ObjectReference { [DataMember] public string kind { get; set; } [DataMember] public string jsonFile { get; set; } }
        [DataContract] private sealed class Model { [DataMember] public Configuration[] configurations { get; set; } }
        [DataContract] private sealed class Configuration { [DataMember] public string name { get; set; } [DataMember] public TargetReference[] targets { get; set; } }
        [DataContract] private sealed class TargetReference { [DataMember] public string id { get; set; } [DataMember] public string jsonFile { get; set; } }
        [DataContract] private sealed class Target { [DataMember] public string name { get; set; } [DataMember] public string type { get; set; } [DataMember] public Artifact[] artifacts { get; set; } }
        [DataContract] private sealed class Artifact { [DataMember] public string path { get; set; } }
    }
}
