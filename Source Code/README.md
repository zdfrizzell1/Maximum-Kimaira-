# Kimera2 - Enhanced FF7 3D Model Editor

A modern C# rewrite of Borde's Kimera tool for viewing and editing
Final Fantasy VII PC 3D models (.P and .HRC files).

## Key Improvements

1. **No more Error 7 (Out of Memory)** - Uses 64-bit architecture, 32-bit integers,
   and dynamic memory. Can handle models with millions of vertices.

2. **Error-tolerant rendering** - If a file is partially corrupt or truncated,
   Kimera2 renders whatever it successfully loaded instead of crashing.

3. **Zero dependencies** - Ships as a single .exe. No COM/OCX registration needed.

## Build

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin/Release/net6.0-windows/win-x64/publish/Kimera2.exe`

## Controls

- Left drag: Rotate
- Right drag: Zoom
- Middle drag: Pan
- Scroll wheel: Zoom
- Home key: Reset camera

## Supported Formats

- .P (polygon mesh binary)
- .HRC (skeleton hierarchy text)
- .RSD (resource data text)
- Textures: .TEX, .BMP, .PNG, .JPG, .GIF
