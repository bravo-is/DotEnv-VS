using System;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Start;

namespace VsDebugEnv
{
    public sealed class LaunchEnvironmentFilter : IDkmProcessLaunchEnvironmentFilter140
    {
        public string GetAdditionalEnvironmentVariables(DkmProcessLaunchEnvironmentFilterInputData inputData)
        {
            if (inputData.AppPackageId != null || string.IsNullOrEmpty(inputData.ExecutablePath)
                || inputData.DebugLaunchSettings?.EngineFilter == null) return null;
            try
            {
                var context = LaunchSnapshot.Resolve(inputData.ExecutablePath);
                var result = EnvironmentLoader.Load(context, SettingsStore.Read());
                LaunchSnapshot.Report(result.Reason + (result.FilePath == null ? "" : ": " + result.FilePath));
                return result.EnvironmentBlock;
            }
            catch (Exception ex)
            {
                var message = ex is DotEnvException ? ex.Message : "DotEnv could not prepare this launch (" + ex.GetType().Name + "). Open Tools > DotEnv for Visual Studio.";
                LaunchSnapshot.Report(message);
                // Post through Concord without waiting for the UI thread during launch.
                DkmUserMessage.Create(inputData.Connection, null, DkmUserMessageOutputKind.MessageBox,
                    "DotEnv for Visual Studio\n\n" + message, (MessageBoxFlags)0, 0).Post();
                const int operationAborted = unchecked((int)0x80004004); // E_ABORT
                throw new DkmException((DkmExceptionCode)operationAborted);
            }
        }
    }
}
