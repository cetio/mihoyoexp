/*using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Mihoyo.Internal;
using Mihoyo.Internal.IOCTL;
using Mihoyo.Internal.Structures;

// a lot of names for stuff is legacy from the original project (https://github.com/kagurazakasanae/Mhyprot2DrvControl/) or from when I picked up this project at first, I wasn't very experienced
namespace Mihoyo.Renovation
{
    public unsafe class Driver
    {
        private IntPtr g_ServiceHandle;
        private IntPtr drvHandle;
        public ulong seed;
        public ulong pid;
        private bool stop;
        private static ulong mt64res;
        private IOCTL driverPage;
        public bool init = false;
        public MhyProtProcess Proc;

        #region Init-Exit

        ///<summary>
        ///<para>Loads the mhyprot2.sys driver, first step</para>
        ///</summary

        public void InitMt64()
        {
            driverPage.m.rand_mt64_init(seed);
            int i = 7;
            do
            {
                mt64res = driverPage.m.rand_mt64_get();
            } while (--i != 0);
            driverPage.isInit = true;
        }

        public byte[] GenInitData(ulong pid, ulong seed)
        {
            byte[] data = new byte[0x10];
            ulong PidData = 0xBAEBAEEC00000000 + pid;
            ulong LOW = seed ^ 0xEBBAAEF4FFF89042;
            ulong HIGH = seed ^ PidData;
            Array.Copy(BitConverter.GetBytes(HIGH), 0, data, 0, 8);
            Array.Copy(BitConverter.GetBytes(LOW), 0, data, 8, 8);
            this.seed = seed;
            InitMt64();
            return data;
        }

        public bool ToggleOperations()
        {
            ComChannel.dontPushCom = !ComChannel.dontPushCom;
            return true;
        }

        #endregion Init-Exit

        public dynamic KernelRead<T>(dynamic address, dynamic length, bool unicodeString = false)
        {
            if (typeof(T) == typeof(byte[]))
            {
                return DefaultKernelRead((IntPtr)address, (uint)length);
            }
            else if (typeof(T) != typeof(string))
            {
                uint size = (uint)Marshal.SizeOf(typeof(T));
                byte[] data = DefaultKernelRead((IntPtr)address, size);
                return Constructors.GetStructure<T>(data);
            }
            else
            {
                byte[] numArray = DefaultRPM((IntPtr)address, 255);
                string str = unicodeString ? Encoding.Unicode.GetString(numArray) : Encoding.Default.GetString(numArray);
                if (str.Contains("\0"))
                {
                    str = str.Substring(0, str.IndexOf('\0'));
                }

                return str;
            }
        }

        public byte[] DefaultKernelRead(IntPtr address, uint length)
        {
            IntPtr readptr = Marshal.AllocHGlobal((IntPtr)length);
            uint read = driverPage.KernelRead(address, length);
            byte[] returnData = Constructors.PtrToByte(readptr, read);
            return returnData;
        }

        public int[] ByteArrayToIntArray(byte[] array)
        {
            List<int> newArr = new List<int>();

            foreach (byte b in array)
            {
                newArr.Add(b);
            }

            return newArr.ToArray();
        }

        public byte[] IntArrayToByteArray(int[] array)
        {
            List<byte> newArr = new List<byte>();

            foreach (int i in array)
            {
                newArr.Add((byte)i);
            }

            return newArr.ToArray();
        }

        public int[] FormPattern(dynamic pattern, bool isHex)
        {
            List<int> patternBytes = new List<int>();

            if (pattern is string)
            {
                if (((string)pattern).StartsWith("-mhy:"))
                {
                    pattern = ((string)pattern).Remove(0, 5);
                    patternBytes.AddRange(Encoding.Unicode.GetBytes(pattern));
                }
                else
                {
                    char[] patternArray = (pattern + ' ').ToCharArray();
                    string buffer = string.Empty;

                    for (int i = 0; i < patternArray.Length; i++)
                    {
                        char c = patternArray[i];

                        if (c == ',')
                        {
                            continue;
                        }
                        else if (c == ' ' || c == '/' || c == '\\' || c == '-')
                        {
                            buffer = buffer.Replace('x', '0');

                            if (buffer.Length == 0 && (i == 0 || i == patternArray.Length - 1))
                            {
                                continue;
                            }

                            while (buffer.Length < 2)
                            {
                                buffer = $"0x0{buffer}";
                            }

                            if (buffer.Contains("-1"))
                            {
                                patternBytes.Add(-1);
                                buffer = string.Empty;
                                continue;
                            }

                            if (isHex == true)
                            {
                                patternBytes.Add(Convert.ToInt32(buffer, 16));
                                buffer = string.Empty;
                                continue;
                            }

                            patternBytes.Add(Convert.ToInt32(buffer));
                            buffer = string.Empty;
                        }
                        else if (c == '?')
                        {
                            buffer += -1;
                        }
                        else
                        {
                            buffer += c;
                        }
                    }
                }
            }
            else if (pattern is byte[])
                patternBytes.AddRange(pattern);
            else if (pattern is float)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((float)pattern)));
            else if (pattern is double)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((double)pattern)));
            else if (pattern is int)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((int)pattern)));
            else if (pattern is long)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((long)pattern)));
            else if (pattern is uint)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((uint)pattern)));
            else if (pattern is ulong)
                patternBytes.AddRange(ByteArrayToIntArray(BitConverter.GetBytes((ulong)pattern)));

            return patternBytes.ToArray();
        }

        private int pool = 0;
        private List<MemoryAddress> tmpAddrPool = new List<MemoryAddress>();
        public List<MemoryAddress> AoBSplitThreads(int threads, dynamic pattern, bool reportUnwritables, bool isHex, long startAddress, long endAddress, int expectedResults, int divisor = 1, DataGridView optDataTable = null)
        {
            List<MemoryAddress> addr = new List<MemoryAddress>();
            startAddress /= threads - 1;
            endAddress /= threads - 1;
            for (int i = 0; i < threads; i++)
            {
                Console.WriteLine("Created new thread");
                Thread t = new Thread(() => AoB(pattern, reportUnwritables, isHex, startAddress * i, endAddress * i, expectedResults, divisor, optDataTable));
                t.Start();
                Thread.Sleep(600);
            }

            return tmpAddrPool;
        }

        ///<summary>
        ///<para>Scans process memory for input pattern (can be signature or array of byte in any common format)</para>
        ///<para>expectedResults should be the number of results you can accurately expect to get from the scan, set this to sub-1 if you do not think you can get an accurate count or are using unique scanning</para>
        ///<para>Example patterns: "0x99 0xA6 0x46 0x5A 0x4D", "99 A6 46 5A 4D", "x99/xA6/x46/x5A/x4D", etc, also supports non string formats like; new byte[] { 99, A6, 46, 5A, 4D }, 4f, 88, false, etc</para>
        ///</summary>
        /// <returns>List of addresses or single address (first found)</returns>
        public List<MemoryAddress> AoB(dynamic pattern, bool unique, bool isHex, string module, int expectedResults, int divisor = 1)
        {
            foreach (MhyProtEnumModule _module in driverPage.process.Modules)
            {
                if (_module.ModuleName.ToLower().Contains(module.ToLower()))
                {
                    return AoB(pattern, unique, isHex, (long)_module.BaseAddress, (long)_module.BaseAddress + _module.SizeOfImage, expectedResults, divisor);
                }
            }

            return null;
        }

        public List<MemoryAddress> AoB(dynamic pattern, bool reportUnwritables, bool isHex, long startAddress, long endAddress, int expectedResults, int divisor = 1, DataGridView optDataTable = null)
        {
            // create pattern

            int[] convertedByteArray = FormPattern(pattern, isHex);

            // init some scan variables

            long totalBytes = endAddress - startAddress;
            uint minSize = 0x800;
            uint scanSize = minSize * 16;
            long concurrentScanChunks = scanSize * 16;

            while (true) // while scanning
            {
                if (concurrentScanChunks == 0)
                {
                    concurrentScanChunks = 16 * scanSize;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(50);
                }

                byte[] dumpedBytes = DefaultRPM((IntPtr)startAddress, scanSize);

                if (startAddress > endAddress - scanSize - 1 || ComChannel.dontPushCom)
                    goto reportFinish;

                for (int i = 0; i < dumpedBytes.Length; i++)
                {
                    startAddress++;

                    if (divisor > 1 && startAddress % divisor - 1 != 0)
                        continue;

                    if (dumpedBytes[i + convertedByteArray.Length - 1] != convertedByteArray.Last())
                        continue;

                    for (int x = 0; x < convertedByteArray.Length; x++)
                    {
                        if (dumpedBytes[i + x] != convertedByteArray[x] && convertedByteArray[x] != -1)
                            break;

                        if (x == convertedByteArray.Length - 1)
                        {
                            MemoryAddress memaddr = new MemoryAddress(startAddress - 1, this);

                            if (reportUnwritables == false && !memaddr.IsWritable)
                                break;

                            tmpAddrPool.Add(memaddr);

                            if (expectedResults > 0 && tmpAddrPool.Count == expectedResults)
                                goto reportFinish;
                        }
                    }

                    concurrentScanChunks--;
                }

                if (dumpedBytes.Length != scanSize)
                {
                    if (startAddress % minSize != 0)
                        startAddress += minSize - startAddress % minSize;
                    else
                        startAddress += minSize;
                }

                Thread.Sleep(3);
            }

        reportFinish:
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine("Completed " + tmpAddrPool.Count);
            pool += 1;
            return tmpAddrPool;
        }

        ///<summary>
        ///<para>Common RPM (read process memory), scans memory at the given address and returns the value found</para>
        ///</summary>
        /// <returns>Value at address, in specified type</returns>
        public dynamic RPM<T>(dynamic address, dynamic length = null)
        {
            if (typeof(T) == typeof(byte[]))
                return DefaultRPM((IntPtr)address, (uint)length);
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
            else
            {
                uint strlen = 255;

                if (length != null)
                {
                    strlen = (uint)length;
                }

                byte[] numArray = DefaultRPM((IntPtr)address, strlen);
                string str = unicodeString ? Encoding.Unicode.GetString(numArray) : Encoding.Default.GetString(numArray);
                if (str.Contains("\0"))
                {
                    str = str.Substring(0, str.IndexOf('\0'));
                }

                return str;
            }
        }

        private byte[] DefaultRPM(IntPtr address, uint length)
        {
            IntPtr _buff = new IntPtr(length);
            IntPtr readPtr = Marshal.AllocHGlobal(_buff);
            uint read = driverPage.UserRW(0, readPtr, address, length);
            return Constructors.PtrToByte(readPtr, read);
        }

        ///<summary>
        ///<para>Common WPM (write process memory), writes memory at the given address</para>
        ///</summary>
        public uint WPM<T>(dynamic address, T INPdata)
        {
            if (typeof(T) == typeof(byte[]))
                return DefaultWPM((IntPtr)address, INPdata);
            else
            {
                int size = Marshal.SizeOf(INPdata);
                byte[] arr = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(INPdata, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);
                return DefaultWPM((IntPtr)address, arr);
            }
        }

        public uint DefaultWPM(IntPtr address, dynamic data)
        {
            if (ComChannel.dontPushCom)
                return 0;

            IntPtr writeptr = Constructors.ByteToPtr((byte[])data);
            return driverPage.UserRW(1, address, writeptr, (uint)((byte[])data).Length);
        }

        ///<summary>
        ///<para>World to screen, not even sure if this works, I've never used it</para>
        ///</summary>
        /// <returns>Bool of something, too lazy to read the code</returns>
        public static bool W2S(Vector3 pos, out Vector2 screen, System.Drawing.Size OverlaySize, Matrix4x4 viewMatrix)
        {
            Vector4 clipCoords;
            clipCoords.X = pos.X * viewMatrix.M11 + pos.Y * viewMatrix.M12 + pos.Z * viewMatrix.M13 + viewMatrix.M14;
            clipCoords.Y = pos.X * viewMatrix.M21 + pos.Y * viewMatrix.M22 + pos.Z * viewMatrix.M23 + viewMatrix.M24;
            clipCoords.Z = pos.X * viewMatrix.M31 + pos.Y * viewMatrix.M32 + pos.Z * viewMatrix.M33 + viewMatrix.M34;
            clipCoords.W = pos.X * viewMatrix.M41 + pos.Y * viewMatrix.M42 + pos.Z * viewMatrix.M43 + viewMatrix.M44;
            screen = new Vector2(0, 0);
            if (clipCoords.W < 0.1)
            {
                return false;
            }

            Vector3 NDC;
            NDC.X = clipCoords.X / clipCoords.W;
            NDC.Y = clipCoords.Y / clipCoords.W;
            NDC.Z = clipCoords.Z / clipCoords.W;

            screen.X = OverlaySize.Width / 2 * NDC.X + (NDC.X + OverlaySize.Width / 2);
            screen.Y = -(OverlaySize.Height / 2 * NDC.Y) + (NDC.Y + OverlaySize.Height / 2);
            return true;
        }

        private List<long> codelistAddr = new List<long>();
        private List<byte> codelistValue = new List<byte>();

        private long backloadAddr;
        private List<int[]> storedOffsets = new List<int[]>();
        private List<long> storedAddr = new List<long>();

        ///<summary>
        ///<para>Reads from process memory using an array of offsets</para>
        ///<para>Address manipulation parameter must be a string containing an integer, this can be 0</para>
        ///<para>cutOff value is -1 by default, this parameter determines at what point in the offset array addresses are stored at (for quicker memory reads)</para>
        ///<para>cutOff value should be the lowest address that changes and CANNOT be less than 3 (unless == -1)</para>
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

        ///<summary>
        ///<para>NOPs (strips operation) of opcode in specified range</para>
        ///<para>Mode 0 will use the driver to NOP the opcode, mode 1 will use your process</para>
        ///</summary>
        public void NOP(dynamic startAddress, dynamic endAddress, int mode)
        {
            List<long> _addresses = new List<long>();

            for (int i = 0; i < endAddress - startAddress + 1; i++)
            {
                if (!codelistAddr.Contains(startAddress + i))
                {
                    codelistAddr.Add(startAddress + i);
                    codelistValue.Add(RPM<byte>(startAddress + i));
                    _addresses.Add(startAddress + i);
                }
            }

            if (mode == 0)
            {
                foreach (long _address in _addresses)
                    WPM<byte>(_address, 144);
            }

            if (mode == 1)
            {
                foreach (long _address in _addresses)
                    WPMDUP(_address, new byte[] { 144 });
            }
        }

        ///<summary>
        ///<para>REPs (replaces operation) of opcode in specified range</para>
        ///<para>Mode 0 will use the driver to REP the opcode, mode 1 will use your process</para>
        ///</summary>
        public void REP(dynamic startAddress, dynamic endAddress, byte[] values, int mode)
        {
            List<long> _addresses = new List<long>();

            for (int i = 0; i < endAddress - startAddress + 1; i++)
            {
                if (!codelistAddr.Contains(startAddress + i))
                {
                    codelistAddr.Add(startAddress + i);
                    codelistValue.Add(RPM<byte>(startAddress + i));
                    _addresses.Add(startAddress + i);
                }
            }

            if (mode == 0)
            {
                for (int i = 0; i < endAddress - startAddress + 1; i++)
                    WPM(_addresses[i], values[i]);
            }

            if (mode == 1)
            {
                for (int i = 0; i < endAddress - startAddress + 1; i++)
                    WPMDUP(_addresses[i], new byte[] { values[i] });
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400,
            All = 2035711
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern int CloseHandle(IntPtr hProcess);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        ///<summary>
        ///<para>RSTs (restores operation) of opcode in specified range</para>
        ///<para>Mode 0 will use the driver to REP the opcode, mode 1 will use your process</para>
        ///</summary>
        public void RST(dynamic startAddress, dynamic endAddress, int mode)
        {
            if (mode == 0)
            {
                for (int i = 0; i < endAddress - startAddress + 1; i++)
                {
                    for (int e = 0; e < codelistAddr.Count; e++)
                    {
                        if (codelistAddr[e] == startAddress + i)
                        {
                            WPM<byte>(startAddress + i, codelistValue[e]);
                            codelistAddr.RemoveAt(e);
                            codelistValue.RemoveAt(e);
                        }
                    }
                }
            }

            if (mode == 1)
            {
                for (int i = 0; i < endAddress - startAddress + 1; i++)
                {
                    for (int e = 0; e < codelistAddr.Count; e++)
                    {
                        if (codelistAddr[e] == startAddress + i)
                        {
                            WPMDUP(startAddress + i, new byte[] { codelistValue[e] });
                            codelistAddr.RemoveAt(e);
                            codelistValue.RemoveAt(e);
                        }
                    }
                }
            }
        }

        ///<summary>
        ///<para>Uses process to write to memory address, only accepts byte arrays</para>
        ///</summary>
        public void WPMDUP(dynamic address, byte[] data)
        {
            var hProc = OpenProcess(ProcessAccessFlags.All, false, (int)driverPage.process.ProcessId);

            WriteProcessMemory(hProc, new IntPtr(address), data, (uint)data.Length, out _);

            CloseHandle(hProc);
        }
    }
}*/