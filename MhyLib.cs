using Mihoyo.Internal.IOCTL;
using Mihoyo.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Mihoyo.Internal.Structures;
using System.Runtime.CompilerServices;

namespace Mihoyo
{
    public unsafe class MhyLib
    {
        private IntPtr ServiceHandle;
        private IntPtr DriverHandle;
        private MT64 Codex = new MT64();
        private ulong MT64Res;

        public ulong Seed = 0x233333333333;
        public bool Initialized;
        public MhyProcess Process;

        #region Init-Exit-Open
        public MhyLib(int pid)
        {
            string path = Environment.SystemDirectory + "\\drivers\\mhyprot2.sys";
            int attempts = 0;

            if (!File.Exists(path))
            {
                // suck my balls I will use WebClient until the day I die
                using (WebClient webClient = new WebClient())
                {
                    // getting the driver for mhyprot2 (this version is especially vulnerable)
                    webClient.DownloadFile("https://github.com/kagurazakasanae/Mhyprot2DrvControl/raw/main/mhyprot2.Sys", path);
                }
            }

            // try to open the service
            while (ServiceHelper.OpenService(out IntPtr serviceHandle, "mhyprot2", 0x0020 | 0x00010000))
            {
                _ = ServiceHelper.StopService(serviceHandle);
                _ = ServiceHelper.DeleteService(serviceHandle);
                ServiceHelper.CloseServiceHandle(serviceHandle);
                attempts++;

                // try to close, otherwise throw failure
                if (!ServiceHelper.OpenService(out _, "mhyprot2", 0x0020 | 0x00010000))
                    Console.WriteLine("Closed existing service");
                else if (attempts >= 10)
                {
                    Console.WriteLine($"Load failed, existing driver service denied control access \nThe developer might not have implemented the Unload method!");
                    return;
                }
            }

            if (!ServiceHelper.CreateService(ref ServiceHandle, "mhyprot2", "mhyprot2", path,
                (uint)NTAPI.SERVICE_ACCESS.SERVICE_ALL_ACCESS, 1,
                (uint)NTAPI.SERVICE_START.SERVICE_DEMAND_START, 1))
            {
                // failed to create service
                Console.WriteLine($"Service creation returned error ({Marshal.GetLastWin32Error()})");
                return;
            }
            else
            {
                Console.WriteLine("Created new service");
                if (!ServiceHelper.StartService(ServiceHandle) && Marshal.GetLastWin32Error() != 31)
                {
                    Console.WriteLine($"Service creation returned error ({Marshal.GetLastWin32Error()})");
                    _ = ServiceHelper.DeleteService(ServiceHandle);
                    return;
                }
            }

            // done, now onto more important stuff
            Console.WriteLine("Started created service");
            Console.Write($"Loaded driver after {attempts} tries <- 0x{ServiceHandle.ToString("X")} : ");

            // internal driver stuff
            NTAPI.OBJECT_ATTRIBUTES objectAttributes = new NTAPI.OBJECT_ATTRIBUTES();
            NTAPI.UNICODE_STRING deviceName = new NTAPI.UNICODE_STRING("\\Device\\mhyprot2");
            NTAPI.IO_STATUS_BLOCK ioStatus;
            objectAttributes.Length = Marshal.SizeOf(typeof(NTAPI.OBJECT_ATTRIBUTES));
            objectAttributes.ObjectName = new IntPtr(&deviceName);
            IntPtr deviceHandle;
            uint state = NTAPI.NtOpenFile(
                &deviceHandle,
                (uint)(NTAPI.ACCESS_MASK.GENERIC_READ | NTAPI.ACCESS_MASK.GENERIC_WRITE | NTAPI.ACCESS_MASK.SYNCHRONIZE),
                &objectAttributes, &ioStatus, 0, 3);

            if (state == 0)
            {
                DriverHandle = deviceHandle;
                Console.WriteLine("HD" + DriverHandle);
            }
            else
                return;

            // failure
            if (DriverHandle == IntPtr.Zero)
                throw new Exception("Driver handle has not been opened");

            ulong seed = 0x233333333333;
            byte[] initdata = Codex.GenInitData((ulong)System.Diagnostics.Process.GetCurrentProcess().Id, ref seed, ref MT64Res);
            IntPtr lpinBuffer = Constructors.ByteToPtr(initdata);
            IntPtr ret = Marshal.AllocHGlobal(8);
            ulong outlen = 0;

            // init & verify connection
            if (NTAPI.DeviceIoControl(DriverHandle, (uint)Structures.IOCTL.Initialize, lpinBuffer, (uint)initdata.Length, ret, 8, &outlen, 0) == true)
            {
                Console.WriteLine("Initialized driver -> 0x" + deviceHandle.ToString("X"));
                Initialized = Marshal.PtrToStructure<ulong>(ret) == MT64Res;

                if (Initialized)
                {
                    OpenProcess(pid);
                }

                return;
            }
            else
            {
                Console.WriteLine("Unable to initialize driver ");
                return;
            }
        }

