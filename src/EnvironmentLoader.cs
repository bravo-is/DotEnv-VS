using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VsDebugEnv
{
    public static class EnvironmentLoader
    {
        public static LoadResult Load(LaunchContext context, Settings settings)
        {
            if (context == null) return new LoadResult { Reason = "Not a supported workspace launch." };
            var selection = SettingsStore.Select(settings, context);
            var scope = string.IsNullOrEmpty(selection.ProjectId) ? "Workspace default" : "Project/target override";
            if (!string.IsNullOrEmpty(selection.Configuration)) scope += " (" + selection.Configuration + ")";
            if (selection.Mode == "Disabled") return new LoadResult { Reason = scope + ": disabled." };
            string path;
            if (selection.Mode == "File")
            {
                if (string.IsNullOrWhiteSpace(selection.File)) throw new DotEnvException("Choose an environment file in Tools > DotEnv for Visual Studio.");
                path = ExpandPath(selection.File, context, selection.LegacyPath, string.IsNullOrEmpty(selection.ProjectId));
                if (selection.LegacyPath && (path.Contains("*") || path.Contains("?")))
                {
                    var dir = Path.GetDirectoryName(path);
                    var matches = Directory.Exists(dir) ? Directory.GetFiles(dir, Path.GetFileName(path)) : new string[0];
                    if (matches.Length != 1) throw new DotEnvException("The migrated environment-file pattern must match exactly one file. Choose a file in DotEnv settings.");
                    path = matches[0];
                }
                if (!File.Exists(path)) throw new DotEnvException("Selected environment file does not exist: " + path);
            }
            else
            {
                path = !context.IsFolder && !string.IsNullOrEmpty(context.ProjectDirectory) ? Path.Combine(context.ProjectDirectory, ".env") : null;
                if (path != null && File.Exists(path)) scope += ": project .env";
                else { path = Path.Combine(context.WorkspaceRoot, ".env"); scope += ": workspace .env"; }
                if (!File.Exists(path)) return new LoadResult { Reason = "Automatic: no .env found; existing launch environment is unchanged." };
            }
            try
            {
                var values = Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
                return new LoadResult { FilePath = path, Reason = scope,
                    EnvironmentBlock = string.Join("\0", values.Select(p => p.Key + "=" + p.Value)) };
            }
            catch (FormatException ex) { throw new DotEnvException(path + ": " + ex.Message); }
            catch (DecoderFallbackException) { throw new DotEnvException("Environment file must be UTF-8: " + path); }
            catch (IOException) { throw new DotEnvException("Cannot read environment file: " + path); }
            catch (UnauthorizedAccessException) { throw new DotEnvException("Access denied reading environment file: " + path); }
        }
        public static string ExpandPath(string pattern, LaunchContext context, bool legacy = false, bool workspaceRelative = false)
        {
            var exeDir = string.IsNullOrEmpty(context.ExecutablePath) ? null : Path.GetDirectoryName(context.ExecutablePath);
            var build = context.BuildDirectory;
            if (legacy && pattern.Contains("${build") && string.IsNullOrEmpty(build))
                for (var dir = exeDir == null ? null : new DirectoryInfo(exeDir); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "CMakeCache.txt"))) { build = dir.FullName; break; }
            var buildType = context.Configuration;
            if (legacy && pattern.Contains("${buildType}"))
            {
                if (build == null) throw new DotEnvException("Cannot resolve the migrated build directory. Choose an explicit file.");
                var line = File.ReadLines(Path.Combine(build, "CMakeCache.txt")).FirstOrDefault(l => l.StartsWith("CMAKE_BUILD_TYPE:", StringComparison.Ordinal));
                if (line == null || !line.Contains("=")) throw new DotEnvException("Cannot resolve the migrated build type. Choose an explicit file.");
                buildType = line.Substring(line.IndexOf('=') + 1).Trim().ToLowerInvariant();
            }
            foreach (var pair in new Dictionary<string, string> {
                ["workspaceRoot"] = context.WorkspaceRoot, ["projectDir"] = context.ProjectDirectory,
                ["exeDir"] = exeDir, ["buildDir"] = build, ["configuration"] = context.Configuration,
                ["platform"] = context.Platform, ["buildType"] = buildType })
            {
                var token = "${" + pair.Key + "}";
                if (!pattern.Contains(token)) continue;
                if (string.IsNullOrEmpty(pair.Value)) throw new DotEnvException("Path macro " + token + " is unavailable for this launch.");
                pattern = pattern.Replace(token, pair.Value);
            }
            if (pattern.Contains("${")) throw new DotEnvException("Unknown environment-file path macro.");
            if (legacy) pattern = Environment.ExpandEnvironmentVariables(pattern);
            if (!Path.IsPathRooted(pattern))
            {
                var basis = legacy ? exeDir : workspaceRelative ? context.WorkspaceRoot : context.ProjectDirectory ?? context.WorkspaceRoot;
                if (basis == null) throw new DotEnvException("The executable directory is not available for this migrated selection.");
                pattern = Path.Combine(basis, pattern);
            }
            return Path.GetFullPath(pattern);
        }
        public static IDictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string line;
                int number = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    number++;
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).TrimStart();
                    var equals = line.IndexOf('=');
                    if (equals <= 0) throw InvalidLine(number);
                    var name = line.Substring(0, equals).Trim();
                    var value = line.Substring(equals + 1).Trim();
                    if (name.Length == 0 || name.Any(c => char.IsWhiteSpace(c) || c == '\0'))
                        throw InvalidLine(number);
                    if (value.Length > 0 && (value[0] == '\'' || value[0] == '"'))
                    {
                        var quote = value[0];
                        var closing = value.IndexOf(quote, 1);
                        if (closing < 0) throw InvalidLine(number);
                        var suffix = value.Substring(closing + 1).Trim();
                        if (suffix.Length != 0 && !suffix.StartsWith("#")) throw InvalidLine(number);
                        value = value.Substring(1, closing - 1);
                    }
                    else
                    {
                        for (int i = 1; i < value.Length; i++)
                            if (value[i] == '#' && char.IsWhiteSpace(value[i - 1]))
                            { value = value.Substring(0, i).TrimEnd(); break; }
                    }
                    if (value.IndexOf('\0') >= 0) throw InvalidLine(number);
                    // Preserve literal Windows backslashes and $/% characters.
                    result[name] = value;
                }
            }
            return result;
        }

        private static FormatException InvalidLine(int number)
        {
            // Do not include variable values (which may contain secrets) in error messages.
            return new FormatException("Invalid .env syntax at line " + number + ". Expected NAME=value.");
        }
    }
}
