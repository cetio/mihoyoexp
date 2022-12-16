using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mihoyo.Internal.IOCTL
{
    public static class ServiceHelper
    {
        public static bool CreateService(
            ref IntPtr hService,
            string ServiceName,
            string DisplayName,
            string BinPath,
            uint DesiredAccess,
            uint ServiceType,
            uint StartType,
            uint ErrorControl)
        {
            IntPtr hSCManager = NTAPI.OpenSCManager(0, 0, 0x0002);

            if (hSCManager == IntPtr.Zero)
            {
                return false;
            }

            hService = NTAPI.CreateServiceW(
                hSCManager,
                ServiceName, DisplayName,
                DesiredAccess,
                ServiceType, StartType,
                ErrorControl, BinPath,
                0, 0, 0, 0, 0, 0);

            _ = NTAPI.CloseServiceHandle(hSCManager);

            return hService != IntPtr.Zero;
        }

        public static bool OpenService(out IntPtr hService, string szServiceName, uint DesiredAccess)
        {
            IntPtr hSCManager = NTAPI.OpenSCManager(0, 0, DesiredAccess);
            hService = NTAPI.OpenService(hSCManager, szServiceName, DesiredAccess);
            _ = NTAPI.CloseServiceHandle(hSCManager);
            return hService != IntPtr.Zero;
        }

        public static bool StopService(IntPtr hService)
        {
            NTAPI.SERVICE_STATUS ServiceStatus = new NTAPI.SERVICE_STATUS();
            return NTAPI.ControlService(hService, NTAPI.SERVICE_CONTROL.STOP, ref ServiceStatus);
        }

        public static bool StartService(IntPtr hService)
        {
            return NTAPI.StartService(hService, 0, null);
        }

        public static bool DeleteService(IntPtr hService)
        {
            return NTAPI.DeleteService(hService);
        }

        public static void CloseServiceHandle(IntPtr hService)
        {
            _ = NTAPI.CloseServiceHandle(hService);
        }
    }
}
