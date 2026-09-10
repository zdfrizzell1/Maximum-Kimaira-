using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kimera2.IO
{
    public class BattleAnimPack
	{
		public List<BattleAnimation> BodyAnimations   = new List<BattleAnimation>();
		public List<BattleAnimation> WeaponAnimations = new List<BattleAnimation>();

		public int TotalCount => BodyAnimations.Count + WeaponAnimations.Count;
	}
	
	public class BattleAnimation
    {
        public int NumBones { get; set; }
        public int NumFrames { get; set; }
        public BattleAnimFrame[] Frames { get; set; }
    }

    public class BattleAnimFrame
    {
        public int RootX, RootY, RootZ;
        public int[] RotationsX;
        public int[] RotationsY;
        public int[] RotationsZ;

        public float GetRotXDeg(int bone) => (RotationsX[bone] / 4096f) * 360f;
        public float GetRotYDeg(int bone) => (RotationsY[bone] / 4096f) * 360f;
        public float GetRotZDeg(int bone) => (RotationsZ[bone] / 4096f) * 360f;
    }


	
	// Bit reader matching Kimera's GetBitBlockV - Most Signicant Bit first, sign-extended
    public class BattleBitReader
    {
        private readonly byte[] _data;
        private int _bitPos;

        public BattleBitReader(byte[] data, int startBit = 0)
        {
            _data = data;
            _bitPos = startBit;
        }

        public int BitPosition => _bitPos;
        public bool HasData => (_bitPos / 8) < _data.Length;

        // Read nBits as unsigned (MSB first) - matches GetBitBlockVUnsigned
        public int ReadUnsigned(int nBits)
        {
            if (nBits <= 0) return 0;
            int result = 0;
            for (int i = 0; i < nBits; i++)
            {
                int byteIdx = _bitPos / 8;
                int bitIdx = 7 - (_bitPos % 8); // MSB first
                if (byteIdx < _data.Length)
                {
                    int bit = (_data[byteIdx] >> bitIdx) & 1;
                    result = (result << 1) | bit;
                }
                _bitPos++;
            }
            return result;
        }

        // Read nBits as signed (sign-extend) - matches GetBitBlockV
        public int ReadSigned(int nBits)
        {
            if (nBits <= 0) return 0;
            int raw = ReadUnsigned(nBits);
            return SignExtend(raw, nBits);
        }

        // Sign extend a value of given bit length
        public static int SignExtend(int value, int bits)
        {
            if (bits >= 16) return (short)(value & 0xFFFF);
            int signBit = 1 << (bits - 1);
            if ((value & signBit) != 0)
                value |= ~((1 << bits) - 1);
            return value;
        }
    }

    // Battle animation loader - faithful translation of Kimera VB6 source
    public static class BattleAnimLoader
    {
        public static List<BattleAnimation> Load(string filePath)
        {
            return Load(File.ReadAllBytes(filePath));
        }

        public static List<BattleAnimation> Load(byte[] fileData)
        {
            var animations = new List<BattleAnimation>();
            using (var ms = new MemoryStream(fileData))
            using (var br = new BinaryReader(ms))
            {
                uint numAnimations = br.ReadUInt32();
                if (numAnimations == 0 || numAnimations > 1000)
                    throw new InvalidDataException($"Invalid: numAnimations={numAnimations}");

                for (int animIdx = 0; animIdx < (int)numAnimations; animIdx++)
                {
                    if (ms.Position + 12 > ms.Length) break;

                    // 12-byte header
                    int numBonesModel = br.ReadInt32();  // bones+1 (includes root)
                    int numFrames1 = br.ReadInt32();     // conservative frame count
                    int blockLength = br.ReadInt32();     // total block data length

                    if (blockLength < 11)
                    {
                        // Placeholder slot - too small to hold the 11-byte sub-header. Keep it in the list so sub-animation indices still match the file. FF7 uses these as padding in its animation table.
                        var emptyFrame = new BattleAnimFrame
                        {
                            RootX = 0, RootY = 0, RootZ = 0, //Give empty animations a location, these do not exist 
                            RotationsX = new int[numBonesModel], //Not rotations, just array for empty bone sets
                            RotationsY = new int[numBonesModel],
                            RotationsZ = new int[numBonesModel]
                        };
                        animations.Add(new BattleAnimation
                        {
                            NumBones  = numBonesModel,
                            NumFrames = 1,
                            Frames    = new BattleAnimFrame[] { emptyFrame }
                        });

                        if (blockLength > 0 && ms.Position + blockLength <= ms.Length)
                            ms.Seek(blockLength, SeekOrigin.Current);
                        continue;
                    }

                    // Read the block data (contains sub-header + animation stream)
                    long blockStart = ms.Position;
                    
                    // Sub-header within block
                    short numFrames2 = br.ReadInt16();     // actual frame count
                    short animLength = br.ReadInt16();     // animation data length
                    int animLengthUnsigned = animLength < 0 ? animLength + 65536 : animLength;
                    byte key = br.ReadByte();              // compression key (0, 2, or 4)

                    // Read animation stream
                    byte[] animStream;
                    if (animLengthUnsigned + 1 > 0)
                    {
                        animStream = new byte[animLengthUnsigned + 1];
                        int toRead = Math.Min(animStream.Length, (int)(ms.Length - ms.Position));
                        br.Read(animStream, 0, toRead);
                    }
                    else
                    {
                        animStream = new byte[0];
                    }

                    // Skip to end of block
                    ms.Position = blockStart + blockLength;

                    if (!(key == 0 || key == 2 || key == 4)) continue;

                    // Use numBonesModel as bone count (includes root bone)
                    int bonesVectorLength = numBonesModel > 1 ? numBonesModel : 1;

                    // Decode frames
                    var reader = new BattleBitReader(animStream);
                    var frameList = new List<BattleAnimFrame>();

                    // Frame 0: uncompressed
                    var frame0 = ReadUncompressedFrame(reader, key, bonesVectorLength);
                    if (frame0 != null)
                        frameList.Add(frame0);

                    // Delta frames
                    for (int fi = 1; fi < 9999; fi++) // read until we run out of data
                    {
                        if (!reader.HasData) break;
                        try
                        {
                            var frame = ReadDeltaFrame(reader, key, bonesVectorLength, frameList[fi - 1]);
                            if (frame != null)
                                frameList.Add(frame);
                            else
                                break;
                        }
                        catch
                        {
                            break; // out of data
                        }
                    }

                    if (frameList.Count > 0)
                    {
                        animations.Add(new BattleAnimation
                        {
                            NumBones = bonesVectorLength,
                            NumFrames = frameList.Count,
                            Frames = frameList.ToArray()
                        });
                    }
                }
            }
            return animations;
        }
		// WHY skeletonBoneCount is needed: Body animations report (skeletonBones + 1) bones, weapon animations report 1. Verified on CIAA/cida - 27-bone skeleton, 16 animations at
		// 28 bones and 16 at 1 bone. BUT on a 1-bone skeleton a body animation also reports 1, making it indistinguishable from a weapon animation. Weapons only exist on playable
		// characters, which are always multi-bone, so a 1-bone skeleton has no weapon and everything it holds is a body animation.
		// ============================================================================
		public static BattleAnimPack LoadPack(string filePath, int skeletonBoneCount)
		{
			var pack = new BattleAnimPack();
			var all = Load(filePath);
			if (all == null || all.Count == 0) return pack;

			if (skeletonBoneCount > 1)
			{
				foreach (var a in all)
				{
					if (a.NumBones > 1) pack.BodyAnimations.Add(a);
					else                pack.WeaponAnimations.Add(a);
				}
			}
			else
			{
				// Single-bone skeleton: no weapon, all body
				pack.BodyAnimations.AddRange(all);
			}

			// Safety net - never hand back an empty body list when the file had data
			if (pack.BodyAnimations.Count == 0)
			{
				pack.BodyAnimations.AddRange(all);
				pack.WeaponAnimations.Clear();
			}

			return pack;
		}

        // Frame 0: absolute values
        private static BattleAnimFrame ReadUncompressedFrame(BattleBitReader reader, byte key, int numBones)
        {
            var frame = new BattleAnimFrame
            {
                RotationsX = new int[numBones],
                RotationsY = new int[numBones],
                RotationsZ = new int[numBones]
            };

            // Root translation: 3x 16-bit signed
            frame.RootX = reader.ReadSigned(16);
            frame.RootY = reader.ReadSigned(16);
            frame.RootZ = reader.ReadSigned(16);

            // Bone rotations: (12-key) bits each, signed, then shift left by key
            int bitsPerRot = 12 - key;
            for (int b = 0; b < numBones; b++)
            {
                int rx = reader.ReadSigned(bitsPerRot) * (1 << key);
                int ry = reader.ReadSigned(bitsPerRot) * (1 << key);
                int rz = reader.ReadSigned(bitsPerRot) * (1 << key);

                // Convert signed to unsigned 12-bit (0-4095)
                frame.RotationsX[b] = rx < 0 ? rx + 0x1000 : rx;
                frame.RotationsY[b] = ry < 0 ? ry + 0x1000 : ry;
                frame.RotationsZ[b] = rz < 0 ? rz + 0x1000 : rz;
            }

            return frame;
        }

        // Delta frames: compressed relative to previous frame
        private static BattleAnimFrame ReadDeltaFrame(BattleBitReader reader, byte key, int numBones, BattleAnimFrame lastFrame)
        {
            var frame = new BattleAnimFrame
            {
                RotationsX = new int[numBones],
                RotationsY = new int[numBones],
                RotationsZ = new int[numBones]
            };

            // Root translation deltas: 1 bit flag -> 7 or 16 bit signed
            frame.RootX = lastFrame.RootX + ReadPositionDelta(reader);
            frame.RootY = lastFrame.RootY + ReadPositionDelta(reader);
            frame.RootZ = lastFrame.RootZ + ReadPositionDelta(reader);

            // Bone rotation deltas
            for (int b = 0; b < numBones; b++)
            {
                int prevX = frame.RotationsX[b] = ToSigned12(lastFrame.RotationsX[b]);
                int prevY = frame.RotationsY[b] = ToSigned12(lastFrame.RotationsY[b]);
                int prevZ = frame.RotationsZ[b] = ToSigned12(lastFrame.RotationsZ[b]);

                int dx = ReadRotationDelta(reader, key);
                int dy = ReadRotationDelta(reader, key);
                int dz = ReadRotationDelta(reader, key);

                int newX = prevX + dx;
                int newY = prevY + dy;
                int newZ = prevZ + dz;

                // Convert back to unsigned 12-bit
                frame.RotationsX[b] = newX < 0 ? newX + 0x1000 : newX & 0xFFF;
                frame.RotationsY[b] = newY < 0 ? newY + 0x1000 : newY & 0xFFF;
                frame.RotationsZ[b] = newZ < 0 ? newZ + 0x1000 : newZ & 0xFFF;
            }

            return frame;
        }

        // Convert unsigned 12-bit (0-4095) to signed (-2048 to 2047)
        private static int ToSigned12(int val)
        {
            if (val >= 0x800) return val - 0x1000;
            return val;
        }

        // Read root translation delta: 1 bit flag -> 7 or 16 bit signed
        private static int ReadPositionDelta(BattleBitReader reader)
        {
            int flag = reader.ReadUnsigned(1);
            if (flag == 0)
                return reader.ReadSigned(7);
            else
                return reader.ReadSigned(16);
        }

        // Read rotation delta - matches Kimera's ReadDAFrameBoneRotationDelta
        private static int ReadRotationDelta(BattleBitReader reader, byte key)
        {
            if (reader.ReadUnsigned(1) == 0)
                return 0; // no change

            int dLength = reader.ReadUnsigned(3);

            int value;
            if (dLength == 0)
            {
                value = -1;
            }
            else if (dLength == 7)
            {
                // Full value like frame 0
                value = reader.ReadSigned(12 - key);
            }
            else
            {
                // Variable length with sign bit flip
                value = reader.ReadSigned(dLength);
                int signVal = 1 << (dLength - 1);
                if (value < 0)
                    value = value - signVal;
                else
                    value = value + signVal;
            }

            // Convert to 12-bit value by shifting
            value = value * (1 << key);
            return value;
        }

        public static bool IsBattleAnimation(string filePath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                if (data.Length < 27) return false;
                uint numAnims = BitConverter.ToUInt32(data, 0);
                if (numAnims == 0 || numAnims > 500) return false;
                uint bones = BitConverter.ToUInt32(data, 4);
                uint frames = BitConverter.ToUInt32(data, 8);
                uint blockLen = BitConverter.ToUInt32(data, 12);
                if (bones == 0 || bones > 100) return false;
                if (blockLen == 0) return false;
                return true;
            }
            catch { return false; }
        }
    }
}