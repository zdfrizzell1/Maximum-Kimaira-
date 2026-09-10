using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Kimera2.Models;

namespace Kimera2.IO
{
    // Imports external mesh formats (3DS, OBJ) and converts them to FF7 PModel format.
    // 3DS chunk logic ported from the original Kimera VB6 Model3DS_Module.
    //
    // HARD LIMIT: PPolygon indices are ushort, so a model can address at most
    // 65,535 vertices. Import will throw if the source mesh exceeds that.
    public static class MeshImporter
    {
        public const int MaxVertices = 65535;

        public static PModel Import(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".3ds") return Import3ds(filePath);
            if (ext == ".obj") return ImportObj(filePath);
            throw new NotSupportedException($"Unsupported mesh format: {ext}");
        }

        // ============================================================
        // OBJ IMPORT
        // Texture filenames go into model.RsdTextureNames, which SaveRsd alreadywrites out as NTEX= / TEX[n]= lines. 
		// PGroup.TextureNumber indexes that list.
		private class ObjMaterial
		{
			public string Name = "";
			public string TextureFile = "";   // from map_Kd
		}

		// One face corner: which vertex, which UV
		private struct Corner
		{
			public int V;
			public int T;   // -1 if the face had no UV
		}

		public static PModel ImportObj(string filePath)
		{
			string dir = Path.GetDirectoryName(filePath) ?? "";

			var rawVerts = new List<Vertex3D>();
			var rawUVs   = new List<TexCoord>();

			var materials = new List<ObjMaterial>();
			var faceGroups = new Dictionary<string, List<Corner[]>>();   // material -> triangles
			string currentMtl = "";
			faceGroups[""] = new List<Corner[]>();                        // default bucket

			foreach (string rawLine in File.ReadLines(filePath))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || line[0] == '#') continue;
				var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length == 0) continue;

				switch (p[0].ToLower())
				{
					case "mtllib":
						if (p.Length >= 2)
						{
							string mtlPath = Path.Combine(dir, string.Join(" ", p, 1, p.Length - 1));
							if (File.Exists(mtlPath)) ParseMtl(mtlPath, materials);
						}
						break;

					case "usemtl":
						currentMtl = p.Length >= 2 ? string.Join(" ", p, 1, p.Length - 1) : "";
						if (!faceGroups.ContainsKey(currentMtl))
							faceGroups[currentMtl] = new List<Corner[]>();
						break;

					case "v":
						if (p.Length >= 4)
						{
							// OBJ is Y-up, FF7 wants Y/Z swapped
							rawVerts.Add(new Vertex3D {
								X = ParseFloat(p[1]), Y = ParseFloat(p[3]), Z = ParseFloat(p[2]) });
						}
						break;

					case "vt":
						if (p.Length >= 3)
							rawUVs.Add(new TexCoord { U = ParseFloat(p[1]), V = 1f - ParseFloat(p[2]) });
						break;

					case "f":
					{
						var corners = new List<Corner>();
						for (int i = 1; i < p.Length; i++)
						{
							var bits = p[i].Split('/');
							int vi = 0, ti = -1;
							if (bits.Length > 0 && int.TryParse(bits[0], out vi))
								vi = vi > 0 ? vi - 1 : rawVerts.Count + vi;
							else continue;
							if (bits.Length > 1 && bits[1].Length > 0 && int.TryParse(bits[1], out int t))
								ti = t > 0 ? t - 1 : rawUVs.Count + t;
							corners.Add(new Corner { V = vi, T = ti });
						}
						// fan-triangulate
						for (int i = 2; i < corners.Count; i++)
							faceGroups[currentMtl].Add(new[] { corners[0], corners[i - 1], corners[i] });
						break;
					}
				}
			}

			// ---- build the model, one PGroup per material ----
			var verts     = new List<Vertex3D>();
			var texCoords = new List<TexCoord>();
			var polys     = new List<PPolygon>();
			var groups    = new List<PGroup>();
			var texNames  = new List<string>();

			foreach (var kv in faceGroups)
			{
				var tris = kv.Value;
				if (tris.Count == 0) continue;

				int vertStart = verts.Count;
				int polyStart = polys.Count;

				// Split vertices so each (vertex, uv) pair becomes its own vertex.
				// FF7 indexes TexCoords by vertex index, so a vertex cannot carry two UVs.
				var map = new Dictionary<long, int>();   // (v,t) packed -> group-local index

				foreach (var tri in tris)
				{
					var idx = new int[3];
					for (int c = 0; c < 3; c++)
					{
						long key = ((long)tri[c].V << 32) ^ (uint)(tri[c].T + 1);
						if (!map.TryGetValue(key, out int local))
						{
							local = verts.Count - vertStart;
							map[key] = local;

							verts.Add(tri[c].V >= 0 && tri[c].V < rawVerts.Count
									  ? rawVerts[tri[c].V] : new Vertex3D());

							texCoords.Add(tri[c].T >= 0 && tri[c].T < rawUVs.Count
										  ? rawUVs[tri[c].T] : new TexCoord { U = 0f, V = 0f });
						}
						idx[c] = local;
					}
					polys.Add(MakePoly(idx[0], idx[1], idx[2]));
				}

				// Resolve this material's texture into the shared texture list
				int texNum = 0;
				bool hasTex = false;
				var mat = materials.Find(m => m.Name == kv.Key);
				if (mat != null && !string.IsNullOrEmpty(mat.TextureFile))
				{
					int existing = texNames.IndexOf(mat.TextureFile);
					if (existing < 0) { texNames.Add(mat.TextureFile); existing = texNames.Count - 1; }
					texNum = existing;
					hasTex = true;
				}

				groups.Add(new PGroup
				{
					PrimitiveType      = 3,
					PolygonStartIndex  = polyStart,
					NumPolygons        = polys.Count - polyStart,
					VerticesStartIndex = vertStart,
					NumVertices        = verts.Count - vertStart,
					EdgeStartIndex     = 0,
					NumEdges           = 0,
					TexCoordStartIndex = vertStart,   // UVs run parallel to vertices
					AreTexturesUsed    = hasTex ? 1 : 0,
					TextureNumber      = texNum
				});
			}

			var model = BuildModel(filePath, verts, texCoords, polys);
			model.Groups = groups;
			model.GroupLoadSuccess = new List<bool>();
			for (int i = 0; i < groups.Count; i++) model.GroupLoadSuccess.Add(true);
			model.Header.NumGroups = groups.Count;
			model.RsdTextureNames = texNames;
			return model;
		}

		// Parse an .mtl file into an ordered material list
		private static void ParseMtl(string mtlPath, List<ObjMaterial> materials)
		{
			ObjMaterial cur = null;
			foreach (string rawLine in File.ReadLines(mtlPath))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || line[0] == '#') continue;
				var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length < 2) continue;

				if (p[0].Equals("newmtl", StringComparison.OrdinalIgnoreCase))
				{
					cur = new ObjMaterial { Name = string.Join(" ", p, 1, p.Length - 1) };
					materials.Add(cur);
				}
				else if (cur != null && p[0].Equals("map_Kd", StringComparison.OrdinalIgnoreCase))
				{
					// last token is the filename; skip any -options
					string file = p[p.Length - 1];
					cur.TextureFile = Path.GetFileName(file);   // strip any path
				}
			}
		}

        // ============================================================
        // 3DS IMPORT, does NOT grab texture file
        public static PModel Import3ds(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length < 6) throw new InvalidDataException("File too small to be a 3DS");
            if (BitConverter.ToUInt16(data, 0) != 0x4D4D)
                throw new InvalidDataException("Not a valid 3DS file (missing 0x4D4D header)");

            var verts = new List<Vertex3D>();
            var texCoords = new List<TexCoord>();
            var polys = new List<PPolygon>();

            // vertBase is threaded through the recursion instead of using a static field,
            // so multiple meshes offset correctly and repeat imports stay clean.
            int vertBase = 0;
            ParseChunks(data, 6, data.Length, verts, texCoords, polys, ref vertBase);

            if (verts.Count == 0)
                throw new InvalidDataException("No geometry found in 3DS file");

            return BuildModel(filePath, verts, texCoords, polys);
        }

        private static void ParseChunks(byte[] data, int offset, int end,
            List<Vertex3D> verts, List<TexCoord> texCoords, List<PPolygon> polys, ref int vertBase)
        {
            while (offset + 6 <= end)
            {
                ushort id = BitConverter.ToUInt16(data, offset);
                uint len = BitConverter.ToUInt32(data, offset + 2);
                if (len < 6) break;
                int chunkEnd = offset + (int)len;
                if (chunkEnd > end) chunkEnd = end;
                int body = offset + 6;

                switch (id)
                {
                    case 0x3D3D: // Editor / mesh data
                    case 0x4100: // Triangular mesh
                        ParseChunks(data, body, chunkEnd, verts, texCoords, polys, ref vertBase);
                        break;

                    case 0x4000: // Named object: skip null-terminated name, then recurse
                        int p = body;
                        while (p < chunkEnd && data[p] != 0) p++;
                        p++;
                        ParseChunks(data, p, chunkEnd, verts, texCoords, polys, ref vertBase);
                        break;

                    case 0x4110: // Vertex list
                    {
                        vertBase = verts.Count; // faces in this mesh are relative to here
                        ushort n = BitConverter.ToUInt16(data, body);
                        int vp = body + 2;
                        for (int i = 0; i < n && vp + 12 <= chunkEnd; i++)
                        {
                            float x = BitConverter.ToSingle(data, vp);
                            float y = BitConverter.ToSingle(data, vp + 4);
                            float z = BitConverter.ToSingle(data, vp + 8);
                            // 3DS is Z-up, FF7 is Y-up
                            verts.Add(new Vertex3D { X = x, Y = z, Z = y });
                            vp += 12;
                        }
                        break;
                    }

                    case 0x4120: // Face list
                    {
                        ushort n = BitConverter.ToUInt16(data, body);
                        int fp = body + 2;
                        for (int i = 0; i < n && fp + 8 <= chunkEnd; i++)
                        {
                            int a = vertBase + BitConverter.ToUInt16(data, fp);
                            int b = vertBase + BitConverter.ToUInt16(data, fp + 2);
                            int c = vertBase + BitConverter.ToUInt16(data, fp + 4);
                            // fp + 6 is the edge-visibility flags word; unused for rendering
                            polys.Add(MakePoly(a, b, c));
                            fp += 8;
                        }
                        break;
                    }

                    case 0x4140: // Texture coordinates
                    {
                        ushort n = BitConverter.ToUInt16(data, body);
                        int tp = body + 2;
                        for (int i = 0; i < n && tp + 8 <= chunkEnd; i++)
                        {
                            float u = BitConverter.ToSingle(data, tp);
                            float v = BitConverter.ToSingle(data, tp + 4);
                            texCoords.Add(new TexCoord { U = u, V = 1f - v });
                            tp += 8;
                        }
                        break;
                    }

                    // Materials (0xAFFF etc.) and transforms (0x4160) are skipped
                }

                offset = chunkEnd;
            }
        }

        // ============================================================
        // Build the PModel (shared by both importers)
        // ============================================================
        private static PModel BuildModel(string filePath, List<Vertex3D> verts,
            List<TexCoord> texCoords, List<PPolygon> polys)
        {
            if (verts.Count > MaxVertices)
                throw new InvalidDataException(
                    $"Mesh has {verts.Count:N0} vertices. The FF7 P format indexes vertices " +
                    $"with 16-bit values, so the maximum is {MaxVertices:N0}. " +
                    "Decimate the mesh before importing.");

            var model = new PModel
            {
                FileName = Path.GetFileNameWithoutExtension(filePath) + ".p",
                OriginalFilePath = filePath,
                Vertices = verts,
                Polygons = polys,
                TexCoords = texCoords,
                Normals = ComputeNormals(verts, polys)
            };

            // Default vertex colors - light grey so the mesh is visible untextured.
            // ColorBGRA field order is B, G, R, A.
            model.VertexColors = new List<ColorBGRA>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
                model.VertexColors.Add(new ColorBGRA { B = 200, G = 200, R = 200, A = 255 });

            // --- 1. PrimitiveType must be 3, not 1. PFileLoader's fallback pattern scan searches for [3, 0, NumPolygons, 0, NumVertices]
			// so a group written with PrimitiveType = 1 would not be recognised.
			model.Groups = new List<PGroup>
			{
				new PGroup
				{
					PrimitiveType      = 3,          // was 1 - WRONG
					PolygonStartIndex  = 0,
					NumPolygons        = polys.Count,
					VerticesStartIndex = 0,
					NumVertices        = verts.Count,
					EdgeStartIndex     = 0,
					NumEdges           = 0,
					TexCoordStartIndex = 0,
					AreTexturesUsed    = texCoords.Count > 0 ? 1 : 0,
					TextureNumber      = 0
				}
			};
			model.GroupLoadSuccess = new List<bool> { true };


			// --- 2. Give every polygon a colour ----------------------------------------
			// The polygonColors section on disk is sized by NumPolygons, so this list
			// should have one entry per polygon. Add this right after VertexColors.
			model.PolygonColors = new List<ColorBGRA>(polys.Count);
			for (int i = 0; i < polys.Count; i++)
				model.PolygonColors.Add(new ColorBGRA { B = 200, G = 200, R = 200, A = 255 });


			// --- 3. NumHundreds must be 0, not 1 --------------------------------------
			// We write no "hundreds" section, so claiming 1 would shift every later
			// offset by 104 bytes. SaveNew recomputes the header anyway, but fix it here
			// too so the in-memory header is honest.
			model.Header = new PFileHeader
			{
				Version            = 1,
				Off04              = 1,
				VertexType         = 1,
				NumVertices        = verts.Count,
				NumNormals         = model.Normals.Count,
				NumUnknown1        = 0,
				NumTexCoords       = texCoords.Count,
				NumVertexColors    = model.VertexColors.Count,
				NumEdges           = 0,
				NumPolygons        = polys.Count,
				NumUnknown2        = 0,
				NumUnknown3        = 0,
				NumHundreds        = 0,          // was 1 - WRONG
				NumGroups          = 1,
				NumBoundingBoxes   = 0,
				NormIndexTableFlag = 0
			};

            model.ScaleX = 1f; model.ScaleY = 1f; model.ScaleZ = 1f;
            return model;
        }

        private static PPolygon MakePoly(int a, int b, int c)
        {
            return new PPolygon
            {
                Zero = 0,
                VertexIndex1 = (ushort)a,
                VertexIndex2 = (ushort)b,
                VertexIndex3 = (ushort)c,
                NormalIndex1 = (ushort)a,
                NormalIndex2 = (ushort)b,
                NormalIndex3 = (ushort)c,
                EdgeIndex1 = 0,
                EdgeIndex2 = 0,
                EdgeIndex3 = 0,
                Unknown1 = 0,
                Unknown2 = 0
            };
        }

        // Average face normals into per-vertex normals
        private static List<Vertex3D> ComputeNormals(List<Vertex3D> verts, List<PPolygon> polys)
        {
            var acc = new Vertex3D[verts.Count];

            foreach (var poly in polys)
            {
                int i1 = poly.VertexIndex1, i2 = poly.VertexIndex2, i3 = poly.VertexIndex3;
                if (i1 >= verts.Count || i2 >= verts.Count || i3 >= verts.Count) continue;

                var v1 = verts[i1]; var v2 = verts[i2]; var v3 = verts[i3];

                float ax = v2.X - v1.X, ay = v2.Y - v1.Y, az = v2.Z - v1.Z;
                float bx = v3.X - v1.X, by = v3.Y - v1.Y, bz = v3.Z - v1.Z;
                float nx = ay * bz - az * by;
                float ny = az * bx - ax * bz;
                float nz = ax * by - ay * bx;

                acc[i1].X += nx; acc[i1].Y += ny; acc[i1].Z += nz;
                acc[i2].X += nx; acc[i2].Y += ny; acc[i2].Z += nz;
                acc[i3].X += nx; acc[i3].Y += ny; acc[i3].Z += nz;
            }

            var result = new List<Vertex3D>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                float x = acc[i].X, y = acc[i].Y, z = acc[i].Z;
                float len = (float)Math.Sqrt(x * x + y * y + z * z);
                if (len > 0.0001f)
                    result.Add(new Vertex3D { X = x / len, Y = y / len, Z = z / len });
                else
                    result.Add(new Vertex3D { X = 0f, Y = 1f, Z = 0f });
            }
            return result;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
        }

        // Rescale an imported mesh so its largest dimension matches targetSize.
        // Imported models are usually in completely different units than FF7.
        public static void NormalizeScale(PModel model, float targetSize)
        {
            if (model.Vertices.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in model.Vertices)
            {
                if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
                if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
            }

            float largest = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            if (largest < 0.0001f) return;

            float scale = targetSize / largest;
            for (int i = 0; i < model.Vertices.Count; i++)
            {
                var v = model.Vertices[i];
                model.Vertices[i] = new Vertex3D { X = v.X * scale, Y = v.Y * scale, Z = v.Z * scale };
            }
        }
    }
}