//============================================================================
// File: IO/BC7Decoder.cs - Full BC7 texture decompression
// ============================================================================
using System;

namespace Kimera2.IO
{
    public static class BC7Decoder
    {
        // Partition table for 2-subset modes (64 entries, 16 values each)
		private static readonly byte[] PartitionTable2Flat = {
			0,0,1,1,0,0,1,1,0,0,1,1,0,0,1,1, 0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,1,
			0,1,1,1,0,1,1,1,0,1,1,1,0,1,1,1, 0,0,0,1,0,0,1,1,0,0,1,1,0,1,1,1,
			0,0,0,0,0,0,0,1,0,0,0,1,0,0,1,1, 0,0,1,1,0,1,1,1,0,1,1,1,1,1,1,1,
			0,0,0,1,0,0,1,1,0,1,1,1,1,1,1,1, 0,0,0,0,0,0,0,1,0,0,1,1,0,1,1,1,
			0,0,0,0,0,0,0,0,0,0,0,1,0,0,1,1, 0,0,1,1,0,1,1,1,1,1,1,1,1,1,1,1,
			0,0,0,0,0,0,0,1,0,1,1,1,1,1,1,1, 0,0,0,0,0,0,0,0,0,0,0,1,0,1,1,1,
			0,0,0,1,0,1,1,1,1,1,1,1,1,1,1,1, 0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,
			0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1, 0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,
			0,0,0,0,1,0,0,0,1,1,1,0,1,1,1,1, 0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0,
			0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0, 0,1,1,1,0,0,1,1,0,0,0,1,0,0,0,0,
			0,0,1,1,0,0,0,1,0,0,0,0,0,0,0,0, 0,0,0,0,1,0,0,0,1,1,0,0,1,1,1,0,
			0,0,0,0,0,0,0,0,1,0,0,0,1,1,0,0, 0,1,1,1,0,0,1,1,0,0,1,1,0,0,0,1,
			0,0,1,1,0,0,0,1,0,0,0,1,0,0,0,0, 0,0,0,0,0,0,0,0,1,1,0,0,1,1,1,0,
			0,1,1,0,0,1,1,0,0,1,1,0,0,1,1,0, 0,0,1,1,0,1,1,0,0,1,1,0,1,1,0,0,
			0,0,0,1,0,1,1,1,1,1,1,0,1,0,0,0, 0,0,0,0,1,1,1,1,1,1,1,1,0,0,0,0,
			0,1,1,0,0,1,1,0,0,1,1,0,0,1,1,0, 0,0,0,0,0,1,1,0,0,1,1,0,0,0,0,0,
			0,1,0,0,0,1,1,0,0,1,1,0,0,0,1,0, 0,0,0,0,1,0,0,0,1,1,0,0,1,1,1,0,
			0,0,0,0,0,0,0,0,0,1,0,0,1,1,1,0, 0,0,0,0,0,0,0,0,1,1,0,0,1,1,0,0,
			0,1,1,0,1,1,0,0,1,0,0,1,0,0,1,1, 0,0,1,1,0,1,1,0,1,1,0,0,1,1,0,0,
			0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1, 0,0,0,0,1,1,1,1,0,0,0,0,1,1,1,1,
			0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0, 0,0,1,1,1,1,0,0,0,0,1,1,1,1,0,0,
			0,1,0,1,0,1,0,1,1,0,1,0,1,0,1,0, 0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1,
			0,1,0,1,1,0,1,0,1,0,1,0,0,1,0,1, 0,1,1,1,0,0,1,1,1,1,0,0,1,1,1,0,
			0,0,0,1,0,0,1,1,1,1,0,0,1,0,0,0, 0,0,1,1,0,0,1,0,0,1,0,0,1,1,0,0,
			0,0,1,1,1,0,0,1,1,0,0,1,1,1,0,0, 0,1,0,1,0,0,1,1,1,1,0,0,1,0,1,0,
			0,0,1,1,1,0,0,1,1,0,0,1,1,1,0,0, 0,0,0,0,1,1,1,0,0,1,1,1,0,0,0,0,
			0,1,0,0,0,1,1,0,0,1,1,0,0,0,1,0, 0,0,1,0,0,1,1,0,0,1,1,0,0,1,0,0,
			0,0,0,0,0,0,1,0,0,1,1,1,0,0,1,0, 0,0,0,0,0,1,0,0,1,1,1,0,0,1,0,0,
			0,1,1,0,1,1,0,0,1,0,0,1,0,0,1,1, 0,0,1,1,0,1,1,0,1,1,0,0,1,1,0,0,
			0,1,1,0,0,0,1,1,1,0,0,1,1,1,0,0, 0,0,1,1,1,0,0,1,1,0,0,1,0,0,1,1,
			0,1,1,0,1,1,0,0,1,1,0,0,0,0,1,1, 0,1,1,0,0,0,1,1,0,0,1,1,1,0,0,1,
			0,1,1,1,1,1,1,0,1,0,0,0,0,0,0,1, 0,0,0,1,1,0,0,0,1,1,1,0,0,1,1,1,
		};

