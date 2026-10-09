using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace VsDebugEnv.UI
{
    internal sealed class EnvSettingsControl : UserControl
    {
        private readonly ComboBox scope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox configuration = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly TextBox file = new TextBox { Dock = DockStyle.Fill };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };
        private readonly Label workspace = new Label { AutoSize = true, MaximumSize = new Size(620, 0) };
        private List<LaunchContext> contexts = new List<LaunchContext>();
        private bool loading;
        private bool legacyPath;
        private FileModeSetting SelectedMode => mode.SelectedItem is string text
            ? text == "Choose file" ? FileModeSetting.File : (FileModeSetting)Enum.Parse(typeof(FileModeSetting), text)
            : FileModeSetting.Automatic;
        internal EnvSettingsControl()
        {
            Dock = DockStyle.Fill; AutoScroll = true; Padding = new Padding(8);
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Add(layout, "Workspace", workspace); Add(layout, "Apply to", scope); Add(layout, "Configuration", configuration); Add(layout, "Mode", mode);
            var fileRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var browse = new Button { Text = "Browse…", AutoSize = true };
            fileRow.Controls.Add(file, 0, 0); fileRow.Controls.Add(browse, 1, 0);
            Add(layout, "File", fileRow);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var validate = new Button { Text = "Validate", AutoSize = true }; var save = new Button { Text = "Save selection", AutoSize = true };
            buttons.Controls.Add(validate); buttons.Controls.Add(save); Add(layout, "", buttons); Add(layout, "Status", status);
            Add(layout, "", new Label { Text = "One file per launch. File values override debugger settings.\nSave selection applies changes immediately.", AutoSize = true, MaximumSize = new Size(620, 0) });
            Controls.Add(layout);
            scope.SelectedIndexChanged += (s, e) => ScopeChanged();
            configuration.SelectedIndexChanged += (s, e) => SelectionChanged();
            mode.SelectedIndexChanged += (s, e) => { file.Enabled = SelectedMode == FileModeSetting.File; if (!loading) Preview(false); };
            file.TextChanged += (s, e) => { if (!loading) legacyPath = false; };
            browse.Click += (s, e) => { using (var dialog = new OpenFileDialog { Filter = "Environment files|.env;*.env|All files|*.*", CheckFileExists = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { file.Text = dialog.FileName; mode.SelectedItem = "Choose file"; } };
            validate.Click += (s, e) => Preview(false); save.Click += (s, e) => Preview(true);
        }
        private static void Add(TableLayoutPanel panel, string label, Control control)
        {
            int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 8, 8) }, 0, row);
            control.Margin = new Padding(0, 5, 0, 5); panel.Controls.Add(control, 1, row);
        }
        internal void Reload()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                contexts = ThreadHelper.JoinableTaskFactory.Run(async () => await EnvPackage.Instance.GetContextsAsync());
                loading = true; scope.Items.Clear(); foreach (var context in contexts) scope.Items.Add(context.DisplayName);
                workspace.Text = contexts.FirstOrDefault()?.WorkspaceRoot ?? "Open a C++ solution or CMake folder first.";
                loading = false; if (scope.Items.Count > 0) scope.SelectedIndex = 0;
                if (SettingsStore.Read().MigrationNotice) status.Text = "Your earlier settings were migrated. Automatic now uses conventional .env files. Explicit selections and disabled settings are preserved.";
            }
            catch (Exception ex) { status.Text = Safe(ex); loading = false; }
        }
        private void ScopeChanged()
        {
            if (loading || scope.SelectedIndex < 0) return;
            loading = true; configuration.Items.Clear(); configuration.Items.Add("All configurations");
            foreach (var name in contexts[scope.SelectedIndex].Configurations.Distinct()) configuration.Items.Add(name);
            configuration.SelectedIndex = 0; loading = false; SelectionChanged();
        }
        private void SelectionChanged()
        {
            if (loading || scope.SelectedIndex < 0) return;
            try
            {
                var ctx = contexts[scope.SelectedIndex]; var config = configuration.SelectedIndex > 0 ? (string)configuration.SelectedItem : null;
                var saved = SettingsStore.Find(SettingsStore.Read(), ctx)?.Overrides.FirstOrDefault(o => o.ProjectId == ctx.ProjectId && o.Configuration == config);
                loading = true;
                var rootScope = ctx.ProjectId == null && config == null;
                mode.Items.Clear();
                if (!rootScope) mode.Items.Add("Inherit");
                mode.Items.AddRange(new object[] { "Automatic", "Choose file", "Disabled" });
                var savedMode = saved?.Mode ?? (rootScope ? "Automatic" : "Inherit");
                mode.SelectedItem = savedMode == "File" ? "Choose file" : savedMode;
                file.Text = saved?.File ?? ""; legacyPath = saved?.LegacyPath ?? false; loading = false;
                Preview(false);
            }
            catch (Exception ex) { status.Text = Safe(ex); loading = false; }
        }
        private void Preview(bool save)
        {
            if (scope.SelectedIndex < 0) return;
            try
            {
                var ctx = contexts[scope.SelectedIndex];
                var selected = new FileSelection { ProjectId = ctx.ProjectId,
                    Configuration = configuration.SelectedIndex > 0 ? (string)configuration.SelectedItem : null,
                    Mode = SelectedMode.ToString(), File = file.Text.Trim(), LegacyPath = legacyPath };
                if (selected.Mode == "File" && string.IsNullOrWhiteSpace(selected.File)) throw new DotEnvException("Choose an environment file first.");
                var settings = SettingsStore.Read();
                var ws = SettingsStore.Find(settings, ctx);
                if (ws == null) { ws = new WorkspaceSettings { Id = ctx.WorkspaceId, Root = ctx.WorkspaceRoot }; settings.Workspaces.Add(ws); }
                ws.Overrides.RemoveAll(o => o.ProjectId == selected.ProjectId && o.Configuration == selected.Configuration);
                if (selected.Mode != "Inherit") ws.Overrides.Add(selected);
                // Preview the selected configuration without mutating the live launch context.
                var preview = new LaunchContext { WorkspaceId = ctx.WorkspaceId, WorkspaceRoot = ctx.WorkspaceRoot, ProjectId = ctx.ProjectId, ProjectDirectory = ctx.ProjectDirectory,
                    ExecutablePath = ctx.ExecutablePath, BuildDirectory = ctx.BuildDirectory, IsFolder = ctx.IsFolder, Configuration = ctx.Configuration, Platform = ctx.Platform };
                if (selected.Configuration != null) { var parts = selected.Configuration.Split('|'); preview.Configuration = parts[0]; preview.Platform = parts.Length > 1 ? parts[1] : null; }
                var result = EnvironmentLoader.Load(preview, settings);
                if (save) SettingsStore.Save(ctx, selected);
                status.Text = (save ? "Saved. " : selected.Mode == "Inherit" ? "Inherited. " : "") + result.Reason + (result.FilePath == null ? "" : "\n" + result.FilePath);
            }
            catch (Exception ex) { status.Text = Safe(ex); }
        }
        private static string Safe(Exception ex) => ex is DotEnvException ? ex.Message : "Could not load settings (" + ex.GetType().Name + ").";
    }
}
