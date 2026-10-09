using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VsDebugEnv
{
    public enum FileModeSetting { Inherit, Automatic, File, Disabled }
    [DataContract]
    public sealed class Settings
    {
        [DataMember(Name = "version")] public int Version { get; set; } = 2;
        [DataMember(Name = "workspaces")] public List<WorkspaceSettings> Workspaces { get; set; } = new List<WorkspaceSettings>();
        [DataMember(Name = "migrationNotice")] public bool MigrationNotice { get; set; }
        [DataMember(Name = "projects", EmitDefaultValue = false)] public ProjectSettings[] Projects { get; set; }
    }
    [DataContract]
    public sealed class WorkspaceSettings
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "root")] public string Root { get; set; }
        [DataMember(Name = "overrides")] public List<FileSelection> Overrides { get; set; } = new List<FileSelection>();
    }
    [DataContract]
    public sealed class FileSelection
    {
        [DataMember(Name = "project", EmitDefaultValue = false)] public string ProjectId { get; set; }
        [DataMember(Name = "configuration", EmitDefaultValue = false)] public string Configuration { get; set; }
        [DataMember(Name = "mode")] public string Mode { get; set; } = "Automatic";
        [DataMember(Name = "file", EmitDefaultValue = false)] public string File { get; set; }
        [DataMember(Name = "legacyPath", EmitDefaultValue = false)] public bool LegacyPath { get; set; }
    }
    [DataContract]
    public sealed class ProjectSettings
    {
        [DataMember(Name = "root", IsRequired = true)] public string Root { get; set; }
        [DataMember(Name = "disabled")] public bool Disabled { get; set; }
        [DataMember(Name = "automatic")] public bool Automatic { get; set; }
        [DataMember(Name = "envFile")] public string EnvFile { get; set; }
    }
}
