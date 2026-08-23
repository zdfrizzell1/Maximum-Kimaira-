using System;
using System.Collections.Generic;
using System.IO;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class PFileLoader
    {
        public static PModel Load(string filePath)
        {
            var model = new PModel { FileName = Path.GetFileName(filePath) };
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, bufferSize: 65536);
                using var reader = new BinaryReader(fs);
                long fileSize = fs.Length;

                // Read header (128 bytes: 64 bytes fields + 64 bytes runtime)
                model.Header = ReadHeader(reader);

                // Calculate section offsets directly from header
                long offset = 128; // header size
                long verticesOffset = offset;
                long normalsOffset = verticesOffset + 12L * model.Header.NumVertices;
                long unknown1Offset = normalsOffset + 12L * model.Header.NumNormals;
                long texCoordsOffset = unknown1Offset + 12L * model.Header.NumUnknown1;
                long vertColorsOffset = texCoordsOffset + 8L * model.Header.NumTexCoords;
                long polyColorsOffset = vertColorsOffset + 4L * model.Header.NumVertexColors;
                long edgesOffset = polyColorsOffset + 4L * model.Header.NumPolygons;
                long polygonsOffset = edgesOffset + 4L * model.Header.NumEdges;
                long unknown2Offset = polygonsOffset + 24L * model.Header.NumPolygons;
                long unknown3Offset = unknown2Offset + 24L * model.Header.NumUnknown2;
                long hundredsOffset = unknown3Offset + 4L * model.Header.NumUnknown3;
                long groupsOffset = hundredsOffset + 104L * model.Header.NumHundreds;
                long bboxOffset = groupsOffset + 56L * model.Header.NumGroups;

                // Read vertices
                fs.Seek(verticesOffset, SeekOrigin.Begin);
                ReadVertices(reader, model);

                // Read normals
                fs.Seek(normalsOffset, SeekOrigin.Begin);
                ReadNormals(reader, model);

                // Read tex coords
                fs.Seek(texCoordsOffset, SeekOrigin.Begin);
                ReadTexCoords(reader, model);

                // Read vertex colors
                fs.Seek(vertColorsOffset, SeekOrigin.Begin);
                ReadVertexColors(reader, model);

                // Read polygon colors
                fs.Seek(polyColorsOffset, SeekOrigin.Begin);
                ReadPolygonColors(reader, model);

                // Read polygons
                fs.Seek(polygonsOffset, SeekOrigin.Begin);
                ReadPolygons(reader, model);

                // Read groups (the critical section!)
                fs.Seek(groupsOffset, SeekOrigin.Begin);
                ReadGroups(reader, model);

                // If groups read failed (all zeros), try alternate Unknown3 size
                if (model.Groups.Count > 0 && model.Groups[0].NumPolygons == 0)
                {
                    model.Groups.Clear();
                    model.LoadWarnings.Add("Retrying groups with alternate offset...");
                    // Try Unknown3 = 3 bytes each instead of 4
                    long altHundredsOffset = unknown2Offset + 24L * model.Header.NumUnknown2 + 3L * model.Header.NumUnknown3;
                    long altGroupsOffset = altHundredsOffset + 104L * model.Header.NumHundreds;
                    fs.Seek(altGroupsOffset, SeekOrigin.Begin);
                    ReadGroups(reader, model);
                }

                // If still failed, try with Hundreds = 100 bytes
                if (model.Groups.Count > 0 && model.Groups[0].NumPolygons == 0)
                {
                    model.Groups.Clear();
                    model.LoadWarnings.Add("Retrying with Hundreds=100 bytes...");
                    long alt2Offset = unknown3Offset + 4L * model.Header.NumUnknown3 + 100L * model.Header.NumHundreds;
                    fs.Seek(alt2Offset, SeekOrigin.Begin);
                    ReadGroups(reader, model);
                }

                // Read bounding boxes
                if (model.Groups.Count > 0 && model.Groups[0].NumPolygons > 0)
                {
                    // Recalculate bbox offset based on successful groups offset
                    long actualGroupsEnd = fs.Position;
                }

				// FALLBACK: Scan file for group data pattern
				if (model.Groups.Count > 0 && model.Groups[0].NumPolygons == 0)
				{
					model.Groups.Clear();
					// Search for the pattern: [3, 0, NumPolygons, 0, NumVertices]
					byte[] fileBytes = File.ReadAllBytes(filePath);
					int numPoly = model.Header.NumPolygons;
					int numVert = model.Header.NumVertices;
					byte[] pattern = new byte[20];
					BitConverter.GetBytes(3).CopyTo(pattern, 0);
					BitConverter.GetBytes(0).CopyTo(pattern, 4);
					BitConverter.GetBytes(numPoly).CopyTo(pattern, 8);
					BitConverter.GetBytes(0).CopyTo(pattern, 12);
					BitConverter.GetBytes(numVert).CopyTo(pattern, 16);

					for (int i = 128; i < fileBytes.Length - 56; i += 4)
					{
						if (fileBytes[i] == pattern[0] && fileBytes[i+4] == pattern[4] &&
							BitConverter.ToInt32(fileBytes, i) == 3 &&
							BitConverter.ToInt32(fileBytes, i + 4) == 0 &&
							BitConverter.ToInt32(fileBytes, i + 8) == numPoly &&
							BitConverter.ToInt32(fileBytes, i + 12) == 0 &&
							BitConverter.ToInt32(fileBytes, i + 16) == numVert)
						{
							// Found it! Read group from this offset
							fs.Seek(i, SeekOrigin.Begin);
							ReadGroups(reader, model);
							break;
						}
					}

					// If still not found, use synthetic group
					if (model.Groups.Count == 0)
					{
						model.Groups.Add(new PGroup
						{
							PrimitiveType = 3,
							PolygonStartIndex = 0,
							NumPolygons = model.Header.NumPolygons,
							VerticesStartIndex = 0,
							NumVertices = model.Header.NumVertices,
							EdgeStartIndex = 0,
							NumEdges = model.Header.NumEdges,
							TexCoordStartIndex = 0,
							AreTexturesUsed = 0,
							TextureNumber = 0
						});
					}
				}
            }
            catch (Exception ex)
            {
                model.LoadWarnings.Add($"Partial load: {ex.Message}");
            }
            return model;
        }

        private static PFileHeader ReadHeader(BinaryReader reader)
        {
            var h = new PFileHeader();
            h.Version = reader.ReadInt32(); h.Off04 = reader.ReadInt32();
            h.VertexType = reader.ReadInt32(); h.NumVertices = reader.ReadInt32();
            h.NumNormals = reader.ReadInt32(); h.NumUnknown1 = reader.ReadInt32();
            h.NumTexCoords = reader.ReadInt32(); h.NumVertexColors = reader.ReadInt32();
            h.NumEdges = reader.ReadInt32(); h.NumPolygons = reader.ReadInt32();
            h.NumUnknown2 = reader.ReadInt32(); h.NumUnknown3 = reader.ReadInt32();
            h.NumHundreds = reader.ReadInt32(); h.NumGroups = reader.ReadInt32();
            h.NumBoundingBoxes = reader.ReadInt32(); h.NormIndexTableFlag = reader.ReadInt32();
            reader.BaseStream.Seek(64, SeekOrigin.Current); // skip runtime data
            return h;
        }

        private static void ReadVertices(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumVertices;
            model.Vertices = new List<Vertex3D>(count);
            try { for (int i = 0; i < count; i++) model.Vertices.Add(new Vertex3D { X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle() }); }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"Vertices truncated: {model.Vertices.Count}/{count}"); }
        }

        private static void ReadNormals(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumNormals;
            model.Normals = new List<Vertex3D>(count);
            try { for (int i = 0; i < count; i++) model.Normals.Add(new Vertex3D { X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle() }); }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"Normals truncated"); }
        }

        private static void ReadTexCoords(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumTexCoords;
            model.TexCoords = new List<TexCoord>(count);
            try { for (int i = 0; i < count; i++) model.TexCoords.Add(new TexCoord { U = reader.ReadSingle(), V = reader.ReadSingle() }); }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"TexCoords truncated"); }
        }

        private static void ReadVertexColors(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumVertexColors;
            model.VertexColors = new List<ColorBGRA>(count);
            try { for (int i = 0; i < count; i++) model.VertexColors.Add(new ColorBGRA { B = reader.ReadByte(), G = reader.ReadByte(), R = reader.ReadByte(), A = reader.ReadByte() }); }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"VertexColors truncated"); }
        }

        private static void ReadPolygonColors(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumPolygons;
            model.PolygonColors = new List<ColorBGRA>(count);
            try { for (int i = 0; i < count; i++) model.PolygonColors.Add(new ColorBGRA { B = reader.ReadByte(), G = reader.ReadByte(), R = reader.ReadByte(), A = reader.ReadByte() }); }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"PolygonColors truncated"); }
        }

        private static void ReadPolygons(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumPolygons;
            model.Polygons = new List<PPolygon>(count);
            try
            {
                for (int i = 0; i < count; i++)
                    model.Polygons.Add(new PPolygon {
                        Zero = reader.ReadUInt16(), VertexIndex1 = reader.ReadUInt16(),
                        VertexIndex2 = reader.ReadUInt16(), VertexIndex3 = reader.ReadUInt16(),
                        NormalIndex1 = reader.ReadUInt16(), NormalIndex2 = reader.ReadUInt16(),
                        NormalIndex3 = reader.ReadUInt16(), EdgeIndex1 = reader.ReadUInt16(),
                        EdgeIndex2 = reader.ReadUInt16(), EdgeIndex3 = reader.ReadUInt16(),
                        Unknown1 = reader.ReadUInt16(), Unknown2 = reader.ReadUInt16()
                    });
            }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"Polygons truncated: {model.Polygons.Count}/{count}"); }
        }

        private static void ReadGroups(BinaryReader reader, PModel model)
        {
            int count = model.Header.NumGroups;
            model.Groups = new List<PGroup>(count);
            try
            {
                for (int i = 0; i < count; i++)
                    model.Groups.Add(new PGroup {
                        PrimitiveType = reader.ReadInt32(), PolygonStartIndex = reader.ReadInt32(),
                        NumPolygons = reader.ReadInt32(), VerticesStartIndex = reader.ReadInt32(),
                        NumVertices = reader.ReadInt32(), EdgeStartIndex = reader.ReadInt32(),
                        NumEdges = reader.ReadInt32(), Unknown1 = reader.ReadInt32(),
                        Unknown2 = reader.ReadInt32(), Unknown3 = reader.ReadInt32(),
                        Unknown4 = reader.ReadInt32(), TexCoordStartIndex = reader.ReadInt32(),
                        AreTexturesUsed = reader.ReadInt32(), TextureNumber = reader.ReadInt32()
                    });
            }
            catch (EndOfStreamException) { model.LoadWarnings.Add($"Groups truncated"); }
        }
    }
}