        public bool Unload()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                while (ComChannel.hasOpenCom == true)
                {
                    Thread.Sleep(20);
                }

                _ = NTAPI.CloseHandle(DriverHandle);
                Thread.Sleep(300);
                _ = ServiceHelper.StopService(ServiceHandle);
                _ = ServiceHelper.DeleteService(ServiceHandle);
                ServiceHelper.CloseServiceHandle(ServiceHandle);
            }).Start();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        public void OpenProcess(int pid)
        {
            EnumModule modStruct = new EnumModule()
            {
                TargetPID = (uint)pid,
                MaxLen = 301
            };

            Process.ProcessID = (uint)pid;
            Process.Modules = Dictionary.ListProcessModules(modStruct, DriverHandle, Codex);
            Process.ModulesCount = Process.Modules.Length;
            Process.ModuleMax = Process.Modules.OrderBy(x => x.Size).ToArray().Last();
            Process.ModuleMin = Process.Modules.OrderBy(x => x.Size).ToArray().First();
            Process.ModuleHigh = Process.Modules.OrderBy(x => x.BaseAddress).ToArray()[Process.Modules.Length - 1];
            Process.ModuleLow = Process.Modules.OrderBy(x => x.BaseAddress).ToArray()[0];
            Process.Executable = Process.Modules[0];

            Console.WriteLine($"Opened process " + pid);
        }
        #endregion

        #region Memory Generic

        /// <summary>
        /// Allows the user to read from an address, accepts any type except String
        /// </summary>
        public dynamic RPM<T>(dynamic address, dynamic length = null)
        {
            if (typeof(T) == typeof(byte[]))
            {
                return DefaultRPM((IntPtr)address, length == null ? 1 : (uint)length);
            }
            else if (typeof(T) != typeof(string))
            {
                uint size = (uint)Marshal.SizeOf(typeof(T));
                byte[] data = DefaultRPM((IntPtr)address, size);

                return Constructors.GetStructure<T>(data);
            }
            else
            {
                throw new Exception("fuck you");
            }
        }

        /// <summary>
        /// Underlying method for RPM
        /// </summary>
        private byte[] DefaultRPM(IntPtr address, uint length)
        {
            UserMem readStruct = new UserMem()
            {
                Mode = 0,
                TargetPID = Process.ProcessID,
                PtrOut = IntPtr.Zero,
                PtrIn = address,
                BufferLen = length
            };

            return Dictionary.UserReadWrite(readStruct, DriverHandle, Codex);
        }

        /// <summary>
        /// Allows the user to write to an address, accepts any type except String
        /// </summary>
        public void WPM<T>(dynamic address, dynamic data)
        {
            if (typeof(T) == typeof(byte[]))
                DefaultWPM((IntPtr)address, (byte[])data);
            else
            {
                int size = Marshal.SizeOf(data);
                byte[] arr = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(data, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);
                DefaultWPM((IntPtr)address, arr);
            }
        }

        /// <summary>
        /// Underlying method for WPM
        /// </summary>
        public void DefaultWPM(IntPtr address, byte[] data)
        {
            UserMem writeStruct = new UserMem()
            {
                Mode = 1,
                TargetPID = Process.ProcessID,
                PtrOut = address,
                PtrIn = IntPtr.Zero,
                BufferLen = (uint)data.Length
            };

            Dictionary.UserReadWrite(writeStruct, DriverHandle, Codex, data /* very very very important param */);
        }

        private long backloadAddr;
        private List<int[]> storedOffsets = new List<int[]>();
        private List<long> storedAddr = new List<long>();

        ///<summary>
        ///<para>Reads from process memory using an array of offsets</para>
        ///<para>Address manipulation parameter must be a string containing an integer, this can be 0</para>
        ///<para>cutOff value is -1 by default, this parameter determines at what point in the offset array addresses are stored at (for quicker memory reads)</para>
        ///<para>cutOff value should be the lowest address that changes and CANNOT be less than 3 (unless == -1)</para>
        ///<para>this method is shit and im too lazy to rework it</para>
        ///</summary>
        /// <returns>Specified type value</returns>
        public dynamic RPMCH<T>(dynamic address, string addressManipulation, int[] offsets, int cutOff = -1, dynamic length = null)
        {
            if (cutOff != -1 && storedOffsets.Contains(offsets))
            {
                address = storedAddr[storedOffsets.IndexOf(offsets)];

                if (offsets.Length > 1)
                {
                    for (int i = offsets.Length - 1 - cutOff; i < offsets.Length - 1; i++)
                    {
                        address = RPM<long>(address + offsets[i]);
                    }
                }

                return RPM<T>(address + offsets.Last() + Convert.ToInt32(addressManipulation), length);
            }

            if (address == null)
                address = backloadAddr;

            if (offsets == null)
            {
                backloadAddr = address;
                return RPM<T>(address + Convert.ToInt32(addressManipulation), length);
            }

            if (offsets.Length > 0)
            {
                for (int i = 0; i < offsets.Length - 1; i++)
                {
                    address = RPM<long>(address + offsets[i]);

                    if (cutOff != -1 && i == offsets.Length - 2 - cutOff)
                    {
                        storedAddr.Add(address);
                    }

                    if (address == 0)
                    {
                        Console.WriteLine(string.Join(", ", offsets) + " failed!");
                    }
                }
            }
            else
            {
                return default(T);
            }

            backloadAddr = address;

            long finalAddr = address + offsets.Last();
            storedOffsets.Add(offsets);

            return RPM<T>(finalAddr + Convert.ToInt32(addressManipulation), length);
        }

        ///<summary>
        ///<para>Writes to process memory using an array of offsets</para>
        ///<para>Address manipulation parameter must be a string containing an integer, this can be 0</para>
        ///<para>cutOff value is -1 by default, this parameter determines at what point in the offset array addresses are stored at (for quicker memory reads)</para>
        ///<para>cutOff value should be the lowest address that changes and CANNOT be less than 3 (unless == -1)</para>
        ///<para>this method is shit and im too lazy to rework it</para>
        ///</summary>
        public void WPMCH<T>(dynamic address, dynamic value, string addressManipulation, int[] offsets, int cutOff = -1)
        {
            if (cutOff != -1 && storedOffsets.Contains(offsets))
            {
                address = storedAddr[storedOffsets.IndexOf(offsets)];

                if (offsets.Length > 1)
                {
                    for (int i = offsets.Length - 1 - cutOff; i < offsets.Length - 1; i++)
                    {
                        address = RPM<long>(address + offsets[i]);
                    }
                }

                WPM<T>(address + offsets.Last() + Convert.ToInt32(addressManipulation), value);
            }

            if (address == null)
                address = backloadAddr;

            if (offsets == null)
            {
                backloadAddr = address;
                WPM<T>(address + Convert.ToInt32(addressManipulation), value);

                return;
            }

            if (offsets.Length > 0)
            {
                for (int i = 0; i < offsets.Length - 1; i++)
                {
                    address = RPM<long>(address + offsets[i]);

                    if (cutOff != -1 && i == offsets.Length - 2 - cutOff)
                        storedAddr.Add(address);
                }
            }

            backloadAddr = address;

            long finalAddr = address + offsets.Last();
            storedOffsets.Add(offsets);

            WPM<T>(finalAddr + Convert.ToInt32(addressManipulation), value);
        }

        #endregion
    }
}