		private static byte GetPartition2(int partition, int index)
		{
			return PartitionTable2Flat[partition * 16 + index];
		}

        // Interpolation weights for 2, 3, and 4 bit indices
        private static readonly byte[] Weights2 = { 0, 21, 43, 64 };
        private static readonly byte[] Weights3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
        private static readonly byte[] Weights4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

        // Mode info: [numSubsets, partBits, rotBits, idxSelBit, colorBits, alphaBits, epPBits, spPBits, idxBits, idx2Bits]
        private static readonly int[,] ModeInfo = {
            {3, 4, 0, 0, 4, 0, 1, 0, 3, 0}, // mode 0
            {2, 6, 0, 0, 6, 0, 0, 1, 3, 0}, // mode 1
            {3, 6, 0, 0, 5, 0, 0, 0, 2, 0}, // mode 2
            {2, 6, 0, 0, 7, 0, 1, 0, 2, 0}, // mode 3
            {1, 0, 2, 1, 5, 6, 0, 0, 2, 3}, // mode 4
            {1, 0, 2, 0, 7, 8, 0, 0, 2, 2}, // mode 5
            {1, 0, 0, 0, 7, 7, 1, 0, 4, 0}, // mode 6
            {2, 6, 0, 0, 5, 5, 1, 0, 2, 0}, // mode 7
        };

        public static void DecompressBlock(byte[] block, byte[] output, int outX, int outY, int stride)
        {
            // Read 128-bit block as two ulongs for bit extraction
            ulong low = BitConverter.ToUInt64(block, 0);
            ulong high = BitConverter.ToUInt64(block, 8);
            int bitPos = 0;

            // Determine mode (first set bit)
            int mode = 0;
            for (mode = 0; mode < 8; mode++)
                if (((low >> mode) & 1) == 1) break;
            if (mode >= 8) return; // invalid block
            bitPos = mode + 1;

            int numSubsets = ModeInfo[mode, 0];
            int partBits = ModeInfo[mode, 1];
            int rotBits = ModeInfo[mode, 2];
            int idxSelBit = ModeInfo[mode, 3];
            int colorBits = ModeInfo[mode, 4];
            int alphaBits = ModeInfo[mode, 5];
            int epPBits = ModeInfo[mode, 6];
            int spPBits = ModeInfo[mode, 7];
            int idxBits = ModeInfo[mode, 8];
            int idx2Bits = ModeInfo[mode, 9];

            // Read partition
            int partition = (int)GetBits(low, high, ref bitPos, partBits);

            // Read rotation
            int rotation = (int)GetBits(low, high, ref bitPos, rotBits);

            // Read index selection
            int indexSel = (int)GetBits(low, high, ref bitPos, idxSelBit);

            // Read color endpoints
            int numEndpoints = numSubsets * 2;
            int[] r = new int[numEndpoints], g = new int[numEndpoints], b = new int[numEndpoints], a = new int[numEndpoints];

            for (int i = 0; i < numEndpoints; i++) r[i] = (int)GetBits(low, high, ref bitPos, colorBits);
            for (int i = 0; i < numEndpoints; i++) g[i] = (int)GetBits(low, high, ref bitPos, colorBits);
            for (int i = 0; i < numEndpoints; i++) b[i] = (int)GetBits(low, high, ref bitPos, colorBits);
            for (int i = 0; i < numEndpoints; i++) a[i] = alphaBits > 0 ? (int)GetBits(low, high, ref bitPos, alphaBits) : 255;

            // Read P-bits
            int[] pBits = new int[numEndpoints];
            if (epPBits > 0)
                for (int i = 0; i < numEndpoints; i++) pBits[i] = (int)GetBits(low, high, ref bitPos, 1);
            else if (spPBits > 0)
                for (int i = 0; i < numSubsets; i++) { int pb = (int)GetBits(low, high, ref bitPos, 1); pBits[i*2] = pb; pBits[i*2+1] = pb; }

            // Expand endpoints to 8 bits
            for (int i = 0; i < numEndpoints; i++)
            {
                r[i] = Unquantize(r[i], colorBits, pBits[i], epPBits > 0 || spPBits > 0);
                g[i] = Unquantize(g[i], colorBits, pBits[i], epPBits > 0 || spPBits > 0);
                b[i] = Unquantize(b[i], colorBits, pBits[i], epPBits > 0 || spPBits > 0);
                if (alphaBits > 0)
                    a[i] = Unquantize(a[i], alphaBits, pBits[i], epPBits > 0 || spPBits > 0);
                else
                    a[i] = 255;
            }

            // Read indices
            byte[] weights = idxBits == 2 ? Weights2 : idxBits == 3 ? Weights3 : Weights4;
            byte[] weights2 = idx2Bits == 2 ? Weights2 : idx2Bits == 3 ? Weights3 : (idx2Bits > 0 ? Weights4 : null);

            int[] colorIdx = new int[16];
            int[] alphaIdx = new int[16];

            for (int i = 0; i < 16; i++)
            {
                int subset = numSubsets > 1 ? GetPartition2(Math.Min(partition, 63), i) : 0;
                // Anchor indices have one fewer bit
                bool isAnchor = (i == 0) || (numSubsets == 2 && i == AnchorIndex2(partition));
                int bits = idxBits - (isAnchor ? 1 : 0);
                colorIdx[i] = (int)GetBits(low, high, ref bitPos, bits);
            }

            if (idx2Bits > 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    bool isAnchor = (i == 0);
                    int bits = idx2Bits - (isAnchor ? 1 : 0);
                    alphaIdx[i] = (int)GetBits(low, high, ref bitPos, bits);
                }
            }
            else
            {
                Array.Copy(colorIdx, alphaIdx, 16);
            }

