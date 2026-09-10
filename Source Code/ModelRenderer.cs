using System;
using Kimera2.Models;

namespace Kimera2.Rendering

{
	// Renderer functionality is now integrated into MainForm.cs directly.
	// This file is kept for compilation compatibility only.
	public class ModelRenderer
		{
			public float RotationX, RotationY, Zoom = -200f, PanX, PanY;
			public bool ShowTextures = true, ShowVertexColors = true, ContinueOnError = true;
		}
}
