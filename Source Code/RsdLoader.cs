using System;
using System.IO;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class RsdLoader
    {
        public static RsdFile Load(string filePath)
        {
            var rsd = new RsdFile();
            try
            {
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("@") || line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim().ToUpper();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "PLY":
							rsd.PolygonFile = val;
							if (!val.EndsWith(".P", StringComparison.OrdinalIgnoreCase) && !val.EndsWith(".PLY", StringComparison.OrdinalIgnoreCase))
								rsd.PolygonFile = val + ".P";
							// Normalize: if it ends with .PLY, change to .P (the actual file extension)
							if (rsd.PolygonFile.EndsWith(".PLY", StringComparison.OrdinalIgnoreCase))
								rsd.PolygonFile = rsd.PolygonFile.Substring(0, rsd.PolygonFile.Length - 4) + ".P";
							break;
						case "MAT": rsd.MaterialFile = val; break;
                        case "NTEX": int.TryParse(val, out int n); rsd.TextureCount = n; break;
                        default:
                            if (key.StartsWith("TEX[")) rsd.TextureFiles.Add(val);
                            break;
                    }
                }
            }
            catch { }
            return rsd;
        }
    }
}
