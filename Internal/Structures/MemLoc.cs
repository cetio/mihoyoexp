using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mihoyo.Internal.Structures;

namespace Mihoyo.Internal
{
    public class MemLoc
    {
        private long address;
        private byte initialValue;
        private MhyLib lib;
        private MemoryFlags flags;
        private AllocationType allocType = AllocationType.Invalid;

        public static byte[] appSig1 = new byte[] { 0x4D, 0x5A };
        public static byte[] appSig2 = new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0x6B };
        public static byte[] certSig = new byte[] { 0x23, 0x0D, 0x0A, 0x23 };
        // ...
        public static byte[][] sigs = new byte[][] { appSig1, appSig2, certSig };

        public MemLoc(long address_, MhyLib lib_)
        {
            address = address_;
            lib = lib_;
            initialValue = lib.RPM<byte>(address);

            byte[] headingDump = lib.RPM<byte[]>(address, 6);

            foreach (byte[] sig in sigs)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (sig[i] == headingDump[i])
                    {
                        if (i == sig.Length - 1 && sig == appSig1)
                            allocType = Structures.AllocationType.Module;
                        else if (sig == appSig2 || AssociatedModule == null)
                            allocType = Structures.AllocationType.Executable;
                        else if (sig == certSig)
                            allocType = Structures.AllocationType.Static;
                    }
                    else
                        break;
                }
            }

            if (headingDump.Length != 0 && allocType == Structures.AllocationType.Invalid)
                allocType = Structures.AllocationType.Generic;

            if (initialValue == 0)
                lib.WPM<byte>(address, 1);
            else
                lib.WPM<byte>(address, initialValue - 1);

            if (lib.RPM<byte>(address) == initialValue)
                flags = Structures.MemoryFlags.ReadOnly;
            else
            {
                flags = Structures.MemoryFlags.ReadWrite;
                lib.WPM<byte>(address, initialValue);
            }
        }

        public virtual long Address
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

        public MemoryFlags MemoryFlags
        {
            get
            {
                return flags;
            }
        }

        public AllocationType AllocationType
        {
            get
            {
                return allocType;
            }
        }

        public MhyLib AllocatedLib
        {
            get
            {
                return lib;
            }
            set
            {
                lib = value;
            }
        }

        public void Write<T>(dynamic value)
        {
            lib.WPM<T>(address, value);
        }

        public dynamic Read<T>()
        {
            return lib.RPM<T>(address);
        }

        public MhyModule? AssociatedModule
        {
            get
            {
                foreach (MhyModule module in lib.Process.Modules)
                {
                    if (address >= (long)module.BaseAddress && address <= (long)module.BaseAddress + module.Size)
                        return module;
                }

                return null;
            }
            set
            {
                throw new Exception("Attempted to modify an immutable (MemLoc.AssociatedModule)");
            }
        }
    }
}