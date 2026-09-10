using System;
using System.IO;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class PFileSaver
    {
		//Write to a tmp file first so you can save it if in the same directory
		public static void SaveNew(string filePath, PModel model)
		{
			// Recompute header counts from the actual lists so they can never disagree
			// with what we write. Sections we have no data for are given a count of 0.
			var h = new PFileHeader
			{
				Version            = 1,
				Off04              = 1,
				VertexType         = 1,
				NumVertices        = model.Vertices.Count,
				NumNormals         = model.Normals.Count,
				NumUnknown1        = 0,
				NumTexCoords       = model.TexCoords.Count,
				NumVertexColors    = model.VertexColors.Count,
				NumEdges           = 0,
				NumPolygons        = model.Polygons.Count,
				NumUnknown2        = 0,
				NumUnknown3        = 0,
				NumHundreds        = 0,
				NumGroups          = model.Groups.Count,
				NumBoundingBoxes   = 0,
				NormIndexTableFlag = 0
			};
			model.Header = h;

			string tempPath = filePath + ".tmp";

			using (var writer = new BinaryWriter(File.Create(tempPath)))
			{
				// ---- header: 16 int fields (64 bytes) then 64 bytes of runtime padding
				writer.Write(h.Version);
				writer.Write(h.Off04);
				writer.Write(h.VertexType);
				writer.Write(h.NumVertices);
				writer.Write(h.NumNormals);
				writer.Write(h.NumUnknown1);
				writer.Write(h.NumTexCoords);
				writer.Write(h.NumVertexColors);
				writer.Write(h.NumEdges);
				writer.Write(h.NumPolygons);
				writer.Write(h.NumUnknown2);
				writer.Write(h.NumUnknown3);
				writer.Write(h.NumHundreds);
				writer.Write(h.NumGroups);
				writer.Write(h.NumBoundingBoxes);
				writer.Write(h.NormIndexTableFlag);
				writer.Write(new byte[64]);              // runtime area - zeros

				// ---- vertices (12 bytes each)
				foreach (var v in model.Vertices)
				{
					writer.Write(v.X); writer.Write(v.Y); writer.Write(v.Z);
				}

				// ---- normals (12 bytes each)
				foreach (var n in model.Normals)
				{
					writer.Write(n.X); writer.Write(n.Y); writer.Write(n.Z);
				}

				// ---- unknown1 : count is 0, nothing written

				// ---- texture coordinates (8 bytes each)
				foreach (var t in model.TexCoords)
				{
					writer.Write(t.U); writer.Write(t.V);
				}

				// ---- vertex colours (4 bytes each, BGRA order)
				foreach (var c in model.VertexColors)
				{
					writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A);
				}

				// ---- polygon colours: this section is sized by NumPolygons, not by a
				// separate count. Pad with white if the list is short or empty.
				for (int i = 0; i < h.NumPolygons; i++)
				{
					if (i < model.PolygonColors.Count)
					{
						var pc = model.PolygonColors[i];
						writer.Write(pc.B); writer.Write(pc.G); writer.Write(pc.R); writer.Write(pc.A);
					}
					else
					{
						writer.Write((byte)255); writer.Write((byte)255);
						writer.Write((byte)255); writer.Write((byte)255);
					}
				}

				// ---- edges : count is 0, nothing written

				// ---- polygons (24 bytes each = 12 ushorts)
				foreach (var p in model.Polygons)
				{
					writer.Write(p.Zero);
					writer.Write(p.VertexIndex1);
					writer.Write(p.VertexIndex2);
					writer.Write(p.VertexIndex3);
					writer.Write(p.NormalIndex1);
					writer.Write(p.NormalIndex2);
					writer.Write(p.NormalIndex3);
					writer.Write(p.EdgeIndex1);
					writer.Write(p.EdgeIndex2);
					writer.Write(p.EdgeIndex3);
					writer.Write(p.Unknown1);
					writer.Write(p.Unknown2);
				}

				// ---- unknown2, unknown3, hundreds : counts are 0, nothing written

				// ---- groups (56 bytes each = 14 ints, order must match PGroup)
				foreach (var g in model.Groups)
				{
					writer.Write(g.PrimitiveType);
					writer.Write(g.PolygonStartIndex);
					writer.Write(g.NumPolygons);
					writer.Write(g.VerticesStartIndex);
					writer.Write(g.NumVertices);
					writer.Write(g.EdgeStartIndex);
					writer.Write(g.NumEdges);
					writer.Write(g.Unknown1);
					writer.Write(g.Unknown2);
					writer.Write(g.Unknown3);
					writer.Write(g.Unknown4);
					writer.Write(g.TexCoordStartIndex);
					writer.Write(g.AreTexturesUsed);
					writer.Write(g.TextureNumber);
				}

				// ---- bounding boxes : count is 0, nothing written
			}

			File.Copy(tempPath, filePath, true);
			File.Delete(tempPath);
		}


		// ---------------------------------------------------------------- PART B ----
		// REPLACE the existing Save() with this. Only change: an else branch that
		// creates a new file when there is no original to patch.
		public static void Save(string filePath, PModel model, string originalFilePath = null)
		{
			bool hasOriginal = !string.IsNullOrEmpty(originalFilePath)
							   && File.Exists(originalFilePath);

			if (!hasOriginal)
			{
				// Imported mesh or brand new part - build a full P file from scratch
				SaveNew(filePath, model);
				return;
			}

			// ---- existing behaviour below, unchanged ----
			string tempPath = filePath + ".tmp";

			if (originalFilePath == filePath)
			{
				byte[] originalBytes = File.ReadAllBytes(originalFilePath);
				File.WriteAllBytes(tempPath, originalBytes);
			}
			else
			{
				File.Copy(originalFilePath, tempPath, true);
			}

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

			File.Copy(tempPath, filePath, true);
			File.Delete(tempPath);
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