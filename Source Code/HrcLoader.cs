using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class HrcLoader
    {
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
		
		public static void Save(string filePath, Skeleton skeleton, string originalFilePath)
		{
			string[] lines = File.ReadAllLines(originalFilePath);
			
			int idx = 0;
			// Skip header lines (starting with :) so those do not get removed
			while (idx < lines.Length && lines[idx].TrimStart().StartsWith(":"))
				idx++;
			
			// Now replace bone lengths in place
			int boneIdx = 0;
			while (idx < lines.Length && boneIdx < skeleton.Bones.Count)
			{
				while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
				if (idx >= lines.Length) break;
				
				idx++; // bone name - keep as-is
				idx++; // parent name - keep as-is
				
				// Replace the length line
				if (idx < lines.Length)
					lines[idx] = skeleton.Bones[boneIdx].Length.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
				idx++;
				
				idx++; // rsd line - keep as-is
				boneIdx++;
			}
			
			File.WriteAllLines(filePath, lines);
		}

    }
}
