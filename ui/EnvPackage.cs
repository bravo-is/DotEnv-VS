using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Debug;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;

namespace VsDebugEnv.UI
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(EnvOptionsPage), "DotEnv for Visual Studio", "Environment files", 100, 101, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuid)]
    public sealed class EnvPackage : AsyncPackage
    {
        public const string PackageGuid = "608bdc47-3a81-40d6-bfb2-661ab15ce1a2";
        internal static EnvPackage Instance { get; private set; }
        private IVsFolderWorkspaceService folders;
        private DTE dte;
        private IVsOutputWindowPane output;
        private CommandEvents commandEvents;
        private System.Windows.Forms.Timer outputTimer;
        private readonly Dictionary<string, LaunchContext> recent = new Dictionary<string, LaunchContext>(StringComparer.OrdinalIgnoreCase);
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var components = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            var dteService = await GetServiceAsync(typeof(SDTE));
            var commands = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var outputService = await GetServiceAsync(typeof(SVsOutputWindow));
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            InitializeServices(components, dteService, commands, outputService);
            StartLaunchCapture();
        }
        private void StartLaunchCapture()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            outputTimer = new System.Windows.Forms.Timer { Interval = 250 };
            outputTimer.Tick += DrainOutput;
            outputTimer.Start();
            commandEvents = dte.Events.CommandEvents;
            commandEvents.BeforeExecute += BeforeCommand;
            CaptureLaunchContext();
        }
        private void DrainOutput(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string message;
            while (LaunchSnapshot.TryReadDiagnostic(out message)) Report(message);
        }
        private void BeforeCommand(string guid, int id, object input, object result, ref bool cancel)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string name;
            try { name = dte.Commands.Item(guid, id).Name; }
            catch (ArgumentException) { return; }
            catch (COMException) { return; }
            if (name != null && name.StartsWith("Debug.", StringComparison.Ordinal)) CaptureLaunchContext();
        }
        private void CaptureLaunchContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var root = folders?.CurrentWorkspace?.Location;
                var contexts = ThreadHelper.JoinableTaskFactory.Run(GetContextsAsync).Where(c => c.ProjectId != null).ToArray();
                if (root == null && dte?.Solution?.IsOpen == true)
                {
                    var startup = dte.Solution.SolutionBuild.StartupProjects as Array;
                    if (startup != null && startup.Length > 0)
                    {
                        var solutionRoot = Path.GetDirectoryName(dte.Solution.FullName);
                        var projects = new HashSet<string>(startup.Cast<object>().Select(p =>
                            Path.GetFullPath(Path.Combine(solutionRoot, Convert.ToString(p)))), StringComparer.OrdinalIgnoreCase);
                        contexts = contexts.Where(c => projects.Contains(c.ProjectId)).ToArray();
                    }
                }
                LaunchSnapshot.Publish(root, contexts);
            }
            catch (Exception ex)
            {
                LaunchSnapshot.Publish(null, null, ex is DotEnvException ? ex.Message : "DotEnv could not capture the launch context (" + ex.GetType().Name + ").");
            }
        }
        private void InitializeServices(IComponentModel components, object dteService, OleMenuCommandService commands, object outputService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte = dteService as DTE;
            var window = outputService as IVsOutputWindow;
            folders = components?.GetService<IVsFolderWorkspaceService>();
            Instance = this;
            if (commands != null) commands.AddCommand(new OleMenuCommand((s, e) => ShowSettings(),
                new CommandID(new Guid("8ed02e2e-e4b6-4b67-8ee2-0d233ff61b8c"), 0x0100)));
            if (window != null)
            {
                var id = new Guid("2cba2a22-41b7-4867-adb1-b401960c741d");
                window.CreatePane(ref id, "DotEnv for Visual Studio", 1, 0);
                window.GetPane(ref id, out output);
            }
        }
        private void ShowSettings() { ThreadHelper.ThrowIfNotOnUIThread(); ShowOptionPage(typeof(EnvOptionsPage)); }
        public void Report(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            output?.OutputStringThreadSafe(DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        public async Task<LaunchContext> ResolveAsync(string executable)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var root = folders?.CurrentWorkspace?.Location;
            if (!string.IsNullOrEmpty(root))
            {
                var context = CMakeContext.Resolve(root, executable);
                if (context == null) throw new DotEnvException("DotEnv could not associate this executable with a CMake target in the open folder. Reconfigure CMake and select a native executable target.");
                recent[context.ProjectId] = context;
                return context;
            }
            var contexts = await GetContextsAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var result = LaunchContext.Match(contexts.Where(c => c.ProjectId != null), executable);
            if (result == null && contexts.Count > 1)
                throw new DotEnvException("DotEnv could not associate this launch with a native C++ project. Check the startup project's Debugging > Command setting.");
            return result;
        }
        internal async Task<List<LaunchContext>> GetContextsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var root = folders?.CurrentWorkspace?.Location;
            if (!string.IsNullOrEmpty(root))
            {
                var result = new List<LaunchContext> { new LaunchContext { WorkspaceId = root, WorkspaceRoot = root, ProjectDirectory = root, DisplayName = "Workspace defaults", IsFolder = true } };
                var workspace = folders.CurrentWorkspace;
                var service = await workspace.GetProjectConfigurationServiceAsync();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                recent.Clear();
                var files = (service?.AllProjectFileConfigurations ?? new ProjectConfiguration[0]).Select(p => p.FilePath)
                    .Concat(new[] { service?.CurrentProject?.FilePath, Path.Combine(root, "CMakeLists.txt") })
                    .Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (var path in files)
                {
                    var groups = await workspace.GetFileContextsAsync(path, new[] { DebugLaunchActionContext.ContextTypeGuid }, CancellationToken.None);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    foreach (var entry in groups.SelectMany(g => g))
                    {
                        var launch = entry.Context as DebugLaunchActionContext;
                        if (launch == null) continue;
                        object value;
                        if (launch.LaunchConfiguration == null || !launch.LaunchConfiguration.TryGetValue("target", out value)) continue;
                        if (!(value is string executable) || !Path.IsPathRooted(executable)) continue;
                        var context = CMakeContext.Resolve(root, executable);
                        if (context == null) continue;
                        context.Configurations = (service.GetBuildConfigurations(launch.ProjectFileContext ?? service.CurrentProject) ?? Enumerable.Empty<string>())
                            .Concat(context.Configurations ?? new string[0])
                            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        recent[context.ProjectId] = context;
                    }
                }
                foreach (var project in service?.AllProjectFileConfigurations ?? new ProjectConfiguration[0])
                {
                    object program;
                    if (project.LaunchSettings != null && project.LaunchSettings.TryGetValue("program", out program)
                        && program is string path && Path.IsPathRooted(path) && !path.Contains("${"))
                    {
                        var context = CMakeContext.Resolve(root, path);
                        if (context != null) recent[context.ProjectId] = context;
                    }
                }
                result.AddRange(recent.Values.Where(c => string.Equals(c.WorkspaceRoot, root, StringComparison.OrdinalIgnoreCase)));
                result[0].Configurations = result.Skip(1).SelectMany(c => c.Configurations).Distinct().ToArray();
                return result;
            }
            if (dte?.Solution == null || !dte.Solution.IsOpen || string.IsNullOrEmpty(dte.Solution.FullName)) return new List<LaunchContext>();
            var solution = Path.GetFullPath(dte.Solution.FullName);
            root = Path.GetDirectoryName(solution);
            var contexts = new List<LaunchContext> { new LaunchContext { WorkspaceId = solution, WorkspaceRoot = root, ProjectDirectory = root, DisplayName = "Solution defaults" } };
            foreach (var project in Projects(dte.Solution.Projects))
            {
                if (string.IsNullOrEmpty(project.FullName) || !project.FullName.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase)) continue;
                dynamic vc = project.Object;
                dynamic config = vc.ActiveConfiguration;
                if (config == null) continue;
                string command = config.Evaluate((string)config.DebugSettings.Command);
                if (string.IsNullOrWhiteSpace(command)) command = config.Evaluate("$(TargetPath)");
                command = command.Trim('"');
                if (command.Contains("$(")) throw new DotEnvException("Cannot resolve the debug executable for " + project.Name + ".");
                var projectDirectory = Path.GetDirectoryName(project.FullName);
                var names = new List<string>();
                foreach (dynamic candidate in vc.Configurations) names.Add((string)candidate.Name);
                string fullConfig = config.Name;
                var parts = fullConfig.Split('|');
                contexts.Add(new LaunchContext { WorkspaceId = solution, WorkspaceRoot = root,
                    ProjectId = Path.GetFullPath(project.FullName), ProjectDirectory = projectDirectory, DisplayName = project.Name,
                    Configuration = parts[0], Platform = parts.Length > 1 ? parts[1] : null,
                    Configurations = names.ToArray(), ExecutablePath = Path.GetFullPath(Path.Combine(projectDirectory, command)),
                    BuildDirectory = Path.GetFullPath(Path.Combine(projectDirectory, (string)config.Evaluate("$(OutDir)"))) });
            }
            contexts[0].Configurations = contexts.Skip(1).SelectMany(c => c.Configurations).Distinct().ToArray();
            return contexts;
        }
        private static IEnumerable<Project> Projects(Projects projects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in projects)
            {
                yield return project;
                if (project.ProjectItems == null) continue;
                foreach (var child in Children(project.ProjectItems)) yield return child;
            }
        }
        private static IEnumerable<Project> Children(ProjectItems items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (ProjectItem item in items)
            {
                if (item.SubProject == null) continue;
                yield return item.SubProject;
                if (item.SubProject.ProjectItems != null)
                    foreach (var child in Children(item.SubProject.ProjectItems)) yield return child;
            }
        }
    }
}
