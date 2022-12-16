using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mihoyo.Internal
{
    public static class Structures
    {
        public enum IOCTL : uint
        {
            Initialize = 0x80034000,
            KernelMap = 0x83064000,
            UserMap = 0x81074000,
            ListProcessModules = 0x81054000,
            ListDrivers = 0x82024000,
            TerminateProcess = 0x81034000
        }

        public enum Architecture : uint
        {
            x64 = 0,
            x86 = 1,
            x32 = 2,
            ARM64 = 3,
            ARM32 = 4
        }

        [Flags]
        public enum MemoryFlags : uint
        {
            NoAccess = 0,
            ReadOnly = 1,
            ReadWrite = 2,
            Invalid = 3
        }

        public enum AllocationType : uint
        {
            TopDown = 0,
            Executable = 1,
            Module = 2,
            Invalid = 3,
            Static = 4,
            Generic = 5
        }

        public struct Dump
        {
            public byte[] Data;
            public MemLoc MemLoc;
            public MemoryFlags MemoryFlags;
            public AllocationType AllocationType;
            public long Size;
            public int Index;
        }

        public struct UserMem
        {
            public uint Mode;
            public uint Padding0;
            public uint TargetPID;
            public uint Padding1;
            public IntPtr PtrOut;
            public IntPtr PtrIn; // memory address to r/w at
            public uint BufferLen;
            public uint Padding2;
        }

        public struct KernelMem
        {
            public uint NTStatus;
            public IntPtr PtrIn;
            public uint BufferLen;
        }

        public struct EnumModule
        {
            public uint TargetPID;
            public uint MaxLen;
        }

        public struct EnumThreads
        {
            public uint ValidationCode;
            public uint TargetPID;
            public uint OwnerPID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public struct MhyDriver
        {
            public uint Status;
            public uint Count;
            public IntPtr Addr1;
            public IntPtr Addr2;
            public IntPtr Addr3;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public struct MhyModule
        {
            public IntPtr BaseAddress;
            public uint Size;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Name;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Path;

            public uint Padding0;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public struct MhyThread
        {
            public IntPtr KernelAddress;
            public IntPtr BaseAddress;
            public bool unk;
        }

        public struct MhyProcess
        {
            public uint ProcessID;
            public string Name;
            public IntPtr EProcess;
            public Architecture Architecture;
            public MhyModule[] Modules;
            public MhyModule ModuleMax;
            public MhyModule ModuleMin;
            public MhyModule ModuleHigh;
            public MhyModule ModuleLow;
            public MhyModule Executable;
            public int ModulesCount;
        }
    }
}
