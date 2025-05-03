using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

// Aliases for pixel‐format enums
using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF  = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal class Game : GameWindow
    {
        private int _skyboxVao, _skyboxVbo;
        private int _cubemapTexture;
        private int _shaderProgram;

        // 36 verts = 6 faces × 2 tris × 3 vertices
        private readonly float[] _skyboxVertices = {
            -1f,  1f, -1f,  -1f, -1f, -1f,   1f, -1f, -1f,
             1f, -1f, -1f,   1f,  1f, -1f,  -1f,  1f, -1f,

            -1f, -1f,  1f,  -1f, -1f, -1f,  -1f,  1f, -1f,
            -1f,  1f, -1f,  -1f,  1f,  1f,  -1f, -1f,  1f,

             1f, -1f, -1f,   1f, -1f,  1f,   1f,  1f,  1f,
             1f,  1f,  1f,   1f,  1f, -1f,   1f, -1f, -1f,

            -1f, -1f,  1f,  -1f,  1f,  1f,   1f,  1f,  1f,
             1f,  1f,  1f,   1f, -1f,  1f,  -1f, -1f,  1f,

            -1f,  1f, -1f,   1f,  1f, -1f,   1f,  1f,  1f,
             1f,  1f,  1f,  -1f,  1f,  1f,  -1f,  1f, -1f,

            -1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f, -1f,
             1f, -1f, -1f,  -1f, -1f,  1f,   1f, -1f,  1f
        };

        private const string SkyboxVertexShaderSource = @"#version 330 core
        layout(location = 0) in vec3 aPosition;
        out vec3 TexCoords;
        uniform mat4 uView;
        uniform mat4 uProjection;
        void main() {
            TexCoords = aPosition;
            vec4 pos = uProjection * uView * vec4(aPosition, 1.0);
            gl_Position = pos.xyww;
        }";

        private const string SkyboxFragmentShaderSource = @"#version 330 core
        in vec3 TexCoords;
        out vec4 FragColor;
        uniform samplerCube skybox;
        void main() {
            FragColor = texture(skybox, TexCoords);
        }";

        public Game(GameWindowSettings gw, NativeWindowSettings nw)
            : base(gw, nw) { }

        protected override void OnLoad()
        {
            base.OnLoad();

            // a non-black clear so you can tell if something drew
            GL.ClearColor(0.1f, 0.2f, 0.3f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            // VAO/VBO setup
            _skyboxVao = GL.GenVertexArray();
            _skyboxVbo = GL.GenBuffer();
            GL.BindVertexArray(_skyboxVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _skyboxVbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          _skyboxVertices.Length * sizeof(float),
                          _skyboxVertices,
                          BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0, 3, VertexAttribPointerType.Float,
                false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);

            
            
            // compile/link
            _shaderProgram = CreateShader(
                SkyboxVertexShaderSource,
                SkyboxFragmentShaderSource);

            
            // tell the shader our cube is on unit 0, enable seamless
            GL.UseProgram(_shaderProgram);
            GL.Uniform1(
                GL.GetUniformLocation(_shaderProgram, "skybox"),
                0);
            GL.Enable(EnableCap.TextureCubeMapSeamless);

            // load & crop the six faces
            var faces = new[]
            {
                "Resources/Textures/skybox/right.jpg",
                "Resources/Textures/skybox/left.jpg",
                "Resources/Textures/skybox/top.jpg",
                "Resources/Textures/skybox/bottom.jpg",
                "Resources/Textures/skybox/front.jpg",
                "Resources/Textures/skybox/back.jpg"
            };

            Console.WriteLine("Working dir: " +
                Directory.GetCurrentDirectory());
            foreach (var f in faces)
                Console.WriteLine($"{f} → exists? {File.Exists(f)}");

            _cubemapTexture = LoadCubemapCropped(faces);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit);

            // draw skybox last, with depth writes off
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.UseProgram(_shaderProgram);

            
            
            // simple camera: just projection + view w/o translation
            var view = Matrix4.Identity;
            view = new Matrix4(new Matrix3(view));
            var proj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                Size.X / (float)Size.Y,
                0.1f, 100f);

            
            
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderProgram, "uView"),
                false, ref view);
            GL.UniformMatrix4(
                GL.GetUniformLocation(_shaderProgram, "uProjection"),
                false, ref proj);

            
            
            GL.BindVertexArray(_skyboxVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(
                TextureTarget.TextureCubeMap,
                _cubemapTexture);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            
            
            // catch any lingering errors
            var err = GL.GetError();
            if (err != ErrorCode.NoError)
                Console.WriteLine($"GL Error: {err}");

            GL.BindVertexArray(0);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);

            SwapBuffers();
        }

        
        
        protected override void OnUnload()
        {
            GL.DeleteVertexArray(_skyboxVao);
            GL.DeleteBuffer(_skyboxVbo);
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteTexture(_cubemapTexture);
            base.OnUnload();
        }

        
        
        private int CreateShader(string vertSrc, string fragSrc)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertSrc);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out int okV);
            if (okV == 0)
                throw new Exception(
                  $"Vertex compile error: {GL.GetShaderInfoLog(vs)}");

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragSrc);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out int okF);
            if (okF == 0)
                throw new Exception(
                  $"Fragment compile error: {GL.GetShaderInfoLog(fs)}");

            
            
            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.GetProgram(prog,
                GetProgramParameterName.LinkStatus,
                out int okP);
            if (okP == 0)
                throw new Exception(
                  $"Link error: {GL.GetProgramInfoLog(prog)}");

            
            
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;
        }

        
        
        // Loads all six bitmaps, finds the smallest side length,
        // center‐crops each to that square, then uploads them.
        private int LoadCubemapCropped(string[] faces)
        {
            // load all bitmaps so we can pick a common square size
            var bitmaps = faces
                .Select(f => new Bitmap(f))
                .ToList();

            // print their original dimensions
            for (int i = 0; i < faces.Length; i++)
            {
                var b = bitmaps[i];
                Console.WriteLine(
                  $"{Path.GetFileName(faces[i])}: " +
                  $"{b.Width}×{b.Height}");
            }

            
            
            // pick the smallest side among them all
            int targetSide = bitmaps
                .Select(b => Math.Min(b.Width, b.Height))
                .Min();
            Console.WriteLine(
              $"→ Cropping each face to {targetSide}×{targetSide}");

            // create & bind texture
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, texID);

            // for each face: center‐crop, flip, lockbits & upload
            for (int i = 0; i < faces.Length; i++)
            {
                var orig = bitmaps[i];
                int x = (orig.Width  - targetSide) / 2;
                int y = (orig.Height - targetSide) / 2;

                using var square = orig.Clone(
                    new Rectangle(x, y, targetSide, targetSide),
                    ImgPF.Format32bppArgb);

                square.RotateFlip(
                    RotateFlipType.RotateNoneFlipY);

                var data = square.LockBits(
                    new Rectangle(0, 0,
                        targetSide, targetSide),
                    ImageLockMode.ReadOnly,
                    ImgPF.Format32bppArgb);

                GL.TexImage2D(
                    TextureTarget.TextureCubeMapPositiveX + i,
                    0,
                    PixelInternalFormat.Rgba,
                    targetSide, targetSide,
                    0,
                    GlPF.Bgra,
                    PixelType.UnsignedByte,
                    data.Scan0);

                square.UnlockBits(data);
                orig.Dispose();
            }

            bitmaps.Clear();

            // set filtering & wrap
            GL.TexParameter(
              TextureTarget.TextureCubeMap,
              TextureParameterName.TextureMinFilter,
              (int)TextureMinFilter.Linear);
            GL.TexParameter(
              TextureTarget.TextureCubeMap,
              TextureParameterName.TextureMagFilter,
              (int)TextureMagFilter.Linear);
            GL.TexParameter(
              TextureTarget.TextureCubeMap,
              TextureParameterName.TextureWrapS,
              (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(
              TextureTarget.TextureCubeMap,
              TextureParameterName.TextureWrapT,
              (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(
              TextureTarget.TextureCubeMap,
              TextureParameterName.TextureWrapR,
              (int)TextureWrapMode.ClampToEdge);

            return texID;
        }
    }
}
