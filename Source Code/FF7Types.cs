using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Kimera2.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PFileHeader
    {
        public int Version;
        public int Off04;
        public int VertexType;
        public int NumVertices;
        public int NumNormals;
        public int NumUnknown1;
        public int NumTexCoords;
        public int NumVertexColors;
        public int NumEdges;
        public int NumPolygons;
        public int NumUnknown2;
        public int NumUnknown3;
        public int NumHundreds;
        public int NumGroups;
        public int NumBoundingBoxes;
        public int NormIndexTableFlag;

        public bool IsValid(long fileSize)
        {
            if (Version < 0 || Version > 10) return false;
            if (NumVertices < 0 || NumVertices > 1_000_000) return false;
            if (NumPolygons < 0 || NumPolygons > 2_000_000) return false;
            if (NumGroups < 0 || NumGroups > 10_000) return false;
            long est = 128L + (12L * NumVertices) + (12L * NumNormals) +
                (12L * NumUnknown1) + (8L * NumTexCoords) + (4L * NumVertexColors) +
                (4L * NumPolygons) + (4L * NumEdges) + (24L * NumPolygons) +
                (24L * NumUnknown2) + (3L * NumUnknown3);
            return est <= fileSize * 2;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex3D
    {
        public float X, Y, Z;
        public Vector3 ToVector3() => new Vector3(X, Y, Z);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TexCoord
    {
        public float U, V;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ColorBGRA
    {
        public byte B, G, R, A;
        public Color ToColor() => Color.FromArgb(A, R, G, B);
        public static ColorBGRA White => new ColorBGRA { B = 255, G = 255, R = 255, A = 255 };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PPolygon
    {
        public ushort Zero;
        public ushort VertexIndex1, VertexIndex2, VertexIndex3;
        public ushort NormalIndex1, NormalIndex2, NormalIndex3;
        public ushort EdgeIndex1, EdgeIndex2, EdgeIndex3;
        public ushort Unknown1, Unknown2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PGroup
    {
        public int PrimitiveType;
        public int PolygonStartIndex;
        public int NumPolygons;
        public int VerticesStartIndex;
        public int NumVertices;
        public int EdgeStartIndex;
        public int NumEdges;
        public int Unknown1, Unknown2, Unknown3, Unknown4;
        public int TexCoordStartIndex;
        public int AreTexturesUsed;
        public int TextureNumber;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BoundingBox
    {
        public float MaxX, MaxY, MaxZ;
        public float MinX, MinY, MinZ;
    }

    public class PModel
    {
        public PFileHeader Header;
        public List<Vertex3D> Vertices = new();
        public List<Vertex3D> Normals = new();
        public List<TexCoord> TexCoords = new();
        public List<ColorBGRA> VertexColors = new();
        public List<ColorBGRA> PolygonColors = new();
        public List<int> Edges = new();
        public List<PPolygon> Polygons = new();
        public List<PGroup> Groups = new();
        public List<BoundingBox> BoundingBoxes = new();
        public List<Texture> Textures = new();
        public List<bool> GroupLoadSuccess = new();
		public bool IsWeapon { get; set;}
        public List<string> LoadWarnings = new();
        public string FileName = "";
		public string OriginalFilePath = ""; //Makes a copy of files when saving
		// Editing where P file parts move/transforms (offset within bone)
		public float OffsetX = 0, OffsetY = 0, OffsetZ = 0;
		public float RotateX = 0, RotateY = 0, RotateZ = 0;
		public float ScaleX = 1, ScaleY = 1, ScaleZ = 1;


        public long EstimatedMemoryBytes =>
            (long)Vertices.Count * 12 + (long)Normals.Count * 12 +
            (long)TexCoords.Count * 8 + (long)VertexColors.Count * 4 +
            (long)Polygons.Count * 24 + (long)Groups.Count * 56;
    }

    public class Texture
    {
        public string FileName = "";
        public int Width, Height;
        public byte[] PixelData = Array.Empty<byte>();
        public int OpenGLTextureId = -1;
        public bool LoadedSuccessfully = false;
        public string LoadError = "";
    }

    public class Bone
    {
        public string Name = "";
        public string ParentName = "";
        public float Length;
        public List<string> RsdNames = new();
        public int ParentIndex = -1;
        public List<PModel> Models = new();
        public Matrix4x4 WorldTransform = Matrix4x4.Identity;
    }

    public class Skeleton
    {
        public string Name = "";
		public string OriginalFilePath { get; set; }
        public List<Bone> Bones = new();
        public List<string> LoadWarnings = new();
		public List<AnimationFrame> Frames = new();  // <-- Adds animations
		public int CurrentFrame = 0;    //Adds start animations
    }

    public class RsdFile
    {
        public string PolygonFile = "";
        public string MaterialFile = "";
        public int TextureCount = 0;
        public List<string> TextureFiles = new();
    }
	public class AnimationFrame
	{
		public Vector3 RootRotation;
		public Vector3 RootTranslation;
		public List<Vector3> BoneRotations = new();
	}
}
