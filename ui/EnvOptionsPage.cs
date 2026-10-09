using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace VsDebugEnv.UI
{
    [Guid("9d99b107-fbf5-47c4-ad14-c72b68ace8d1"), ComVisible(true)]
    public sealed class EnvOptionsPage : DialogPage
    {
        private EnvSettingsControl control;
        protected override IWin32Window Window => control ?? (control = new EnvSettingsControl());
        public override void LoadSettingsFromStorage() { }
        public override void SaveSettingsToStorage() { }
        protected override void OnActivate(CancelEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (control == null) control = new EnvSettingsControl();
            control.Reload();
            base.OnActivate(e);
        }
    }
}
