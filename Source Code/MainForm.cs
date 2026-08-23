using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Linq;
using Kimera2.Models;
using Kimera2.IO;
using Kimera2.Rendering;
using System.Numerics; //Needed to have animations

namespace Kimera2
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("gdi32.dll")] static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] static extern bool SetPixelFormat(IntPtr hdc, int fmt, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("opengl32.dll")] static extern IntPtr wglCreateContext(IntPtr hdc);
        [DllImport("opengl32.dll")] static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
        [DllImport("gdi32.dll")] static extern bool SwapBuffers(IntPtr hdc);
        [DllImport("opengl32.dll")] static extern void glClear(uint mask);
        [DllImport("opengl32.dll")] static extern void glClearColor(float r, float g, float b, float a);
        [DllImport("opengl32.dll")] static extern void glEnable(uint cap);
        [DllImport("opengl32.dll")] static extern void glDisable(uint cap);
        [DllImport("opengl32.dll")] static extern void glBegin(uint mode);
        [DllImport("opengl32.dll")] static extern void glEnd();
        [DllImport("opengl32.dll")] static extern void glVertex3f(float x, float y, float z);
        [DllImport("opengl32.dll")] static extern void glColor3f(float r, float g, float b);
        [DllImport("opengl32.dll")] static extern void glColor3ub(byte r, byte g, byte b);
        [DllImport("opengl32.dll")] static extern void glColor4f(float r, float g, float b, float a);
        [DllImport("opengl32.dll")] static extern void glNormal3f(float x, float y, float z);
        [DllImport("opengl32.dll")] static extern void glTexCoord2f(float u, float v);
        [DllImport("opengl32.dll")] static extern void glMatrixMode(uint mode);
        [DllImport("opengl32.dll")] static extern void glLoadIdentity();
        [DllImport("opengl32.dll")] static extern void glLoadMatrixd(double[] m);
        [DllImport("opengl32.dll")] static extern void glTranslatef(float x, float y, float z);
        [DllImport("opengl32.dll")] static extern void glRotatef(float angle, float x, float y, float z);
        [DllImport("opengl32.dll")] static extern void glPushMatrix();
        [DllImport("opengl32.dll")] static extern void glPopMatrix();
        [DllImport("opengl32.dll")] static extern void glViewport(int x, int y, int w, int h);
        [DllImport("opengl32.dll")] static extern void glDepthFunc(uint func);
        [DllImport("opengl32.dll")] static extern void glBlendFunc(uint s, uint d);
        [DllImport("opengl32.dll")] static extern void glBindTexture(uint target, uint texture);
        [DllImport("opengl32.dll")] static extern void glGenTextures(int n, uint[] textures);
        [DllImport("opengl32.dll")] static extern void glTexImage2D(uint target, int level, int fmt, int w, int h, int border, uint format, uint type, byte[] pixels);
        [DllImport("opengl32.dll")] static extern void glTexParameteri(uint target, uint pname, int param);
        [DllImport("opengl32.dll")] static extern void glPolygonMode(uint face, uint mode);
		[DllImport("opengl32.dll")] static extern void glMultMatrixf(float[] m);
		[DllImport("opengl32.dll")] static extern void glScalef(float x, float y, float z); //DLL for scaling and moving P files



        const uint GL_TRIANGLES = 0x0004, GL_LINES = 0x0001;
        const uint GL_DEPTH_TEST = 0x0B71, GL_TEXTURE_2D = 0x0DE1, GL_BLEND = 0x0BE2;
        const uint GL_COLOR_BUFFER_BIT = 0x4000, GL_DEPTH_BUFFER_BIT = 0x0100;
        const uint GL_MODELVIEW = 0x1700, GL_PROJECTION = 0x1701;
        const uint GL_RGBA = 0x1908, GL_UNSIGNED_BYTE = 0x1401;
        const uint GL_TEXTURE_MIN_FILTER = 0x2801, GL_TEXTURE_MAG_FILTER = 0x2800, GL_LINEAR = 0x2601;
        const uint GL_LEQUAL = 0x0203, GL_SRC_ALPHA = 0x0302, GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        const uint GL_FRONT_AND_BACK = 0x0408, GL_LINE = 0x1B01, GL_FILL = 0x1B02;

        [StructLayout(LayoutKind.Sequential)]
        struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize, nVersion;
            public uint dwFlags;
            public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift;
            public byte cAlphaBits, cAlphaShift, cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
            public byte cDepthBits, cStencilBits, cAuxBuffers, iLayerType, bReserved;
            public uint dwLayerMask, dwVisibleMask, dwDamageMask;
        }

        private Panel glPanel;
        private TreeView modelTree;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel, memoryLabel;
        private Timer renderTimer;
        private PModel currentModel;
        private Skeleton currentSkeleton;
        private IntPtr hdc, hglrc;
        private bool glReady;
        private Point lastMouse;
        private bool rotating, zooming, panning;
        private float rotX, rotY, zoom = -200f, panX, panY;
        private bool showWireframe = false, showTextures = true, showVertexColors = true;
		private Label editLabel;
		private ListBox logList;
		private List<PModel> selectedModels = new List<PModel>(); //Allows selecting multiple P models
		//Start animation loader
		private int currentFrameIndex = 0;
		private bool animPlaying = false;
		private Timer animTimer;
		private TrackBar frameSlider;
		private Label frameLabel;
		private ComboBox animSelect; //Animation .a file selector
		private Vector3 rootTranslationOffset = Vector3.Zero;
		private string animDir = "";
		private List<BattleAnimation> battleAnimations;
		private int currentBattleAnimIndex = 0;
		private bool isBattleModel = false; //Sometimes debug to force no battle model
		private ComboBox folderSelect;
		private string parentDir = "";
		//Finish animation loaders
		private ComboBox weaponSelect; //Need weapon box
		private List<string> weaponFiles = new List<string>();

		public MainForm()
		{
			try
			{
				Text = "Kimera 2.0 - FF7 3D Model Editor";
				Size = new Size(1280, 800);
				StartPosition = FormStartPosition.CenterScreen;
				BuildUI();
				Shown += (s, e) => { InitGL(); renderTimer.Start(); };
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Startup error: {ex.Message}\n\n{ex.StackTrace}", "Error");
			}
		}

        private void BuildUI()
        {
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("Open &P File...", null, (s, e) => OpenFile("P Files|*.p|All|*.*", LoadPFile));
            fileMenu.DropDownItems.Add("Open &HRC Skeleton...", null, (s, e) => OpenFile("HRC Files|*.hrc|All|*.*", LoadHrcFile));
			fileMenu.DropDownItems.Add("Open &Battle Model...", null, (s, e) => OpenFile("All Files|*.*", LoadBattleFile));
			
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());
			fileMenu.DropDownItems.Add("&Save Selected P File...", null, (s, e) => SaveSelectedP()); //Save menu for selected file only
			fileMenu.DropDownItems.Add("Apply Move to Current Animation", null, (s, e) => ApplyMoveAnim(true)); 
			fileMenu.DropDownItems.Add("Apply Move to All Animations", null, (s, e) => ApplyMoveAnim(false));
			fileMenu.DropDownItems.Add("Save &All Modified Files", null, (s, e) => SaveAllP()); //Save menu for all changes
			
            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add("Toggle &Wireframe", null, (s, e) => showWireframe = !showWireframe);
            viewMenu.DropDownItems.Add("Toggle &Textures", null, (s, e) => showTextures = !showTextures);
            viewMenu.DropDownItems.Add("Toggle Vertex &Colors", null, (s, e) => showVertexColors = !showVertexColors);
            viewMenu.DropDownItems.Add("&Reset Camera (Home)", null, (s, e) => ResetCamera());
            menu.Items.AddRange(new[] { fileMenu, viewMenu });
			//Start Adding controls to move bones/P files
			var editPanel = new Panel { Dock = DockStyle.Right, Width = 200 };
			editLabel = new Label { Text = "Selected Part:", Dock = DockStyle.Top };
			
			var lblPos = new Label { Text = "Position X/Y/Z:", Dock = DockStyle.Top, Height = 20 };
			var txtPosX = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "posX" };
			var txtPosY = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "posY" };
			var txtPosZ = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "posZ" };

			var lblRot = new Label { Text = "Rotation X/Y/Z:", Dock = DockStyle.Top, Height = 20 };
			var txtRotX = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "rotX" };
			var txtRotY = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "rotY" };
			var txtRotZ = new TextBox { Text = "0", Dock = DockStyle.Top, Tag = "rotZ" };

			var lblScale = new Label { Text = "Scale X/Y/Z:", Dock = DockStyle.Top, Height = 20 };
			var txtScaleX = new TextBox { Text = "100", Dock = DockStyle.Top, Tag = "scaleX" };
			var txtScaleY = new TextBox { Text = "100", Dock = DockStyle.Top, Tag = "scaleY" };
			var txtScaleZ = new TextBox { Text = "100", Dock = DockStyle.Top, Tag = "scaleZ" };

			var btnApply = new Button { Text = "Apply Transform Changes", Dock = DockStyle.Top, Height = 40 };
			var btnMoveModel = new Button { Text = "XYZ Move Entire Model", Dock = DockStyle.Top, Height = 40 };
			var btnScaleModel = new Button { Text = "Scale Whole Model", Dock = DockStyle.Top, Height = 40 };
			var btnUnselectAll = new Button { Text = "Unselect All Parts", Dock = DockStyle.Top, Height = 40};

			editPanel.Controls.AddRange(new Control[] { btnScaleModel, btnMoveModel, btnUnselectAll, btnApply, txtScaleZ, txtScaleY, txtScaleX, lblScale, txtRotZ, txtRotY, txtRotX, lblRot, txtPosZ, txtPosY, txtPosX, lblPos, editLabel });

			Controls.Add(editPanel);
			Controls.Add(new Splitter { Dock = DockStyle.Right });
			modelTree = new TreeView { Dock = DockStyle.Left, Width = 220 }; //Make tree before slecting it, or will crash
			btnUnselectAll.Click += (s, e) => //Start unselect all logic
			{
				selectedModels.Clear();
				selectedModel = null;
				// Reset tree node colors
				foreach (TreeNode bone in modelTree.Nodes)
					foreach (TreeNode node in bone.Nodes)
						foreach (TreeNode pNode in node.Nodes)
							pNode.BackColor = Color.White;
				editLabel.Text = "Selected: 0 part(s)";
				btnApply.Text = "Apply Transform Changes";
			}; //Finish unselect all logic
			modelTree.NodeMouseClick += (s, e) =>
			{
				var node = e.Node;
				if (node?.Tag is PModel m)
				{
					if (selectedModels.Contains(m))
					{
						// Unselect it
						selectedModels.Remove(m);
						e.Node.BackColor = Color.White;
					}
					else
					{
						// Select it
						selectedModels.Add(m);
						e.Node.BackColor = Color.LightGreen;
					}
					selectedModel = selectedModels.Count > 0 ? selectedModels[selectedModels.Count - 1] : null;
					editLabel.Text = $"Selected: {selectedModels.Count} part(s)";
					if (selectedModels.Count > 1)
						btnApply.Text = $"Apply to {selectedModels.Count} Parts";
					else
						btnApply.Text = $"Apply Transform Changes";
					
					// Show values of last selected
					if (selectedModel != null)
					{
						txtPosX.Text = selectedModel.OffsetX.ToString();
						txtPosY.Text = selectedModel.OffsetY.ToString();
						txtPosZ.Text = selectedModel.OffsetZ.ToString();
						txtRotX.Text = selectedModel.RotateX.ToString();
						txtRotY.Text = selectedModel.RotateY.ToString();
						txtRotZ.Text = selectedModel.RotateZ.ToString();
						txtScaleX.Text = (selectedModel.ScaleX * 100).ToString();
						txtScaleY.Text = (selectedModel.ScaleY * 100).ToString();
						txtScaleZ.Text = (selectedModel.ScaleZ * 100).ToString();
					}
				}
			}; //Finish multi select P files
			
			//Start add apply button for P files
			btnApply.Click += (s, e) =>
			{
				if (selectedModels.Count == 0) { Log("No parts selected"); return; }
				float px = 0, py = 0, pz = 0, rx = 0, ry = 0, rz = 0, sx = 100, sy = 100, sz = 100;
				float.TryParse(txtPosX.Text, out px);
				float.TryParse(txtPosY.Text, out py);
				float.TryParse(txtPosZ.Text, out pz);
				float.TryParse(txtRotX.Text, out rx);
				float.TryParse(txtRotY.Text, out ry);
				float.TryParse(txtRotZ.Text, out rz);
				float.TryParse(txtScaleX.Text, out sx);
				float.TryParse(txtScaleY.Text, out sy);
				float.TryParse(txtScaleZ.Text, out sz);
				
				foreach (var model in selectedModels)
				{
					model.OffsetX = px; model.OffsetY = py; model.OffsetZ = pz;
					model.RotateX = rx; model.RotateY = ry; model.RotateZ = rz;
					model.ScaleX = sx / 100f; model.ScaleY = sy / 100f; model.ScaleZ = sz / 100f;
				}
				Log($"Transform applied to {selectedModels.Count} part(s)");
			};
			// Finish apply for moving bones/P files
			btnMoveModel.Click += (s, e) => //Start move entire model
			{
				if (currentSkeleton == null || currentSkeleton.Frames.Count == 0) { Log("No skeleton/animation loaded"); return; }
				float px = 0, py = 0, pz = 0;
				float.TryParse(txtPosX.Text, out px);
				float.TryParse(txtPosY.Text, out py);
				float.TryParse(txtPosZ.Text, out pz);
				
				foreach (var frame in currentSkeleton.Frames)
				{
					frame.RootTranslation = new Vector3(
						frame.RootTranslation.X + px,
						frame.RootTranslation.Y + py,
						frame.RootTranslation.Z + pz);
				}
				
				txtPosX.Text = "0"; txtPosY.Text = "0"; txtPosZ.Text = "0";
				rootTranslationOffset += new Vector3(px, py, pz);
				Log($"Entire model moved by ({px}, {py}, {pz}) via root translation");
			}; //Finish move entire model
			btnScaleModel.Click += (s, e) => //Start button to scale skeleton with scaling
			{
				float sx = 100;
				float.TryParse(txtScaleX.Text, out sx);
				float scale = sx / 100f;
				if (scale <= 0 || scale == 1 || currentSkeleton == null) return;
				
				// Scale all bone lengths
				foreach (var bone in currentSkeleton.Bones)
					bone.Length *= scale;
				
				// Scale all P file vertices
				foreach (var bone in currentSkeleton.Bones)
					foreach (var model in bone.Models)
						for (int i = 0; i < model.Vertices.Count; i++)
						{
							var v = model.Vertices[i];
							model.Vertices[i] = new Vertex3D { X = v.X * scale, Y = v.Y * scale, Z = v.Z * scale };
						}
				
				// Scale root translation in animation
				foreach (var frame in currentSkeleton.Frames)
					frame.RootTranslation *= scale;
				
				Log($"Entire model scaled by {scale:F2}x (bones + vertices + root translation)");
				txtScaleX.Text = "100"; txtScaleY.Text = "100"; txtScaleZ.Text = "100";
			}; //Finish button to scale skeleton with scaling

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready - Open a .P or .HRC file");
            memoryLabel = new ToolStripStatusLabel("") { Alignment = ToolStripItemAlignment.Right };
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, memoryLabel });

            
            glPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            glPanel.MouseDown += (s, e) => { lastMouse = e.Location; rotating = e.Button == MouseButtons.Left; zooming = e.Button == MouseButtons.Right; panning = e.Button == MouseButtons.Middle; glPanel.Focus(); };
            glPanel.MouseMove += (s, e) => { int dx = e.X - lastMouse.X, dy = e.Y - lastMouse.Y; if (rotating) { rotY += dx * 0.5f; rotX += dy * 0.5f; } if (zooming) zoom += dy * 0.5f; if (panning) { panX += dx * 0.2f; panY -= dy * 0.2f; } lastMouse = e.Location; };
            glPanel.MouseUp += (s, e) => rotating = zooming = panning = false;
            glPanel.MouseWheel += (s, e) => zoom += e.Delta * 0.1f;
            glPanel.Resize += (s, e) => { if (glReady) SetupProjection(); };
			logList = new ListBox { Dock = DockStyle.Bottom, Height = 150, Font = new Font("Consolas", 8) }; //adds log as console

            Controls.Add(glPanel);
            Controls.Add(new Splitter { Dock = DockStyle.Left });
            Controls.Add(modelTree);
			// Animation controls panel (bottom, above log)
			var animPanel = new Panel { Dock = DockStyle.Bottom, Height = 70 };
			var btnPlay = new Button { Text = "▶ Play", Width = 60, Location = new Point(5, 8) };
			var btnStop = new Button { Text = "■ Stop", Width = 60, Location = new Point(70, 8) };
			var btnPrev = new Button { Text = "◀", Width = 30, Location = new Point(135, 8) };
			var btnNext = new Button { Text = "▶", Width = 30, Location = new Point(170, 8) };
			frameLabel = new Label { Text = "Frame: 0 / 0", Location = new Point(210, 12), Width = 100 };
			frameSlider = new TrackBar { Minimum = 0, Maximum = 1, Location = new Point(310, 5), Width = 300 };
			var chkLoop = new CheckBox { Text = "Loop", Checked = false, Location = new Point(620, 10), Width = 60 }; //Ends at 680
			animPanel.Controls.Add(chkLoop);
			//Start buttons for playing animations
			btnPlay.Click += (s, e) => { animPlaying = true; animTimer.Start(); };
			btnStop.Click += (s, e) => { animPlaying = false; animTimer.Stop(); };
			btnPrev.Click += (s, e) => { if (currentSkeleton?.Frames.Count > 0) { currentFrameIndex = Math.Max(0, currentFrameIndex - 1); frameSlider.Value = currentFrameIndex; UpdateFrameLabel(); } };
			btnNext.Click += (s, e) => { if (currentSkeleton?.Frames.Count > 0) { currentFrameIndex = Math.Min(currentSkeleton.Frames.Count - 1, currentFrameIndex + 1); frameSlider.Value = currentFrameIndex; UpdateFrameLabel(); } };
			frameSlider.Scroll += (s, e) => { currentFrameIndex = frameSlider.Value; UpdateFrameLabel(); };
			
			animPanel.Controls.AddRange(new Control[] { btnPlay, btnStop, btnPrev, btnNext, frameLabel, frameSlider });
			
			Controls.Add(animPanel);
			animSelect = new ComboBox { Location = new Point(690, 8), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList }; //UI for selecting animations
			animSelect.SelectedIndexChanged += (s, e) =>
			{
				if (animSelect.SelectedItem == null || currentSkeleton == null) return;
				string fileName = animSelect.SelectedItem.ToString();
				string animPath = Path.Combine(animDir, fileName);

				if (isBattleModel && !fileName.EndsWith(".a", StringComparison.OrdinalIgnoreCase))
				{
					// BATTLE: Load the selected file
					try
					{
						battleAnimations = BattleAnimLoader.Load(animPath);
						currentBattleAnimIndex = 0;
						currentFrameIndex = 0;
						frameSlider.Maximum = Math.Max(battleAnimations[0].NumFrames - 1, 1);
						frameSlider.Value = 0;
						UpdateFrameLabel();
						Log($"Battle anim loaded: {fileName} ({battleAnimations.Count} anims, {battleAnimations[0].NumFrames} frames)");
					}
					catch (Exception ex)
					{
						Log($"Battle anim failed: {ex.Message}");
					}
				}
				else
				{
					// FIELD: Load the selected file
					var anim = AnimLoader.Load(animPath, currentSkeleton.Bones.Count);
					if (anim.Count > 0)
					{
						currentSkeleton.Frames = anim;
						currentFrameIndex = 0;
						frameSlider.Maximum = Math.Max(anim.Count - 1, 1);
						frameSlider.Value = 0;
						UpdateFrameLabel();
						Log($"Switched animation: {fileName}, {anim.Count} frames");
					}
				}
			};
			animPanel.Controls.Add(animSelect);
			
			folderSelect = new ComboBox { Location = new Point(850, 8), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList }; //Folder selection of animations
			animPanel.Controls.Add(folderSelect);
			folderSelect.SelectedIndexChanged += (s, e) =>
			{
				if (folderSelect.SelectedItem == null) return;
				animDir = Path.Combine(parentDir, folderSelect.SelectedItem.ToString());
				// Update animation list for selected folder
				animSelect.Items.Clear();
				if (Directory.Exists(animDir))
				{
					string[] anims = Directory.GetFiles(animDir, "*.a", SearchOption.TopDirectoryOnly);
					foreach (var a in anims)
						animSelect.Items.Add(Path.GetFileName(a));
					if (animSelect.Items.Count > 0)
						animSelect.SelectedIndex = 0;
				}
				Log($"Folder changed: {folderSelect.SelectedItem} ({animSelect.Items.Count} animations)");
			};


			// Animation timer (30fps playback)
			animTimer = new Timer { Interval = 33 };
			animTimer.Tick += (s, e) =>
			{
				// === BATTLE ANIMATION ===
				if (isBattleModel && battleAnimations != null && battleAnimations.Count > 0)
				{
					var anim = battleAnimations[currentBattleAnimIndex];
					currentFrameIndex++;
					    // DIAGNOSTIC - only print once (on first frame)
							var testAnim = battleAnimations[0];
							var f0 = testAnim.Frames[0];
							Log($"Battle Anim: {testAnim.NumBones} bones, {testAnim.NumFrames} frames");
							Log($"Root pos: {f0.RootX}, {f0.RootY}, {f0.RootZ}");
							for (int b = 0; b < Math.Min(5, testAnim.NumBones); b++)
							{
								Log($"Bone {b}: rotX={f0.GetRotXDeg(b):F1}  rotY={f0.GetRotYDeg(b):F1}  rotZ={f0.GetRotZDeg(b):F1}");
							}
	
					if (currentFrameIndex >= anim.NumFrames)
					{
						if (chkLoop.Checked)
							currentFrameIndex = 0;
						
						else //Allow looping battle animations
						{
							currentFrameIndex = anim.NumFrames - 1;
							animPlaying = false;
							animTimer.Stop();
						}
				
					}
					glPanel.Invalidate();
					
				}
				// === FIELD ANIMATION
				else
				{
					if (currentSkeleton == null || currentSkeleton.Frames.Count == 0) { animTimer.Stop(); return; } //Safety check to not allow frame "1" to play if no frames, app will crash
					if (currentSkeleton == null || currentSkeleton.Frames.Count == 0) return;
				
					currentFrameIndex++;
					if (currentFrameIndex >= currentSkeleton.Frames.Count)
					{
						if (chkLoop.Checked)
							currentFrameIndex = 0;
						else //Allow looping to keep playing
						{
							currentFrameIndex = currentSkeleton.Frames.Count - 1;
							animPlaying = false;
							animTimer.Stop();
						}
					}
					frameSlider.Value = currentFrameIndex;
					UpdateFrameLabel();
				}
			};
			
			
			//Finish animation controls
            Controls.Add(statusStrip);
            Controls.Add(menu);
			Controls.Add(logList); //Log list console made, add as control
            MainMenuStrip = menu;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Home) ResetCamera(); };
            renderTimer = new Timer { Interval = 16 };
            renderTimer.Tick += (s, e) => Render();
			
			animPanel.Controls.Add(new Label { Text = "Weapon:", Location = new Point(5, 42), AutoSize = true }); //d new panel and label
			weaponSelect = new ComboBox { Location = new Point(70, 38), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList }; //Start weapon select logic
			animPanel.Controls.Add(weaponSelect);
			weaponSelect.SelectedIndexChanged += (s, e) =>
			{
				int weaponBone = 11; // Bone to attach weapon to
				currentSkeleton.Bones[weaponBone].Models.RemoveAll(m => m.IsWeapon);
				
				if (weaponSelect.SelectedIndex > 0)
				{
					string weaponPath = weaponFiles[weaponSelect.SelectedIndex - 1];
					var weapon = PFileLoader.Load(weaponPath);
					//weapon.OriginalFilePath = weaponPath;
					weapon.IsWeapon = true;
					weapon.Textures.Clear();
					int weaponIndex = weaponSelect.SelectedIndex - 1; // 0-based
					string skeletonPrefix = Path.GetFileName(currentSkeleton.Name).Substring(0, 2); //Weapons are using better dds files, so map from dds name
					var tex = TextureLoader.LoadWeaponTexture(weaponPath, Path.GetDirectoryName(weaponPath) ?? "", weaponIndex, skeletonPrefix);
					if (tex != null)
					{
						weapon.RotateX = -90f; //Weapon attachs do not always reflect the perfect angle, fune tune it
						weapon.Textures.Add(tex);
						if (glReady) UploadTexture(tex);
					}
					currentSkeleton.Bones[weaponBone].Models.Add(weapon);
					
					Log($"Weapon loaded: {Path.GetFileName(weaponPath)} -> bone {weaponBone} -> Texture: loaded={tex?.LoadedSuccessfully}, file={tex?.FileName}, w={tex?.Width}, h={tex?.Height}");
				}
				glPanel.Invalidate();
			}; //Finish weapon select logic
        }
		
		private void PopulateAnimControls(string dir, string prefix = null)
			{
				animDir = dir;
				
				// Populate folder dropdown with sibling folders
				parentDir = Path.GetDirectoryName(dir) ?? "";
				folderSelect.Items.Clear();
				if (Directory.Exists(parentDir))
				{
					foreach (var subDir in Directory.GetDirectories(parentDir))
						folderSelect.Items.Add(Path.GetFileName(subDir));
					string currentFolder = Path.GetFileName(dir);
					int idx = folderSelect.Items.IndexOf(currentFolder);
					if (idx >= 0) folderSelect.SelectedIndex = idx;
				}
				
				// Populate animation dropdown
				animSelect.Items.Clear();
				
				// Add .a files (field animations)
				foreach (var file in Directory.GetFiles(dir, "*.a", SearchOption.TopDirectoryOnly))
					animSelect.Items.Add(Path.GetFileName(file));
				
				// Add ALL battle animation files (any file matching **da pattern)
				foreach (var file in Directory.GetFiles(dir).OrderBy(f => Path.GetFileName(f)))
				{
					string fname = Path.GetFileName(file);
					if (fname.Length >= 4 && fname.Substring(2, 2).ToLower() == "da")
					{
						if (!animSelect.Items.Contains(fname))
							animSelect.Items.Add(fname);
					}
				}
				
				if (animSelect.Items.Count > 0)
					animSelect.SelectedIndex = 0;
			}
			


		private PModel selectedModel = null; //Selection to apply move P file 
		
		//Logic for animations
		void UpdateFrameLabel()
		{
			frameLabel.Text = $"Frame: {currentFrameIndex} / {frameSlider.Maximum}";
		}


        private void InitGL()
        {
            hdc = GetDC(glPanel.Handle);
            var pfd = new PIXELFORMATDESCRIPTOR();
            pfd.nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>();
            pfd.nVersion = 1;
            pfd.dwFlags = 0x00000001 | 0x00000004 | 0x00000020;
            pfd.iPixelType = 0;
            pfd.cColorBits = 32;
            pfd.cDepthBits = 24;
            pfd.cStencilBits = 8;

            int pixelFormat = ChoosePixelFormat(hdc, ref pfd);
            if (pixelFormat == 0) { MessageBox.Show("ChoosePixelFormat failed", "GL Error"); return; }
            if (!SetPixelFormat(hdc, pixelFormat, ref pfd)) { MessageBox.Show("SetPixelFormat failed", "GL Error"); return; }
            hglrc = wglCreateContext(hdc);
            if (hglrc == IntPtr.Zero) { MessageBox.Show("wglCreateContext failed", "GL Error"); return; }
            wglMakeCurrent(hdc, hglrc);

            glEnable(GL_DEPTH_TEST);
            glDepthFunc(GL_LEQUAL);
            glEnable(GL_BLEND);
            glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
            SetupProjection();
            glReady = true;
            statusLabel.Text = "OpenGL ready - Open a .P or .HRC file";
        }

        private void Log(string msg)
		{
			string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kimera2_log.txt");
			string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
			File.AppendAllText(logPath, line + "\n");
			if (logList != null)
			{
				logList.Items.Add(line);
				logList.TopIndex = logList.Items.Count - 1; // auto-scroll to bottom
			}
		}

		private void SetupProjection()
        {
            int w = glPanel.Width, h = Math.Max(glPanel.Height, 1);
            glViewport(0, 0, w, h);
            glMatrixMode(GL_PROJECTION);
            glLoadIdentity();
            double fov = 45.0 * Math.PI / 180.0;
            double aspect = (double)w / h;
            double near = 1.0, far = 50000.0;
            double f = 1.0 / Math.Tan(fov / 2.0);
            double[] proj = new double[16];
            proj[0] = f / aspect; proj[5] = f;
            proj[10] = (far + near) / (near - far); proj[11] = -1.0;
            proj[14] = (2.0 * far * near) / (near - far);
            glLoadMatrixd(proj);
            glMatrixMode(GL_MODELVIEW);
        }

        private void Render()
        {
            if (!glReady) return;
            wglMakeCurrent(hdc, hglrc);
            glClearColor(0.3f, 0.4f, 0.7f, 1.0f);
            glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            glLoadIdentity();
            glTranslatef(panX, panY, zoom);
            glRotatef(rotX, 1, 0, 0);
            glRotatef(rotY, 0, 1, 0);

            DrawGrid();
            if (showWireframe) glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
            else glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
			glDisable(GL_BLEND);

            if (currentSkeleton != null) RenderSkeleton();
            else if (currentModel != null) RenderPModel(currentModel);
            else DrawTestTriangle();

            glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
            SwapBuffers(hdc);
        }

        private void DrawGrid()
        {
            glDisable(GL_TEXTURE_2D);
            glBegin(GL_LINES);
            glColor3f(0.4f, 0.4f, 0.4f);
            for (int i = -100; i <= 100; i += 20)
            { glVertex3f(i, 0, -100); glVertex3f(i, 0, 100); glVertex3f(-100, 0, i); glVertex3f(100, 0, i); }
            glEnd();
        }

        private void DrawTestTriangle()
        {
            glDisable(GL_TEXTURE_2D);
            glBegin(GL_TRIANGLES);
            glColor3f(1, 0, 0); glVertex3f(0, 50, 0);
            glColor3f(0, 1, 0); glVertex3f(-50, -50, 0);
            glColor3f(0, 0, 1); glVertex3f(50, -50, 0);
            glEnd();
        }

        private void RenderPModel(PModel model)
        {
            if (model == null || model.Groups.Count == 0) return;
            for (int gi = 0; gi < model.Groups.Count; gi++)
            {
                try
                {                   
				   var grp = model.Groups[gi];
                    bool hasTex = showTextures && model.Textures.Count > 0 && model.Textures[0].OpenGLTextureId > 0;
					if (hasTex) { glEnable(GL_TEXTURE_2D); glBindTexture(GL_TEXTURE_2D, (uint)model.Textures[0].OpenGLTextureId); }
					if (!hasTex) glDisable(GL_TEXTURE_2D);
					if (model.Textures.Count == 0)
						glDisable(GL_TEXTURE_2D);

                    glBegin(GL_TRIANGLES);
                    int pEnd = Math.Min(grp.PolygonStartIndex + grp.NumPolygons, model.Polygons.Count);
                    for (int p = grp.PolygonStartIndex; p < pEnd; p++)
                    {
                        var poly = model.Polygons[p];
                        int v1 = grp.VerticesStartIndex + poly.VertexIndex1;
                        int v2 = grp.VerticesStartIndex + poly.VertexIndex2;
                        int v3 = grp.VerticesStartIndex + poly.VertexIndex3;
                        if (v1 >= model.Vertices.Count || v2 >= model.Vertices.Count || v3 >= model.Vertices.Count) continue;
                        EmitVert(model, grp, v1, poly.NormalIndex1, poly.VertexIndex1, hasTex);
                        EmitVert(model, grp, v2, poly.NormalIndex2, poly.VertexIndex2, hasTex);
                        EmitVert(model, grp, v3, poly.NormalIndex3, poly.VertexIndex3, hasTex);
                    }
                    glEnd();
                }
                catch { }
            }
        }

        private void EmitVert(PModel m, PGroup g, int vi, int ni, int localVi, bool hasTex)
        {
			if (hasTex)
				glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
			else if (showVertexColors && vi < m.VertexColors.Count) { var c = m.VertexColors[vi]; glColor3ub(c.R, c.G, c.B); }
			else glColor4f(0.6f, 0.6f, 0.6f, 1.0f);
            int absN = g.VerticesStartIndex + ni;
            if (absN < m.Normals.Count) { var n = m.Normals[absN]; glNormal3f(n.X, n.Y, n.Z); }
            if (hasTex) { int ti = g.TexCoordStartIndex + localVi; if (ti < m.TexCoords.Count) glTexCoord2f(m.TexCoords[ti].U, m.TexCoords[ti].V); }
            var v = m.Vertices[vi]; glVertex3f(v.X, v.Y, v.Z);
        }

		
		private void RenderBoneRecursive(int boneIndex, AnimationFrame frame) //Used in hrc/field and binary battle skeleton build files
		{
			var bone = currentSkeleton.Bones[boneIndex];
			glPushMatrix();
			
			// Apply this bone's rotation (YXZ order - FF7 standard)
			if (isBattleModel && battleAnimations != null && battleAnimations.Count > 0)
			{
				var battleAnim = battleAnimations[currentBattleAnimIndex];
				int animBoneIdx = boneIndex + 1; //Offset by 1 as bone0 is root, bone 1 is skeleton etc
				if (currentFrameIndex < battleAnim.NumFrames && animBoneIdx < battleAnim.NumBones)
				{
				  	var bFrame = battleAnim.Frames[currentFrameIndex];
				  	glRotatef(bFrame.GetRotYDeg(animBoneIdx), 0, 1, 0); 
				  	glRotatef(bFrame.GetRotXDeg(animBoneIdx), 1, 0, 0); 
				  	glRotatef(bFrame.GetRotZDeg(animBoneIdx), 0, 0, 1);
				}
			}
			else if (frame != null && boneIndex < frame.BoneRotations.Count)
			{
				// For Field animations and drawing
				var rot = frame.BoneRotations[boneIndex];
				glRotatef(rot.Y, 0, 1, 0);
				glRotatef(rot.X, 1, 0, 0);
				glRotatef(rot.Z, 0, 0, 1);
			}
			
			// Draw this bone's models, this must also update from controls added when moved
			foreach (var model in bone.Models)
			{
				try
				{
					glPushMatrix();
					glTranslatef(model.OffsetX, model.OffsetY, model.OffsetZ);
					glRotatef(model.RotateX, 1, 0, 0);
					glRotatef(model.RotateY, 0, 1, 0);
					glRotatef(model.RotateZ, 0, 0, 1);
					glScalef(model.ScaleX, model.ScaleY, model.ScaleZ);
					RenderPModel(model);
					// Draw green wireframe box around selected model
					if (selectedModels.Contains(model) && model.Vertices.Count > 0)
					{
						glDisable(GL_TEXTURE_2D);
						glColor3f(0, 1, 0); // Green, can use other colors here is wanted
						DrawBoundingBox(model);
					}
					//Finish draw green wireframe box 
					glPopMatrix();
				}
				catch { }
			}
			
			// Render children - translate along THIS bone's length on Y axis for animations
			for (int i = 0; i < currentSkeleton.Bones.Count; i++)
			{
				if (currentSkeleton.Bones[i].ParentIndex == boneIndex)
				{
					glPushMatrix();
					//if (isBattleModel)
						//glTranslatef(0, -bone.Length, 0); //Y axis needed for battle models
					//else
						glTranslatef(0, 0, -bone.Length); //This does the movement of p parts to diff. areas arond a bone structure
					
					RenderBoneRecursive(i, frame);
					glPopMatrix();
				}
			}
			
			glPopMatrix();
		} // Finish animation render logic 
		
		private void RenderSkeleton() //Start animation render logic based on original working kimera .99 way
		{
			if (currentSkeleton == null) return;
			var frame = (currentSkeleton.Frames.Count > 0 && currentFrameIndex < currentSkeleton.Frames.Count) ? currentSkeleton.Frames[currentFrameIndex] : null;
			
			// Apply root translation
			if (frame != null && !isBattleModel)
				glTranslatef(frame.RootTranslation.X, frame.RootTranslation.Z, frame.RootTranslation.Y);
			
			if (isBattleModel) //Battle model at a much diff. scale, so scale down
				{
					glScalef(0.05f,  0.05f,  0.05f);
				}
			// Start recursive rendering from root bones
			for (int i = 0; i < currentSkeleton.Bones.Count; i++)
			{
				if (currentSkeleton.Bones[i].ParentIndex == -1)
				{
						// Apply anim bone 0 rotation to all roots
						if (battleAnimations != null && battleAnimations.Count > 0)
						{
							var bFrame = battleAnimations[currentBattleAnimIndex].Frames[currentFrameIndex];
							glRotatef(bFrame.GetRotYDeg(0), 0, 1, 0);
							glRotatef(bFrame.GetRotXDeg(0), 1, 0, 0);
							glRotatef(bFrame.GetRotZDeg(0), 0, 0, 1);
						}
					RenderBoneRecursive(i, frame);
				}
			}
		}

		
		
		//Start code chunk to have green wirebox around slected model
		private void DrawBoundingBox(PModel model)
		{
			float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
			foreach (var v in model.Vertices)
			{
				if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
				if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
				if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
			}
			// Draw 12 edges of the box
			glBegin(GL_LINES);
			// Bottom face
			glVertex3f(minX,minY,minZ); glVertex3f(maxX,minY,minZ);
			glVertex3f(maxX,minY,minZ); glVertex3f(maxX,maxY,minZ);
			glVertex3f(maxX,maxY,minZ); glVertex3f(minX,maxY,minZ);
			glVertex3f(minX,maxY,minZ); glVertex3f(minX,minY,minZ);
			// Top face
			glVertex3f(minX,minY,maxZ); glVertex3f(maxX,minY,maxZ);
			glVertex3f(maxX,minY,maxZ); glVertex3f(maxX,maxY,maxZ);
			glVertex3f(maxX,maxY,maxZ); glVertex3f(minX,maxY,maxZ);
			glVertex3f(minX,maxY,maxZ); glVertex3f(minX,minY,maxZ);
			// Connecting edges
			glVertex3f(minX,minY,minZ); glVertex3f(minX,minY,maxZ);
			glVertex3f(maxX,minY,minZ); glVertex3f(maxX,minY,maxZ);
			glVertex3f(maxX,maxY,minZ); glVertex3f(maxX,maxY,maxZ);
			glVertex3f(minX,maxY,minZ); glVertex3f(minX,maxY,maxZ);
			glEnd();
		}

        private uint UploadTexture(Texture tex)
        {
            if (!tex.LoadedSuccessfully || tex.PixelData.Length == 0) return 0;
            uint[] ids = new uint[1];
            glGenTextures(1, ids);
            glBindTexture(GL_TEXTURE_2D, ids[0]);
            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
            glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, tex.Width, tex.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, tex.PixelData);
            tex.OpenGLTextureId = (int)ids[0];
            return ids[0];
        }

		private void OpenFile(string filter, Action<string> loader)
		{
			using var dlg = new OpenFileDialog { Filter = filter };
			if (dlg.ShowDialog() == DialogResult.OK) loader(dlg.FileName);
		}
		
		private void LoadPFile(string path)
		{
			statusLabel.Text = $"Loading {Path.GetFileName(path)}..."; Application.DoEvents();
			selectedModels.Clear();
			selectedModel = null;
			currentModel = PFileLoader.Load(path); currentSkeleton = null;
			statusLabel.Text = $"H={currentModel.Header.NumHundreds} U2={currentModel.Header.NumUnknown2} U3={currentModel.Header.NumUnknown3}";
			string dir = Path.GetDirectoryName(path) ?? ".";

			// Find the RSD that references this P file
			string baseName = Path.GetFileNameWithoutExtension(path).ToUpper();
			string[] rsdFiles = Directory.GetFiles(dir, "*.rsd", SearchOption.TopDirectoryOnly);
			bool foundRsd = false;

			foreach (var rsdPath in rsdFiles)
			{
				var rsd = RsdLoader.Load(rsdPath);
				string plyName = Path.GetFileNameWithoutExtension(rsd.PolygonFile).ToUpper();
				if (plyName == baseName)
				{
					// Found the matching RSD - load its textures
					foreach (var tn in rsd.TextureFiles)
					{
						// TEX[0]=CLOUCLAF.TIM -> try CLOUCLAF.TEX, CLOUCLAF_00.dds, CLOUCLAF.dds
						string texBase = Path.GetFileNameWithoutExtension(tn);
						var tex = TextureLoader.Load(texBase + ".TEX", dir);
						if (!tex.LoadedSuccessfully) tex = TextureLoader.Load(texBase + "_00.dds", dir);
						if (!tex.LoadedSuccessfully) tex = TextureLoader.Load(texBase + ".dds", dir);
						if (!tex.LoadedSuccessfully) tex = TextureLoader.Load(tn, dir);
						currentModel.Textures.Add(tex);
					}
					foundRsd = true;
					break;
				}
			}

			if (!foundRsd)
			{
				// Fallback: load all textures from folder
				foreach (var f in Directory.GetFiles(dir, "*.dds", SearchOption.TopDirectoryOnly))
					currentModel.Textures.Add(TextureLoader.Load(f, dir));
				foreach (var f in Directory.GetFiles(dir, "*.tex", SearchOption.TopDirectoryOnly))
					currentModel.Textures.Add(TextureLoader.Load(f, dir));
			}
			//End Finding textures for P files
			foreach (var f in Directory.GetFiles(dir, "*.bmp", SearchOption.TopDirectoryOnly))
				currentModel.Textures.Add(TextureLoader.Load(f, dir));

			if (glReady)
				foreach (var tex in currentModel.Textures)
					UploadTexture(tex);

			UpdateUI();
			Log($"Loaded {currentModel.FileName}: {currentModel.Vertices.Count} verts, {currentModel.Polygons.Count} polys, {currentModel.Groups.Count} groups");
			Log($"Header: NumHundreds={currentModel.Header.NumHundreds}, NumUnknown2={currentModel.Header.NumUnknown2}, NumUnknown3={currentModel.Header.NumUnknown3}, NumNormals={currentModel.Header.NumNormals}, NumTexCoords={currentModel.Header.NumTexCoords}");
			Log($"Textures found: {currentModel.Textures.Count}");
			for (int i = 0; i < currentModel.Textures.Count; i++)
				Log($"  Tex[{i}]: {currentModel.Textures[i].FileName} loaded={currentModel.Textures[i].LoadedSuccessfully} glId={currentModel.Textures[i].OpenGLTextureId} err={currentModel.Textures[i].LoadError}");
			for (int i = 0; i < currentModel.Groups.Count; i++)
				Log($"  Group[{i}]: polyStart={currentModel.Groups[i].PolygonStartIndex} numPoly={currentModel.Groups[i].NumPolygons} vertStart={currentModel.Groups[i].VerticesStartIndex} numVert={currentModel.Groups[i].NumVertices} texUsed={currentModel.Groups[i].AreTexturesUsed} texNum={currentModel.Groups[i].TextureNumber} texCoordStart={currentModel.Groups[i].TexCoordStartIndex}");

			if (currentModel.Textures.Count > 0)
				Log($"  Tex[0] dimensions: {currentModel.Textures[0].Width}x{currentModel.Textures[0].Height}, pixeldata size={currentModel.Textures[0].PixelData.Length}");
			{
				float maxD = 0;
				foreach (var v in currentModel.Vertices)
				{
					float d = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
					if (d > maxD) maxD = d;
				}
				zoom = -(maxD * 4f + 50f);
			}

			rotX = 0; rotY = 0; panX = 0; panY = 0;
		}

        private void LoadHrcFile(string path)
        {
            statusLabel.Text = $"Loading {Path.GetFileName(path)}..."; Application.DoEvents();
			selectedModels.Clear();
			selectedModel = null;
            string dir = Path.GetDirectoryName(path) ?? ".";
            currentSkeleton = HrcLoader.Load(path); currentModel = null; //Grab original file name to save back later
			currentSkeleton.OriginalFilePath = path;
			if (currentSkeleton.LoadWarnings.Count > 0) //Make user aware header data missing, this can break the game
			{
				Log($"⚠️ {currentSkeleton.LoadWarnings[0]}"); //Let user know if header data missing, but still load file
				MessageBox.Show(currentSkeleton.LoadWarnings[0], "HRC Header warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
			int boneIdx = 0;
			isBattleModel=false; //Set var to tell animation player this is a field skeleton
			foreach (var bone in currentSkeleton.Bones)
			{
				boneIdx++;
				statusLabel.Text = $"Loading bone {boneIdx}/{currentSkeleton.Bones.Count}: {bone.Name}...";
				Log($"Loading bone {boneIdx}/{currentSkeleton.Bones.Count}: {bone.Name} ({bone.RsdNames.Count} parts)");
				Application.DoEvents(); // Forces the UI to update immediately
				foreach (var rsdName in bone.RsdNames)
				{
					string rp = Path.Combine(dir, rsdName + ".rsd");
					if (!File.Exists(rp)) rp = Path.Combine(dir, rsdName.ToLower() + ".rsd");
					if (!File.Exists(rp)) rp = Path.Combine(dir, rsdName.ToUpper() + ".RSD");
					if (!File.Exists(rp)) continue;
					var rsd = RsdLoader.Load(rp);

					// Try finding the P file with multiple extensions/cases
					string pName = rsd.PolygonFile;
					if (string.IsNullOrEmpty(pName)) pName = rsdName + ".P";
					string pp = Path.Combine(dir, pName);
					if (!File.Exists(pp)) pp = Path.Combine(dir, pName.ToLower());
					if (!File.Exists(pp)) pp = Path.Combine(dir, pName.ToUpper());
					if (!File.Exists(pp)) pp = Path.Combine(dir, rsdName + ".p");
					if (!File.Exists(pp)) pp = Path.Combine(dir, rsdName.ToUpper() + ".P");
					if (!File.Exists(pp)) continue;
					
					var m = PFileLoader.Load(pp);
					m.OriginalFilePath = pp; //Need when saving
					foreach (var tn in rsd.TextureFiles)
					{ var t = TextureLoader.Load(tn, dir); m.Textures.Add(t); if (glReady) UploadTexture(t); }
					// Skip duplicate P files on same bone
					if (bone.Models.Any(existing => existing.FileName == m.FileName))
						continue;
					bone.Models.Add(m);
					Log($"  Bone '{bone.Name}': loaded {m.FileName} ({m.Vertices.Count} verts, {m.Textures.Count} tex, firstTexLoaded={( m.Textures.Count > 0 ? m.Textures[0].LoadedSuccessfully.ToString() : "none")})");

				}
			}
			
			
			
			// Load first animation file found
			string[] animFiles = Directory.GetFiles(dir, "*.a", SearchOption.TopDirectoryOnly);
			if (animFiles.Length > 0)
			{
				var anim = AnimLoader.Load(animFiles[0], currentSkeleton.Bones.Count);
				if (anim.Count > 0)
				{
					currentSkeleton.Frames = anim;
					frameSlider.Maximum = currentSkeleton.Frames.Count - 1;
					frameSlider.Value = 0;
					currentFrameIndex = 0;
					UpdateFrameLabel();
					Log($"Animation loaded: {Path.GetFileName(animFiles[0])}, {anim.Count} frames");
				}
			}

			PopulateAnimControls(dir);

			UpdateUI(); ResetCamera();
        }
	
		
		private void LoadBattleFile(string path) //Start load battle file, has no extension so find file header data first, then range of the files with it
		{
			byte[] header = new byte[64];
			using (var fs = File.OpenRead(path))
				fs.Read(header, 0, Math.Min(64, (int)fs.Length));
			
			long fileSize = new FileInfo(path).Length;
			int val0 = BitConverter.ToInt32(header, 0);
			int val3 = BitConverter.ToInt32(header, 12); // potential bone count
			int val4 = BitConverter.ToInt32(header, 16); // potential normals count
			
			// Check if it's a skeleton file: size = 52 + bones * 12
			if (val0 <= 2 && val3 > 0 && val3 < 100 && fileSize == 52 + val3 * 12)
			{
				Log($"Detected: Battle Skeleton ({val3} bones)");
				LoadBattleSkeleton(path);
			}
			// Check if it's a P file: version=1, off04=1, reasonable vertex/poly counts
			else if (val0 == 1 && BitConverter.ToInt32(header, 4) == 1 && val3 > 0 && val3 < 100000)
			{
				Log($"Detected: P model file ({val3} vertices)");
				LoadPFile(path);
			}
			// Check if DDS texture
			else if (header[0] == 0x44 && header[1] == 0x44 && header[2] == 0x53 && header[3] == 0x20)
			{
				Log($"This is a texture file (DDS), not a model.");
				MessageBox.Show("This is a texture file (DDS). Please select a skeleton or model file.", "Wrong File Type");
			}
			else
			{
				Log($"Unknown file type: size={fileSize}, first int={val0}");
				MessageBox.Show($"Could not identify this file type.\n\nFile: {Path.GetFileName(path)}\nSize: {fileSize} bytes\n\nExpected a battle skeleton (rtaa-style) or P model file.", "Unknown Format");
			}
		}

		private void LoadBattleSkeleton(string path) //Start load battle skeleton, has no extenstion so use known xyaa syntax for skeleton files
		{
			string dir = Path.GetDirectoryName(path) ?? ".";
			string baseName = Path.GetFileNameWithoutExtension(path);
			
			// Read skeleton
			byte[] data = File.ReadAllBytes(path);
			int numBones = BitConverter.ToInt32(data, 12);
			int numParts = BitConverter.ToInt32(data, 0x1C); // offset 0x1C not 0x24, some battle models are simpler
			if (numParts <= 0 || numParts > 50) numParts = 50; // fallback: load up to 50 for bigger models
			isBattleModel=true; //Set var to tell animation player this is a battle skeleton
			Log($"Battle skeleton: {numBones} bones, {numParts} parts");
			
			// Create skeleton
			currentSkeleton = new Skeleton { Name = baseName };
			currentModel = null;
			
			for (int i = 0; i < numBones; i++)
			{
				int off = 0x34 + i * 12;
				int parent = BitConverter.ToInt32(data, off);
				float length = BitConverter.ToSingle(data, off + 4);
				int hasModel = BitConverter.ToInt32(data, off + 8);
				
				var bone = new Bone
				{
					Name = $"bone_{i}",
					ParentIndex = parent,
					Length = Math.Abs(length), // battle bones use negative lengths
					ParentName = parent >= 0 ? $"bone_{parent}" : "root"
				};
				currentSkeleton.Bones.Add(bone);
			}
			
			// Assign to bones that have hasModel=1
			var bonesWithModels = new List<int>();
			for (int i = 0; i < numBones; i++)
			{
				int off = 0x34 + i * 12;
				int hasModel = BitConverter.ToInt32(data, off + 8);
				if (hasModel == 1) bonesWithModels.Add(i);
			}

			
			// Battle naming: if skeleton is "rtaa", mesh files are "rtab", "rtbc", "rtbh" etc.
			// Find and load P model files matching this character's prefix
			string prefix = baseName.Length >= 2 ? baseName.Substring(0, 2) : baseName;

			var modelFiles = new List<string>();
			foreach (var file in Directory.GetFiles(dir).OrderBy(f => Path.GetFileName(f)))
			{
				string fname = Path.GetFileName(file).ToLower();
				if (!fname.StartsWith(prefix.ToLower())) continue;
				if (fname.Length < 4) continue;
				if (fname == baseName.ToLower()) continue; // skip skeleton itself
				
				string suffix = fname.Substring(2);
				// Skip skeleton (aa), unknown (ab), textures (ac-al for complex models), animations (d*)
				if (suffix == "aa") continue; // skip skeleton only
				if (suffix == "ab") continue; // Unknown, skip for now
				if (suffix.StartsWith("da")) continue; // skip animations
				
				// Check if it's a P file
				if (new FileInfo(file).Length < 128) continue;
				byte[] fHeader = new byte[16];
				using (var fs = File.OpenRead(file)) fs.Read(fHeader, 0, 16);
				if (BitConverter.ToInt32(fHeader, 0) == 1 && BitConverter.ToInt32(fHeader, 4) == 1)
				{
					modelFiles.Add(file);
					if (modelFiles.Count >= bonesWithModels.Count) break; // stop at body part count, omit extra weapons
				}
			}
			Log($"Found {modelFiles.Count} model files for prefix '{prefix}'");


			int partsLoaded = 0;
			for (int i = 0; i < modelFiles.Count; i++)
			{
				statusLabel.Text = $"Loading part {i + 1}/{modelFiles.Count}..."; 
				Application.DoEvents();
				
				var model = PFileLoader.Load(modelFiles[i]);
				model.OriginalFilePath = modelFiles[i];
				
				int boneIdx = i < bonesWithModels.Count ? bonesWithModels[i] : 0;
				currentSkeleton.Bones[boneIdx].Models.Add(model);
				partsLoaded++;
				Log($"  {Path.GetFileName(modelFiles[i])} → {currentSkeleton.Bones[boneIdx].Name} (bone {boneIdx})");
			}

			// Find weapon files (suffix starts with 'c': rtca, rtcb, rtcc, etc.)
			weaponFiles.Clear();
			foreach (var file in Directory.GetFiles(dir).OrderBy(f => Path.GetFileName(f)))
			{
				string fname = Path.GetFileName(file).ToLower();
				if (!fname.StartsWith(prefix.ToLower())) continue;
				if (fname.Length < 4) continue;
				string suffix = fname.Substring(2);
				if (suffix.Length >= 1 && suffix[0] == 'c')
				{
					// Verify it's a P file
					if (new FileInfo(file).Length < 128) continue;
					byte[] fHeader = new byte[16];
					using (var fs = File.OpenRead(file)) fs.Read(fHeader, 0, 16);
					if (BitConverter.ToInt32(fHeader, 0) == 1 && BitConverter.ToInt32(fHeader, 4) == 1)
						weaponFiles.Add(file);
				}
			}
			Log($"Found {weaponFiles.Count} weapon files");
			// Populate weapon dropdown
			weaponSelect.Items.Clear();
			weaponSelect.Items.Add("(none)");
			foreach (var wf in weaponFiles)
				weaponSelect.Items.Add(Path.GetFileName(wf));
			if ( weaponFiles.Count > 0 ) // select first weapon
				weaponSelect.SelectedIndex = 1;
							
			
			// Load textures - battle textures are files between "ab" and "am" in suffix order
			// (ac through al are textures, am onwards are body parts)
			var textureFiles = new List<string>();
			foreach (var file in Directory.GetFiles(dir).OrderBy(f => Path.GetFileName(f)))
			{
				string fname = Path.GetFileName(file).ToLower();
				if (!fname.StartsWith(prefix.ToLower())) continue;
				if (fname.Length < 4) continue;
				
				string suffix = fname.Substring(2);
				// Texture files are between "ac" and "al" inclusive
				if (suffix.Length >= 2 && suffix[0] >= 'a' && suffix[0] <= 'a'
					&& suffix[1] >= 'c' && suffix[1] <= 'l')
				{
					textureFiles.Add(file);
				}
			}

			// Load and assign textures to models
			foreach (var texFile in textureFiles)
			{
				try
				{
					var tex = TextureLoader.Load(texFile, dir);
					if (glReady) UploadTexture(tex);
					
					// Assign to models that don't have a texture yet
					foreach (var bone in currentSkeleton.Bones)
						foreach (var model in bone.Models)
							if (model.Textures.Count == 0)
							{
								model.Textures.Add(tex);
							}
					
					Log($"  Loaded texture: {Path.GetFileName(texFile)}");
					break; // use first valid texture for now
				}
				catch { }
			}
			
			Log($"Battle model loaded: {partsLoaded} parts across {numBones} bones");
			for (int i = 0; i < currentSkeleton.Bones.Count; i++)
			{
				var bone = currentSkeleton.Bones[i];
				Log($"Bone {i}: parent={bone.ParentIndex}, length={bone.Length:F1}, hasModel={((bone.Models.Count > 0) ? "yes" : "no")}");
			}

			for (int i = 0; i < Math.Min(5, currentSkeleton.Bones.Count); i++)
			{
				var bone = currentSkeleton.Bones[i];
				Log($"Bone {i}: parent={bone.ParentIndex}, length={bone.Length}");
			}
			// Populate animation dropdown with da files (battle animations have no extension)
			PopulateAnimControls(dir, prefix);
			
			// Load first battle animation file found
			string[] battleAnimFiles = Directory.GetFiles(dir)
				.Where(f => Path.GetFileName(f).StartsWith(prefix + "d", StringComparison.OrdinalIgnoreCase))
				.OrderBy(f => f)
				.ToArray();

			if (battleAnimFiles.Length > 0)
			{
				battleAnimations = BattleAnimLoader.Load(battleAnimFiles[0]);
				currentFrameIndex = 0;
				currentBattleAnimIndex = 0;
				//frameSlider.Maximum = currentSkeleton.Frames.Count - 1;
				frameSlider.Value = 0;
				currentFrameIndex = 0;
				UpdateFrameLabel();
				Log($"Battle animation loaded: {Path.GetFileName(battleAnimFiles[0])}, {battleAnimations.Count} anims, {battleAnimations[0].NumFrames} frames");
			}
			

			//folderSelect.Items.Clear(); //This clears the animation folder which is bad
			UpdateUI(); ResetCamera();
			Log($"weaponSelect items after populate: {weaponSelect.Items.Count}");
			
		}

		

        private void UpdateUI()
        {
            modelTree.Nodes.Clear(); long mem = 0;
            if (currentModel != null)
            {
                var r = modelTree.Nodes.Add(currentModel.FileName);
                r.Nodes.Add($"Vertices: {currentModel.Vertices.Count:N0}");
                r.Nodes.Add($"Polygons: {currentModel.Polygons.Count:N0}");
                r.Nodes.Add($"Groups: {currentModel.Groups.Count}");
                r.Nodes.Add($"Textures: {currentModel.Textures.Count}");
                r.Expand(); mem = currentModel.EstimatedMemoryBytes;
                statusLabel.Text = $"{currentModel.FileName}: {currentModel.Vertices.Count:N0} verts, {currentModel.Polygons.Count:N0} polys";
            }
            else if (currentSkeleton != null)
            {
                var r = modelTree.Nodes.Add(currentSkeleton.Name);
				//Allow applied bones/P files changes to update in the UI
                foreach (var b in currentSkeleton.Bones) { var n = r.Nodes.Add($"{b.Name} ({b.Models.Count})"); foreach (var m in b.Models) { var mNode = n.Nodes.Add($"{m.FileName}: {m.Vertices.Count:N0} v"); mNode.Tag = m; mem += m.EstimatedMemoryBytes; } }
                r.Expand(); statusLabel.Text = $"Skeleton: {currentSkeleton.Name} ({currentSkeleton.Bones.Count} bones)";
            }
            memoryLabel.Text = $"Model: {mem/1024.0/1024.0:F1} MB";
        }
		
		private void SaveSelectedP()
		{
			if (selectedModel == null) { Log("No model selected"); return; }
			using var dlg = new SaveFileDialog { Filter = "P Files|*.p", FileName = selectedModel.FileName };
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				PFileSaver.BakeTransform(selectedModel);
				PFileSaver.Save(dlg.FileName, selectedModel);
				Log($"Saved: {dlg.FileName}");
			}
		}

		private void ApplyMoveAnim(bool currentOnly) //Grabs current boolean value from menu menu clicked of true or false to save one or all animations
		{
			if (currentSkeleton == null || rootTranslationOffset == Vector3.Zero) { Log("No offset to apply"); return; }
			
			if (currentOnly)
			{
				string animFile = animSelect.SelectedItem.ToString();
				string dest = Path.Combine(animDir, animFile);
				AnimLoader.Save(dest, currentSkeleton.Frames, currentSkeleton.Bones.Count);
				Log($"Saved current animation: {animFile}");
				rootTranslationOffset = Vector3.Zero;
				return;
			}
			
			// Apply to ALL animations in a folder
			string[] allAnims = Directory.GetFiles(animDir, "*.a", SearchOption.TopDirectoryOnly);
			var result = MessageBox.Show(
				$"This will modify ALL {allAnims.Length} animation files in:\n{animDir}\n\nContinue?",
				"Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (result != DialogResult.Yes) return;
			
			using var dlg = new FolderBrowserDialog { Description = "Select folder to save modified animations", SelectedPath = animDir };
			if (dlg.ShowDialog() != DialogResult.OK) return;
			string destDir = dlg.SelectedPath;
			
			foreach (var animPath in allAnims)
			{
				var frames = AnimLoader.Load(animPath, currentSkeleton.Bones.Count);
				foreach (var frame in frames)
					frame.RootTranslation += rootTranslationOffset;
				string dest = Path.Combine(destDir, Path.GetFileName(animPath));
				AnimLoader.Save(dest, frames, currentSkeleton.Bones.Count);
				Log($"Saved: {Path.GetFileName(animPath)}");
			}
			
			Log($"Applied offset to {allAnims.Length} animations in: {destDir}");
			rootTranslationOffset = Vector3.Zero;
		} //Finish save animation offsets
		
		private void SaveAllP() //Saves all files, including hrc, animations and dds texture files so any new folder has all needed files
		{
			if (currentSkeleton == null) { Log("No skeleton loaded"); return; }
			using var dlg = new FolderBrowserDialog { Description = "Select folder to save model files" };
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				string destDir = dlg.SelectedPath;
				string srcDir = Path.GetDirectoryName(currentSkeleton.Bones[0].Models[0].OriginalFilePath) ?? "";
				var copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				// Save P files (only the ones loaded)
				foreach (var bone in currentSkeleton.Bones)
					foreach (var model in bone.Models)
					{
						PFileSaver.BakeTransform(model);
						string dest = Path.Combine(destDir, model.FileName);
						PFileSaver.Save(dest, model, model.OriginalFilePath);
						copiedFiles.Add(model.FileName);
						Log($"Saved P: {model.FileName}");
					}

				// Save HRC file with updated bone lengths
				string hrcName = Path.GetFileName(currentSkeleton.OriginalFilePath);
				string hrcDest = Path.Combine(destDir, hrcName);
				HrcLoader.Save(hrcDest, currentSkeleton, currentSkeleton.OriginalFilePath);
				Log($"Saved HRC: {hrcName}");


				// Copy only RSD files referenced by loaded bones
				foreach (var bone in currentSkeleton.Bones)
					foreach (var rsdName in bone.RsdNames)
					{
						string rsdFile = rsdName + ".rsd";
						string src = Path.Combine(srcDir, rsdFile);
						if (!File.Exists(src)) src = Path.Combine(srcDir, rsdName.ToUpper() + ".RSD");
						if (!File.Exists(src)) src = Path.Combine(srcDir, rsdName.ToLower() + ".rsd");
						if (File.Exists(src) && !copiedFiles.Contains(Path.GetFileName(src)))
						{
							string dest = Path.Combine(destDir, Path.GetFileName(src));
							if (!string.Equals(src, dest, StringComparison.OrdinalIgnoreCase))
								File.Copy(src, dest, true);
							copiedFiles.Add(Path.GetFileName(src));
							Log($"Saved RSD: {Path.GetFileName(src)}");
						}
					}

				// Copy only texture files that were actually loaded
				foreach (var bone in currentSkeleton.Bones)
					foreach (var model in bone.Models)
						foreach (var tex in model.Textures)
						{
							if (string.IsNullOrEmpty(tex.FileName) || copiedFiles.Contains(tex.FileName)) continue;
							string src = Path.Combine(srcDir, tex.FileName);
							if (File.Exists(src))
							{
								string dest = Path.Combine(destDir, tex.FileName);
								if (!string.Equals(src, dest, StringComparison.OrdinalIgnoreCase))
									File.Copy(src, dest, true);
								  copiedFiles.Add(tex.FileName);
								  Log($"Copied over texture: {tex.FileName}");
							}
						}

				// Copy animation file if loaded
				if (currentSkeleton.Frames.Count > 0 && animSelect.SelectedItem != null)
				{
					string animFile = animSelect.SelectedItem.ToString();
					string src = Path.Combine(animDir, animFile);
					if (File.Exists(src))
					{
						string dest = Path.Combine(destDir, animFile);
						if (!string.Equals(src, dest, StringComparison.OrdinalIgnoreCase))
							File.Copy(src, dest, true);
						Log($"Copied anim: {animFile}");
					}
				}
				// Save animation with modified root translation
				if (currentSkeleton.Frames.Count > 0 && animSelect.SelectedItem != null)
				{
					string animFile = animSelect.SelectedItem.ToString();
					string dest = Path.Combine(destDir, animFile);
					AnimLoader.Save(dest, currentSkeleton.Frames, currentSkeleton.Bones.Count);
					Log($"Saved animation: {animFile}");
				}

				Log($"Done! Saved {copiedFiles.Count} files to: {destDir}");
			}
		}


        private void ResetCamera() { rotX = 0; rotY = 0; panX = 0; panY = 0; zoom = -200; }
    }
}