            // Interpolate and write pixels
            for (int i = 0; i < 16; i++)
            {
                int py = i / 4, px = i % 4;
                int outOff = ((outY + py) * stride + (outX + px)) * 4;
                if (outOff + 3 >= output.Length) continue;

                int subset = numSubsets > 1 ? GetPartition2(Math.Min(partition, 63), i) : 0;
                int ep0 = subset * 2, ep1 = subset * 2 + 1;

                int cIdx = colorIdx[i];
                int aIdx = alphaIdx[i];

                byte[] cw = (indexSel == 0) ? weights : (weights2 ?? weights);
                byte[] aw = (indexSel == 0) ? (weights2 ?? weights) : weights;
                int ci = (indexSel == 0) ? cIdx : aIdx;
                int ai = (indexSel == 0) ? aIdx : cIdx;

                int finalR = Interpolate(r[ep0], r[ep1], cw, ci);
                int finalG = Interpolate(g[ep0], g[ep1], cw, ci);
                int finalB = Interpolate(b[ep0], b[ep1], cw, ci);
                int finalA = Interpolate(a[ep0], a[ep1], aw, ai);

                // Apply rotation
                switch (rotation)
                {
                    case 1: (finalA, finalR) = (finalR, finalA); break;
                    case 2: (finalA, finalG) = (finalG, finalA); break;
                    case 3: (finalA, finalB) = (finalB, finalA); break;
                }

                output[outOff + 0] = (byte)finalR;
                output[outOff + 1] = (byte)finalG;
                output[outOff + 2] = (byte)finalB;
                output[outOff + 3] = 255; // Force opaque
            }
        }

        private static int AnchorIndex2(int partition)
        {
            int[] anchors = { 15,15,15,15,15,15,15,15,15,15,15,15,15,15,15,15,
                15,2,8,2,2,8,8,15,2,8,2,2,8,8,2,2,15,15,6,8,2,8,15,15,2,8,2,2,2,15,15,6,6,2,6,8,15,15,2,2,15,15,15,15,15,2,2,15 };
            return partition < 64 ? anchors[partition] : 15;
        }

        private static int Interpolate(int e0, int e1, byte[] weights, int index)
        {
            if (index >= weights.Length) index = weights.Length - 1;
            if (index < 0) index = 0;
            return (e0 * (64 - weights[index]) + e1 * weights[index] + 32) >> 6;
        }

        private static int Unquantize(int val, int bits, int pBit, bool hasPBit)
        {
            if (hasPBit)
            {
                val = (val << 1) | pBit;
                bits++;
            }
            if (bits >= 8) return val;
            // Expand to 8 bits
            val = (val << (8 - bits)) | (val >> (2 * bits - 8));
            return Math.Clamp(val, 0, 255);
        }

        private static ulong GetBits(ulong low, ulong high, ref int bitPos, int numBits)
        {
            if (numBits == 0) return 0;
            ulong result = 0;
            for (int i = 0; i < numBits; i++)
            {
                int pos = bitPos + i;
                ulong bit;
                if (pos < 64)
                    bit = (low >> pos) & 1;
                else
                    bit = (high >> (pos - 64)) & 1;
                result |= bit << i;
            }
            bitPos += numBits;
            return result;
        }
    }
}