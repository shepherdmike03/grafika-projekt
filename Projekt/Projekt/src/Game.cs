
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using ImgPF = System.Drawing.Imaging.PixelFormat;
using GlPF  = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace MyLavaRunner
{
    internal class Game : GameWindow
    {
        // skybox GL objects
        private int _skyboxVao, _skyboxVbo;
        private int _cubemapTexture;
        private int _shaderProgram;

        // camera
        private Camera _camera;

        // 36 verts
        private readonly float[] _skyboxVertices =
        {
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

        // shaders
        private const string SkyboxVertexShaderSource = @"#version 330 core
layout(location = 0) in vec3 aPosition;
out vec3 TexCoords;
uniform mat4 uView;
uniform mat4 uProjection;
void main()
{
    TexCoords    = aPosition;
    vec4 pos     = uProjection * uView * vec4(aPosition, 1.0);
    gl_Position  = pos.xyww;  // keep depth = 1.0
}";

        private const string SkyboxFragmentShaderSource = @"#version 330 core
in vec3 TexCoords;
out vec4 FragColor;
uniform samplerCube skybox;
void main()
{
    FragColor = texture(skybox, TexCoords);
}";

        // ctor
        public Game(GameWindowSettings gw, NativeWindowSettings nw)
            : base(gw, nw) { }

        // load
        protected override void OnLoad()
        {
            base.OnLoad();

            // camera
            _camera = new Camera(Vector3.Zero);
            CursorState = OpenTK.Windowing.Common.CursorState.Grabbed;
            
            // GL state
            GL.ClearColor(0.1f, 0.2f, 0.3f, 1f);
            GL.Enable(EnableCap.DepthTest);

            // VAO / VBO
            _skyboxVao = GL.GenVertexArray();
            _skyboxVbo = GL.GenBuffer();
            GL.BindVertexArray(_skyboxVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _skyboxVbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          _skyboxVertices.Length * sizeof(float),
                          _skyboxVertices,
                          BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float,
                                   false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);

            // shader program
            _shaderProgram = CreateShader(SkyboxVertexShaderSource,
                                          SkyboxFragmentShaderSource);

            GL.UseProgram(_shaderProgram);
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "skybox"), 0);
            GL.Enable(EnableCap.TextureCubeMapSeamless);

            // cubemap textures
            var faces = new[]
            {
                "Resources/Textures/skybox/right.jpg",
                "Resources/Textures/skybox/left.jpg",
                "Resources/Textures/skybox/top.jpg",
                "Resources/Textures/skybox/bottom.jpg",
                "Resources/Textures/skybox/front.jpg",
                "Resources/Textures/skybox/back.jpg"
            };

            Console.WriteLine("Working dir: " + Directory.GetCurrentDirectory());
            foreach (var f in faces) Console.WriteLine($"{f} exists? {File.Exists(f)}");

            _cubemapTexture = LoadCubemapCropped(faces);
        }

        // resize
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        // per logic frame
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            // close on Escape
            if (KeyboardState.IsKeyPressed(Keys.Escape))
                Close();

            _camera.Update(e, KeyboardState, MouseState, IsFocused);
        }

        // per render frame
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit |
                     ClearBufferMask.DepthBufferBit);

            // draw skybox depth writes off
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.UseProgram(_shaderProgram);

            // matrices
            Matrix4 view = _camera.ViewMatrix;
            view.Row3.Xyz = Vector3.Zero;      // remove translation
            Matrix4 proj = _camera.Projection(Size.X / (float)Size.Y);

            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uView"),
                              false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uProjection"),
                              false, ref proj);

            GL.BindVertexArray(_skyboxVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            GL.BindVertexArray(0);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);

            // swap
            SwapBuffers();
        }

        // unload
        protected override void OnUnload()
        {
            GL.DeleteVertexArray(_skyboxVao);
            GL.DeleteBuffer(_skyboxVbo);
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteTexture(_cubemapTexture);
            base.OnUnload();
        }

        // shader help
        private int CreateShader(string vertSrc, string fragSrc)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertSrc);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out int okV);
            if (okV == 0)
                throw new Exception("Vertex compile error: " + GL.GetShaderInfoLog(vs));

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragSrc);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out int okF);
            if (okF == 0)
                throw new Exception("Fragment compile error: " + GL.GetShaderInfoLog(fs));

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.GetProgram(prog, GetProgramParameterName.LinkStatus, out int okP);
            if (okP == 0)
                throw new Exception("Link error: " + GL.GetProgramInfoLog(prog));

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;
        }

        // cubemap loader
        private int LoadCubemapCropped(string[] faces)
        {
            var bitmaps = faces.Select(f => new Bitmap(f)).ToList();

            for (int i = 0; i < faces.Length; i++)
                Console.WriteLine($"{Path.GetFileName(faces[i])}: {bitmaps[i].Width}×{bitmaps[i].Height}");

            int targetSide = bitmaps.Select(b => Math.Min(b.Width, b.Height)).Min();
            Console.WriteLine($"Cropping all faces to {targetSide}×{targetSide}");

            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, texID);

            for (int i = 0; i < faces.Length; i++)
            {
                var orig = bitmaps[i];
                int x = (orig.Width  - targetSide) / 2;
                int y = (orig.Height - targetSide) / 2;

                using var square = orig.Clone(
                    new Rectangle(x, y, targetSide, targetSide),
                    ImgPF.Format32bppArgb);

                square.RotateFlip(RotateFlipType.RotateNoneFlipY);

                var data = square.LockBits(
                    new Rectangle(0, 0, targetSide, targetSide),
                    ImageLockMode.ReadOnly,
                    ImgPF.Format32bppArgb);

                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0,
                              PixelInternalFormat.Rgba,
                              targetSide, targetSide, 0,
                              GlPF.Bgra, PixelType.UnsignedByte, data.Scan0);

                square.UnlockBits(data);
                orig.Dispose();
            }
            bitmaps.Clear();

            GL.TexParameter(TextureTarget.TextureCubeMap,
                            TextureParameterName.TextureMinFilter,
                            (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap,
                            TextureParameterName.TextureMagFilter,
                            (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap,
                            TextureParameterName.TextureWrapS,
                            (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap,
                            TextureParameterName.TextureWrapT,
                            (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap,
                            TextureParameterName.TextureWrapR,
                            (int)TextureWrapMode.ClampToEdge);

            return texID;
        }
    }
}
