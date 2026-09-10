using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class SkeletonLoader
    {
        //Field and Battle skeleton loading
		public static Skeleton Load(string filePath)
        {
            var skeleton = new Skeleton();
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int idx = 0;

                while (idx < lines.Length && lines[idx].TrimStart().StartsWith(":"))
                {
                    string line = lines[idx].Trim();
                    if (line.StartsWith(":SKELETON"))
                        skeleton.Name = line.Substring(":SKELETON".Length).Trim();
                    idx++;
                }
				// Check for required header lines
				bool hasSkeleton = lines.Any(l => l.TrimStart().StartsWith(":SKELETON"));
				bool hasBones = lines.Any(l => l.TrimStart().StartsWith(":BONES"));
				if (!hasSkeleton || !hasBones)
					skeleton.LoadWarnings.Add("WARNING: HRC file may be corrupted - missing :SKELETON or :BONES header");

                while (idx < lines.Length)
                {
                    while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
                    if (idx >= lines.Length - 3) break;

                    var bone = new Bone();
                    bone.Name = lines[idx++].Trim();
                    bone.ParentName = lines[idx++].Trim();
                    if (float.TryParse(lines[idx++].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float len))
                        bone.Length = len;

                    string rsdLine = lines[idx++].Trim();
                    string[] parts = rsdLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[0], out int rsdCount))
                        for (int i = 1; i <= rsdCount && i < parts.Length; i++)
                            bone.RsdNames.Add(parts[i]);

                    skeleton.Bones.Add(bone);
                }

                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    var bone = skeleton.Bones[i];
                    if (bone.ParentName.Equals("root", StringComparison.OrdinalIgnoreCase))
                        bone.ParentIndex = -1;
                    else
                        bone.ParentIndex = skeleton.Bones.FindIndex(
                            b => b.Name.Equals(bone.ParentName, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex) { skeleton.LoadWarnings.Add($"HRC error: {ex.Message}"); }
            return skeleton;
        }
		
		// Generate a unique part name based on the bone name: ABCD -> ABCD1, ABCD2...
		public static string GenerateUniquePartName(Skeleton skeleton, int boneIndex, string dir)
		{
			var bone = skeleton.Bones[boneIndex];

			// --- work out the base name ---
			string baseName = null;

			// Preferred: the first real P file already attached to this bone.
			// Skip imports we added earlier (they have no OriginalFilePath).
			foreach (var m in bone.Models)
			{
				if (string.IsNullOrEmpty(m.FileName)) continue;
				if (string.IsNullOrEmpty(m.OriginalFilePath)) continue;
				baseName = Path.GetFileNameWithoutExtension(m.FileName);
				break;
			}

			// Fallback 1: the bone's existing RSD name
			if (string.IsNullOrEmpty(baseName) && bone.RsdNames.Count > 0)
				baseName = bone.RsdNames[0];

			// Fallback 2: the bone name, stripped to letters and digits
			if (string.IsNullOrEmpty(baseName))
			{
				var sb = new System.Text.StringBuilder();
				foreach (char c in bone.Name ?? "")
					if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpper(c));
				baseName = sb.Length > 0 ? sb.ToString() : "PART";
			}

			baseName = baseName.ToUpper();

			// If the base already ends in digits (e.g. ADAA1), strip them so we
			// increment from the root rather than producing ADAA11.
			int cut = baseName.Length;
			while (cut > 1 && char.IsDigit(baseName[cut - 1])) cut--;
			baseName = baseName.Substring(0, cut);

			// --- collect every name already taken anywhere in the skeleton ---
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var b in skeleton.Bones)
			{
				foreach (var n in b.RsdNames)
					used.Add(n);
				foreach (var m in b.Models)
					if (!string.IsNullOrEmpty(m.FileName))
						used.Add(Path.GetFileNameWithoutExtension(m.FileName));
			}

			// --- find the first free suffix, also checking the folder on disk ---
			for (int i = 1; i < 1000; i++)
			{
				string candidate = baseName + i;

				if (used.Contains(candidate)) continue;

				if (!string.IsNullOrEmpty(dir))
				{
					if (File.Exists(Path.Combine(dir, candidate + ".rsd"))) continue;
					if (File.Exists(Path.Combine(dir, candidate + ".RSD"))) continue;
					if (File.Exists(Path.Combine(dir, candidate + ".p")))   continue;
					if (File.Exists(Path.Combine(dir, candidate + ".P")))   continue;
				}

				return candidate;
			}

			throw new InvalidOperationException(
				$"Could not find a free part name based on '{baseName}'");
		}

		// Attach a model to a bone - IN MEMORY ONLY, nothing written to disk
		public static string AttachPart(Skeleton skeleton, int boneIndex, PModel model, string dir)
		{
			if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
			if (boneIndex < 0 || boneIndex >= skeleton.Bones.Count)
				throw new ArgumentOutOfRangeException(nameof(boneIndex));

			string partName = GenerateUniquePartName(skeleton, boneIndex, dir);

			// Name the P file to match the part
			model.FileName = partName + ".p";

			// Imported meshes have no P-file ancestor. Clear this so PFileSaver does not
			// try to use an .obj/.3ds path as a template.
			model.OriginalFilePath = "";

			var bone = skeleton.Bones[boneIndex];
			bone.Models.Add(model);      // renders immediately
			bone.RsdNames.Add(partName); // makes it persist when SaveHrc runs

			return partName;
		}

		public static bool RemovePart(Skeleton skeleton, int boneIndex, PModel model,
									 out string removedRsdName)
		{
			removedRsdName = null;

			if (skeleton == null) return false;
			if (boneIndex < 0 || boneIndex >= skeleton.Bones.Count) return false;

			var bone = skeleton.Bones[boneIndex];

			int modelIdx = bone.Models.IndexOf(model);
			if (modelIdx < 0) return false;

			// Work out which RSD name belongs to this model.
			// Imported parts use  <rsdName>.p  so try a name match first.
			string stem = Path.GetFileNameWithoutExtension(model.FileName ?? "");
			int rsdIdx = -1;
			for (int i = 0; i < bone.RsdNames.Count; i++)
			{
				if (bone.RsdNames[i].Equals(stem, StringComparison.OrdinalIgnoreCase))
				{
					rsdIdx = i;
					break;
				}
			}

			// Original FF7 parts have a different RSD name than P name
			// (RSD ACJD -> ACJE.P), so fall back to positional pairing.
			if (rsdIdx < 0 && modelIdx < bone.RsdNames.Count)
				rsdIdx = modelIdx;

			if (rsdIdx >= 0 && rsdIdx < bone.RsdNames.Count)
			{
				removedRsdName = bone.RsdNames[rsdIdx];
				bone.RsdNames.RemoveAt(rsdIdx);
			}

			bone.Models.RemoveAt(modelIdx);
			return true;
		}

		// Write an .rsd file for a part
		public static void SaveRsd(string rsdPath, string rsdName, string plyName,
								   List<string> textureNames)
		{
			// plyName should be the P file stem with no extension, e.g. "ACHD1"
			var lines = new List<string>();
			lines.Add("@RSD940102");
			lines.Add("PLY=" + plyName + ".PLY");
			lines.Add("MAT=" + plyName + ".MAT");
			lines.Add("GRP=" + plyName + ".GRP");

			int nTex = textureNames?.Count ?? 0;
			lines.Add("NTEX=" + nTex);
			for (int i = 0; i < nTex; i++)
				lines.Add("TEX[" + i + "]=" + textureNames[i]);

			File.WriteAllLines(rsdPath, lines);
		}

		// Write .rsd files for any part that doesn't have one yet - safe to call on every save, no dirty tracking needed.
		// FIX: the old version paired bone.RsdNames[i] with bone.Models[i] by index. That is unsafe - a bone can have more RsdNames than Models (duplicate RSD
		// entries in the HRC), so the indexes drift. This version matches by NAME. Returns a list of log lines so MainForm can show exactly what happened.
		public static List<string> SaveMissingRsdFiles(Skeleton skeleton, string destDir)
		{
			var log = new List<string>();

			for (int bi = 0; bi < skeleton.Bones.Count; bi++)
			{
				var bone = skeleton.Bones[bi];

				for (int ri = 0; ri < bone.RsdNames.Count; ri++)
				{
					string rsdName = bone.RsdNames[ri];

					string destLower = Path.Combine(destDir, rsdName + ".rsd");
					string destUpper = Path.Combine(destDir, rsdName + ".RSD");
					if (File.Exists(destLower) || File.Exists(destUpper))
						continue;   // already written or copied - leave it alone

					// Find the model this RSD should point at.
					// New parts from AttachPart use  <rsdName>.p  so match by name first.
					PModel match = null;
					foreach (var m in bone.Models)
					{
						string stem = Path.GetFileNameWithoutExtension(m.FileName ?? "");
						if (stem.Equals(rsdName, StringComparison.OrdinalIgnoreCase))
						{
							match = m;
							break;
						}
					}

					// Fall back to positional pairing for original FF7 parts, where the
					// RSD name and P name differ (RSD ACHC -> ACHD.P).
					if (match == null && ri < bone.Models.Count)
						match = bone.Models[ri];

					if (match == null)
					{
						log.Add($"RSD skipped: '{rsdName}' on bone {bi} '{bone.Name}' - no model to point at");
						continue;
					}

					string plyName = Path.GetFileNameWithoutExtension(match.FileName ?? rsdName);

					SaveRsd(destLower, rsdName, plyName, match.RsdTextureNames);
					log.Add($"Wrote RSD: {rsdName}.rsd -> PLY={plyName}.PLY " +
							$"({match.RsdTextureNames?.Count ?? 0} texture ref(s))");
				}
			}

			if (log.Count == 0)
				log.Add("No new RSD files needed");

			return log;
		}

		// ----------------------------------------------------------------------------
		//    Difference: the RSD list line is now rewritten from bone.RsdNames instead of being preserved from the original file.
		public static void SaveHrc(string filePath, Skeleton skeleton, string originalFilePath)
		{
			string[] lines = File.ReadAllLines(originalFilePath);
			var output = new List<string>();

			int idx = 0;

			// Copy header lines (":HEADER_BLOCK", ":SKELETON", ":BONES") unchanged
			while (idx < lines.Length && lines[idx].TrimStart().StartsWith(":"))
				output.Add(lines[idx++]);

			int boneIdx = 0;
			while (idx < lines.Length && boneIdx < skeleton.Bones.Count)
			{
				// Preserve blank lines and comments between bone blocks
				while (idx < lines.Length &&
					   (string.IsNullOrWhiteSpace(lines[idx]) || lines[idx].TrimStart().StartsWith("#")))
					output.Add(lines[idx++]);

				if (idx >= lines.Length) break;

				var bone = skeleton.Bones[boneIdx];

				output.Add(lines[idx++]);   // bone name   - unchanged
				if (idx < lines.Length) output.Add(lines[idx++]);   // parent name - unchanged

				// bone length - write the current in-memory value
				if (idx < lines.Length)
				{
					output.Add(bone.Length.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
					idx++;
				}

				// RSD list - rebuild from bone.RsdNames so new parts are included
				if (idx < lines.Length)
				{
					if (bone.RsdNames.Count > 0)
						output.Add(bone.RsdNames.Count + " " + string.Join(" ", bone.RsdNames));
					else
						output.Add("0");
					idx++;
				}

				boneIdx++;
			}

			// Copy any trailing content
			while (idx < lines.Length)
				output.Add(lines[idx++]);

			File.WriteAllLines(filePath, output);
		}
		
		//Battle Skeleton saving
		public static void SaveBattle(string filePath, Skeleton skeleton, string originalFilePath)
		{
			// Read original into memory
			byte[] skelData = File.ReadAllBytes(originalFilePath);	
			// Patch bone lengths
			for (int i = 0; i < skeleton.Bones.Count; i++)
			{
				int off = 0x34 + i * 12 + 4;
				byte[] lengthBytes = BitConverter.GetBytes(-Math.Abs(skeleton.Bones[i].Length));
				Array.Copy(lengthBytes, 0, skelData, off, 4);
			}	
			// Write to skelDESTination (safe even if same path - data is in memory)
			File.WriteAllBytes(filePath, skelData);
		}

    }
}
