using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace VsDebugEnv
{
    public static class SettingsStore
    {
        public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VsDebugEnv", "settings.json");
        private static bool Equal(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        public static Settings Read(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) return new Settings();
            try
            {
                Settings settings;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    settings = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                if (settings == null) throw new SerializationException();
                if (settings.Version > 2) throw new DotEnvException("Settings were written by a newer DotEnv version. Update before editing them.");
                settings.Workspaces = settings.Workspaces ?? new List<WorkspaceSettings>();
                if (settings.Version < 2)
                {
                    foreach (var legacy in settings.Projects ?? new ProjectSettings[0])
                    {
                        if (legacy == null || string.IsNullOrWhiteSpace(legacy.Root)) throw new SerializationException();
                        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(legacy.Root));
                        settings.Workspaces.Add(new WorkspaceSettings { Id = root, Root = root,
                            Overrides = new List<FileSelection> { new FileSelection {
                                Mode = legacy.Disabled ? "Disabled" : string.IsNullOrWhiteSpace(legacy.EnvFile) ? "Automatic" : "File",
                                File = legacy.EnvFile, LegacyPath = !string.IsNullOrWhiteSpace(legacy.EnvFile)
                            }}});
                    }
                    settings.Projects = null;
                    settings.Version = 2;
                    settings.MigrationNotice = true;
                }
                if (settings.Workspaces.Where(w => w != null).GroupBy(w => w.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new SerializationException();
                foreach (var workspace in settings.Workspaces)
                {
                    if (workspace == null || string.IsNullOrWhiteSpace(workspace.Id)) throw new SerializationException();
                    workspace.Overrides = workspace.Overrides ?? new List<FileSelection>();
                    foreach (var selection in workspace.Overrides)
                    {
                        FileModeSetting mode;
                        if (selection == null || !Enum.TryParse(selection.Mode, out mode) || !Enum.IsDefined(typeof(FileModeSetting), mode)
                            || selection.Mode != mode.ToString()) throw new SerializationException();
                    }
                    if (workspace.Overrides.GroupBy(o => (o.ProjectId ?? "") + "\0" + (o.Configuration ?? ""), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new SerializationException();
                }
                return settings;
            }
            catch (DotEnvException) { throw; }
            catch (Exception ex) when (ex is SerializationException || ex is System.Xml.XmlException || ex is ArgumentException)
            { throw new DotEnvException("DotEnv settings are invalid. Restore settings.json.bak or correct the JSON. No settings were overwritten."); }
        }
        public static WorkspaceSettings Find(Settings settings, LaunchContext context) =>
            settings.Workspaces.FirstOrDefault(w => Equal(w.Id, context.WorkspaceId))
            ?? settings.Workspaces.FirstOrDefault(w => Equal(w.Id, context.WorkspaceRoot) && Equal(w.Root, context.WorkspaceRoot));
        public static FileSelection Select(Settings settings, LaunchContext context) =>
            Find(settings, context)?.Overrides.Where(o => o.Mode != "Inherit"
                && (string.IsNullOrEmpty(o.ProjectId) || Equal(o.ProjectId, context.ProjectId))
                && (string.IsNullOrEmpty(o.Configuration) || Equal(o.Configuration, context.ConfigurationKey)))
                .OrderByDescending(o => (string.IsNullOrEmpty(o.ProjectId) ? 0 : 2) + (string.IsNullOrEmpty(o.Configuration) ? 0 : 1))
                .FirstOrDefault() ?? new FileSelection();
        public static void Save(LaunchContext context, FileSelection selection, string path = null)
        {
            path = path ?? DefaultPath;
            using (var mutex = new Mutex(false, "VsDebugEnv.Settings.v2"))
            {
                bool entered = false;
                try
                {
                    try { entered = mutex.WaitOne(TimeSpan.FromSeconds(5)); } catch (AbandonedMutexException) { entered = true; }
                    if (!entered) throw new DotEnvException("Another Visual Studio instance is saving settings. Try again.");
                    var settings = Read(path);
                    var workspace = Find(settings, context);
                    if (workspace == null) { workspace = new WorkspaceSettings { Id = context.WorkspaceId, Root = context.WorkspaceRoot }; settings.Workspaces.Add(workspace); }
                    workspace.Overrides.RemoveAll(o => Equal(o.ProjectId, selection.ProjectId) && Equal(o.Configuration, selection.Configuration));
                    if (selection.Mode != "Inherit") workspace.Overrides.Add(selection);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var stream = File.Create(temporary)) new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, settings);
                        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                finally { if (entered) mutex.ReleaseMutex(); }
            }
        }
    }
}
