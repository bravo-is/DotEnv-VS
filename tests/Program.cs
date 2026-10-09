using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VsDebugEnv;

internal static class Program
{
    static int passed;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        passed++; Console.WriteLine("PASS: " + label);
    }
    static void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { Check(true, label); return; }
        throw new Exception("FAIL: " + label);
    }
    static void Json(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
    public static void Main()
    {
        var parsed = EnvironmentLoader.Parse("\uFEFF# comment\r\nPATH=\"C:\\new\\bin;C:\\tools\"\r\nTOKEN=a=b=c\nEMPTY=\nQUOTED='a # b' # comment\nexport FOO=bar # comment\npath=last\n");
        Check(parsed["PATH"] == "last", "Case-insensitive names; last entry wins");
        Check(parsed["TOKEN"] == "a=b=c", "Equals signs retained");
        Check(parsed["EMPTY"] == "", "Empty values retained");
        Check(parsed["QUOTED"] == "a # b" && parsed["FOO"] == "bar", "Quoted hashes, export, and comments");
        Check(EnvironmentLoader.Parse("PATH=\"C:\\new\\bin;C:\\tools\"")["PATH"] == @"C:\new\bin;C:\tools", "Literal Windows paths");
        Check(EnvironmentLoader.Parse("X=${Y};%PATH%;$(command);a#b")["X"] == "${Y};%PATH%;$(command);a#b", "No interpolation or execution");
        foreach (var invalid in new[] { "BAD LINE", "X=\"unclosed", "X=a\0b", "X=\"a\"junk\"", "=value", "BAD NAME=value", "X='a' junk" })
            Throws<FormatException>(() => EnvironmentLoader.Parse(invalid), "Invalid syntax rejected");
        try { EnvironmentLoader.Parse("GOOD=yes\nBAD secret-value"); }
        catch (FormatException ex) { Check(ex.Message.Contains("line 2") && !ex.Message.Contains("secret-value"), "Safe parser diagnostics"); }
        var temp = Path.Combine(Path.GetTempPath(), "DotEnvTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try { TestFiles(temp); TestCMake(temp); }
        finally { Directory.Delete(temp, true); }
        Console.WriteLine(passed + " tests passed.");
    }
    static void TestFiles(string temp)
    {
        var root = Path.Combine(temp, "workspace ü with spaces"); var project = Path.Combine(root, "project"); var build = Path.Combine(temp, "external build");
        Directory.CreateDirectory(project); Directory.CreateDirectory(build);
        var ctx = new LaunchContext { WorkspaceId = Path.Combine(root, "app.sln"), WorkspaceRoot = root, ProjectId = Path.Combine(project, "app.vcxproj"), ProjectDirectory = project, Configuration = "Debug", Platform = "x64", ExecutablePath = Path.Combine(build, "app.exe"), BuildDirectory = build };
        var settings = new Settings();
        Check(EnvironmentLoader.Load(ctx, settings).EnvironmentBlock == null, "Missing automatic file leaves launch unchanged");
        File.WriteAllText(Path.Combine(root, ".env"), "FROM=workspace");
        Check(EnvironmentLoader.Load(ctx, settings).EnvironmentBlock == "FROM=workspace", "Solution fallback with external executable");
        File.WriteAllText(Path.Combine(project, ".env"), "FROM=project\nDOTENV_ISOLATION_TEST=yes");
        Check(EnvironmentLoader.Load(ctx, settings).EnvironmentBlock == "FROM=project\0DOTENV_ISOLATION_TEST=yes", "Project file wins without merging");
        Check(Environment.GetEnvironmentVariable("DOTENV_ISOLATION_TEST") == null, "No host environment changes");
        File.WriteAllText(Path.Combine(project, ".env"), "FROM=edited");
        Check(EnvironmentLoader.Load(ctx, settings).EnvironmentBlock == "FROM=edited", "Reread every launch");
        ctx.IsFolder = true;
        Check(EnvironmentLoader.Load(ctx, settings).EnvironmentBlock == "FROM=workspace", "CMake uses workspace file"); ctx.IsFolder = false;
        var path = Path.Combine(temp, "settings.json");
        SettingsStore.Save(ctx, new FileSelection { Mode = "File", File = ".env" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == "FROM=workspace", "Workspace relative selection");
        SettingsStore.Save(ctx, new FileSelection { Configuration = "Debug|x64", Mode = "Disabled" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == null, "Workspace configuration override");
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Mode = "Automatic" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == "FROM=edited", "Project overrides workspace configuration");
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Configuration = "Debug|x64", Mode = "Disabled" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == null, "Project configuration highest precedence");
        ctx.Configuration = "Release";
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == "FROM=edited", "Configuration switching"); ctx.Configuration = "Debug";
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Configuration = "Debug|x64", Mode = "Inherit" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == "FROM=edited", "Inherit removes override");
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Mode = "File", File = "missing.env" }, path);
        Throws<DotEnvException>(() => EnvironmentLoader.Load(ctx, SettingsStore.Read(path)), "Missing explicit file blocks launch");
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Mode = "File", File = ".env" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == "FROM=edited", "Project relative selection");
        File.WriteAllText(Path.Combine(project, ".env"), "invalid");
        Throws<DotEnvException>(() => EnvironmentLoader.Load(ctx, SettingsStore.Read(path)), "Malformed file blocks launch");
        SettingsStore.Save(ctx, new FileSelection { ProjectId = ctx.ProjectId, Mode = "Disabled" }, path);
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == null, "Disabled skips malformed file");
        Check(File.Exists(path + ".bak"), "Settings backup retained");
        var other = new LaunchContext { WorkspaceId = "another.sln", WorkspaceRoot = build, ProjectDirectory = build };
        SettingsStore.Save(other, new FileSelection { Mode = "Disabled" }, path);
        Check(SettingsStore.Read(path).Workspaces.Count == 2, "Saving preserves other workspace");
        Check(LaunchContext.Match(new[] { ctx }, ctx.ExecutablePath.ToUpperInvariant()) == ctx, "Full executable identity case insensitive");
        Check(LaunchContext.Match(new[] { ctx }, Path.Combine(root, "app.exe")) == null, "Basename cannot select project");
        Throws<DotEnvException>(() => LaunchContext.Match(new[] { ctx, ctx }, ctx.ExecutablePath), "Ambiguous association rejected");
        Check(EnvironmentLoader.Load(null, settings).EnvironmentBlock == null, "Unsupported context unchanged");
        LaunchSnapshot.Publish(null, new[] { ctx });
        ctx.Configuration = "Release";
        Check(LaunchSnapshot.Resolve(ctx.ExecutablePath).Configuration == "Debug", "Published context is isolated from UI edits");
        LaunchSnapshot.Resolve(ctx.ExecutablePath).Configuration = "Altered";
        Check(LaunchSnapshot.Resolve(ctx.ExecutablePath).Configuration == "Debug", "Concurrent launch cannot mutate another launch context");
        LaunchSnapshot.Publish(null, new[] { ctx });
        Check(LaunchSnapshot.Resolve(ctx.ExecutablePath).Configuration == "Release", "New capture replaces old configuration");
        ctx.Configuration = "Debug";
        Check(EnvironmentLoader.ExpandPath("${buildDir}/${configuration}.env", ctx) == Path.Combine(build, "Debug.env"), "Macros use launch context");
        Throws<DotEnvException>(() => EnvironmentLoader.ExpandPath("${unknown}", ctx), "Unknown macro rejected");
        Json(path, new { projects = new[] { new { root, envFile = "legacy.env" } } });
        File.WriteAllText(Path.Combine(build, "legacy.env"), "OLD=preserved"); var migrated = SettingsStore.Read(path);
        Check(migrated.Version == 2 && migrated.MigrationNotice, "Migration version and notice");
        Check(EnvironmentLoader.Load(ctx, migrated).EnvironmentBlock == "OLD=preserved", "Legacy explicit path meaning preserved");
        Json(path, new { projects = new[] { new { root, disabled = true, envFile = "missing.env" } } });
        Check(EnvironmentLoader.Load(ctx, SettingsStore.Read(path)).EnvironmentBlock == null, "Legacy disabled preserved");
        Json(path, new { projects = new[] { new { root, automatic = true } } });
        Check(SettingsStore.Select(SettingsStore.Read(path), ctx).Mode == "Automatic", "Legacy automatic migrates");
        Json(path, new { version = 99 });
        Throws<DotEnvException>(() => SettingsStore.Save(ctx, new FileSelection(), path), "Future settings protected");
        Check(File.ReadAllText(path).Contains("99"), "Future settings retained");
        File.WriteAllText(path, "not json"); Throws<DotEnvException>(() => SettingsStore.Read(path), "Malformed settings rejected");
        Json(path, new { version = 2, workspaces = new[] { new { id = root, root, overrides = new[] { new { mode = "3" } } } } });
        Throws<DotEnvException>(() => SettingsStore.Read(path), "Numeric mode cannot silently enable automatic selection");
    }
    static void TestCMake(string temp)
    {
        var root = Path.Combine(temp, "cmake source"); var build = Path.Combine(temp, "cmake external");
        var reply = Path.Combine(build, ".cmake", "api", "v1", "reply"); Directory.CreateDirectory(root); Directory.CreateDirectory(reply);
        File.WriteAllText(Path.Combine(build, "CMakeCache.txt"), "CMAKE_HOME_DIRECTORY:INTERNAL=" + root);
        Json(Path.Combine(reply, "index-test.json"), new { objects = new[] { new { kind = "codemodel", jsonFile = "model.json" } } });
        Json(Path.Combine(reply, "model.json"), new { configurations = new[] { new { name = "Debug", targets = new[] { new { id = "target-id", jsonFile = "target.json" } } } } });
        Json(Path.Combine(reply, "target.json"), new { name = "app", type = "EXECUTABLE", artifacts = new[] { new { path = "app.exe" } } });
        var ctx = CMakeContext.Resolve(root, Path.Combine(build, "app.exe"));
        Check(ctx != null && ctx.ProjectId == "target-id" && ctx.Configuration == "Debug", "CMake out-of-tree target and configuration");
        Check(CMakeContext.Resolve(root, Path.Combine(build, "other.exe")) == null, "Unknown CMake executable not guessed");
        Check(CMakeContext.Resolve(root + "-other", Path.Combine(build, "app.exe")) == null, "Separate workspace isolated");
        Json(Path.Combine(reply, "index-test.json"), new { objects = new[] { new { kind = "codemodel", jsonFile = "../model.json" } } });
        Throws<DotEnvException>(() => CMakeContext.Resolve(root, Path.Combine(build, "app.exe")), "Invalid CMake reference rejected");
    }
}
