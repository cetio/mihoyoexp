namespace Mihoyo.Internal
{
    public class MT64
    {
        public struct rand_mt64
        {
            public ulong[] array;
            public ulong index;
            public ulong decodeKey;
        };

        public rand_mt64 mt;

        public MT64()
        {
            mt = new rand_mt64
            {
                array = new ulong[312]
            };
        }

        public void InitMt64(ref ulong mt64res, ref ulong seed)
        {
            rand_mt64_init(seed);
            int i = 7;
            do
            {
                mt64res = rand_mt64_get();
            } while (--i != 0);
            // initialized
        }

        public byte[] GenInitData(ulong pid, ref ulong seed, ref ulong mt64res)
        {
            byte[] data = new byte[0x10];
            ulong PidData = 0xBAEBAEEC00000000 + pid;
            ulong LOW = seed ^ 0xEBBAAEF4FFF89042;
            ulong HIGH = seed ^ PidData;
            Array.Copy(BitConverter.GetBytes(HIGH), 0, data, 0, 8);
            Array.Copy(BitConverter.GetBytes(LOW), 0, data, 8, 8);
            InitMt64(ref mt64res, ref seed);
            return data;
        }

        public static byte[] MT64Cryptor(byte[] data, MT64 m)
        {
            byte[] ret = new byte[data.Length];
            int EncryptRound = data.Length >> 3;
            int i = 0;
            if (EncryptRound > 0)
            {
                ulong offset = 0;
                do
                {
                    ulong randNum = m.rand_mt64_get();
                    ulong v14 = m.mt.decodeKey + offset;
                    offset += 16;
                    ulong thisdata = BitConverter.ToUInt64(data, i * 8);
                    ulong outdata = v14 ^ randNum ^ thisdata;
                    Array.Copy(BitConverter.GetBytes(outdata), 0, ret, i * 8, 8);
                    m.mt.index %= 312;
                    ++i;
                } while (i < EncryptRound);
                return ret;
            }
            else
            {
                return data;
            }
        }

        public void rand_mt64_init(ulong seed)
        {
            ulong f = 0x5851f42d4c957f2d;
            ulong prev_value = seed;
            mt.index = 312;
            mt.array[0] = prev_value;
            for (ulong i = 1; i < 312; i += 1)
            {
                prev_value = i + f * (prev_value ^ prev_value >> 62);
                mt.array[i] = prev_value;
            }
        }

        public ulong rand_mt64_get()
        {
            ulong m = 156;
            ulong n = 312;
            ulong[] mag01 = new ulong[2] { 0, 0xB5026F5AA96619E9 };
            ulong UM = 0xFFFFFFFF80000000;
            ulong LM = 0x7FFFFFFF;
            ulong x;

            if (mt.index >= n)
            {
                ulong i;

                for (i = 0; i < n - m; i += 1)
                {
                    x = mt.array[i] & UM | mt.array[i + 1] & LM;
                    mt.array[i] = mt.array[i + m] ^ x >> 1 ^
                        mag01[x & 0x1];
                }
                for (; i < n - 1; i += 1)
                {
                    x = mt.array[i] & UM | mt.array[i + 1] & LM;
                    mt.array[i] = mt.array[i + (m - n)] ^ x >> 1 ^
                        mag01[x & 0x1];
                }
                x = mt.array[i] & UM | mt.array[0] & LM;
                mt.array[i] = mt.array[m - 1] ^ x >> 1 ^
                    mag01[x & 0x1];

                mt.index = 0;
            }

            x = mt.array[mt.index];
            mt.index += 1;

            x ^= x >> 29 & 0x5555555555555555;
            x ^= x << 17 & 0x71D67FFFEDA60000;
            x ^= x << 37 & 0xFFF7EEE000000000;
            x ^= x >> 43;

            return x;
        }
    }
}