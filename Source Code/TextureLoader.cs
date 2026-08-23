using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Kimera2.Models;

namespace Kimera2.IO
{
    public class TextureLoader
    {
        public static Texture Load(string filePath, string baseDir)
        {
            var tex = new Texture { FileName = Path.GetFileName(filePath) };
            string fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(baseDir, filePath);

			if (!File.Exists(fullPath))
			{
				string baseName = Path.GetFileNameWithoutExtension(fullPath);
				string dir2 = Path.GetDirectoryName(fullPath) ?? baseDir;
				// Try standard extensions
				foreach (var ext in new[] { ".dds", ".tex", ".bmp", ".png", ".jpg", ".gif", ".tga" })
				{
					string tryPath = Path.Combine(dir2, baseName + ext);
					if (File.Exists(tryPath)) { fullPath = tryPath; break; }
				}
				// Try _00.dds pattern (common in FF7 mods)
				if (!File.Exists(fullPath))
				{
					string tryPath = Path.Combine(dir2, baseName + "_00.dds");
					if (File.Exists(tryPath)) fullPath = tryPath;
				}
			}

            if (!File.Exists(fullPath))
            {
                tex.LoadError = $"Not found: {filePath}";
                CreatePlaceholder(tex);
                return tex;
            }

            try
			{
				string ext = Path.GetExtension(fullPath).ToLower(); 
				if (ext == "" || ext == ".tex")
				{
					// Check header to determine if it's a TEX file
					byte[] hdr = new byte[4];
					using (var fs = File.OpenRead(fullPath)) fs.Read(hdr, 0, 4);
					
					if (hdr[0] == 0x44 && hdr[1] == 0x44 && hdr[2] == 0x53 && hdr[3] == 0x20)
					{
						LoadDdsFormat(fullPath, tex);
						tex.LoadedSuccessfully = true;
					}
					else if (BitConverter.ToInt32(hdr, 0) == 1)
					{
						LoadTexFormat(fullPath, tex);
						tex.LoadedSuccessfully = true;
					}
					else
						CreatePlaceholder(tex);
				}
				else if (ext == ".dds") { LoadDdsFormat(fullPath, tex); tex.LoadedSuccessfully = true; }
				else LoadImageFormat(fullPath, tex);
			}
            catch (Exception ex)
            {
                tex.LoadError = ex.Message;
                CreatePlaceholder(tex);
            }
			tex.FileName = Path.GetFileName(fullPath); //This makes sure if a non .tex file is used (Like dds) it grabs the correct name
            return tex;
        }

        // Finds the correct DDS texture for a battle weapon model
		public static Texture LoadWeaponTexture(string weaponFilePath, string dir, int weaponIndex, string skeletonPrefix)
		{
			string texNum = weaponIndex == 0 ? "T01" : "T02";
			
			// Map skeleton prefix to character texture name, rt is CLOUD, RU is TIFA etc
			string charName = GetCharacterName(skeletonPrefix);
			if (charName == null) return null;
			
			// Search for HI{CHARNAME}_T##_00.dds
			string pattern = $"{charName}_{texNum}_00.dds";
			var match = Directory.GetFiles(dir)
				.FirstOrDefault(f => Path.GetFileName(f).Equals(pattern, StringComparison.OrdinalIgnoreCase));
			
			if (match == null) return null;
			
			var tex = Load(match, dir);
			return tex.LoadedSuccessfully ? tex : null;
		}

private static string GetCharacterName(string prefix)
{
    switch (prefix.ToLower())
    {
        case "rt": return "CLOUD";
        case "ru": return "TIFA";
        case "rv": return "EARITH";
        case "sb": return "BARRETT";
        case "sf": return "VINSENT";
        // Add more as needed
        default: return null;
    }
}


		private static void CreatePlaceholder(Texture tex)
        {
            tex.Width = 64; tex.Height = 64;
            tex.PixelData = new byte[64 * 64 * 4];
            for (int i = 0; i < tex.PixelData.Length; i += 4)
            { tex.PixelData[i] = 255; tex.PixelData[i+1] = 0; tex.PixelData[i+2] = 255; tex.PixelData[i+3] = 255; }
        }

