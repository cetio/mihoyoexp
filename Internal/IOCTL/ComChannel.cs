using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mihoyo.Internal.IOCTL
{
    public unsafe class ComChannel
    {
        public static bool dontPushCom = false;
        public static bool hasOpenCom = false;
        private static MT64 codex;

        /// <summary>
        /// Initializes a ComChannel and sends back the communication sent from *X*.
        /// </summary>
        public static byte[] Create<T>(T data, IntPtr driverHandle, uint request, MT64 mtcodex, uint bytesReturned)
        {
            if (dontPushCom)
                return new byte[4] { 0, 0, 2, 4 }; // blaze it?

            hasOpenCom = true;

            // init request
            codex = mtcodex;
            IntPtr ret = Marshal.AllocHGlobal((int)bytesReturned);
            byte[] reqdata = Encrypt(Constructors.StructureToByte(data), 0x233333333333);
            IntPtr lpinBuffer = Constructors.ByteToPtr(reqdata);
            ulong outlen = 0;

            // check if request happened
            if (!NTAPI.DeviceIoControl(driverHandle, request, lpinBuffer, (uint)reqdata.Length, ret, bytesReturned, &outlen, 0))
            {
                Console.WriteLine("IOCTL communication failed, the requested communication may be unavailable.");
                hasOpenCom = false;
                return new byte[0];
            }
            else
                Marshal.FreeHGlobal(lpinBuffer);

            // return recieved data, has to be decrypted
            byte[] decryptedCom = Decrypt(Constructors.PtrToByte(ret, (uint)outlen));
            hasOpenCom = false;
            return decryptedCom;
        }

        private static byte[] Encrypt(byte[] data, ulong ts)
        {
            codex.mt.index = 0;
            codex.mt.decodeKey = ts;
            byte[] endata = MT64.MT64Cryptor(data, codex);
            byte[] ret = new byte[endata.Length + 8];
            Array.Copy(BitConverter.GetBytes(ts), ret, 8);
            Array.Copy(endata, 0, ret, 8, endata.Length);
            return ret;
        }

        private static byte[] Decrypt(byte[] data)
        {
            ulong ts = BitConverter.ToUInt64(data, 0);
            byte[] endata = new byte[data.Length - 8];
            Array.Copy(data, 8, endata, 0, data.Length - 8);
            codex.mt.index = 0;
            codex.mt.decodeKey = ts;
            return MT64.MT64Cryptor(endata, codex);
        }
    }
}