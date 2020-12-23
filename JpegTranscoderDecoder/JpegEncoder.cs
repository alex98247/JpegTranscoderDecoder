﻿using System;
using System.IO;
using System.Linq;

namespace JpegTranscoderDecoder
{
    class JpegEncoder
    {
        byte[] s_jo_ZigZag =
        {
            0, 1, 5, 6, 14, 15, 27, 28, 2, 4, 7, 13, 16, 26, 29, 42, 3, 8, 12, 17, 25, 30, 41, 43, 9, 11, 18, 24, 31,
            40, 44, 53, 10, 19, 23, 32, 39, 45, 52, 54, 20, 22, 33, 38, 46, 51, 55, 60, 21, 34, 37, 47, 50, 56, 59, 61,
            35, 36, 48, 49, 57, 58, 62, 63
        };

        T[,] ResizeArray<T>(T[,] original, int rows, int cols)
        {
            var newArray = new T[rows, cols];
            int minRows = Math.Min(rows, original.GetLength(0));
            int minCols = Math.Min(cols, original.GetLength(1));
            for (int i = 0; i < minRows; i++)
            for (int j = 0; j < minCols; j++)
                newArray[i, j] = original[i, j];
            return newArray;
        }

        T[] GetRow<T>(T[,] matrix, int rowNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(1))
                .Select(x => matrix[rowNumber, x])
                .ToArray();
        }

        static void jo_writeBits(Stream fp, ref int bitBuf, ref int bitCnt, ushort[] bs)
        {
            bitCnt += bs[1];
            bitBuf |= bs[0] << (24 - bitCnt);
            while (bitCnt >= 8)
            {
                int c = (bitBuf >> 16) & 255;
                fp.WriteByte((byte) c);
                if (c == 255)
                {
                    fp.WriteByte(0);
                }

                bitBuf <<= 8;
                bitCnt -= 8;
            }
        }

        static void jo_DCT(ref float d0, ref float d1, ref float d2, ref float d3, ref float d4, ref float d5,
            ref float d6, ref float d7)
        {
            float tmp0 = d0 + d7;
            float tmp7 = d0 - d7;
            float tmp1 = d1 + d6;
            float tmp6 = d1 - d6;
            float tmp2 = d2 + d5;
            float tmp5 = d2 - d5;
            float tmp3 = d3 + d4;
            float tmp4 = d3 - d4;

            // Even part
            float tmp10 = tmp0 + tmp3; // phase 2
            float tmp13 = tmp0 - tmp3;
            float tmp11 = tmp1 + tmp2;
            float tmp12 = tmp1 - tmp2;

            d0 = tmp10 + tmp11; // phase 3
            d4 = tmp10 - tmp11;

            float z1 = (tmp12 + tmp13) * 0.707106781f; // c4
            d2 = tmp13 + z1; // phase 5
            d6 = tmp13 - z1;

            // Odd part
            tmp10 = tmp4 + tmp5; // phase 2
            tmp11 = tmp5 + tmp6;
            tmp12 = tmp6 + tmp7;

            // The rotator is modified from fig 4-8 to avoid extra negations.
            float z5 = (tmp10 - tmp12) * 0.382683433f; // c6
            float z2 = tmp10 * 0.541196100f + z5; // c2-c6
            float z4 = tmp12 * 1.306562965f + z5; // c2+c6
            float z3 = tmp11 * 0.707106781f; // c4

            float z11 = tmp7 + z3; // phase 5
            float z13 = tmp7 - z3;

            d5 = z13 + z2; // phase 6
            d3 = z13 - z2;
            d1 = z11 + z4;
            d7 = z11 - z4;
        }

        static void jo_calcBits(int val, ushort[] bits)
        {
            int tmp1 = val < 0 ? -val : val;
            val = val < 0 ? val - 1 : val;
            bits[1] = 1;
            tmp1 >>= 1;
            while (tmp1 != 0)
            {
                ++bits[1];
                tmp1 >>= 1;
            }

            bits[0] = Convert.ToUInt16(val & ((1 << bits[1]) - 1));
        }