		private static void LoadTexFormat(string path, Texture tex)
		{
			byte[] data = File.ReadAllBytes(path);
			if (data.Length < 72) { CreatePlaceholder(tex); return; }

			// Read key header fields
			int bitDepth = BitConverter.ToInt32(data, 20);      // 8 = palettized, 24 = direct RGB
			int realBitDepth = BitConverter.ToInt32(data, 56);   // actual output depth (32 for BGRA)
			int width = BitConverter.ToInt32(data, 60);
			int height = BitConverter.ToInt32(data, 64);

			if (width <= 0 || width > 8192) width = 256;
			if (height <= 0 || height > 8192) height = 256;

			tex.Width = width;
			tex.Height = height;
			tex.PixelData = new byte[width * height * 4];

			if (bitDepth == 24)
			{
				// Direct 24-bit BGR (original small TEX files like 1000x1000)
				int headerSize = 236;
				int srcOffset = headerSize;
				for (int i = 0; i < width * height && srcOffset + 2 < data.Length; i++)
				{
					tex.PixelData[i * 4 + 0] = data[srcOffset + 2]; // R
					tex.PixelData[i * 4 + 1] = data[srcOffset + 1]; // G
					tex.PixelData[i * 4 + 2] = data[srcOffset + 0]; // B
					tex.PixelData[i * 4 + 3] = 255;                  // A
					srcOffset += 3;
				}
			}
			else if (realBitDepth == 32)
			{
				// 32-bit BGRA (modded high-res TEX files)
				// Pixel data is at end of file: fileSize - (width * height * 4)
				int pixelDataSize = width * height * 4;
				int pixelStart = data.Length - pixelDataSize;
				if (pixelStart < 0) pixelStart = 236;

				for (int i = 0; i < width * height && pixelStart + i * 4 + 3 < data.Length; i++)
				{
					tex.PixelData[i * 4 + 0] = data[pixelStart + i * 4 + 2]; // R
					tex.PixelData[i * 4 + 1] = data[pixelStart + i * 4 + 1]; // G
					tex.PixelData[i * 4 + 2] = data[pixelStart + i * 4 + 0]; // B
					tex.PixelData[i * 4 + 3] = data[pixelStart + i * 4 + 3]; // A
				}
			}
			else
			{
				// Palettized 8-bit (classic FF7 TEX, 256x256)
				int headerSize = 236;
				int palSize = 256;
				byte[] palette = new byte[palSize * 4];
				if (headerSize + palette.Length <= data.Length)
					Array.Copy(data, headerSize, palette, 0, palette.Length);

				int pixelStart = headerSize + palSize * 4;
				for (int i = 0; i < width * height && pixelStart + i < data.Length; i++)
				{
					int idx = data[pixelStart + i] * 4;
					if (idx + 3 < palette.Length)
					{
						tex.PixelData[i * 4 + 0] = palette[idx + 2]; // R
						tex.PixelData[i * 4 + 1] = palette[idx + 1]; // G
						tex.PixelData[i * 4 + 2] = palette[idx + 0]; // B
						tex.PixelData[i * 4 + 3] = 255;
					}
				}
			}
		}

