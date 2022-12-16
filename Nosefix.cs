using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mihoyo
{
    public static class Nosefix
    {
        public static bool IsRunningAsLocalAdmin()
        {
            Thread.Sleep(200);
            WindowsIdentity cur = WindowsIdentity.GetCurrent();
            if (cur.Groups != null)
            {
                foreach (IdentityReference role in cur.Groups)
                {
                    if (role.IsValidTargetType(typeof(SecurityIdentifier)))
                    {
                        SecurityIdentifier sid = (SecurityIdentifier)role.Translate(typeof(SecurityIdentifier));
                        if (sid.IsWellKnown(WellKnownSidType.AccountAdministratorSid) ||
                            sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static void PatchFod()
        {
            Registry.CurrentUser.CreateSubKey(@"Software\Classes\ms-settings\shell\open\command");
            Registry.CurrentUser.OpenSubKey(@"Software\Classes\ms-settings\shell\open\command", true)
                ?.SetValue("", Assembly.GetExecutingAssembly().Location);
            Registry.CurrentUser.OpenSubKey(@"Software\Classes\ms-settings\shell\open\command", true)
                ?.SetValue("DelegateExecute", "");

            IntPtr wow64Value = IntPtr.Zero;
            Wow64Interop.DisableWow64FSRedirection(ref wow64Value);

            try
            {
                Process.Start("fodhelper");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nosefix failed! " + ex.Message + "\n Stack-Trace: " + ex.StackTrace, "Nosefix Patcher", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            Wow64Interop.Wow64RevertWow64FsRedirection(wow64Value);
            Environment.Exit(1);
        }
    }

    public static class Wow64Interop
    {
        private const string Kernel32dll = "Kernel32.Dll";

        [DllImport(Kernel32dll, EntryPoint = "Wow64DisableWow64FsRedirection")]
        public static extern bool DisableWow64FSRedirection(ref IntPtr ptr);

        [DllImport(Kernel32dll, EntryPoint = "Wow64RevertWow64FsRedirection")]
        public static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);
    }
}
