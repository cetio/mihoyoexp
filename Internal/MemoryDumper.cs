using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static Mihoyo.Internal.Structures;

namespace Mihoyo.Internal
{
    public static class MemoryDumper
    {
        public static Dump[] CreateDump(this MhyLib library)
        {
            Dump[] dumps = new Dump[512];
            int dumpIndexer = 0;

            long baseAddress = (long)library.Process.ModuleLow.BaseAddress / 10;
            long endAddress = (long)library.Process.ModuleHigh.BaseAddress;

            long enumeratorSize = (long)(0x1600 * ModByClamp(baseAddress, endAddress));

            for (long init = baseAddress; init < endAddress; init += enumeratorSize)
            {
                if (dumpIndexer == 2)
                    enumeratorSize = dumps[1].MemLoc.Address - dumps[0].MemLoc.Address;

                if (library.RPM<byte[]>(init, 1).Length != 0) // if there is data present
                {
                    init = init - QueryOffset(library, init); // get the header/start of memory region
                    Console.WriteLine("Found new region @ " + init);

                    long size = QuerySize(library, init);
                    byte[] data = library.RPM<byte[]>(init, size);
                    MemLoc memLoc = new MemLoc(init, library);

                    Dump dump = new Dump()
                    {
                        Data = data,
                        MemLoc = memLoc,
                        Size = size,
                        MemoryFlags = memLoc.MemoryFlags,
                        AllocationType = memLoc.AllocationType,
                        Index = dumpIndexer
                    };

                    dumps[dumpIndexer++] = dump;
                    init += size;
                }
            }

            return dumps;
        }

        private static float ModByClamp(long baseAddress, long endAddress)
        {
            float init = (float)endAddress / (float)baseAddress;

            while (init > 1f) // prevent numbers over 1, only want decimals
            {
                init /= 10f;
            }

            return init + 1f; // dont want to reduce size, only increase it
        }

        private static long QuerySize(MhyLib library, long address)
        {
            while (true)
            {
                long enumeratorSize = 0x800;

                if (library.RPM<byte[]>(address, enumeratorSize).Length != enumeratorSize)
                    return enumeratorSize - 0x800;
                else
                    enumeratorSize += 0x800;
            }
        }

        private static long QueryOffset(MhyLib library, long address)
        {
            while (true)
            {
                address = address - address % 0x800;
                long enumeratorSize = 0x800;

                if (library.RPM<byte[]>(address - enumeratorSize, 1).Length == 0)
                    return enumeratorSize + 0x800;
            }
        }

    }
}