        int jo_processDU(Stream fp, ref int bitBuf, ref int bitCnt, int[] DU, int du_stride, float[] fdtbl,
            int DC,
            ushort[,] HTDC, ushort[,] HTAC, int ofset)
        {
            ushort[] EOB = {HTAC[0x00, 0], HTAC[0x00, 1]};
            ushort[] M16zeroes = {HTAC[0xF0, 0], HTAC[0xF0, 1]};

            // Encode DC
            int diff = DU[0] - DC;
            if (diff == 0)
            {
                jo_writeBits(fp, ref bitBuf, ref bitCnt, GetRow(HTDC, 0));
            }
            else
            {
                ushort[] bits = new ushort[2];
                jo_calcBits(diff, bits);
                jo_writeBits(fp, ref bitBuf, ref bitCnt, GetRow(HTDC, bits[1]));
                jo_writeBits(fp, ref bitBuf, ref bitCnt, bits);
            }

            // Encode ACs
            int end0pos = 63;
            for (; (end0pos > 0) && (DU[end0pos] == 0); --end0pos)
            {
            }

            // end0pos = first element in reverse order !=0
            if (end0pos == 0)
            {
                jo_writeBits(fp, ref bitBuf, ref bitCnt, EOB);
                return DU[0];
            }

            for (int i = 1; i <= end0pos; ++i)
            {
                int startpos = i;
                for (; DU[i] == 0 && i <= end0pos; ++i)
                {
                }

                int nrzeroes = i - startpos;
                if (nrzeroes >= 16)
                {
                    int lng = nrzeroes >> 4;
                    for (int nrmarker = 1; nrmarker <= lng; ++nrmarker)
                        jo_writeBits(fp, ref bitBuf, ref bitCnt, M16zeroes);
                    nrzeroes &= 15;
                }

                ushort[] bits = new ushort[2];
                jo_calcBits(DU[i], bits);
                jo_writeBits(fp, ref bitBuf, ref bitCnt, GetRow(HTAC, (nrzeroes << 4) + bits[1]));
                jo_writeBits(fp, ref bitBuf, ref bitCnt, bits);
            }

            if (end0pos != 63)
            {
                jo_writeBits(fp, ref bitBuf, ref bitCnt, EOB);
            }

            return DU[0];
        }

