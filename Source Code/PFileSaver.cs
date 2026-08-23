using System;
using System.IO;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class PFileSaver
    {
		//Write to a tmp file first so you can save it if in the same directory
		public static void Save(string filePath, PModel model, string originalFilePath = null)
		{
			if (originalFilePath != null && File.Exists(originalFilePath))
			{
				// Write to temp file first, then replace
				string tempPath = filePath + ".tmp";
				
				if (originalFilePath == filePath)
				{
					// Saving over the original - read it into memory first
					byte[] originalBytes = File.ReadAllBytes(originalFilePath);
					File.WriteAllBytes(tempPath, originalBytes);
				}
				else
				{
					File.Copy(originalFilePath, tempPath, true);
				}
				
				// Overwrite vertices in the temp file
				using (var writer = new BinaryWriter(File.Open(tempPath, FileMode.Open)))
				{
					writer.Seek(128, SeekOrigin.Begin);
					foreach (var v in model.Vertices)
					{
						writer.Write(v.X);
						writer.Write(v.Y);
						writer.Write(v.Z);
					}
				}
				
				// Replace original with temp
				File.Copy(tempPath, filePath, true);
				File.Delete(tempPath);
			}
		}
        public static void BakeTransform(PModel model)
        {
            if (model.OffsetX == 0 && model.OffsetY == 0 && model.OffsetZ == 0 &&
                model.RotateX == 0 && model.RotateY == 0 && model.RotateZ == 0 &&
                model.ScaleX == 1 && model.ScaleY == 1 && model.ScaleZ == 1)
                return;

            float cosX = MathF.Cos(model.RotateX * MathF.PI / 180f);
            float sinX = MathF.Sin(model.RotateX * MathF.PI / 180f);
            float cosY = MathF.Cos(model.RotateY * MathF.PI / 180f);
            float sinY = MathF.Sin(model.RotateY * MathF.PI / 180f);
            float cosZ = MathF.Cos(model.RotateZ * MathF.PI / 180f);
            float sinZ = MathF.Sin(model.RotateZ * MathF.PI / 180f);

            for (int i = 0; i < model.Vertices.Count; i++)
            {
                var v = model.Vertices[i];
                float x = v.X * model.ScaleX;
                float y = v.Y * model.ScaleY;
                float z = v.Z * model.ScaleZ;
                float y1 = y * cosX - z * sinX; float z1 = y * sinX + z * cosX; y = y1; z = z1;
                float x1 = x * cosY + z * sinY; z1 = -x * sinY + z * cosY; x = x1; z = z1;
                x1 = x * cosZ - y * sinZ; y1 = x * sinZ + y * cosZ; x = x1; y = y1;
                x += model.OffsetX; y += model.OffsetY; z += model.OffsetZ;
                model.Vertices[i] = new Vertex3D { X = x, Y = y, Z = z };
            }

            model.OffsetX = model.OffsetY = model.OffsetZ = 0;
            model.RotateX = model.RotateY = model.RotateZ = 0;
            model.ScaleX = model.ScaleY = model.ScaleZ = 1;
        }
    }
}