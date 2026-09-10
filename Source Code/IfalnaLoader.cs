// Parses ifalna.fil, the community model/animation index.
//
// Format (verified against the real file):
//   ACGDNames=main_ballet.char,main_ballet02.char,
//   ACGDAnims=ADCB,ADCC,ADCD,...
//   ACGDAnims2=DCGC,DCGD,...          <- continuation, up to Anims3
//
// The 4-char code is the HRC filename stem, so ACGD.hrc -> "ACGD".
// 370 models carry anim lists; ACGD has 255 animations.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Kimera2.IO
{
    public static class IfalnaLoader
    {
        // model code -> animation codes (Anims + Anims2 + Anims3 merged)
        private static readonly Dictionary<string, List<string>> _anims =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<string>> _names =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static bool IsLoaded { get; private set; }
        public static string LoadError { get; private set; } = "";
        public static int ModelCount => _anims.Count;

        // Look for ifalna.fil next to the exe, then in the supplied folders.
        public static bool TryLoad(params string[] extraDirs)
        {
            if (IsLoaded) return true;

            var candidates = new List<string>();
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "ifalna.fil"));
            if (extraDirs != null)
                foreach (var d in extraDirs)
                    if (!string.IsNullOrEmpty(d))
                        candidates.Add(Path.Combine(d, "ifalna.fil"));

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    Parse(path);
                    IsLoaded = true;
                    LoadError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LoadError = $"Failed to parse {path}: {ex.Message}";
                    return false;
                }
            }

            LoadError = "ifalna.fil not found (looked next to the exe and in the model folder)";
            return false;
        }

        private static void Parse(string path)
        {
            _anims.Clear();
            _names.Clear();

            var animRx  = new Regex(@"^([A-Za-z]{4})Anims\d*$");
            var nameRx  = new Regex(@"^([A-Za-z]{4})Names$");

            foreach (string rawLine in File.ReadLines(path))
            {
                string line = rawLine.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Replace("\r", "");

                var am = animRx.Match(key);
                if (am.Success)
                {
                    string code = am.Groups[1].Value;
                    if (!_anims.TryGetValue(code, out var list))
                    {
                        list = new List<string>();
                        _anims[code] = list;
                    }
                    foreach (string a in val.Split(','))
                    {
                        string t = a.Trim();
                        if (t.Length > 0) list.Add(t);
                    }
                    continue;
                }

                var nm = nameRx.Match(key);
                if (nm.Success)
                {
                    var list = new List<string>();
                    foreach (string n in val.Split(','))
                    {
                        string t = n.Trim();
                        if (t.Length > 0) list.Add(t);
                    }
                    _names[nm.Groups[1].Value] = list;
                }
            }
        }

        // Animation codes for a model. Returns null when the model is unknown -
        // callers should fall back to showing everything.
        public static List<string> GetAnimCodes(string modelCode)
        {
            if (string.IsNullOrEmpty(modelCode)) return null;
            return _anims.TryGetValue(modelCode, out var list) ? list : null;
        }

        // Friendly names, e.g. ACGD -> main_ballet.char
        public static List<string> GetModelNames(string modelCode)
        {
            if (string.IsNullOrEmpty(modelCode)) return null;
            return _names.TryGetValue(modelCode, out var list) ? list : null;
        }
    }
}