        public bool jo_write_jpg(string filename, int[][] data, int width, int height, int comp, int quality)
        {
            // Constants that don't pollute global namespace
            byte[] std_dc_luminance_nrcodes = {0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0};
            byte[] std_dc_luminance_values = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};
            byte[] std_ac_luminance_nrcodes = {0, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d};
            byte[] std_ac_luminance_values =
            {
                0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22,
                0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
                0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17,
                0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
                0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a,
                0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
                0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
                0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8,
                0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
                0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5,
                0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
                0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9,
                0xfa
            };
            byte[] std_dc_chrominance_nrcodes = {0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0};
            byte[] std_dc_chrominance_values = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};
            byte[] std_ac_chrominance_nrcodes = {0, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77};
            byte[] std_ac_chrominance_values =
            {
                0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13,
                0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
                0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34, 0xe1,
                0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
                0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
                0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
                0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
                0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6,
                0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
                0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3,
                0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
                0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9,
                0xfa
            };
            // Huffman tables
            ushort[,] YDC_HT =
            {
                {0, 2}, {2, 3}, {3, 3}, {4, 3}, {5, 3}, {6, 3}, {14, 4}, {30, 5}, {62, 6}, {126, 7}, {254, 8}, {510, 9}
            };
            YDC_HT = ResizeArray(YDC_HT, 256, 2);
            ushort[,] UVDC_HT =
            {
                {0, 2}, {1, 2}, {2, 2}, {6, 3}, {14, 4}, {30, 5}, {62, 6}, {126, 7}, {254, 8}, {510, 9}, {1022, 10},
                {2046, 11}
            };
            UVDC_HT = ResizeArray(UVDC_HT, 256, 2);
            ushort[,] YAC_HT =
            {
                {10, 4}, {0, 2}, {1, 2}, {4, 3}, {11, 4}, {26, 5}, {120, 7}, {248, 8}, {1014, 10}, {65410, 16},
                {65411, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {12, 4}, {27, 5}, {121, 7}, {502, 9}, {2038, 11}, {65412, 16}, {65413, 16}, {65414, 16}, {65415, 16},
                {65416, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {28, 5}, {249, 8}, {1015, 10}, {4084, 12}, {65417, 16}, {65418, 16}, {65419, 16}, {65420, 16},
                {65421, 16}, {65422, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {58, 6}, {503, 9}, {4085, 12}, {65423, 16}, {65424, 16}, {65425, 16}, {65426, 16}, {65427, 16},
                {65428, 16}, {65429, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {59, 6}, {1016, 10}, {65430, 16}, {65431, 16}, {65432, 16}, {65433, 16}, {65434, 16}, {65435, 16},
                {65436, 16}, {65437, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {122, 7}, {2039, 11}, {65438, 16}, {65439, 16}, {65440, 16}, {65441, 16}, {65442, 16}, {65443, 16},
                {65444, 16}, {65445, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {123, 7}, {4086, 12}, {65446, 16}, {65447, 16}, {65448, 16}, {65449, 16}, {65450, 16}, {65451, 16},
                {65452, 16}, {65453, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {250, 8}, {4087, 12}, {65454, 16}, {65455, 16}, {65456, 16}, {65457, 16}, {65458, 16}, {65459, 16},
                {65460, 16}, {65461, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {504, 9}, {32704, 15}, {65462, 16}, {65463, 16}, {65464, 16}, {65465, 16}, {65466, 16}, {65467, 16},
                {65468, 16}, {65469, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {505, 9}, {65470, 16}, {65471, 16}, {65472, 16}, {65473, 16}, {65474, 16}, {65475, 16}, {65476, 16},
                {65477, 16}, {65478, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {506, 9}, {65479, 16}, {65480, 16}, {65481, 16}, {65482, 16}, {65483, 16}, {65484, 16}, {65485, 16},
                {65486, 16}, {65487, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {1017, 10}, {65488, 16}, {65489, 16}, {65490, 16}, {65491, 16}, {65492, 16}, {65493, 16}, {65494, 16},
                {65495, 16}, {65496, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {1018, 10}, {65497, 16}, {65498, 16}, {65499, 16}, {65500, 16}, {65501, 16}, {65502, 16}, {65503, 16},
                {65504, 16}, {65505, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {2040, 11}, {65506, 16}, {65507, 16}, {65508, 16}, {65509, 16}, {65510, 16}, {65511, 16}, {65512, 16},
                {65513, 16}, {65514, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {65515, 16}, {65516, 16}, {65517, 16}, {65518, 16}, {65519, 16}, {65520, 16}, {65521, 16}, {65522, 16},
                {65523, 16}, {65524, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {2041, 11}, {65525, 16}, {65526, 16}, {65527, 16}, {65528, 16}, {65529, 16}, {65530, 16}, {65531, 16},
                {65532, 16}, {65533, 16}, {65534, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}
            };
            YAC_HT = ResizeArray(YAC_HT, 256, 2);
            ushort[,] UVAC_HT =
            {
                {0, 2}, {1, 2}, {4, 3}, {10, 4}, {24, 5}, {25, 5}, {56, 6}, {120, 7}, {500, 9}, {1014, 10}, {4084, 12},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {11, 4}, {57, 6}, {246, 8}, {501, 9}, {2038, 11}, {4085, 12}, {65416, 16}, {65417, 16}, {65418, 16},
                {65419, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {26, 5}, {247, 8}, {1015, 10}, {4086, 12}, {32706, 15}, {65420, 16}, {65421, 16}, {65422, 16},
                {65423, 16}, {65424, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {27, 5}, {248, 8}, {1016, 10}, {4087, 12}, {65425, 16}, {65426, 16}, {65427, 16}, {65428, 16},
                {65429, 16}, {65430, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {58, 6}, {502, 9}, {65431, 16}, {65432, 16}, {65433, 16}, {65434, 16}, {65435, 16}, {65436, 16},
                {65437, 16}, {65438, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {59, 6}, {1017, 10}, {65439, 16}, {65440, 16}, {65441, 16}, {65442, 16}, {65443, 16}, {65444, 16},
                {65445, 16}, {65446, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {121, 7}, {2039, 11}, {65447, 16}, {65448, 16}, {65449, 16}, {65450, 16}, {65451, 16}, {65452, 16},
                {65453, 16}, {65454, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {122, 7}, {2040, 11}, {65455, 16}, {65456, 16}, {65457, 16}, {65458, 16}, {65459, 16}, {65460, 16},
                {65461, 16}, {65462, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {249, 8}, {65463, 16}, {65464, 16}, {65465, 16}, {65466, 16}, {65467, 16}, {65468, 16}, {65469, 16},
                {65470, 16}, {65471, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {503, 9}, {65472, 16}, {65473, 16}, {65474, 16}, {65475, 16}, {65476, 16}, {65477, 16}, {65478, 16},
                {65479, 16}, {65480, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {504, 9}, {65481, 16}, {65482, 16}, {65483, 16}, {65484, 16}, {65485, 16}, {65486, 16}, {65487, 16},
                {65488, 16}, {65489, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {505, 9}, {65490, 16}, {65491, 16}, {65492, 16}, {65493, 16}, {65494, 16}, {65495, 16}, {65496, 16},
                {65497, 16}, {65498, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {506, 9}, {65499, 16}, {65500, 16}, {65501, 16}, {65502, 16}, {65503, 16}, {65504, 16}, {65505, 16},
                {65506, 16}, {65507, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {2041, 11}, {65508, 16}, {65509, 16}, {65510, 16}, {65511, 16}, {65512, 16}, {65513, 16}, {65514, 16},
                {65515, 16}, {65516, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {16352, 14}, {65517, 16}, {65518, 16}, {65519, 16}, {65520, 16}, {65521, 16}, {65522, 16}, {65523, 16},
                {65524, 16}, {65525, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {1018, 10}, {32707, 15}, {65526, 16}, {65527, 16}, {65528, 16}, {65529, 16}, {65530, 16}, {65531, 16},
                {65532, 16}, {65533, 16}, {65534, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}
            };
            UVAC_HT = ResizeArray(UVAC_HT, 256, 2);
            int[] YQT =
            {
                16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56, 14, 17,
                22, 29, 51, 87, 80, 62, 18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92, 49, 64, 78,
                87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99
            };
            int[] UVQT =
            {
                17, 18, 24, 47, 99, 99, 99, 99, 18, 21, 26, 66, 99, 99, 99, 99, 24, 26, 56, 99, 99, 99, 99, 99, 47, 66,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99
            };
            float[] aasf =
            {
                1.0f * 2.828427125f, 1.387039845f * 2.828427125f, 1.306562965f * 2.828427125f,
                1.175875602f * 2.828427125f, 1.0f * 2.828427125f, 0.785694958f * 2.828427125f,
                0.541196100f * 2.828427125f, 0.275899379f * 2.828427125f
            };

            FileStream fp = new FileStream(filename, FileMode.Create, FileAccess.Write);

            int subsample = quality <= 90 ? 1 : 0;
            quality = quality < 1 ? 1 : quality > 100 ? 100 : quality;
            quality = quality < 50 ? 5000 / quality : 200 - quality * 2;

            byte[] YTable = new byte[64];
            byte[] UVTable = new byte[64];
            for (int i = 0; i < 64; ++i)
            {
                int yti = (YQT[i] * quality + 50) / 100;
                YTable[s_jo_ZigZag[i]] = Convert.ToByte(yti < 1 ? 1 : yti > 255 ? 255 : yti);
                int uvti = (UVQT[i] * quality + 50) / 100;
                UVTable[s_jo_ZigZag[i]] = Convert.ToByte(uvti < 1 ? 1 : uvti > 255 ? 255 : uvti);
            }

            float[] fdtbl_Y = new float[64];
            float[] fdtbl_UV = new float[64];
            for (int row = 0, k = 0; row < 8; ++row)
            {
                for (int col = 0; col < 8; ++col, ++k)
                {
                    fdtbl_Y[k] = 1 / (YTable[s_jo_ZigZag[k]] * aasf[row] * aasf[col]);
                    fdtbl_UV[k] = 1 / (UVTable[s_jo_ZigZag[k]] * aasf[row] * aasf[col]);
                }
            }

            // Write Headers
            byte[] head0 =
            {
                0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte) 'J', (byte) 'F', (byte) 'I', (byte) 'F', 0, 1, 1, 0, 0, 1, 0, 1,
                0, 0, 0xFF, 0xDB, 0, 0x84,
                0
            };
            fp.Write(head0, 0, head0.Length);
            fp.Write(YTable, 0, YTable.Length);
            fp.WriteByte(1);
            fp.Write(UVTable, 0, UVTable.Length);
            byte[] head1 =
            {
                0xFF, 0xC0, 0, 0x11, 8, (byte) (height >> 8), (byte) (height & 0xFF), (byte) (width >> 8),
                (byte) (width & 0xFF), 3, 1, (byte) (subsample != 0 ? 0x22 : 0x11), 0, 2, 0x11, 1, 3, 0x11, 1, 0xFF,
                0xC4,
                0x01, 0xA2, 0
            };
            byte[] head2 = {0xFF, 0xDA, 0, 0xC, 3, 1, 0, 2, 0x11, 3, 0x11, 0, 0x3F, 0};

            fp.Write(head1, 0, head1.Length);
            fp.Write(std_dc_luminance_nrcodes, 1, std_dc_luminance_nrcodes.Length - 1);
            fp.Write(std_dc_luminance_values, 0, std_dc_luminance_values.Length);
            fp.WriteByte(0x10); // HTYACinfo
            fp.Write(std_ac_luminance_nrcodes, 1, std_ac_luminance_nrcodes.Length - 1);
            fp.Write(std_ac_luminance_values, 0, std_ac_luminance_values.Length);
            fp.WriteByte(1); // HTUDCinfo
            fp.Write(std_dc_chrominance_nrcodes, 1, std_dc_chrominance_nrcodes.Length - 1);
            fp.Write(std_dc_chrominance_values, 0, std_dc_chrominance_values.Length);
            fp.WriteByte(0x11); // HTUACinfo
            fp.Write(std_ac_chrominance_nrcodes, 1, std_ac_chrominance_nrcodes.Length - 1);
            fp.Write(std_ac_chrominance_values, 0, std_ac_chrominance_values.Length);
            fp.Write(head2, 0, head2.Length);

            // Encode 8x8 macroblocks
            int ofsG = comp > 1 ? 1 : 0, ofsB = comp > 1 ? 2 : 0;
            int DCY = 0, DCU = 0, DCV = 0;
            int bitBuf = 0, bitCnt = 0;
            if (subsample != 0)
            {
                for (int i = 0; i < data.Length; i += 6)
                {
                    DCY = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i], 8, fdtbl_Y, DCY, YDC_HT, YAC_HT, 0);
                    DCY = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 1], 8, fdtbl_Y, DCY, YDC_HT, YAC_HT, 8);
                    DCY = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 2], 8, fdtbl_Y, DCY, YDC_HT, YAC_HT, 128);
                    DCY = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 3], 8, fdtbl_Y, DCY, YDC_HT, YAC_HT, 136);
                    DCU = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 4], 8, fdtbl_UV, DCU, UVDC_HT, UVAC_HT, 0);
                    DCV = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 5], 8, fdtbl_UV, DCV, UVDC_HT, UVAC_HT, 0);
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i += 3)
                {
                    DCY = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i], 8, fdtbl_Y, DCY, YDC_HT, YAC_HT, 0);
                    DCU = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 1], 8, fdtbl_UV, DCU, UVDC_HT, UVAC_HT, 0);
                    DCV = jo_processDU(fp, ref bitBuf, ref bitCnt, data[i + 2], 8, fdtbl_UV, DCV, UVDC_HT, UVAC_HT, 0);
                }
            }

            // Do the bit alignment of the EOI marker
            ushort[] fillBits = {0x7F, 7};
            jo_writeBits(fp, ref bitBuf, ref bitCnt, fillBits);
            fp.WriteByte(0xFF);
            fp.WriteByte(0xD9);
            fp.Close();
            return true;
        }
    }
}