        private static void LoadImageFormat(string path, Texture tex)
        {
            using var bmp = new Bitmap(path);
            tex.Width = bmp.Width; tex.Height = bmp.Height;
            tex.PixelData = new byte[tex.Width * tex.Height * 4];
            var data = bmp.LockBits(new Rectangle(0, 0, tex.Width, tex.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(data.Scan0, tex.PixelData, 0, tex.PixelData.Length);
            bmp.UnlockBits(data);
            for (int i = 0; i < tex.PixelData.Length; i += 4)
                (tex.PixelData[i], tex.PixelData[i+2]) = (tex.PixelData[i+2], tex.PixelData[i]);
        }
 
		private static void LoadDdsFormat(string path, Texture tex)
		{
			try
			{
				using var bmp = new System.Drawing.Bitmap(path);
				tex.Width = bmp.Width;
				tex.Height = bmp.Height;
				tex.PixelData = new byte[tex.Width * tex.Height * 4];
				var data = bmp.LockBits(
					new System.Drawing.Rectangle(0, 0, tex.Width, tex.Height),
					System.Drawing.Imaging.ImageLockMode.ReadOnly,
					System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				System.Runtime.InteropServices.Marshal.Copy(data.Scan0, tex.PixelData, 0, tex.PixelData.Length);
				bmp.UnlockBits(data);
				for (int i = 0; i < tex.PixelData.Length; i += 4)
					(tex.PixelData[i], tex.PixelData[i+2]) = (tex.PixelData[i+2], tex.PixelData[i]);
				// LOG: WIC worked
				File.AppendAllText("kimera2_log.txt", $"[DDS] WIC loaded OK: {path} ({tex.Width}x{tex.Height})\n");
			}
			catch (Exception ex)
			{
				// LOG: WIC failed, falling back
				File.AppendAllText("kimera2_log.txt", $"[DDS] WIC FAILED: {path} error={ex.Message} - using manual\n");
				LoadDdsManual(path, tex);
			}
		}

		private static void LoadDdsManual(string path, Texture tex)
		{
			using var reader = new BinaryReader(File.OpenRead(path));
			int magic = reader.ReadInt32();
			int headerSize = reader.ReadInt32();
			int flags = reader.ReadInt32();
			int height = reader.ReadInt32();
			int width = reader.ReadInt32();
			int pitchOrLinearSize = reader.ReadInt32();
			int depth = reader.ReadInt32();
			int mipMapCount = reader.ReadInt32();
			reader.BaseStream.Seek(44, SeekOrigin.Current);
			int pfSize = reader.ReadInt32();
			int pfFlags = reader.ReadInt32();
			int fourCC = reader.ReadInt32();
			int rgbBitCount = reader.ReadInt32();
			int rMask = reader.ReadInt32();
			int gMask = reader.ReadInt32();
			int bMask = reader.ReadInt32();
			int aMask = reader.ReadInt32();
			reader.BaseStream.Seek(20, SeekOrigin.Current);

			tex.Width = width > 0 ? width : 256;
			tex.Height = height > 0 ? height : 256;
			int pixelCount = tex.Width * tex.Height;
			tex.PixelData = new byte[pixelCount * 4];

			bool isDX10 = (fourCC == 0x30315844);
			int dxgiFormat = 0;
			if (isDX10)
			{
				dxgiFormat = reader.ReadInt32();
				reader.BaseStream.Seek(16, SeekOrigin.Current);
			}

			if (isDX10 && (dxgiFormat == 98 || dxgiFormat == 99))
			{
				int blockW = (tex.Width + 3) / 4;
				int blockH = (tex.Height + 3) / 4;
				for (int by = 0; by < blockH; by++)
					for (int bx = 0; bx < blockW; bx++)
					{
						byte[] block = reader.ReadBytes(16);
						if (block.Length < 16) break;
						BC7Decoder.DecompressBlock(block, tex.PixelData, bx * 4, by * 4, tex.Width);
					}
			}
			else
			{
				for (int i = 0; i < pixelCount && reader.BaseStream.Position + 3 < reader.BaseStream.Length; i++)
				{
					tex.PixelData[i*4+2] = reader.ReadByte();
					tex.PixelData[i*4+1] = reader.ReadByte();
					tex.PixelData[i*4+0] = reader.ReadByte();
					tex.PixelData[i*4+3] = 255;
					if (rgbBitCount >= 32) reader.ReadByte();
				}
			}
		}

		private static byte[] DecodeColors565(ushort c0, ushort c1, bool fourColors)
		{
			byte[] result = new byte[16]; // 4 colors x 4 bytes (RGBA)
			byte r0 = (byte)((c0 >> 11) * 255 / 31), g0 = (byte)(((c0 >> 5) & 0x3F) * 255 / 63), b0 = (byte)((c0 & 0x1F) * 255 / 31);
			byte r1 = (byte)((c1 >> 11) * 255 / 31), g1 = (byte)(((c1 >> 5) & 0x3F) * 255 / 63), b1 = (byte)((c1 & 0x1F) * 255 / 31);
			result[0] = r0; result[1] = g0; result[2] = b0; result[3] = 255;
			result[4] = r1; result[5] = g1; result[6] = b1; result[7] = 255;
			if (fourColors)
			{
				result[8] = (byte)((2 * r0 + r1) / 3); result[9] = (byte)((2 * g0 + g1) / 3); result[10] = (byte)((2 * b0 + b1) / 3); result[11] = 255;
				result[12] = (byte)((r0 + 2 * r1) / 3); result[13] = (byte)((g0 + 2 * g1) / 3); result[14] = (byte)((b0 + 2 * b1) / 3); result[15] = 255;
			}
			else
			{
				result[8] = (byte)((r0 + r1) / 2); result[9] = (byte)((g0 + g1) / 2); result[10] = (byte)((b0 + b1) / 2); result[11] = 255;
				result[12] = 0; result[13] = 0; result[14] = 0; result[15] = 0;
			}
			return result;
		}

		private static byte[] BuildAlphaTable(byte a0, byte a1)
		{
			byte[] t = new byte[8];
			t[0] = a0; t[1] = a1;
			if (a0 > a1) { for (int i = 2; i < 8; i++) t[i] = (byte)((a0 * (8 - i) + a1 * (i - 1)) / 7); }
			else { for (int i = 2; i < 6; i++) t[i] = (byte)((a0 * (6 - i) + a1 * (i - 1)) / 5); t[6] = 0; t[7] = 255; }
			return t;
		}
	}

}
