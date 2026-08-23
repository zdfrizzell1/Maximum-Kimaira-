using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class AnimLoader
    {
        public static List<AnimationFrame> Load(string filePath, int expectedBones)
        {
            var frames = new List<AnimationFrame>();
            try
            {
                using var reader = new BinaryReader(File.OpenRead(filePath));
                
                // Header: 36 bytes
                int version = reader.ReadInt32();
                int frameCount = reader.ReadInt32();
                int boneCount = reader.ReadInt32();
                reader.BaseStream.Seek(24, SeekOrigin.Current); // skip rest of header

                // Use the smaller of file's bone count and expected
                int useBones = Math.Min(boneCount, expectedBones);

                for (int f = 0; f < frameCount; f++)
                {
                    var frame = new AnimationFrame();
                    
                    // Root rotation (3 floats, degrees)
                    float rx = reader.ReadSingle();
                    float ry = reader.ReadSingle();
                    float rz = reader.ReadSingle();
                    frame.RootRotation = new Vector3(rx, ry, rz);
                    
                    // Root translation (3 floats)
                    float tx = reader.ReadSingle();
                    float ty = reader.ReadSingle();
                    float tz = reader.ReadSingle();
                    frame.RootTranslation = new Vector3(tx, ty, tz);
                    
                    // Bone rotations (3 floats each, degrees)
                    for (int b = 0; b < boneCount; b++)
                    {
                        float bx = reader.ReadSingle();
                        float by = reader.ReadSingle();
                        float bz = reader.ReadSingle();
                        if (b < useBones)
                            frame.BoneRotations.Add(new Vector3(bx, by, bz));
                    }
                    
                    // Pad if we have fewer bones in file than expected
                    while (frame.BoneRotations.Count < expectedBones)
                        frame.BoneRotations.Add(Vector3.Zero);
                    
                    frames.Add(frame);
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("kimera2_log.txt", $"[ANIM] Error loading {filePath}: {ex.Message}\n");
            }
            return frames;
        }
		public static void Save(string filePath, List<AnimationFrame> frames, int boneCount)
		{
			using var writer = new BinaryWriter(File.Create(filePath));
			
			// Header: 36 bytes
			writer.Write(1); // version
			writer.Write(frames.Count); // frame count
			writer.Write(boneCount); // bone count
			writer.Write(new byte[24]); // padding/runtime (24 bytes of zeros)
			
			// Frames
			foreach (var frame in frames)
			{
				// Root rotation (3 floats)
				writer.Write(frame.RootRotation.X);
				writer.Write(frame.RootRotation.Y);
				writer.Write(frame.RootRotation.Z);
				// Root translation (3 floats)
				writer.Write(frame.RootTranslation.X);
				writer.Write(frame.RootTranslation.Y);
				writer.Write(frame.RootTranslation.Z);
				// Bone rotations (3 floats each)
				for (int b = 0; b < boneCount; b++)
				{
					if (b < frame.BoneRotations.Count)
					{
						writer.Write(frame.BoneRotations[b].X);
						writer.Write(frame.BoneRotations[b].Y);
						writer.Write(frame.BoneRotations[b].Z);
					}
					else
					{
						writer.Write(0f); writer.Write(0f); writer.Write(0f);
					}
				}
			}
		}
    }
}