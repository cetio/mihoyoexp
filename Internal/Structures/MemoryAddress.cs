using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mihoyo.Internal.Structures;

namespace Mihoyo.Internal
{
    public class MemoryAddress
    {
        private long address;
        private bool isWritable;
        private byte initialValue;
        private MemoryFlags memFlags;
        private AllocationType memType;
        private MhyLib library;

        public MemoryAddress(long address_, MhyLib library_)
        {
            address = address_;
            isWritable = true;
            library = library_;
            initialValue = library.RPM<byte>(address);
            memFlags = MemoryFlags.ReadWrite;
            memType = AllocationType.Generic;

            byte writeByte = 0;

            if (initialValue == 255)
                writeByte = 254;
            else
                writeByte = (byte)(initialValue + 1);

            library.WPM<byte>(address, writeByte);

            if (library.RPM<byte>(address) == writeByte)
                library.WPM<byte>(address, initialValue);
            else
            {
                memFlags = MemoryFlags.ReadOnly;
                isWritable = false;
            }
        }

        public void UpdateMemType(byte[] dumpHeader)
        {
            if (dumpHeader.Length == 0)
            {
                memType = AllocationType.Invalid;
                return;
            }

            if ((dumpHeader[0] == 0x4D && dumpHeader[1] == 0x5A && (dumpHeader[2] == 0x78 || dumpHeader[2] == 0x90)) ||
                (dumpHeader[1] == 0xCC && dumpHeader[2] == 0xCC && dumpHeader[3] == 0xCC && dumpHeader[4] == 0xCC && dumpHeader[5] == 0xCC && dumpHeader[6] == 0x6B))
            {
                MhyModule? module = AssociatedModule;

                if (module == null)
                    memType = AllocationType.Executable;
                else if (module.Value.Name.Contains("dll"))
                    memType = AllocationType.Module;
                else
                    memType = AllocationType.Executable;

            }
            else if ((dumpHeader[2] == 0x0C && dumpHeader[3] == 0x4D && dumpHeader[4] == 0x45) ||
                (dumpHeader[1] == 0x23 && dumpHeader[2] == 0x0D && dumpHeader[3] == 0x0A && dumpHeader[4] == 0x23)) // intended to catch xml/fetch data & certs
                memType = AllocationType.Static;
        }

        public long Address
        {
            get
            {
                return address;
            }
            set
            {
                address = value;
            }
        }

        public bool IsWritable
        {
            get
            {
                return isWritable;
            }
        }

        public AllocationType AllocationType
        {
            get
            {
                return memType;
            }
        }

        public MemoryFlags MemoryFlags
        {
            get
            {
                return memFlags;
            }
        }

        public byte CurrentValue
        {
            get
            {
                return library.RPM<byte>(address);
            }
        }

        public char CurrentAsciiValue
        {
            get
            {
                return (char)library.RPM<byte>(address);
            }
        }

        public byte InitialValue
        {
            get
            {
                return initialValue;
            }
            set
            {
                initialValue = value;
            }
        }

        public Dump? AssociatedDump
        {
            get
            {
                /*foreach (Dump dump in Dumps)
                {
                    if (address >= dump.MemoryAddress.Address && address <= dump.MemoryAddress.Address + dump.Size)
                        return dump;
                }*/

                return null;
            }
        }

        public MhyModule? AssociatedModule
        {
            get
            {
                foreach (MhyModule module in library.Process.Modules)
                {
                    if (address >= (long)module.BaseAddress && address <= (long)module.BaseAddress + module.Size)
                        return module;
                }

                return null;
            }
        }

        public void Zero()
        {
            library.WPM<byte>(address, 0);
        }

        public void Restore()
        {
            library.WPM<byte>(address, initialValue);
        }

        public static MemoryAddress operator /(MemoryAddress a, MemoryAddress b)
        => new MemoryAddress(a.Address / b.Address, a.library);

        public static MemoryAddress operator -(MemoryAddress a, MemoryAddress b)
        => new MemoryAddress(a.Address - b.Address, a.library);

        public static MemoryAddress operator +(MemoryAddress a, MemoryAddress b)
        => new MemoryAddress(a.Address + b.Address, a.library);

        public static MemoryAddress operator *(MemoryAddress a, MemoryAddress b)
        => new MemoryAddress(a.Address * b.Address, a.library);
    }
}