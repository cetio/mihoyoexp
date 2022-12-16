using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Mihoyo.Internal.Structures;

namespace Mihoyo.Internal.IOCTL
{
    public static class Dictionary
    {
        public static byte[] KernelRead(KernelMem data, IntPtr driverHandle, MT64 codex)
        {
            if ((long)data.PtrIn < 0)
                return new byte[0];

            return ComChannel.Create(data, driverHandle, (uint)Structures.IOCTL.KernelMap, codex, 12);
        }

        public static byte[] UserReadWrite(UserMem data, IntPtr driverHandle, MT64 codex, dynamic writeBytes = null)
        {
            if ((long)data.PtrIn < 0)
                return new byte[0];

            if (data.Mode == 0)
            {
                IntPtr _buff = new IntPtr(data.BufferLen);
                IntPtr readPtr = Marshal.AllocHGlobal(_buff);
                data.PtrOut = readPtr;
                uint outLen = BitConverter.ToUInt32(ComChannel.Create(data, driverHandle, (uint)Structures.IOCTL.UserMap, codex, 12), 0);
                return Constructors.PtrToByte(readPtr, outLen);
            }
            else
            {
                IntPtr writePtr = Constructors.ByteToPtr(writeBytes);
                data.PtrIn = writePtr;
                ComChannel.Create(data, driverHandle, (uint)Structures.IOCTL.UserMap, codex, 12);
                return new byte[0];
            }
        }

        public static bool KillProcess(int pid, IntPtr driverHandle, MT64 codex)
        {
            byte[] retdata = ComChannel.Create(pid, driverHandle, (uint)Structures.IOCTL.ListProcessModules, codex, 12);
            return BitConverter.ToUInt32(retdata, 0) == 0;
        }

        public static MhyModule[] ListProcessModules(EnumModule data, IntPtr driverHandle, MT64 codex)
        {
            byte[] comChannel = ComChannel.Create(data, driverHandle, (uint)Structures.IOCTL.ListProcessModules, codex, 301 /* max index */  * 792);
            uint count = BitConverter.ToUInt32(comChannel, 0);

            List<MhyModule> modules = new List<MhyModule>();
            for (int i = 0; i < count; i++)
            {
                byte[] singlemodule = new byte[792];
                Array.Copy(comChannel, 4 + i * 792, singlemodule, 0, 792);
                modules.Add(Constructors.ByteToStructure<MhyModule>(singlemodule));
            }

            return modules.ToArray();
        }
    }
}
