using System;
using System.Collections.Generic;
using System.IO;

namespace Kimera2.IO
{
	public class BattleAnimation
	{
	public int NumBones { get; set; }
	public int NumFrames { get; set; }
	public BattleAnimFrame[] Frames { get; set; }
	}

	public class BattleAnimFrame
	{
		public short RootX, RootY, RootZ;
		public int[] RotationsX;
		public int[] RotationsY;
		public int[] RotationsZ;

		public float GetRotXDeg(int bone) => (RotationsX[bone] / 4096f) * 360f;
		public float GetRotYDeg(int bone) => (RotationsY[bone] / 4096f) * 360f;
		public float GetRotZDeg(int bone) => (RotationsZ[bone] / 4096f) * 360f;
	}

	public static class BattleAnimLoader
	{
		public static List<BattleAnimation> Load(string filePath)
		{
			byte[] fileData = File.ReadAllBytes(filePath);
			return Load(fileData);
		}

		public static List<BattleAnimation> Load(byte[] fileData)
		{
			var animations = new List<BattleAnimation>();

			using (var ms = new MemoryStream(fileData))
			using (var br = new BinaryReader(ms))
			{
				uint numAnimations = br.ReadUInt32();

				if (numAnimations == 0 || numAnimations > 1000)
					throw new InvalidDataException($"Invalid battle animation: numAnimations={numAnimations}");

				for (int animIdx = 0; animIdx < (int)numAnimations; animIdx++)
				{
					// CHECK: enough bytes left for a header?
					if (ms.Position + 23 > ms.Length)
						break;

					uint rec_a = br.ReadUInt32();
					uint rec_b = br.ReadUInt32();
					uint block_len = br.ReadUInt32();
					ushort block_a = br.ReadUInt16();
					ushort real_data_len = br.ReadUInt16();
					short transX = br.ReadInt16();
					short transY = br.ReadInt16();
					short transZ = br.ReadInt16();
					byte u1 = br.ReadByte();

					int dataSize = (int)block_len - 11;
					if (dataSize <= 0) { continue; }

					byte[] rotData = br.ReadBytes(dataSize);

					int numBones = (int)rec_a;
					int numFrames = (int)rec_b;

					if (numBones <= 0 || numBones > 100 || numFrames <= 0 || numFrames > 10000)
						continue;

					var anim = new BattleAnimation
					{
						NumBones = numBones,
						NumFrames = numFrames,
						Frames = new BattleAnimFrame[numFrames]
					};

					int bitPos = 0;

					for (int f = 0; f < numFrames; f++)
					{
						var frame = new BattleAnimFrame
						{
							RootX = transX,
							RootY = transY,
							RootZ = transZ,
							RotationsX = new int[numBones],
							RotationsY = new int[numBones],
							RotationsZ = new int[numBones]
						};

						for (int b = 0; b < numBones; b++)
						{
							frame.RotationsX[b] = Read12Bits(rotData, ref bitPos);
							frame.RotationsY[b] = Read12Bits(rotData, ref bitPos);
							frame.RotationsZ[b] = Read12Bits(rotData, ref bitPos);
						}

						anim.Frames[f] = frame;
					}

					animations.Add(anim);
				}
			}

			return animations;
		}

		private static int Read12Bits(byte[] data, ref int bitPos)
		{
			int byteOffset = bitPos / 8;
			int bitOffset = bitPos % 8;

			if (byteOffset >= data.Length)
			{
				bitPos += 12;
				return 0;
			}

			int value = 0;

			if (bitOffset == 0)
			{
				value = (data[byteOffset] << 4);
				if (byteOffset + 1 < data.Length)
					value |= (data[byteOffset + 1] >> 4);
			}
			else if (bitOffset == 4)
			{
				value = (data[byteOffset] & 0x0F) << 8;
				if (byteOffset + 1 < data.Length)
					value |= data[byteOffset + 1];
			}

			bitPos += 12;
			return value & 0xFFF;
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
				if (frames == 0 || frames > 10000) return false;
				if (blockLen == 0 || 4 + 23 + (blockLen - 11) > data.Length) return false;

				return true;
			}
			catch
			{
				return false;
			}
		}
	}

}